"""
entrypoint.py — Bridge between RabbitMQ and OpenCode CLI.

Role-agnostic harness that works for developer, tester, and architect agents.
Connects to RabbitMQ, publishes lifecycle events, consumes commands, and
delegates task execution to OpenCode CLI (or mock_llm.py in test mode).

Environment variables (all required unless noted):
    RABBITMQ_HOST    - RabbitMQ hostname (default: localhost)
    RABBITMQ_PORT    - AMQP port (default: 5672)
    RABBITMQ_USER    - RabbitMQ username (default: guest)
    RABBITMQ_PASS    - RabbitMQ password (default: guest)
    RABBITMQ_VHOST   - RabbitMQ virtual host (default: /)
    AGENT_QUEUE      - Queue name to consume commands from
    AGENT_ID         - Unique agent/session identifier (GUID)
    PROJECT_ID       - Project identifier (GUID)
    AGENT_ROLE       - Agent role: architect, developer, tester
    LLM_PROVIDER     - LLM provider identifier (e.g. "google", "anthropic")
    MODEL_NAME       - Model name (e.g. "gemini-2.0-flash")
    MOCK_LLM         - Set to "true" to use mock_llm.py instead of OpenCode (optional)
    LLM_API_KEY      - Fallback env var for API key (optional, prefers /run/secrets/llm_api_key)

REF: JOB-021 T-182, CON-004 §5-§6
"""

import json
import os
import signal
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone

import pika


# ── Configuration ───────────────────────────────────────────────────────

RABBITMQ_HOST = os.environ.get("RABBITMQ_HOST", "localhost")
RABBITMQ_PORT = int(os.environ.get("RABBITMQ_PORT", "5672"))
RABBITMQ_USER = os.environ.get("RABBITMQ_USER", "guest")
RABBITMQ_PASS = os.environ.get("RABBITMQ_PASS", "guest")
RABBITMQ_VHOST = os.environ.get("RABBITMQ_VHOST", "/")
AGENT_QUEUE = os.environ.get("AGENT_QUEUE", "")
AGENT_ID = os.environ.get("AGENT_ID", str(uuid.uuid4()))
PROJECT_ID = os.environ.get("PROJECT_ID", "")
AGENT_ROLE = os.environ.get("AGENT_ROLE", "developer")
LLM_PROVIDER = os.environ.get("LLM_PROVIDER", "google")
MODEL_NAME = os.environ.get("MODEL_NAME", "gemini-2.0-flash")
MOCK_LLM = os.environ.get("MOCK_LLM", "false").lower() == "true"

# Exchange names per CON-004 §2
COMMANDS_EXCHANGE = "stewie.commands"
EVENTS_EXCHANGE = "stewie.events"

# Maximum connection retry attempts
MAX_RETRIES = 5
RETRY_DELAY = 2  # seconds


# ── API Key Resolution ──────────────────────────────────────────────────

def resolve_api_key() -> str:
    """
    Read the LLM API key from the secrets mount, falling back to env var.

    Priority:
        1. /run/secrets/llm_api_key (file-based secret from Docker mount)
        2. LLM_API_KEY env var (backward compatibility)
        3. Empty string (mock mode doesn't need a key)
    """
    secrets_path = "/run/secrets/llm_api_key"
    if os.path.isfile(secrets_path):
        with open(secrets_path, "r") as f:
            key = f.read().strip()
            if key:
                log("API key loaded from /run/secrets/llm_api_key")
                return key

    key = os.environ.get("LLM_API_KEY", "")
    if key:
        log("API key loaded from LLM_API_KEY env var (fallback)")
    return key


# ── Helpers ─────────────────────────────────────────────────────────────

def log(msg: str) -> None:
    """Print a timestamped log message."""
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    print(f"[{ts}] {msg}", flush=True)


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
            delivery_mode=2,  # persistent
            content_type="application/json",
        ),
    )
    log(f"Published {message['type']} -> {routing_key}")


