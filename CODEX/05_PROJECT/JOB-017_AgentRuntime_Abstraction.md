---
id: JOB-017
title: "Job 017 — IAgentRuntime Abstraction + Stub Runtime"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5b, agent-runtime, containers]
related: [PRJ-001, BLU-001, CON-004, JOB-016]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Build the pluggable agent runtime abstraction (`IAgentRuntime`) and a stub implementation for end-to-end testing. After this job, the API can launch a stub agent container that connects to RabbitMQ, receives commands, and publishes events — validating the entire messaging loop without any LLM dependency.

# Job 017 — IAgentRuntime Abstraction + Stub Runtime

---

## 1. Context

JOB-016 built the RabbitMQ messaging backbone. This job builds on top of it:

1. **IAgentRuntime** — the pluggable abstraction for launching agent containers
2. **AgentSession** — entity tracking running agent containers
3. **AgentLifecycleService** — manages launch/terminate/status with event emission
4. **StubAgentRuntime** — a test runtime that launches a Python container connected to RabbitMQ
5. **stewie-stub-agent** — Docker image with a Python script that speaks the CON-004 protocol
6. **Agent REST API** — endpoints for launching, terminating, and querying agents

This job does NOT wire the Architect into the chat system — that's JOB-018.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | IAgentRuntime, AgentSession entity + migration, AgentLifecycleService, AgentsController | `feature/JOB-017-runtime` |
| Dev B | StubAgentRuntime, stewie-stub-agent Docker image, integration tests | `feature/JOB-017-stub` |

**Merge order: Dev A first, Dev B second.**

**Dependency: JOB-016 must be merged to main.**

---

## 3. Tasks

### Dev A — Runtime Abstraction

#### T-162: IAgentRuntime Interface

**Create** `Stewie.Application/Interfaces/IAgentRuntime.cs`:

```csharp
/// <summary>
/// Pluggable abstraction for launching and managing agent containers.
/// Each implementation wraps a specific agentic framework (Claude Code, Aider, etc.).
/// REF: BLU-001 §7, PRJ-001 Phase 5b
/// </summary>
public interface IAgentRuntime
{
    /// <summary>Human-readable name of this runtime (e.g., "stub", "claude-code", "aider").</summary>
    string RuntimeName { get; }

    /// <summary>Launch an agent container. Returns the container ID.</summary>
    Task<string> LaunchAsync(AgentLaunchRequest request, CancellationToken ct = default);

    /// <summary>Terminate a running agent container.</summary>
    Task TerminateAsync(string containerId, CancellationToken ct = default);

    /// <summary>Check if a container is still running.</summary>
    Task<bool> IsRunningAsync(string containerId, CancellationToken ct = default);
}
```

**Create** `Stewie.Application/Models/AgentLaunchRequest.cs`:

```csharp
public class AgentLaunchRequest
{
    /// <summary>Unique session ID for this agent.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Project this agent belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Task this agent is assigned to (null for Architect).</summary>
    public Guid? TaskId { get; set; }

    /// <summary>Agent role: "architect", "developer", "tester".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>RabbitMQ connection details for the container.</summary>
    public RabbitMqConnectionInfo RabbitMq { get; set; } = new();

    /// <summary>Workspace path to mount (for dev/tester agents).</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>Additional runtime-specific configuration.</summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

public class RabbitMqConnectionInfo
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string AgentQueueName { get; set; } = string.Empty;
}
```

**Acceptance criteria:**
- [ ] Interface is framework-agnostic (no Docker or RabbitMQ types in signature)
- [ ] LaunchRequest carries all info the container needs to connect

---

#### T-163: AgentSession Entity + Migration

**Create** `Stewie.Domain/Entities/AgentSession.cs`:

```csharp
public class AgentSession
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string ContainerId { get; set; } = string.Empty;
    public string RuntimeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;   // "architect", "developer", "tester"
    public string Status { get; set; } = "Pending";     // Pending, Running, Stopped, Failed
    public DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
}
```

**Create** NHibernate mapping `Stewie.Infrastructure/Mappings/AgentSessionMap.cs`.

**Create** FluentMigrator migration `Migration_017_CreateAgentSessions`:
- Table `AgentSessions`: Id (PK), ProjectId, TaskId (nullable), ContainerId, RuntimeName, Role, Status, StartedAt, StoppedAt (nullable)
- Index on `ProjectId`
- Index on `Status`

