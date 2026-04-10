---
id: JOB-016
title: "Job 016 — RabbitMQ Infrastructure + CON-004"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5b, rabbitmq, messaging]
related: [PRJ-001, BLU-001, CON-002, JOB-012]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Stand up RabbitMQ, define the agent messaging contract (CON-004), and wire a publish/consume pipeline into the API. After this job, the API can publish commands to RabbitMQ and consume agent events — the messaging backbone for all future agent communication.

# Job 016 — RabbitMQ Infrastructure + CON-004

---

## 1. Context

Phase 5b introduces agent-to-API communication via RabbitMQ. This job builds the foundation:

1. **Docker Compose** — RabbitMQ container with management UI
2. **CON-004** — formal message contract (exchanges, routing keys, JSON schemas)
3. **IRabbitMqService** — application-layer interface for publishing/consuming
4. **RabbitMqService** — infrastructure-layer implementation using official `RabbitMQ.Client`
5. **Consumer hosted service** — background worker consuming agent events
6. **Health check** — RabbitMQ connectivity in `/health`

This job does NOT launch any agent containers — that's JOB-017. This is purely the messaging plumbing.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Docker compose, CON-004, RabbitMQ connection config, health check | `feature/JOB-016-infra` |
| Dev B | IRabbitMqService, RabbitMqService, consumer hosted service, unit tests | `feature/JOB-016-service` |

**Merge order: Dev A first, Dev B second.**

**Dependency: JOB-012 must be merged to main (it is).**

---

## 3. Design Decisions

### 3.1 Exchange Topology (3 exchanges)

| Exchange | Type | Purpose | Publisher | Consumer |
|:---------|:-----|:--------|:----------|:---------|
| `stewie.commands` | direct | Task assignments, config pushes, chat relay | API | Agent containers |
| `stewie.events` | topic | Agent progress, blockers, completion | Agent containers | API |
| `stewie.chat` | direct | Human chat → Architect Agent | API | Architect Agent |

### 3.2 Routing Keys

**`stewie.commands` (direct):**
- Routing key = agent's queue name (e.g., `agent.{containerId}`)

**`stewie.events` (topic):**

| Pattern | Example | Meaning |
|:--------|:--------|:--------|
| `agent.{agentId}.started` | `agent.abc123.started` | Agent connected and ready |
| `agent.{agentId}.progress` | `agent.abc123.progress` | Intermediate progress update |
| `agent.{agentId}.blocker` | `agent.abc123.blocker` | Agent blocked, needs input |
| `agent.{agentId}.completed` | `agent.abc123.completed` | Task finished successfully |
| `agent.{agentId}.failed` | `agent.abc123.failed` | Task failed |
| `agent.{agentId}.chat` | `agent.abc123.chat` | Agent sending a chat message |

**`stewie.chat` (direct):**
- Routing key = `project.{projectId}` (targets the Architect for that project)

### 3.3 Client Library

Use official `RabbitMQ.Client` NuGet package (v7.x). No MassTransit — keep dependencies minimal and control explicit.

---

## 4. Tasks

### Dev A — Infrastructure

#### T-154: Docker Compose for RabbitMQ

**Create** `docker-compose.yml` (or update existing) at repo root:

```yaml
services:
  rabbitmq:
    image: rabbitmq:4-management
    container_name: stewie-rabbitmq
    ports:
      - "5672:5672"    # AMQP
      - "15672:15672"  # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: stewie
      RABBITMQ_DEFAULT_PASS: stewie_dev
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rabbitmq_data:
```

**Also include** the existing SQL Server container so we have a single `docker-compose up` for all infrastructure.

**Acceptance criteria:**
- [ ] `docker compose up -d` starts both RabbitMQ and SQL Server
- [ ] Management UI accessible at `http://localhost:15672` (stewie/stewie_dev)
- [ ] AMQP port 5672 accessible

---

#### T-155: RabbitMQ Connection Configuration

**Add to** `appsettings.json`:

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "stewie",
    "Password": "stewie_dev",
    "VirtualHost": "/",
    "ExchangePrefix": "stewie"
  }
}
```

**Create** `Stewie.Infrastructure/Messaging/RabbitMqOptions.cs`:
- POCO class matching the config section
- Registered via `IOptions<RabbitMqOptions>` in DI

**Acceptance criteria:**
- [ ] Options bind from config
- [ ] Connection string overridable via environment variables

---

#### T-156: CON-004 Agent Messaging Contract

**Create** `CODEX/20_BLUEPRINTS/CON-004_Agent_Messaging_Contract.md`:

Must define:
- Exchange topology (§1)
- Routing key patterns (§2)
- Message envelope schema (§3):
  ```json
  {
    "messageId": "guid",
    "timestamp": "ISO-8601",
    "type": "agent.started | agent.progress | agent.blocker | agent.completed | agent.failed | agent.chat",
    "agentId": "containerId",
    "projectId": "guid",
    "jobId": "guid (optional)",
    "taskId": "guid (optional)",
    "payload": { ... }
  }
  ```
- Command message schemas (task assignment, chat relay) (§4)
- Event message schemas (started, progress, blocker, completed, failed, chat) (§5)
- Dead-letter handling (§6)
- Heartbeat/keepalive (§7)

**Acceptance criteria:**
- [ ] Contract follows CON format (frontmatter, versioning)
- [ ] All message types have JSON schema definitions
- [ ] Registered in MANIFEST.yaml

---

#### T-157: RabbitMQ Health Check

**Add** a RabbitMQ health check to the existing `/health` endpoint:

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ(/* connection string from options */);
```

