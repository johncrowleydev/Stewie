---
id: BLU-001
title: "Stewie System Blueprint"
type: reference
status: DRAFT
owner: architect
agents: [all]
tags: [architecture, standards, specification, governance]
related: [PRJ-001, CON-001, CON-002, CON-003, GOV-008]
created: 2026-04-09
updated: 2026-04-10
version: 2.0.0
---

> **BLUF:** Stewie is a control plane for autonomous AI-driven software development. It manages state, routes messages between agents via RabbitMQ, serves a chat-driven dashboard via SignalR, and orchestrates ephemeral LLM agent containers. The API contains zero AI — all intelligence lives in pluggable agent containers.

# Stewie System Blueprint

> **"Stewie does not write code. Stewie orchestrates agents that do."**

---

## 1. Architecture Overview

### 1.1 End-State Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                          HUMAN                                   │
│              (vision, decisions, final authority)                 │
│              Interacts ONLY via chat interface                    │
└────────────────────────┬────────────────────────────────────────┘
                         │ SignalR WebSocket
┌────────────────────────▼────────────────────────────────────────┐
│                     STEWIE.API                                    │
│                  (control plane)                                  │
│                                                                   │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────────┐   │
│  │ SignalR Hub  │  │ REST API     │  │ RabbitMQ Consumer     │   │
│  │ (chat push)  │  │ (state CRUD) │  │ (agent events)        │   │
│  └──────────────┘  └──────────────┘  └───────────────────────┘   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐     │
│  │               Persistence Layer                          │     │
│  │  NHibernate + SQL Server + FluentMigrator               │     │
│  │  (Jobs, Tasks, Chat, Events, Users, Governance)         │     │
│  └─────────────────────────────────────────────────────────┘     │
└────────────────────────┬────────────────────────────────────────┘
                         │ RabbitMQ (message bus)
              ┌──────────┼──────────────┐
              ↓          ↓              ↓
     ┌────────────┐ ┌────────────┐ ┌────────────┐
     │ Architect  │ │  Dev A     │ │  Dev B     │
     │ Agent      │ │  Agent     │ │  Agent     │
     │ (container)│ │ (container)│ │ (container)│
     │            │ │            │ │            │
     │ LLM-powered│ │ LLM-powered│ │ LLM-powered│
     │ [runtime]  │ │ [runtime]  │ │ [runtime]  │
     └────────────┘ └────────────┘ └────────────┘
      persistent     ephemeral      ephemeral
      (per project)  (per task)     (per task)
```

### 1.2 Component Roles

| Component | Role | Contains AI? |
|:----------|:-----|:-------------|
| **Stewie.Api** | Control plane — state, messaging, dashboard | No |
| **React Dashboard** | Chat UI + monitoring views | No |
| **RabbitMQ** | Message bus between all agents and the API | No |
| **Architect Agent** | Plans work, creates jobs, reviews output, enforces governance | Yes (LLM) |
| **Dev Agent** | Writes code, runs tests, commits to Git | Yes (LLM) |
| **Tester Agent** | Verifies output against contracts and governance | Yes (LLM) |

---

## 2. Layer Architecture

```
Stewie.Domain            ← Entities, Enums, Contracts (DTOs). Zero dependencies.
    ↑
Stewie.Application       ← Interfaces, Orchestration Services. Depends on Domain.
    ↑
Stewie.Infrastructure    ← NHibernate, Migrations, Docker, Filesystem. Implements interfaces.
    ↑