**Create** `IAgentSessionRepository` + `AgentSessionRepository`:
- `SaveAsync(AgentSession session)`
- `GetByIdAsync(Guid id)`
- `GetByProjectIdAsync(Guid projectId)` — returns active sessions for a project
- `GetActiveByProjectAndRoleAsync(Guid projectId, string role)` — find running Architect

**Register** repository in DI.

**Acceptance criteria:**
- [ ] Migration runs clean on fresh DB
- [ ] NHibernate mapping verified by build

---

#### T-164: AgentLifecycleService

**Create** `Stewie.Application/Services/AgentLifecycleService.cs`:

```csharp
public class AgentLifecycleService
{
    private readonly IAgentRuntime _runtime;
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IRealTimeNotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AgentLifecycleService> _logger;

    /// <summary>Launch an agent container and track its session.</summary>
    public async Task<AgentSession> LaunchAgentAsync(
        Guid projectId, Guid? taskId, string role, CancellationToken ct);

    /// <summary>Terminate a running agent and update session.</summary>
    public async Task TerminateAgentAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Get active sessions for a project.</summary>
    public async Task<IList<AgentSession>> GetActiveSessionsAsync(Guid projectId);
}
```

**LaunchAgentAsync flow:**
1. Create `AgentSession` (Status = "Pending"), persist
2. Declare agent's RabbitMQ queue via `IRabbitMqService.DeclareAgentQueueAsync`
3. Build `AgentLaunchRequest` with RabbitMQ connection info + queue name
4. Call `IAgentRuntime.LaunchAsync` → get containerId
5. Update session with containerId, Status = "Running"
6. Emit event + SignalR notification
7. Return session

**TerminateAgentAsync flow:**
1. Load session
2. Call `IAgentRuntime.TerminateAsync(containerId)`
3. Delete agent's RabbitMQ queue
4. Update session: Status = "Stopped", StoppedAt = now
5. Emit event + SignalR notification

**Acceptance criteria:**
- [ ] Launch creates queue, starts container, persists session
- [ ] Terminate kills container, deletes queue, updates session
- [ ] Errors during launch roll back (delete queue, update session to Failed)
- [ ] Thread-safe for concurrent launches

---

#### T-165: AgentsController REST API

**Create** `Stewie.Api/Controllers/AgentsController.cs`:

```csharp
[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    /// <summary>Launch a new agent for a project.</summary>
    [HttpPost]
    public async Task<IActionResult> Launch([FromBody] LaunchAgentRequest request);

    /// <summary>Terminate a running agent.</summary>
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> Terminate(Guid sessionId);

    /// <summary>Get active agents for a project.</summary>
    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(Guid projectId);

    /// <summary>Get a specific agent session.</summary>
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(Guid sessionId);
}
```

**Request body for POST:**
```json
{
    "projectId": "guid",
    "taskId": "guid (optional)",
    "role": "architect | developer | tester"
}
```

**Response (201 Created):**
```json
{
    "id": "session-guid",
    "projectId": "guid",
    "containerId": "docker-container-id",
    "runtimeName": "stub",
    "role": "architect",
    "status": "Running",
    "startedAt": "ISO-8601"
}
```

**Business rules:**
- Only one Architect agent per project at a time (409 Conflict if already running)
- Validate project exists (404 if not)
- Extract user from JWT for audit trail

**Acceptance criteria:**
- [ ] POST launches agent, returns 201
- [ ] DELETE terminates agent, returns 204
- [ ] GET returns session(s)
- [ ] 409 for duplicate Architect
- [ ] 404 for nonexistent project/session

---

#### T-166: Unit Tests (Lifecycle)

**Create** `Stewie.Tests/Services/AgentLifecycleServiceTests.cs`:

| Test | Description |
|:-----|:------------|
| `LaunchAgent_CreatesSession_ReturnsRunning` | Happy path launch |
| `LaunchAgent_DuplicateArchitect_Throws` | Second Architect for same project rejected |
| `TerminateAgent_UpdatesSession_DeletesQueue` | Happy path terminate |
| `LaunchAgent_RuntimeFailure_SetsStatusFailed` | Container launch fails → session marked Failed |
| `GetActiveSessions_FiltersCorrectly` | Only Running sessions returned |

**Acceptance criteria:**
- [ ] All 5 tests pass
- [ ] Mocked IAgentRuntime, IRabbitMqService

---

### Dev B — Stub Runtime

#### T-167: StubAgentRuntime Implementation

**Create** `Stewie.Infrastructure/AgentRuntimes/StubAgentRuntime.cs`:

