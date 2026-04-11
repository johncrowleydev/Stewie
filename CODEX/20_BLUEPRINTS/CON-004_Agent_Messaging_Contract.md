---
id: CON-004
title: "Agent Messaging Contract"
type: contract
status: APPROVED
version: "1.1.0"
created: 2026-04-10
updated: 2026-04-11
owner: architect
tags: [rabbitmq, messaging, agent, contract, infrastructure]
agents: [architect, developer]
related: [BLU-001, JOB-016]
---

> **BLUF:** Defines the RabbitMQ exchange topology, routing keys, and JSON message schemas for all API-to-agent communication. Three exchanges (stewie.commands, stewie.events, stewie.chat) with 10 message types covering task assignment, progress events, chat relay, and plan approval.

# CON-004: Agent Messaging Contract

> **Scope:** This contract governs wire-format messages only. Application-level semantics
> (how agents act on messages) are defined in the agent role templates (AGT-001/002/003).

---

## 1. Transport

| Property | Value |
|:---------|:------|
| Broker | RabbitMQ 3.x with management plugin |
| Protocol | AMQP 0-9-1 |
| Serialization | JSON (UTF-8, `application/json`) |
| Serialization Policy | Strict `camelCase` enforcement for all top-level properties and nested domains |
| Virtual Host | `stewie` |

---

## 2. Exchange Topology

Three exchanges separate concerns by message flow direction and purpose.

| Exchange | Type | Durable | Publisher | Consumer | Purpose |
|:---------|:-----|:--------|:----------|:---------|:--------|
| `stewie.commands` | direct | yes | API | Agent containers | Task assignments, configuration pushes |
| `stewie.events` | topic | yes | Agent containers | API | Progress, blockers, completion, failures |
| `stewie.chat` | direct | yes | API | Architect Agent | Human chat relay to Architect |

### 2.1 Why Three Exchanges

A single topic exchange was considered and rejected. Separating command flow (API → agents)
from event flow (agents → API) makes message ownership explicit and simplifies dead-letter
handling. Chat gets its own exchange because it has distinct delivery semantics (guaranteed
delivery to a specific Architect instance).

---

## 3. Queue Naming Conventions

| Queue Pattern | Bound Exchange | Routing Key | Consumer |
|:-------------|:---------------|:------------|:---------|
| `agent.{agentId}.commands` | `stewie.commands` | `agent.{agentId}` | Specific agent container |
| `stewie.events.api` | `stewie.events` | `agent.#` | API event consumer service |
| `architect.{projectId}` | `stewie.chat` | `architect.{projectId}` | Architect Agent for project |

- `{agentId}` — GUID assigned when an agent session is created
- `{projectId}` — GUID of the Stewie project

### 3.1 Queue Properties

All queues are:
- **Durable** — survive broker restarts
- **Non-exclusive** — not tied to connection lifetime
- **Auto-delete: false** — must be explicitly deleted by the API when agent sessions end

---

## 4. Routing Keys

### 4.1 Commands Exchange (direct)

Routing key is the literal agent queue binding: `agent.{agentId}`

### 4.2 Events Exchange (topic)

| Routing Key Pattern | Example | Meaning |
|:-------------------|:--------|:--------|
| `agent.{agentId}.started` | `agent.abc123.started` | Agent container connected and ready |
| `agent.{agentId}.progress` | `agent.abc123.progress` | Intermediate progress update |
| `agent.{agentId}.blocker` | `agent.abc123.blocker` | Agent is blocked, needs human/architect input |
| `agent.{agentId}.completed` | `agent.abc123.completed` | Task finished successfully |
| `agent.{agentId}.failed` | `agent.abc123.failed` | Task failed |
| `agent.{agentId}.stdout` | `agent.abc123.stdout` | Container stdout line (streaming) |

### 4.3 Chat Exchange (direct)

Routing key: `architect.{projectId}`

---

## 5. Message Envelope

All messages share a common envelope structure. Payload varies by message type.

```json
{
  "messageId": "uuid-v4",
  "timestamp": "2026-04-10T20:00:00Z",
  "type": "command.assign_task | event.started | event.progress | ...",
  "source": "api | agent.{agentId}",
  "correlationId": "uuid-v4 (optional, for request-response pairing)",
  "payload": { }
}
```

| Field | Type | Required | Description |
|:------|:-----|:---------|:------------|
| `messageId` | string (UUID) | yes | Unique identifier for this message |
| `timestamp` | string (ISO 8601) | yes | When the message was created |
| `type` | string | yes | Discriminator for payload deserialization |
| `source` | string | yes | Who published this message |
| `correlationId` | string (UUID) | no | Links related messages (e.g., command → completion event) |
| `payload` | object | yes | Type-specific content — see §6 |

---

## 6. Message Types and Payloads

### 6.1 Commands (API → Agent)

#### `command.assign_task`

Sent when the API assigns a task to an agent container.

```json
{
  "type": "command.assign_task",
  "payload": {
    "taskId": "uuid",
    "jobId": "uuid",
    "projectId": "uuid",
    "taskTitle": "Implement user authentication",
    "taskDescription": "Build JWT-based auth with...",
    "workspacePath": "/workspaces/project-abc/task-123",
    "agentRole": "developer | tester",
    "configuration": {
      "governanceRules": ["GOV-003", "GOV-004"],
      "timeoutSeconds": 300,
      "maxRetries": 2
    }
  }
}
```

#### `command.terminate`

Sent when the API needs to stop an agent.

