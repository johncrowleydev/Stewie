"""
architect_main.py — Architect Agent main loop.

The brain of the Architect Agent. Receives human chat messages via RabbitMQ,
builds context, calls LLM to produce structured plans, presents plans for
approval, creates jobs on approval, monitors dev agents, and reports results.

Uses plan_first mode: propose → wait for approval → execute.

Environment variables (inherited from opencode entrypoint.py):
    RABBITMQ_HOST    - RabbitMQ hostname
    RABBITMQ_PORT    - AMQP port
    RABBITMQ_USER    - RabbitMQ username
    RABBITMQ_PASS    - RabbitMQ password
    RABBITMQ_VHOST   - RabbitMQ virtual host
    AGENT_ID         - Architect session ID (GUID)
    PROJECT_ID       - Project ID (GUID)
    MOCK_LLM         - "true" to use mock responses
    LLM_PROVIDER     - LLM provider identifier
    MODEL_NAME       - Model name
    STEWIE_API_URL   - Stewie API base URL (default: http://localhost:5275)

REF: JOB-022 T-190, T-196, CON-004 §6
"""

import json
import logging
import os
import re
import signal
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone

import pika

# Local modules (same directory)
from stewie_api_client import StewieApiClient, StewieApiError
from context_builder import ContextBuilder
from job_parser import JobParser, PlanValidationError


# ── Configuration ───────────────────────────────────────────────────────

RABBITMQ_HOST = os.environ.get("RABBITMQ_HOST", "localhost")
RABBITMQ_PORT = int(os.environ.get("RABBITMQ_PORT", "5672"))
RABBITMQ_USER = os.environ.get("RABBITMQ_USER", "guest")
RABBITMQ_PASS = os.environ.get("RABBITMQ_PASS", "guest")
RABBITMQ_VHOST = os.environ.get("RABBITMQ_VHOST", "/")
AGENT_ID = os.environ.get("AGENT_ID", str(uuid.uuid4()))
PROJECT_ID = os.environ.get("PROJECT_ID", "")
AGENT_ROLE = "architect"
LLM_PROVIDER = os.environ.get("LLM_PROVIDER", "google")
MODEL_NAME = os.environ.get("MODEL_NAME", "gemini-2.0-flash")
MOCK_LLM = os.environ.get("MOCK_LLM", "false").lower() == "true"
STEWIE_API_URL = os.environ.get("STEWIE_API_URL", "http://localhost:5275")
DEFAULT_RUNTIME = os.environ.get("DEFAULT_RUNTIME", "opencode")
ARCHITECT_MODE = os.environ.get("ARCHITECT_MODE", "plan_first")

# Exchange / queue names per CON-004
COMMANDS_EXCHANGE = "stewie.commands"
EVENTS_EXCHANGE = "stewie.events"
CHAT_EXCHANGE = "stewie.chat"
CHAT_QUEUE = f"architect.{PROJECT_ID}"
COMMAND_QUEUE = f"agent.{AGENT_ID}.commands"

MAX_RETRIES = 5
RETRY_DELAY = 2

# ── Logging ─────────────────────────────────────────────────────────────

logging.basicConfig(
    level=logging.INFO,
    format="[%(asctime)s] %(levelname)s %(name)s: %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%SZ",
)
logger = logging.getLogger("architect")


# ── Helpers ─────────────────────────────────────────────────────────────

def make_message(msg_type: str, payload: dict, correlation_id: str | None = None) -> dict:
    """Build a CON-004 compliant message envelope."""
    return {
        "messageId": str(uuid.uuid4()),
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "type": msg_type,
        "source": f"agent.{AGENT_ID}",
        "correlationId": correlation_id or "",
        "payload": payload,
    }


def publish_event(channel, routing_key: str, message: dict) -> None:
    """Publish a message to the events exchange."""
    body = json.dumps(message).encode("utf-8")
    channel.basic_publish(
        exchange=EVENTS_EXCHANGE,
        routing_key=routing_key,
        body=body,
        properties=pika.BasicProperties(
            delivery_mode=2,
            content_type="application/json",
        ),
    )
    logger.debug("Published %s -> %s", message["type"], routing_key)


