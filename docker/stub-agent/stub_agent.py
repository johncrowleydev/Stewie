"""
stewie-stub-agent — Lightweight test agent that speaks the CON-004 protocol.

This agent connects to RabbitMQ, publishes lifecycle events, consumes commands,
and responds to chat messages. It validates the full messaging loop without any
LLM dependency.

Environment variables (all required):
    RABBITMQ_HOST   - RabbitMQ hostname (e.g., host.docker.internal)
    RABBITMQ_PORT   - AMQP port (default: 5672)
    RABBITMQ_USER   - RabbitMQ username
    RABBITMQ_PASS   - RabbitMQ password
    RABBITMQ_VHOST  - RabbitMQ virtual host (default: /)
    AGENT_QUEUE     - Queue name to consume commands from
    AGENT_ID        - Unique agent/session identifier (GUID)
    PROJECT_ID      - Project identifier (GUID)
    AGENT_ROLE      - Agent role: architect, developer, tester

REF: JOB-017 T-168, CON-004 §5-§6
"""

import json
import os
import signal
import sys
import threading
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

# Exchange names per CON-004 §2
COMMANDS_EXCHANGE = "stewie.commands"
EVENTS_EXCHANGE = "stewie.events"
CHAT_EXCHANGE = "stewie.chat"

# Heartbeat interval in seconds
HEARTBEAT_INTERVAL = 30

# Maximum connection retry attempts
MAX_RETRIES = 5
RETRY_DELAY = 2  # seconds


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
            delivery_mode=2,  # persistent
            content_type="application/json",
        ),
    )
    print(f"[event] Published {message['type']} -> {routing_key}", flush=True)


def log(msg: str) -> None:
    """Print a timestamped log message."""
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    print(f"[{ts}] {msg}", flush=True)


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


# ── Command Handlers ────────────────────────────────────────────────────

def handle_chat_message(channel, message: dict) -> None:
    """Handle chat.human_message — echo the content back as an architect response."""
    payload = message.get("payload", {})
    content = payload.get("content", "(empty message)")
    project_id = payload.get("projectId", PROJECT_ID)

    log(f"Received chat message: '{content[:80]}...' " if len(content) > 80
        else f"Received chat message: '{content}'")

    response = make_message(
        "chat.architect_response",
        {
            "agentId": AGENT_ID,
            "projectId": project_id,
            "content": f"[stub-agent echo] {content}",
            "replyToChatMessageId": payload.get("chatMessageId", ""),
        },
        correlation_id=message.get("correlationId"),
    )
    routing_key = f"agent.{AGENT_ID}.completed"
    publish_event(channel, routing_key, response)


def handle_assign_task(channel, message: dict) -> None:
    """Handle command.assign_task — publish progress, wait 2s, publish completed."""
    payload = message.get("payload", {})
    task_id = payload.get("taskId", "unknown")
    task_title = payload.get("taskTitle", "unnamed task")

    log(f"Received task assignment: {task_title} (taskId={task_id})")

    # Publish progress
    progress = make_message(
        "event.progress",
        {
            "agentId": AGENT_ID,
            "taskId": task_id,
            "percentComplete": 50,
            "currentStep": "Working on it...",
            "details": f"Stub agent processing: {task_title}",
        },
        correlation_id=message.get("correlationId"),
    )
    publish_event(channel, f"agent.{AGENT_ID}.progress", progress)

    # Simulate work
    time.sleep(2)

    # Publish completed
    completed = make_message(
        "event.completed",
        {
            "agentId": AGENT_ID,
            "taskId": task_id,
            "summary": f"Stub agent completed: {task_title}",
            "artifacts": [],
            "governanceReport": {"passed": True, "checks": []},
        },
        correlation_id=message.get("correlationId"),
    )
    publish_event(channel, f"agent.{AGENT_ID}.completed", completed)


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
    # Signal the main loop to exit
    raise SystemExit(0)


# ── Message Consumer ────────────────────────────────────────────────────

def on_message(channel, method, _properties, body):
    """Dispatch incoming commands to the appropriate handler."""
    try:
        message = json.loads(body.decode("utf-8"))
        msg_type = message.get("type", "")

        log(f"Received command: type={msg_type}")

        if msg_type == "chat.human_message":
            handle_chat_message(channel, message)
        elif msg_type == "command.assign_task":
            handle_assign_task(channel, message)
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