```json
{
  "type": "command.terminate",
  "payload": {
    "reason": "timeout | user_cancelled | job_cancelled",
    "gracePeriodSeconds": 10
  }
}
```

---

### 6.2 Events (Agent → API)

#### `event.started`

Agent container is running and ready to receive work.

```json
{
  "type": "event.started",
  "payload": {
    "agentId": "uuid",
    "runtimeName": "stub | claude-code | open-code",
    "capabilities": ["code-generation", "testing", "governance-check"]
  }
}
```

#### `event.progress`

Intermediate progress update during task execution.

```json
{
  "type": "event.progress",
  "payload": {
    "agentId": "uuid",
    "taskId": "uuid",
    "percentComplete": 45,
    "currentStep": "Running governance checks",
    "details": "Checking GOV-003 compliance..."
  }
}
```

#### `event.blocker`

Agent is blocked and needs human or architect input.

```json
{
  "type": "event.blocker",
  "payload": {
    "agentId": "uuid",
    "taskId": "uuid",
    "blockerType": "question | approval_required | missing_context",
    "question": "The API contract specifies JWT auth but no secret is configured. Should I generate one?",
    "context": { }
  }
}
```

#### `event.completed`

Task finished successfully.

```json
{
  "type": "event.completed",
  "payload": {
    "agentId": "uuid",
    "taskId": "uuid",
    "summary": "Implemented JWT authentication with...",
    "artifacts": [
      { "path": "src/Auth/JwtService.cs", "action": "created" },
      { "path": "src/Auth/JwtServiceTests.cs", "action": "created" }
    ],
    "governanceReport": {
      "passed": true,
      "checks": [
        { "rule": "GOV-003", "status": "pass", "details": "All functions under 60 lines" }
      ]
    }
  }
}
```

#### `event.failed`

Task failed.

```json
{
  "type": "event.failed",
  "payload": {
    "agentId": "uuid",
    "taskId": "uuid",
    "errorType": "build_failure | test_failure | timeout | runtime_error",
    "errorMessage": "dotnet build failed with 3 errors",
    "details": "error CS1002: ; expected at line 42...",
    "retryable": true
  }
}
```

#### `event.stdout`

Container stdout/stderr line for real-time streaming.

```json
{
  "type": "event.stdout",
  "payload": {
    "agentId": "uuid",
    "taskId": "uuid",
    "stream": "stdout | stderr",
    "line": "Building project src/Stewie.Api..."
  }
}
```

---

### 6.3 Chat (API → Architect)

#### `chat.human_message`

Relayed from the SignalR chat system when a human sends a message.

```json
{
  "type": "chat.human_message",
  "payload": {
    "projectId": "uuid",
    "userId": "uuid",
    "username": "admin",
    "content": "Can you explain why the build failed?",
    "chatMessageId": "uuid"
  }
}
```

#### `chat.architect_response`

Published by the Architect agent back to the API (via `stewie.events` exchange).

```json
{
  "type": "chat.architect_response",
  "payload": {
    "agentId": "uuid",
    "projectId": "uuid",
    "content": "The build failed because of a missing dependency...",
    "replyToChatMessageId": "uuid"
  }
}
```

---

## 7. Error Handling

### 7.1 Dead Letter Exchange

Messages that fail processing are routed to a dead-letter exchange:

| Property | Value |
|:---------|:------|
| DLX name | `stewie.dlx` |
| DLX type | fanout |
| DLQ name | `stewie.dead-letters` |

All queues are configured with `x-dead-letter-exchange: stewie.dlx`.

### 7.2 Message TTL

| Message Type | TTL |
|:-------------|:----|
| Commands | 5 minutes (300,000 ms) |
| Events | No TTL (persisted immediately) |
| Chat | 1 minute (60,000 ms) |

### 7.3 Retry Policy

Failed event processing retries up to 3 times with exponential backoff (1s, 2s, 4s)
before routing to DLQ.

---

## 8. Health Check

The API exposes RabbitMQ connection status via the `/health` endpoint.

```json
{
  "status": "Healthy",
  "checks": {
    "rabbitmq": {
      "status": "Healthy",
      "description": "RabbitMQ connection is active"
    },
    "sqlserver": {
      "status": "Healthy"
    }
  }
}
```

---

## 9. Versioning

Message schemas are versioned via the contract version (this document).
Breaking changes require a new major version and an EVO- proposal.

| Version | Date | Changes |
|:--------|:-----|:--------|
| 1.0.0 | 2026-04-10 | Initial release — 3 exchanges, 8 message types |
| 1.1.0 | 2026-04-11 | Added chat.plan_proposed, command.plan_decision (JOB-022) |

---

## 10. Plan Approval Protocol (v1.1.0)

Added in JOB-022 to support the Architect Agent's plan-first workflow.

### 10.1 `chat.plan_proposed` (Architect → API → Human)

Published by the Architect when it has a plan ready for review.
Routed through the `stewie.events` exchange.

```json
{
  "type": "chat.plan_proposed",
  "payload": {
    "agentId": "uuid",
    "projectId": "uuid",
    "planId": "uuid",
    "summary": "I propose creating 2 jobs with 5 tasks...",
    "planMarkdown": "# Plan\n\n## Job 1: ...",
    "planJson": { "jobs": [] }
  }
}
```

### 10.2 `command.plan_decision` (API → Architect)

Sent when the Human approves or rejects a plan.
Routed through the `stewie.commands` exchange.

```json
{
  "type": "command.plan_decision",
  "payload": {
    "planId": "uuid",
    "decision": "approved | rejected",
    "feedback": "optional human feedback text"
  }
}
```