# ── State ───────────────────────────────────────────────────────────────

class ArchitectState:
    """Tracks the Architect's in-flight state."""

    def __init__(self):
        self.pending_plan: dict | None = None
        self.pending_plan_id: str | None = None
        self.monitoring_jobs: dict[str, dict] = {}  # job_id -> job_info
        self.running = True


# ── LLM Invocation ──────────────────────────────────────────────────────

def call_llm(prompt: str) -> str:
    """
    Call the LLM via OpenCode CLI or mock mode.

    Args:
        prompt: The full context + instructions prompt.

    Returns:
        LLM response text.
    """
    if MOCK_LLM:
        return _mock_llm_response(prompt)

    cmd = ["opencode", "run", "--model", f"{LLM_PROVIDER}/{MODEL_NAME}", prompt]
    env = os.environ.copy()

    # Read API key from secrets mount
    secrets_path = "/run/secrets/llm_api_key"
    if os.path.isfile(secrets_path):
        with open(secrets_path, "r") as f:
            api_key = f.read().strip()
            if api_key:
                env["ANTHROPIC_API_KEY"] = api_key
                env["OPENAI_API_KEY"] = api_key
                env["GOOGLE_API_KEY"] = api_key

    logger.info("Calling LLM: %s/%s", LLM_PROVIDER, MODEL_NAME)

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            env=env,
            cwd="/workspace",
            timeout=120,
        )
        if result.returncode != 0:
            logger.error("LLM call failed (exit %d): %s", result.returncode, result.stderr[:500])
            return f"Error: LLM call failed with exit code {result.returncode}"
        return result.stdout
    except FileNotFoundError:
        logger.error("opencode CLI not found in PATH")
        return "Error: opencode CLI not found"
    except subprocess.TimeoutExpired:
        logger.error("LLM call timed out after 120s")
        return "Error: LLM call timed out"


def _mock_llm_response(prompt: str) -> str:
    """
    Generate a mock LLM response for CI testing.

    Produces a realistic-looking plan response with valid JSON.
    """
    return """I'll analyze the request and create a plan.

Based on the project context and your request, here is my proposed plan:

```json
{
  "summary": "Implement the requested changes with proper testing and governance compliance.",
  "jobs": [
    {
      "title": "Implementation Job",
      "tasks": [
        {
          "title": "Core Implementation",
          "description": "Implement the core functionality as described in the request.",
          "role": "developer",
          "dependsOn": []
        },
        {
          "title": "Write Tests",
          "description": "Write unit and integration tests for the new functionality.",
          "role": "tester",
          "dependsOn": ["Core Implementation"]
        }
      ]
    }
  ]
}
```

This plan creates one job with two tasks: the core implementation and testing."""


def extract_plan_json(llm_response: str) -> dict | None:
    """
    Extract JSON plan from LLM response text.

    The LLM wraps its JSON in a markdown code block. This extracts
    and parses the first JSON block found.

    Args:
        llm_response: Raw LLM response text.

    Returns:
        Parsed plan dict, or None if no valid JSON found.
    """
    # Try to find JSON in code blocks first
    json_match = re.search(r"```(?:json)?\s*\n({[\s\S]*?})\s*\n```", llm_response)
    if json_match:
        try:
            return json.loads(json_match.group(1))
        except json.JSONDecodeError:
            logger.warning("Found JSON block but failed to parse")

    # Fallback: find any JSON object in the response
    brace_start = llm_response.find("{")
    if brace_start >= 0:
        # Find matching closing brace
        depth = 0
        for i in range(brace_start, len(llm_response)):
            if llm_response[i] == "{":
                depth += 1
            elif llm_response[i] == "}":
                depth -= 1
                if depth == 0:
                    try:
                        return json.loads(llm_response[brace_start : i + 1])
                    except json.JSONDecodeError:
                        break

    logger.error("Could not extract valid JSON from LLM response")
    return None