# ── Heartbeat Thread ────────────────────────────────────────────────────

def heartbeat_loop(connection: pika.BlockingConnection, stop_event: threading.Event) -> None:
    """Publish heartbeat events every HEARTBEAT_INTERVAL seconds."""
    while not stop_event.is_set():
        stop_event.wait(HEARTBEAT_INTERVAL)
        if stop_event.is_set():
            break

        try:
            # Use connection.add_callback_threadsafe for thread-safe publishing
            def publish_heartbeat():
                try:
                    channel = connection.channel()
                    heartbeat = make_message(
                        "agent.heartbeat",
                        {
                            "agentId": AGENT_ID,
                            "projectId": PROJECT_ID,
                            "role": AGENT_ROLE,
                            "status": "running",
                        },
                    )
                    publish_event(channel, f"agent.{AGENT_ID}.progress", heartbeat)
                    channel.close()
                except Exception as e:
                    log(f"Heartbeat publish failed: {e}")

            connection.add_callback_threadsafe(publish_heartbeat)
        except Exception as e:
            log(f"Heartbeat scheduling failed: {e}")


# ── Main ────────────────────────────────────────────────────────────────

def main() -> None:
    """Entry point — connect, publish started, consume commands, heartbeat."""
    log(f"stewie-stub-agent starting")
    log(f"  AGENT_ID:    {AGENT_ID}")
    log(f"  PROJECT_ID:  {PROJECT_ID}")
    log(f"  AGENT_ROLE:  {AGENT_ROLE}")
    log(f"  AGENT_QUEUE: {AGENT_QUEUE}")
    log(f"  RABBITMQ:    {RABBITMQ_HOST}:{RABBITMQ_PORT}")

    if not AGENT_QUEUE:
        log("ERROR: AGENT_QUEUE environment variable is required")
        sys.exit(1)

    # Connect to RabbitMQ
    connection = connect_with_retry()
    channel = connection.channel()

    # Declare the events exchange (idempotent — safe if API already declared it)
    channel.exchange_declare(
        exchange=EVENTS_EXCHANGE,
        exchange_type="topic",
        durable=True,
    )

    # Declare the commands exchange (idempotent)
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

    # If this is an architect agent, bind to the chat exchange per CON-004
    if AGENT_ROLE == "architect":
        channel.exchange_declare(
            exchange=CHAT_EXCHANGE,
            exchange_type="direct",
            durable=True,
        )
        chat_queue = f"architect.{PROJECT_ID}"
        channel.queue_declare(queue=chat_queue, durable=True)
        channel.queue_bind(
            queue=chat_queue,
            exchange=CHAT_EXCHANGE,
            routing_key=chat_queue,
        )
        # Consume from the chat queue too
        channel.basic_consume(
            queue=chat_queue,
            on_message_callback=on_message,
            auto_ack=False,
        )
        log(f"Architect agent bound to chat exchange on queue '{chat_queue}'")

    # Publish event.started
    started = make_message(
        "event.started",
        {
            "agentId": AGENT_ID,
            "runtimeName": "stub",
            "capabilities": ["echo", "task-simulation"],
        },
    )
    publish_event(channel, f"agent.{AGENT_ID}.started", started)

    # Set up QoS — process one message at a time
    channel.basic_qos(prefetch_count=1)

    # Start heartbeat thread
    stop_event = threading.Event()
    heartbeat_thread = threading.Thread(
        target=heartbeat_loop,
        args=(connection, stop_event),
        daemon=True,
    )
    heartbeat_thread.start()

    # SIGTERM handler for graceful shutdown
    def handle_sigterm(signum, frame):
        log("Received SIGTERM — shutting down gracefully")
        stop_event.set()

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
    channel.basic_consume(
        queue=AGENT_QUEUE,
        on_message_callback=on_message,
        auto_ack=False,
    )

    log(f"Stub agent ready — consuming from queue '{AGENT_QUEUE}'")

    try:
        channel.start_consuming()
    except (SystemExit, KeyboardInterrupt):
        pass
    finally:
        stop_event.set()
        try:
            connection.close()
        except Exception:
            pass
        log("Stub agent stopped")


if __name__ == "__main__":
    main()