# ── OpenCode Config ─────────────────────────────────────────────────────

def generate_opencode_config(api_key: str) -> None:
    """
    Generate opencode.json config from template with provider settings.

    Reads opencode.json.template, substitutes provider/model vars,
    and writes the result to /workspace/.opencode.json.
    """
    template_path = "/app/opencode.json.template"
    output_path = "/workspace/.opencode.json"

    try:
        with open(template_path, "r") as f:
            template = f.read()

        config = template.replace("${LLM_PROVIDER}", LLM_PROVIDER)
        config = config.replace("${MODEL_NAME}", MODEL_NAME)

        with open(output_path, "w") as f:
            f.write(config)

        log(f"Generated OpenCode config: provider={LLM_PROVIDER}, model={MODEL_NAME}")
    except OSError as e:
        log(f"Warning: Could not generate OpenCode config: {e}")


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
            log(f"Connecting to RabbitMQ at {RABBITMQ_HOST}:{RABBITMQ_PORT} "
                f"(attempt {attempt}/{MAX_RETRIES})...")
            connection = pika.BlockingConnection(params)
            log("Connected to RabbitMQ successfully")
            return connection
        except pika.exceptions.AMQPConnectionError as e:
            if attempt == MAX_RETRIES:
                log(f"Failed to connect after {MAX_RETRIES} attempts: {e}")
                raise
            delay = RETRY_DELAY * (2 ** (attempt - 1))
            log(f"Connection failed, retrying in {delay}s...")
            time.sleep(delay)

    # Unreachable, but satisfies type checker
    raise RuntimeError("Connection retry loop exited unexpectedly")


# ── Task Execution ──────────────────────────────────────────────────────

def execute_task_opencode(channel, task_description: str, correlation_id: str | None, api_key: str) -> tuple[int, str]:
    """
    Execute a task using the OpenCode CLI.

    Runs opencode as a subprocess, streams stdout lines as event.stdout messages,
    and returns the exit code and collected output.

    Args:
        channel: RabbitMQ channel for publishing events.
        task_description: The task description to pass to OpenCode.
        correlation_id: Optional correlation ID for message pairing.
        api_key: LLM API key for the provider.

    Returns:
        Tuple of (exit_code, collected_output).
    """
    cmd = ["opencode", "run", "--model", f"{LLM_PROVIDER}/{MODEL_NAME}", task_description]
    env = os.environ.copy()
    if api_key:
        # Set provider-specific env var for OpenCode
        env["ANTHROPIC_API_KEY"] = api_key
        env["OPENAI_API_KEY"] = api_key
        env["GOOGLE_API_KEY"] = api_key

    log(f"Running: opencode run --model {LLM_PROVIDER}/{MODEL_NAME} ...")

    try:
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            env=env,
            cwd="/workspace",
        )
    except FileNotFoundError:
        log("ERROR: opencode CLI not found in PATH")
        return (1, "opencode CLI not found in PATH")

    output_lines = []
    for line in iter(process.stdout.readline, ""):
        line = line.rstrip("\n")
        output_lines.append(line)

        # Stream stdout as event.stdout
        stdout_msg = make_message(
            "event.stdout",
            {
                "agentId": AGENT_ID,
                "stream": "stdout",
                "line": line,
            },
            correlation_id=correlation_id,
        )
        publish_event(channel, f"agent.{AGENT_ID}.stdout", stdout_msg)

    process.wait()
    return (process.returncode, "\n".join(output_lines))