# ── Chat Message Handlers ──────────────────────────────────────────────

def handle_human_message(
    channel,
    state: ArchitectState,
    api_client: StewieApiClient,
    context_builder: ContextBuilder,
    job_parser: JobParser,
    message: dict,
) -> None:
    """
    Handle an incoming human chat message.

    Flow:
    1. Build context from project state + chat history
    2. Call LLM to generate a plan
    3. Send the plan to the Human for approval
    4. Wait for plan decision (handled in separate callback)

    Args:
        channel: RabbitMQ channel.
        state: Current architect state.
        api_client: Stewie API client.
        context_builder: Context builder.
        job_parser: Job parser.
        message: The incoming chat message.
    """
    payload = message.get("payload", {})
    content = payload.get("content", "")
    project_id = payload.get("projectId", PROJECT_ID)
    correlation_id = message.get("correlationId", "")

    if not content.strip():
        logger.info("Ignoring empty chat message")
        return

    logger.info("Processing human message: %s", content[:100])

    # Build LLM context
    prompt = context_builder.build_context(project_id, content)

    # Call LLM
    llm_response = call_llm(prompt)

    # Extract plan JSON from response
    plan_json = extract_plan_json(llm_response)

    if plan_json is None:
        # LLM didn't produce a structured plan — send raw response as chat
        _send_chat_response(channel, api_client, project_id, llm_response, correlation_id)
        return

    # Validate the plan
    validation_errors = job_parser.validate_plan(plan_json)
    if validation_errors:
        error_msg = (
            "I generated a plan but it has validation issues:\n\n"
            + "\n".join(f"- {e}" for e in validation_errors)
            + "\n\nPlease refine your request and I'll try again."
        )
        _send_chat_response(channel, api_client, project_id, error_msg, correlation_id)
        return

    # Store pending plan
    plan_id = str(uuid.uuid4())
    state.pending_plan = plan_json
    state.pending_plan_id = plan_id

    # Extract the markdown/text part of the response (before JSON)
    plan_markdown = llm_response

    # Send plan proposal event
    plan_event = make_message(
        "chat.plan_proposed",
        {
            "agentId": AGENT_ID,
            "projectId": project_id,
            "planId": plan_id,
            "summary": plan_json.get("summary", ""),
            "planMarkdown": plan_markdown,
            "planJson": plan_json,
        },
        correlation_id=correlation_id,
    )
    publish_event(channel, f"agent.{AGENT_ID}.completed", plan_event)

    # Also send as a chat response for the dashboard
    plan_summary = plan_json.get("summary", "")
    jobs_list = plan_json.get("jobs", [])
    plan_text = f"📋 **Plan Proposal** (ID: `{plan_id}`)\n\n{plan_summary}\n\n"

    for i, job in enumerate(jobs_list, 1):
        plan_text += f"### Job {i}: {job.get('title', '?')}\n"
        for j, task in enumerate(job.get("tasks", []), 1):
            role = task.get("role", "developer")
            plan_text += f"  {j}. [{role}] {task.get('title', '?')}\n"
        plan_text += "\n"

    plan_text += "👉 **Approve** or **reject** this plan to proceed."

    _send_chat_response(channel, api_client, project_id, plan_text, correlation_id)

    logger.info("Plan %s proposed — waiting for approval", plan_id)