Stewie.Api               ← ASP.NET Core host. DI wiring, Controllers. Depends on all.
```

| Layer | Responsibility | May Reference |
|:------|:---------------|:-------------|
| **Domain** | Entities, enums, value objects, DTO contracts | Nothing |
| **Application** | Service interfaces, orchestration logic | Domain |
| **Infrastructure** | Persistence, Docker, filesystem, external I/O | Domain, Application |
| **Api** | HTTP controllers, DI composition, startup | All layers |

**Stewie.Web** is a separate host (React SPA). It calls Stewie.Api over HTTP/WebSocket and does not reference any .NET layers directly.

---

## 3. Entity Model

### 3.1 Core Entities

| Entity | Key Fields | Purpose |
|:-------|:-----------|:--------|
| **Job** | Id, ProjectId, Status, Branch, CreatedByUserId, timestamps | Top-level execution unit. Contains tasks. |
| **WorkTask** | Id, JobId, Role, Status, WorkspacePath, Sequence, timestamps | Individual unit of work within a Job. |
| **TaskDependency** | Id, TaskId, DependsOnTaskId | DAG edges between tasks in a Job. |
| **Artifact** | Id, TaskId, Type, ContentJson, CreatedAt | Stored output from a completed task. |
| **Project** | Id, Name, RepoUrl, RepoProvider, RepoId | Groups jobs under a repository context. |
| **User** | Id, Username, PasswordHash, Role, CreatedAt | Authentication and authorization. |
| **UserCredential** | Id, UserId, Provider, EncryptedToken | Encrypted API tokens (GitHub PATs, etc.). |
| **InviteCode** | Id, Code, CreatedByUserId, UsedByUserId | Invite-only registration tokens. |
| **Event** | Id, EntityType, EntityId, EventType, Payload, Timestamp | Audit trail for all state changes. |
| **Workspace** | Id, TaskId, Path, Status, timestamps | Tracks workspace filesystem lifecycle. |
| **GovernanceReport** | Id, TaskId, JobId, RawOutput, Passed, CheckResults | Per-task governance check results. |
| **ChatMessage** | Id, ProjectId, SenderRole, SenderName, Content, CreatedAt | Planned — Phase 5a |

### 3.2 Status Enums

**JobStatus:** `Pending` → `Running` → `Completed` | `Failed` | `PartiallyCompleted`

**WorkTaskStatus:** `Pending` | `Blocked` → `Running` → `Completed` | `Failed` | `Cancelled`

---

## 4. Execution Loop

### 4.1 Current: API-Driven Multi-Task Execution

```
1. API receives POST /api/jobs (task list + dependencies)
       ↓
2. Create Job (Pending) + WorkTasks (Pending/Blocked)
       ↓