def execute_task_mock(channel, task_description: str, correlation_id: str | None) -> tuple[int, str]:
    """
    Execute a task using the mock LLM responder.

    Runs mock_llm.py as a subprocess, streams stdout lines as event.stdout messages.

    Args:
        channel: RabbitMQ channel for publishing events.
        task_description: The task description to pass to the mock.
        correlation_id: Optional correlation ID for message pairing.

    Returns:
        Tuple of (exit_code, collected_output).
    """
    cmd = ["python3", "/app/mock_llm.py", task_description]

    log("Running in MOCK LLM mode (no API key required)")

    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        cwd="/workspace",
    )

    output_lines = []
    for line in iter(process.stdout.readline, ""):
        line = line.rstrip("\n")
        output_lines.append(line)

        # Stream stdout as event.stdout
        stdout_msg = make_message(
            "event.stdout",
            {
                "agentId": AGENT_ID,
                "stream": "stdout",
                "line": line,
            },
            correlation_id=correlation_id,
        )
        publish_event(channel, f"agent.{AGENT_ID}.stdout", stdout_msg)

    process.wait()
    return (process.returncode, "\n".join(output_lines))


# ── Command Handlers ────────────────────────────────────────────────────

def handle_assign_task(channel, message: dict, api_key: str) -> None:
    """
    Handle command.assign_task — invoke OpenCode or mock, publish result.

    Flow:
        1. Extract taskDescription from payload
        2. Delegate to mock_llm.py or OpenCode CLI
        3. Stream stdout lines as event.stdout
        4. Publish event.completed or event.failed
    """
    payload = message.get("payload", {})
    task_id = payload.get("taskId", "unknown")
    task_description = payload.get("taskDescription", payload.get("taskTitle", "No description"))
    correlation_id = message.get("correlationId")

    log(f"Received task assignment: taskId={task_id}")

    if MOCK_LLM:
        exit_code, output = execute_task_mock(channel, task_description, correlation_id)
    else:
        exit_code, output = execute_task_opencode(channel, task_description, correlation_id, api_key)

    if exit_code == 0:
        completed = make_message(
            "event.completed",
            {
                "agentId": AGENT_ID,
                "taskId": task_id,
                "summary": f"Task completed: {task_description[:100]}",
                "artifacts": [],
                "governanceReport": {"passed": True, "checks": []},
            },
            correlation_id=correlation_id,
        )
        publish_event(channel, f"agent.{AGENT_ID}.completed", completed)
    else:
        failed = make_message(
            "event.failed",
            {
                "agentId": AGENT_ID,
                "taskId": task_id,
                "errorType": "runtime_error",
                "errorMessage": f"Process exited with code {exit_code}",
                "details": output[-500:] if len(output) > 500 else output,
                "retryable": True,
            },
            correlation_id=correlation_id,
        )
        publish_event(channel, f"agent.{AGENT_ID}.failed", failed)


def handle_terminate(channel, message: dict) -> None:
    """Handle command.terminate — publish stopped event and exit."""
    payload = message.get("payload", {})
    reason = payload.get("reason", "unknown")

    log(f"Received terminate command: reason={reason}")

    stopped = make_message(
        "event.stopped",
        {
            "agentId": AGENT_ID,
            "reason": reason,
        },
    )
    publish_event(channel, f"agent.{AGENT_ID}.completed", stopped)

    log("Shutting down after terminate command...")
    raise SystemExit(0)


# ── Message Consumer ────────────────────────────────────────────────────

def make_on_message(channel_ref, api_key: str):
    """
    Factory for the message callback.

    Uses a closure to pass the api_key and channel reference
    to the message handler without global state.

    Args:
        channel_ref: Reference to the RabbitMQ channel (may be reassigned).
        api_key: Decrypted LLM API key for provider authentication.

    Returns:
        Callback function compatible with pika's basic_consume.
    """
    def on_message(channel, method, _properties, body):
        """Dispatch incoming commands to the appropriate handler."""
        try:
            message = json.loads(body.decode("utf-8"))
            msg_type = message.get("type", "")

            log(f"Received command: type={msg_type}")

            if msg_type == "command.assign_task":
                handle_assign_task(channel, message, api_key)
            elif msg_type == "command.terminate":
                handle_terminate(channel, message)
            else:
                log(f"Unknown command type: {msg_type} — ignoring")

            channel.basic_ack(delivery_tag=method.delivery_tag)

        except SystemExit:
            channel.basic_ack(delivery_tag=method.delivery_tag)
            raise
        except Exception as e:
            log(f"Error processing message: {e}")
            channel.basic_nack(delivery_tag=method.delivery_tag, requeue=True)

    return on_message