def handle_plan_decision(
    channel,
    state: ArchitectState,
    api_client: StewieApiClient,
    job_parser: JobParser,
    message: dict,
) -> None:
    """
    Handle a plan approval/rejection decision from the Human.

    On approval: create jobs and tasks via the API.
    On rejection: acknowledge and wait for next message.

    Args:
        channel: RabbitMQ channel.
        state: Current architect state.
        api_client: Stewie API client.
        job_parser: Job parser.
        message: The plan decision message.
    """
    payload = message.get("payload", {})
    plan_id = payload.get("planId", "")
    decision = payload.get("decision", "")
    feedback = payload.get("feedback", "")

    logger.info("Plan decision received: planId=%s, decision=%s", plan_id, decision)

    if state.pending_plan is None or state.pending_plan_id != plan_id:
        logger.warning("Received decision for unknown plan %s — ignoring", plan_id)
        return

    project_id = PROJECT_ID

    if decision == "approved":
        logger.info("Plan %s approved — creating jobs", plan_id)

        try:
            created_ids = job_parser.parse_and_create(project_id, state.pending_plan)

            response_text = (
                f"✅ **Plan approved and executed!**\n\n"
                f"Created {len(created_ids)} job(s):\n"
                + "\n".join(f"- `{jid}`" for jid in created_ids)
            )

            # Track jobs for monitoring (T-196)
            for jid in created_ids:
                state.monitoring_jobs[jid] = {"status": "Pending", "tasks_done": 0}

            _send_chat_response(channel, api_client, project_id, response_text)

        except PlanValidationError as e:
            error_text = (
                f"❌ Plan validation failed:\n"
                + "\n".join(f"- {err}" for err in e.errors)
            )
            _send_chat_response(channel, api_client, project_id, error_text)

        except StewieApiError as e:
            error_text = f"❌ Failed to create jobs: {e}"
            _send_chat_response(channel, api_client, project_id, error_text)

    elif decision == "rejected":
        feedback_text = f" Feedback: {feedback}" if feedback else ""
        response_text = f"👍 Plan rejected.{feedback_text}\n\nSend me your next request when ready."
        _send_chat_response(channel, api_client, project_id, response_text)

    else:
        logger.warning("Unknown plan decision: %s", decision)

    # Clear pending plan regardless
    state.pending_plan = None
    state.pending_plan_id = None


# ── Dev Agent Monitoring (T-196) ────────────────────────────────────────

def handle_agent_event(
    channel,
    state: ArchitectState,
    api_client: StewieApiClient,
    message: dict,
) -> None:
    """
    Handle agent lifecycle events for monitoring.

    Filters to meaningful updates and reports to the Human:
    - First task starting
    - Task completion
    - Task failure
    - All tasks complete (summary)

    Args:
        channel: RabbitMQ channel.
        state: Current architect state.
        api_client: Stewie API client.
        message: The agent event message.
    """
    msg_type = message.get("type", "")
    payload = message.get("payload", {})
    task_id = payload.get("taskId", "")
    agent_id = payload.get("agentId", "")

    project_id = PROJECT_ID

    if msg_type == "event.completed":
        summary = payload.get("summary", "Task completed")
        logger.info("Dev agent %s completed task %s", agent_id, task_id)
        _send_chat_response(
            channel, api_client, project_id,
            f"✅ Task `{task_id[:8]}...` completed: {summary}",
        )
        _check_all_jobs_done(channel, state, api_client)

    elif msg_type == "event.failed":
        error_msg = payload.get("errorMessage", "Unknown error")
        logger.warning("Dev agent %s failed task %s: %s", agent_id, task_id, error_msg)
        _send_chat_response(
            channel, api_client, project_id,
            f"❌ Task `{task_id[:8]}...` failed: {error_msg}\n\n"
            "I can retry this task or adjust the plan. What would you like to do?",
        )

    elif msg_type == "event.started":
        logger.info("Dev agent %s started", agent_id)
        # Only report first start, not every agent
        if len(state.monitoring_jobs) > 0:
            _send_chat_response(
                channel, api_client, project_id,
                f"🚀 Agent `{agent_id[:8]}...` started working on assigned tasks.",
            )