3. Build TaskGraph (Kahn's algorithm for topological sort)
       ↓
4. Scheduler loop: find ready tasks (all deps completed)
       ↓
5. For each ready task (up to MaxConcurrentTasks):
   a. Prepare workspace (clone repo, write task.json)
   b. Launch worker container
   c. Wait for exit
   d. Read result.json
   e. Run governance checks (dev → tester → retry loop)
   f. Update task status
   g. If failed: cancel all downstream dependent tasks
       ↓
6. All tasks done → aggregate Job status → emit events
```

### 4.2 Future: Agent-Driven Execution (Phase 6)

```
1. Human sends chat message: "Build me a REST API for..."
       ↓
2. Architect Agent receives message via RabbitMQ
       ↓
3. Architect plans work → calls POST /api/jobs via Stewie API
       ↓
4. Stewie creates Job + Tasks, spins up Dev Agent containers
       ↓
5. Dev Agents connect to RabbitMQ, receive task assignments
       ↓
6. Dev Agents work, publishing progress to RabbitMQ:
   - "Task started"
   - "Committed to branch feature/..."
   - "Blocked: need API schema clarification"
       ↓
7. Architect receives events, decides:
   - Answer blocker itself, OR
   - Escalate to Human via chat
       ↓
8. Dev Agent finishes → publishes completion → container exits
       ↓
9. Architect reviews, runs governance, reports to Human via chat
```

---

## 5. Key Services

### 5.1 JobOrchestrationService (Application Layer)

The central orchestration service. Coordinates the multi-task execution loop.

- Creates Job and WorkTask entities with dependency DAGs
- Builds `TaskGraph` for parallel scheduling (Kahn's algorithm)
- Manages `SemaphoreSlim(MaxConcurrentTasks)` for concurrency control
- Delegates workspace preparation to `IWorkspaceService`
- Delegates container execution to `IContainerService`
- Runs governance checks with configurable retry loop
- Cascades failures to downstream dependent tasks
- Emits events at every state transition

### 5.2 WorkspaceService (Infrastructure Layer)

Manages the filesystem workspace for each task.

- Creates directory structure (`input/`, `output/`, `repo/`)
- Clones Git repositories into workspace
- Serializes `TaskPacket` to `task.json` (includes `ProjectConfig` from stewie.json)
- Deserializes `ResultPacket` from `result.json`

### 5.3 DockerContainerService (Infrastructure Layer)

Manages Docker container lifecycle.

- Executes containers with volume mounts (workspace → container)
- Supports multiple images (script worker, governance worker)
- Enforces configurable timeout with `CancellationToken`
- Returns exit code (0 = success, 124 = timeout)

### 5.4 TaskGraph (Application Layer)

DAG scheduler for multi-task jobs.

- Builds dependency graph from `TaskDependency` edges
- Detects cycles (rejects invalid DAGs)
- Returns topologically sorted execution order
- Identifies "ready" tasks (all dependencies completed)

---

## 6. Communication Model

### 6.1 Current: Polling + Events Table

- Frontend polls API every 5 seconds (`usePolling` hook)
- State changes written to `Events` table for audit trail
- No real-time push

### 6.2 Planned: SignalR + RabbitMQ (Phase 5)

| Channel | Technology | Direction | Purpose |
|:--------|:-----------|:----------|:--------|
| Human ↔ Dashboard | SignalR WebSocket | Bidirectional | Chat messages, live updates |
| Dashboard ← API | SignalR WebSocket | Server → Client | Job/task state push, container output |
| API ↔ Agents | RabbitMQ | Bidirectional | Task assignment, progress, blockers, completion |

---

## 7. Agent Runtime Abstraction (Phase 5b)

```
IAgentRuntime
├── LaunchArchitectAsync(projectId, config) → containerId
├── LaunchDevAgentAsync(taskId, config) → containerId
├── TerminateAgentAsync(containerId)
└── GetAgentStatusAsync(containerId) → status
```

Implementations:
- `ClaudeCodeRuntime` — launches Claude Code CLI in a container
- `OpenCodeRuntime` — launches OpenCode in a container
- `AiderRuntime` — launches Aider in a container
- `DirectApiRuntime` — raw LLM API calls without a framework

Each runtime configures the container with:
- LLM provider API key (from encrypted `UserCredential`)
- Model selection (from project config)
- RabbitMQ connection string
- Workspace volume mount

---

## 8. Persistence

| Technology | Purpose |
|:-----------|:--------|
| **SQL Server 2022** | Primary data store |
| **NHibernate** | ORM (FluentNHibernate mappings) |
| **FluentMigrator** | Schema migrations (run on startup) |

### 8.1 Migration Strategy

Migrations live in `Stewie.Infrastructure/Migrations/` and run automatically when the API starts. The `DatabaseInitializer` creates the database if it doesn't exist (guarded for EF/testing environments).

Current: 15 migrations (001–015).

---

## 9. Extension Points

### 9.1 New Agent Runtimes

Implement `IAgentRuntime`, register in DI. The runtime handles container image selection, LLM configuration, and RabbitMQ wiring.

### 9.2 New Git Providers

Implement `IGitPlatformService`. Currently: GitHub. Planned: GitLab, Bitbucket.

### 9.3 New Entities

Standard path: Domain entity → NHibernate mapping → FluentMigrator migration → repository interface → repository implementation → DI registration.

### 9.4 New API Endpoints

Add controller to `Stewie.Api/Controllers/`, document in `CON-002`.

---

## 10. Configuration

All configuration lives in `src/Stewie.Api/appsettings.json` + environment variables:

| Key | Default | Description |
|:----|:--------|:------------|
| `ConnectionStrings:Stewie` | `Server=localhost,1433;...` | SQL Server connection |
| `Stewie:WorkspaceRoot` | `./workspaces` | Base directory for task workspaces |
| `Stewie:DockerImageName` | `stewie-dummy-worker` | Docker image for worker containers |
| `Stewie:ScriptWorkerImage` | `stewie-script-worker` | Docker image for script workers |
| `Stewie:GovernanceWorkerImage` | `stewie-governance-worker` | Docker image for governance checks |
| `Stewie:MaxGovernanceRetries` | `2` | Max governance retry attempts |
| `Stewie:TaskTimeoutSeconds` | `300` | Container timeout (seconds) |
| `Stewie:MaxConcurrentTasks` | `5` | Max parallel task containers |
| `Stewie:JwtSecret` | (env var) | JWT signing key |
| `Stewie:EncryptionKey` | (env var) | AES-256 key for credential encryption |
| `Stewie:AdminPassword` | (env var) | Initial admin user password |