# ── Main ────────────────────────────────────────────────────────────────

def main() -> None:
    """Entry point — connect, configure, publish started, consume commands."""
    log("stewie-opencode-agent starting")
    log(f"  AGENT_ID:      {AGENT_ID}")
    log(f"  PROJECT_ID:    {PROJECT_ID}")
    log(f"  AGENT_ROLE:    {AGENT_ROLE}")
    log(f"  AGENT_QUEUE:   {AGENT_QUEUE}")
    log(f"  LLM_PROVIDER:  {LLM_PROVIDER}")
    log(f"  MODEL_NAME:    {MODEL_NAME}")
    log(f"  MOCK_LLM:      {MOCK_LLM}")
    log(f"  RABBITMQ:      {RABBITMQ_HOST}:{RABBITMQ_PORT}")

    if not AGENT_QUEUE:
        log("ERROR: AGENT_QUEUE environment variable is required")
        sys.exit(1)

    # Resolve API key (file-based secret or env var fallback)
    api_key = resolve_api_key()
    if not MOCK_LLM and not api_key:
        log("WARNING: No API key found. Set MOCK_LLM=true or provide a key via secrets mount.")

    # Generate OpenCode config from template
    if not MOCK_LLM:
        generate_opencode_config(api_key)

    # Connect to RabbitMQ
    connection = connect_with_retry()
    channel = connection.channel()

    # Declare exchanges (idempotent)
    channel.exchange_declare(
        exchange=EVENTS_EXCHANGE,
        exchange_type="topic",
        durable=True,
    )
    channel.exchange_declare(
        exchange=COMMANDS_EXCHANGE,
        exchange_type="direct",
        durable=True,
    )

    # Declare and bind the agent's command queue
    channel.queue_declare(queue=AGENT_QUEUE, durable=True)
    channel.queue_bind(
        queue=AGENT_QUEUE,
        exchange=COMMANDS_EXCHANGE,
        routing_key=f"agent.{AGENT_ID}",
    )

    # Publish event.started
    started = make_message(
        "event.started",
        {
            "agentId": AGENT_ID,
            "runtimeName": "opencode",
            "capabilities": ["code-generation", "testing", "governance-check"],
        },
    )
    publish_event(channel, f"agent.{AGENT_ID}.started", started)

    # Set up QoS — process one message at a time
    channel.basic_qos(prefetch_count=1)

    # SIGTERM handler for graceful shutdown
    def handle_sigterm(signum, frame):
        log("Received SIGTERM — shutting down gracefully")
        try:
            stopped = make_message(
                "event.stopped",
                {
                    "agentId": AGENT_ID,
                    "reason": "sigterm",
                },
            )
            publish_event(channel, f"agent.{AGENT_ID}.completed", stopped)
        except Exception as e:
            log(f"Failed to publish stopped event: {e}")

        try:
            channel.stop_consuming()
        except Exception:
            pass

    signal.signal(signal.SIGTERM, handle_sigterm)

    # Start consuming commands
    on_message = make_on_message(channel, api_key)
    channel.basic_consume(
        queue=AGENT_QUEUE,
        on_message_callback=on_message,
        auto_ack=False,
    )

    log(f"OpenCode agent ready — consuming from queue '{AGENT_QUEUE}'")

    try:
        channel.start_consuming()
    except (SystemExit, KeyboardInterrupt):
        pass
    finally:
        try:
            connection.close()
        except Exception:
            pass
        log("OpenCode agent stopped")


if __name__ == "__main__":
    main()