def _check_all_jobs_done(
    channel,
    state: ArchitectState,
    api_client: StewieApiClient,
) -> None:
    """Check if all monitored jobs are complete and send summary."""
    if not state.monitoring_jobs:
        return

    project_id = PROJECT_ID
    all_done = True

    for job_id in list(state.monitoring_jobs.keys()):
        try:
            job = api_client.get_job(job_id)
            status = job.get("status", "")
            if status in ("Pending", "Running"):
                all_done = False
            else:
                state.monitoring_jobs[job_id]["status"] = status
        except Exception as e:
            logger.warning("Failed to check job %s status: %s", job_id, e)
            all_done = False

    if all_done and state.monitoring_jobs:
        summary_parts = []
        for job_id, info in state.monitoring_jobs.items():
            status = info.get("status", "?")
            emoji = "✅" if status == "Completed" else "❌"
            summary_parts.append(f"{emoji} Job `{job_id[:8]}...`: {status}")

        summary_text = (
            "📊 **All jobs complete!**\n\n"
            + "\n".join(summary_parts)
            + "\n\nWhat would you like to do next?"
        )
        _send_chat_response(channel, api_client, project_id, summary_text)
        state.monitoring_jobs.clear()


# ── Chat Response ──────────────────────────────────────────────────────

def _send_chat_response(
    channel,
    api_client: StewieApiClient,
    project_id: str,
    content: str,
    correlation_id: str = "",
) -> None:
    """
    Send a chat response to the Human via both RabbitMQ event and REST API.

    The RabbitMQ event is picked up by the API's consumer service and persisted
    as a ChatMessage. The API client call is a fallback.
    """
    # Publish via RabbitMQ events (primary path)
    chat_event = make_message(
        "chat.architect_response",
        {
            "agentId": AGENT_ID,
            "projectId": project_id,
            "content": content,
        },
        correlation_id=correlation_id,
    )
    try:
        publish_event(channel, f"agent.{AGENT_ID}.completed", chat_event)
    except Exception as e:
        logger.warning("Failed to publish chat response via RabbitMQ: %s", e)
        # Fallback: try REST API
        try:
            api_client.send_chat_message(project_id, content)
        except Exception as api_e:
            logger.error("Failed to send chat response via API fallback: %s", api_e)


# ── Connection ──────────────────────────────────────────────────────────

def connect_with_retry() -> pika.BlockingConnection:
    """Connect to RabbitMQ with exponential backoff retry."""
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
    params = pika.ConnectionParameters(
        host=RABBITMQ_HOST,
        port=RABBITMQ_PORT,
        virtual_host=RABBITMQ_VHOST,
        credentials=credentials,
        heartbeat=60,
        blocked_connection_timeout=30,
    )

    for attempt in range(1, MAX_RETRIES + 1):
        try:
            logger.info(
                "Connecting to RabbitMQ at %s:%d (attempt %d/%d)...",
                RABBITMQ_HOST, RABBITMQ_PORT, attempt, MAX_RETRIES,
            )
            connection = pika.BlockingConnection(params)
            logger.info("Connected to RabbitMQ successfully")
            return connection
        except pika.exceptions.AMQPConnectionError as e:
            if attempt == MAX_RETRIES:
                logger.error("Failed to connect after %d attempts: %s", MAX_RETRIES, e)
                raise
            delay = RETRY_DELAY * (2 ** (attempt - 1))
            logger.info("Connection failed, retrying in %ds...", delay)
            time.sleep(delay)

    raise RuntimeError("Connection retry loop exited unexpectedly")


# ── Main ────────────────────────────────────────────────────────────────