```csharp
public class StubAgentRuntime : IAgentRuntime
{
    public string RuntimeName => "stub";

    public async Task<string> LaunchAsync(AgentLaunchRequest request, CancellationToken ct)
    {
        // docker run -d --name stewie-agent-{sessionId}
        //   -e RABBITMQ_HOST=host.docker.internal
        //   -e RABBITMQ_PORT=5672
        //   -e RABBITMQ_USER=stewie
        //   -e RABBITMQ_PASS=stewie_dev
        //   -e AGENT_QUEUE=agent.{sessionId}
        //   -e AGENT_ID={sessionId}
        //   -e PROJECT_ID={projectId}
        //   -e AGENT_ROLE={role}
        //   stewie-stub-agent
    }

    public async Task TerminateAsync(string containerId, CancellationToken ct)
    {
        // docker stop {containerId} && docker rm {containerId}
    }

    public async Task<bool> IsRunningAsync(string containerId, CancellationToken ct)
    {
        // docker inspect -f '{{.State.Running}}' {containerId}
    }
}
```

**Register** `StubAgentRuntime` as the `IAgentRuntime` implementation in DI (for now, it's the only one).

**Acceptance criteria:**
- [ ] Launches container with correct env vars
- [ ] Container connects to RabbitMQ using host.docker.internal
- [ ] Terminate stops and removes container
- [ ] IsRunning checks Docker state

---

#### T-168: stewie-stub-agent Docker Image

**Create** `docker/stub-agent/` directory:

**`Dockerfile`:**
```dockerfile
FROM python:3.12-slim

RUN pip install pika==1.3.2

COPY stub_agent.py /app/stub_agent.py

CMD ["python", "/app/stub_agent.py"]
```

**`stub_agent.py`:**
A Python script that:
1. Reads env vars: `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USER`, `RABBITMQ_PASS`, `AGENT_QUEUE`, `AGENT_ID`, `PROJECT_ID`, `AGENT_ROLE`
2. Connects to RabbitMQ
3. Publishes `agent.started` event to `stewie.events`
4. Subscribes to its command queue (`agent.{AGENT_ID}`)
5. On receiving a command:
   - If type = `chat.message`: publishes `agent.chat` response echoing the content
   - If type = `task.assign`: publishes `agent.progress` ("Working on it..."), waits 2s, publishes `agent.completed`
6. Publishes heartbeat every 30s (type = `agent.heartbeat`)
7. On SIGTERM: publishes `agent.stopped`, disconnects, exits cleanly

**Build script** `docker/stub-agent/build.sh`:
```bash
docker build -t stewie-stub-agent ./docker/stub-agent/
```

**Acceptance criteria:**
- [ ] Image builds successfully
- [ ] Container connects to RabbitMQ and publishes `started` event
- [ ] Container receives commands and publishes appropriate responses
- [ ] Clean shutdown on SIGTERM
- [ ] Script is well-documented with docstrings

---

#### T-169: Integration Test — Full Round-Trip

**Create** `Stewie.Tests/Integration/AgentLifecycleTests.cs`:

This is a higher-level test that validates the full loop (requires RabbitMQ running):

| Test | Description |
|:-----|:------------|
| `LaunchAgent_Returns201_SessionRunning` | POST /api/agents → 201, session Status = Running |
| `TerminateAgent_Returns204_SessionStopped` | DELETE /api/agents/{id} → 204, session updated |
| `LaunchDuplicateArchitect_Returns409` | Second Architect → 409 Conflict |
| `GetAgentsByProject_ReturnsActiveSessions` | GET /api/agents/project/{id} → list |

> **Note:** These tests use `WebApplicationFactory` but may skip if RabbitMQ isn't available (mark with `[Trait("Category", "Integration")]`).

**Acceptance criteria:**
- [ ] All 4 tests pass when RabbitMQ is running
- [ ] Tests skip gracefully when RabbitMQ is unavailable

---

## 4. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add agent lifecycle endpoints (§4.N) | → v2.1.0 |
| CON-004 | Reference only (defined in JOB-016) | — |

---

## 5. Verification

```bash
# Build stub agent image
cd docker/stub-agent && docker build -t stewie-stub-agent .

# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Manual: launch stub agent
curl -X POST http://localhost:5275/api/agents \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"projectId":"<guid>","role":"architect"}'

# Check RabbitMQ management UI for agent queue + events
```

**Exit criteria:**
- Stub agent launches, connects to RabbitMQ, publishes `started` event
- API receives and persists the event
- API can send a command to the agent
- Agent responds with appropriate event
- Terminate stops container cleanly
- All tests pass

---

## 6. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-017 created for Phase 5b Agent Runtime Abstraction |