Use the `AspNetCore.HealthChecks.Rabbitmq` NuGet package or implement a simple custom check that opens/closes a connection.

**Acceptance criteria:**
- [ ] `/health` reports RabbitMQ status (UP/DOWN)
- [ ] Degraded when RabbitMQ is unreachable (API still starts)
- [ ] Logs connection failures at Warning level

---

### Dev B — Service Layer

#### T-158: IRabbitMqService Interface

**Create** `Stewie.Application/Interfaces/IRabbitMqService.cs`:

```csharp
public interface IRabbitMqService : IAsyncDisposable
{
    /// <summary>Publish a command to a specific agent.</summary>
    Task PublishCommandAsync(string routingKey, AgentMessage message, CancellationToken ct = default);

    /// <summary>Publish a chat message to a project's Architect.</summary>
    Task PublishChatAsync(Guid projectId, AgentMessage message, CancellationToken ct = default);

    /// <summary>Subscribe to agent events with a topic pattern.</summary>
    Task SubscribeEventsAsync(string topicPattern, Func<AgentMessage, Task> handler, CancellationToken ct = default);

    /// <summary>Declare a queue for a specific agent and bind it to commands exchange.</summary>
    Task DeclareAgentQueueAsync(string agentId, CancellationToken ct = default);

    /// <summary>Delete an agent's queue (on container teardown).</summary>
    Task DeleteAgentQueueAsync(string agentId, CancellationToken ct = default);
}
```

**Create** `Stewie.Domain/Messaging/AgentMessage.cs`:

```csharp
public class AgentMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? TaskId { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}
```

**Acceptance criteria:**
- [ ] Interface is in Application layer (no RabbitMQ dependency)
- [ ] AgentMessage is in Domain layer
- [ ] DTOs are JSON-serializable

---

#### T-159: RabbitMqService Implementation

**Create** `Stewie.Infrastructure/Messaging/RabbitMqService.cs`:

- Uses `RabbitMQ.Client` v7.x (async API)
- Manages a persistent `IConnection` (lazy-initialized, reconnects on failure)
- Creates `IChannel` per operation (channels are lightweight)
- On first use, declares the 3 exchanges:
  - `stewie.commands` (direct, durable)
  - `stewie.events` (topic, durable)
  - `stewie.chat` (direct, durable)
- `PublishCommandAsync`: serializes `AgentMessage` to JSON, publishes to `stewie.commands` with routing key
- `PublishChatAsync`: publishes to `stewie.chat` with routing key `project.{projectId}`
- `SubscribeEventsAsync`: creates an exclusive auto-delete queue bound to `stewie.events` with the topic pattern, registers async consumer
- `DeclareAgentQueueAsync`: declares `agent.{agentId}` queue, binds to `stewie.commands`
- `DeleteAgentQueueAsync`: deletes `agent.{agentId}` queue
- Implements `IAsyncDisposable` — closes connection on shutdown

**Register** as singleton in DI.

**Acceptance criteria:**
- [ ] Connection resilient to transient failures (logs and retries)
- [ ] Exchanges created idempotently
- [ ] Messages serialized as JSON with `System.Text.Json`
- [ ] Thread-safe

---

#### T-160: RabbitMqConsumerHostedService

**Create** `Stewie.Api/Services/RabbitMqConsumerHostedService.cs`:

An `IHostedService` that subscribes to `stewie.events` topic exchange with pattern `agent.#` on startup:

- Deserializes incoming `AgentMessage`
- Dispatches to appropriate handler based on `Type`:
  - `agent.started` → log + persist Event + SignalR push
  - `agent.progress` → persist Event + SignalR push
  - `agent.blocker` → persist Event + SignalR push
  - `agent.completed` → persist Event + SignalR push + trigger orchestration
  - `agent.failed` → persist Event + SignalR push + trigger orchestration
  - `agent.chat` → persist as ChatMessage + SignalR push
- All handlers are exception-safe (log + continue, never crash the consumer)

**Register** in `Program.cs` as a hosted service.

**Acceptance criteria:**
- [ ] Consumes messages continuously while API is running
- [ ] Persists events to Events table
- [ ] Pushes updates via IRealTimeNotifier
- [ ] Does not crash on malformed messages (logs warning, skips)

---

#### T-161: Unit Tests

**Create** `Stewie.Tests/Services/RabbitMqServiceTests.cs`:

| Test | Description |
|:-----|:------------|
| `AgentMessage_SerializesCorrectly` | Round-trip JSON serialization |
| `AgentMessage_DefaultsPopulated` | MessageId and Timestamp auto-populated |
| `ConsumerHostedService_DispatchesStarted` | Mocked consumer dispatches `agent.started` correctly |
| `ConsumerHostedService_IgnoresMalformed` | Malformed JSON logged but not thrown |
| `ConsumerHostedService_PersistsEvent` | Event written to repository |

**Acceptance criteria:**
- [ ] All 5 tests pass
- [ ] No RabbitMQ server required for unit tests (mocked interfaces)

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-004 | NEW — Agent Messaging Contract | v1.0.0 |
| CON-002 | No changes yet (agent endpoints added in JOB-017) | — |

---

## 6. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# RabbitMQ running
docker compose up -d
curl -sf http://localhost:15672/api/overview -u stewie:stewie_dev

# Health check includes RabbitMQ
curl -sf http://localhost:5275/health
```

**Exit criteria:**
- Docker compose starts RabbitMQ + SQL Server
- API connects to RabbitMQ on startup
- Health check reports RabbitMQ status
- Console consumer logs received test messages
- All existing tests pass + 5 new unit tests

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-016 created for Phase 5b RabbitMQ Infrastructure |