def main() -> None:
    """Entry point — connect, configure, consume chat + commands."""
    logger.info("stewie-architect-agent starting")
    logger.info("  AGENT_ID:       %s", AGENT_ID)
    logger.info("  PROJECT_ID:     %s", PROJECT_ID)
    logger.info("  MOCK_LLM:       %s", MOCK_LLM)
    logger.info("  ARCHITECT_MODE: %s", ARCHITECT_MODE)
    logger.info("  STEWIE_API_URL: %s", STEWIE_API_URL)
    logger.info("  RABBITMQ:       %s:%d", RABBITMQ_HOST, RABBITMQ_PORT)

    if not PROJECT_ID:
        logger.error("PROJECT_ID environment variable is required")
        sys.exit(1)

    # Initialize API client and helpers
    api_client = StewieApiClient(STEWIE_API_URL)
    context_builder = ContextBuilder(api_client)
    job_parser = JobParser(api_client)
    state = ArchitectState()

    # Connect to RabbitMQ
    connection = connect_with_retry()
    channel = connection.channel()

    # Declare exchanges (idempotent)
    channel.exchange_declare(exchange=EVENTS_EXCHANGE, exchange_type="topic", durable=True)
    channel.exchange_declare(exchange=COMMANDS_EXCHANGE, exchange_type="direct", durable=True)
    channel.exchange_declare(exchange=CHAT_EXCHANGE, exchange_type="direct", durable=True)

    # Declare and bind chat queue (for human messages)
    channel.queue_declare(queue=CHAT_QUEUE, durable=True)
    channel.queue_bind(queue=CHAT_QUEUE, exchange=CHAT_EXCHANGE, routing_key=CHAT_QUEUE)

    # Declare and bind command queue (for plan decisions)
    channel.queue_declare(queue=COMMAND_QUEUE, durable=True)
    channel.queue_bind(
        queue=COMMAND_QUEUE,
        exchange=COMMANDS_EXCHANGE,
        routing_key=f"agent.{AGENT_ID}",
    )

    # Publish event.started
    started = make_message(
        "event.started",
        {
            "agentId": AGENT_ID,
            "runtimeName": "architect",
            "capabilities": ["planning", "job-creation", "monitoring"],
        },
    )
    publish_event(channel, f"agent.{AGENT_ID}.started", started)

    # Set up QoS
    channel.basic_qos(prefetch_count=1)

    # Message handler
    def on_message(ch, method, _properties, body):
        """Dispatch incoming messages to appropriate handlers."""
        try:
            message = json.loads(body.decode("utf-8"))
            msg_type = message.get("type", "")

            logger.info("Received message: type=%s", msg_type)

            if msg_type == "chat.human_message":
                handle_human_message(
                    ch, state, api_client,
                    context_builder, job_parser, message,
                )
            elif msg_type == "command.plan_decision":
                handle_plan_decision(ch, state, api_client, job_parser, message)
            elif msg_type == "command.terminate":
                logger.info("Received terminate command")
                stopped = make_message("event.stopped", {"agentId": AGENT_ID, "reason": "terminated"})
                publish_event(ch, f"agent.{AGENT_ID}.completed", stopped)
                raise SystemExit(0)
            elif msg_type.startswith("event."):
                handle_agent_event(ch, state, api_client, message)
            else:
                logger.info("Ignoring message type: %s", msg_type)

            ch.basic_ack(delivery_tag=method.delivery_tag)

        except SystemExit:
            ch.basic_ack(delivery_tag=method.delivery_tag)
            raise
        except Exception as e:
            logger.error("Error processing message: %s", e, exc_info=True)
            ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True)

    # SIGTERM handler
    def handle_sigterm(signum, frame):
        logger.info("Received SIGTERM — shutting down gracefully")
        try:
            stopped = make_message("event.stopped", {"agentId": AGENT_ID, "reason": "sigterm"})
            publish_event(channel, f"agent.{AGENT_ID}.completed", stopped)
        except Exception:
            pass
        try:
            channel.stop_consuming()
        except Exception:
            pass

    signal.signal(signal.SIGTERM, handle_sigterm)

    # Consume from both queues
    channel.basic_consume(queue=CHAT_QUEUE, on_message_callback=on_message, auto_ack=False)
    channel.basic_consume(queue=COMMAND_QUEUE, on_message_callback=on_message, auto_ack=False)

    logger.info(
        "Architect agent ready — consuming from '%s' and '%s'",
        CHAT_QUEUE, COMMAND_QUEUE,
    )

    try:
        channel.start_consuming()
    except (SystemExit, KeyboardInterrupt):
        pass
    finally:
        try:
            connection.close()
        except Exception:
            pass
        logger.info("Architect agent stopped")


if __name__ == "__main__":
    main()
