<p align="center">
  <img src="docs/assets/stewie-logo.png" alt="Stewie" width="160" />
</p>

# stewie

Autonomous AI development platform where a Human directs software development entirely through conversation with an Architect Agent.

Stewie is the **control plane** — it manages state, persistence, messaging, and the dashboard. It contains **zero AI**. All intelligence lives in pluggable agent containers that can run any LLM provider (Claude, GPT, Gemini) via any agentic framework (Claude Code, OpenCode, Aider).

The Human never creates jobs, writes task specs, or manages agents directly — they chat, and the Architect handles everything else.

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  React Dashboard (Vite)                           :5173         │
│  Chat · Projects · Jobs · DAG View · Governance · Agent Status  │
└───────────────┬──────────────────────┬───────────────────────────┘
                │ HTTP/JSON            │ WebSocket
┌───────────────▼──────────────────────▼───────────────────────────┐
│  .NET 10 API (Control Plane)                      :5275         │
│  JWT Auth · SignalR Hub · REST API · Job Orchestration          │
├──────────────────────────────────────────────────────────────────┤
│  Core Services                                                   │
│  ┌─── JobOrchestrationService ───┐  ┌─── AgentLifecycleService ─┐│
│  │ DAG Scheduler (TaskGraph)     │  │ IAgentRuntime (pluggable) ││
│  │ Parallel Execution (≤5)       │  │ Container Create/Monitor  ││
│  │ Per-Task Governance           │  │ Secret Injection           ││
│  └───────────────────────────────┘  └────────────────────────────┘│
├──────────────────────────────────────────────────────────────────┤
│  SQL Server 2022   NHibernate   FluentMigrator   RabbitMQ       │
└────────┬──────────────┬─────────────────┬───────────────────────┘
    ┌────▼────┐    ┌────▼────┐      ┌─────▼──────┐
    │ Architect│    │ Dev A   │      │ GitHub API │
    │ Agent   │    │ Agent   │      │ (Octokit)  │
    └─────────┘    └─────────┘      └────────────┘
     (container)    (container)
     [OpenCode]     [OpenCode/Aider]
         ↕               ↕
       RabbitMQ Message Bus
```

### Three-Tier Agent Hierarchy

| Role | Description |
|:-----|:------------|
| **Human** | Final authority. Sets vision, approves plans, provides guidance via chat. |
| **Architect Agent** | AI project manager. Plans work, creates jobs, spins up Dev Agents, reviews output, enforces governance. Does not write feature code. |
| **Developer/Tester Agents** | Ephemeral LLM containers. Execute tasks, write code, run tests, produce results. Destroyed on completion. |

### Agent Runtime Abstraction

Agent runtimes are pluggable via the `IAgentRuntime` interface:

```
IAgentRuntime
├── OpenCodeRuntime       (OpenCode CLI in a container)
├── StubAgentRuntime      (Mock agent for testing)
├── AiderRuntime          (planned)
└── ClaudeCodeRuntime     (planned)
```

Each runtime knows how to build/launch a container, configure it with the right LLM provider and API keys, wire it to RabbitMQ, and manage its lifecycle. The model and runtime can be configured per project.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 20+](https://nodejs.org/) (for the React dashboard)

## Quick Start

### 1. Start Infrastructure (SQL Server + RabbitMQ)

```bash
docker-compose up -d
```

This starts:
- **SQL Server 2022** on port `1433`
- **RabbitMQ** on port `5672` (AMQP) and `15672` (management UI)

### 2. Build Container Images

```bash
# Worker containers (task execution)
docker build -t stewie-dummy-worker workers/dummy-worker/
docker build -t stewie-script-worker workers/script-worker/
docker build -t stewie-governance-worker workers/governance-worker/

# Agent containers (LLM-powered)
docker build -t stewie-opencode-agent docker/opencode-agent/
docker build -t stewie-architect-agent docker/architect-agent/
docker build -t stewie-stub-agent docker/stub-agent/
```

### 3. Set Required Environment Variables

```bash
export Stewie__JwtSecret="your-32-char-minimum-secret-key-here"
export Stewie__EncryptionKey="your-32-char-aes-encryption-key-here"
export Stewie__AdminPassword="YourAdminPassword123!"
export Stewie__AdminUsername="admin"  # optional, defaults to "admin"
```

### 4. Run the API

```bash
dotnet run --project src/Stewie.Api
```

On first start:
- FluentMigrator creates the database and runs all migrations
- An admin user is seeded with the configured credentials

### 5. Run the Dashboard

```bash
cd src/Stewie.Web/ClientApp
npm install
npm run dev
```

Dashboard: `http://localhost:5173` · API: `http://localhost:5275`

### 6. Login and Start Building

1. Open `http://localhost:5173` → Login with your admin credentials
2. Navigate to **Settings** → Add your GitHub PAT and LLM provider API keys
3. Create a **Project** — link an existing repo or create a new one on GitHub
4. **Chat with the Architect** — describe what you want built in natural language
5. The Architect plans the work, creates jobs, spins up Dev Agents, and manages the entire lifecycle

## How It Works

### Chat-Driven Development

1. Human opens a project and types a request in the chat panel
2. The **Architect Agent** (an LLM running in a container) receives the message via RabbitMQ
3. The Architect plans the work and proposes a plan back to the Human
4. Human approves → Architect creates jobs and tasks via the Stewie API
5. **Developer Agent** containers are spun up per task with isolated workspaces
6. Dev Agents write code, commit, and report progress via RabbitMQ
7. The Architect reviews output, runs governance, and iterates
8. When Dev Agents are blocked, the Architect answers — or escalates to the Human
9. The Human watches progress in real time via the dashboard

### Multi-Task Jobs (DAG)

1. The Architect creates a job with multiple tasks and dependency edges
2. Stewie validates the dependency graph is acyclic (Kahn's algorithm)
3. The DAG scheduler loop runs:
   - Tasks with no unmet dependencies are launched in parallel (max 5 concurrent containers)
   - Each task gets its own isolated workspace and per-task governance cycle
   - When a task completes, its downstream dependents become ready
   - When a task fails, all transitive downstream tasks are cancelled
4. Job status is computed from aggregate task states:
   - All completed → `Completed`
   - All failed → `Failed`
   - Mix of completed/failed/cancelled → `PartiallyCompleted`
5. Successful tasks are pushed and PR'd independently

### Governance Engine

Every worker's output is automatically validated against all 8 governance documents:

| Category | Checks |
|:---------|:-------|
| GOV-001 Documentation | README exists, XML doc comments on public members |
| GOV-002 Testing | Build succeeds, tests pass, test count > 0 |
| GOV-003 Coding Standard | No `any` types, no `console.log`, no `Console.WriteLine` |
| GOV-004 Error Handling | Error middleware present |
| GOV-005 Dev Lifecycle | Branch naming, commit message format |
| GOV-006 Logging | ILogger usage, no bare Console.Write |
| GOV-008 Infrastructure | Dockerfile present |
| SEC-001 Security | No secrets in git diff |

### Governance Analytics

Stewie tracks governance failures over time and provides:
- **Trending violations** — which rules fail most, and whether they're getting better or worse
- **GOV update suggestions** — recommendations to relax, strengthen, or review specific governance rules based on historical failure data
- Filterable by project and time window (7d, 30d, 90d)

### Retry Logic

- **Transient failures** (timeout, Docker errors) → automatic 1 retry
- **Permanent failures** (worker crash, bad results) → no retry, failure reason recorded
- **Governance failures** → retry with violation feedback (up to 2 attempts, configurable)

### Project Configuration (`stewie.json`)

Drop a `stewie.json` in your repo root to configure Stewie explicitly:

```json
{
  "version": "1.0",
  "stack": "dotnet",
  "language": "csharp",
  "buildCommand": "dotnet build",
  "testCommand": "dotnet test",
  "governance": {
    "rules": "all",
    "warningsBlockAcceptance": false,
    "maxRetries": 2
  },
  "paths": {
    "source": ["src/"],
    "tests": ["tests/"],
    "forbidden": ["secrets/", ".env"]
  }
}
```

If absent, the governance worker falls back to heuristic stack detection.
Full contract: `CODEX/20_BLUEPRINTS/CON-003_ProjectConfig_Contract.md`

## Project Structure

```
src/
  Stewie.Domain/           # Entities, enums, contracts (DTOs)
  Stewie.Application/      # Service interfaces, orchestration logic, TaskGraph
  Stewie.Infrastructure/   # NHibernate, FluentMigrator, Docker, GitHub, RabbitMQ
  Stewie.Api/              # ASP.NET Core API host, controllers, SignalR hub
  Stewie.Web/ClientApp/    # React + Vite dashboard
  Stewie.Tests/            # xUnit integration + unit tests (260 tests)
workers/
  dummy-worker/            # Test worker (proves container contract)
  script-worker/           # Real worker (Alpine + bash + git)
  governance-worker/       # Governance checker (runs 15 GOV rules)
docker/
  opencode-agent/          # OpenCode agent runtime (LLM-powered dev agent)
  architect-agent/         # Architect agent (LLM-powered project manager)
  stub-agent/              # Mock agent for CI/testing
CODEX/                     # Project governance documentation
  00_INDEX/                # MANIFEST.yaml — document registry
  05_PROJECT/              # Sprints, backlog, roadmap
  10_GOVERNANCE/           # Standards (GOV-001 through GOV-008)
  20_BLUEPRINTS/           # Design specs, API/runtime/config/messaging contracts
  40_VERIFICATION/         # Audit reports
  80_AGENTS/               # Agent role definitions
```

## API Overview

All endpoints require `Authorization: Bearer {jwt}` unless noted.

### Auth & Users

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `POST` | `/api/auth/login` | Authenticate, receive JWT (24hr expiry) — **no auth required** |
| `POST` | `/api/auth/register` | Register (requires invite code) — **no auth required** |
| `GET` | `/api/users` | List all users (admin only) |
| `DELETE` | `/api/users/{id}` | Delete user (admin only) |
| `GET` | `/api/users/me` | Current user profile |
| `PUT` | `/api/users/me/github-token` | Store GitHub PAT (AES-256 encrypted) |
| `DELETE` | `/api/users/me/github-token` | Remove stored GitHub PAT |
| `GET` | `/api/users/me/github-status` | Check PAT configuration status |

### Projects & Chat

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/api/projects` | List all projects |
| `POST` | `/api/projects` | Create project (link existing repo or create new) |
| `GET` | `/api/projects/{id}` | Get project details |
| `GET` | `/api/projects/{id}/chat` | Get chat history for a project |
| `POST` | `/api/projects/{id}/chat` | Send a chat message |
| `POST` | `/api/projects/{id}/chat/plan-decision` | Approve/reject an Architect plan |

### Jobs & Tasks

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/api/jobs` | List jobs (filterable by `?projectId=`) |
| `POST` | `/api/jobs` | Create job (single-task or multi-task with deps) |
| `GET` | `/api/jobs/{id}` | Job details with nested tasks and aggregate counts |
| `GET` | `/api/jobs/{id}/tasks` | List tasks for a job |
| `GET` | `/api/tasks/{id}` | Get task details |
| `GET` | `/api/tasks/{id}/output` | Stream container output for a task |
| `POST` | `/jobs/test` | Trigger a test job (dummy worker) |

### Governance

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/api/jobs/{id}/governance` | Latest governance report for a job |
| `GET` | `/api/tasks/{id}/governance` | Governance report for a tester task |
| `GET` | `/api/governance/analytics` | Violation trending and GOV update suggestions |

### Agents

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `POST` | `/api/agents/launch` | Launch an agent container |
| `DELETE` | `/api/agents/{id}` | Stop an agent container |
| `GET` | `/api/agents/{id}/status` | Get agent session status |
| `GET` | `/api/projects/{id}/agents` | List agents for a project |
| `POST` | `/api/projects/{id}/architect/start` | Start Architect agent for a project |
| `DELETE` | `/api/projects/{id}/architect` | Stop Architect agent |
| `GET` | `/api/projects/{id}/architect/status` | Architect agent status |

### Settings & Admin

| Method | Endpoint | Description |
|:-------|:---------|:------------|
| `GET` | `/api/settings/credentials` | List stored LLM provider credentials |
| `POST` | `/api/settings/credentials` | Store an LLM provider API key |
| `DELETE` | `/api/settings/credentials/{id}` | Delete a stored credential |
| `GET` | `/api/github/repos` | List GitHub repos (proxied via user PAT) |
| `POST` | `/api/invites` | Generate an invite code (admin only) |
| `GET` | `/api/invites` | List invite codes (admin only) |
| `DELETE` | `/api/invites/{id}` | Revoke an invite code (admin only) |
| `GET` | `/api/events` | List system events |
| `GET` | `/health` | Health check (SQL Server + RabbitMQ) |

### Real-Time (SignalR)

The API exposes a SignalR hub at `/hubs/stewie` for real-time updates:
- Job/task status changes
- Chat messages
- Container output streaming
- Agent session events

Full contract: `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md`

## Configuration

Configuration via `src/Stewie.Api/appsettings.json` and environment variables:

| Key | Env Variable | Default | Description |
|:----|:-------------|:--------|:------------|
| `ConnectionStrings:Stewie` | — | localhost SQL Server | Database connection string |
| `Stewie:WorkspaceRoot` | — | `./workspaces` | Base directory for task workspaces |
| `Stewie:DockerImageName` | — | `stewie-dummy-worker` | Default Docker image for test runs |
| `Stewie:ScriptWorkerImage` | — | `stewie-script-worker` | Docker image for real task execution |
| `Stewie:GovernanceWorkerImage` | — | `stewie-governance-worker` | Docker image for governance checks |
| `Stewie:TaskTimeoutSeconds` | — | `300` | Hard timeout for container execution (seconds) |
| `Stewie:MaxConcurrentTasks` | — | `5` | Max parallel containers per job |
| `Stewie:MaxGovernanceRetries` | — | `2` | Max governance retry attempts per task |
| `Stewie:WarningsBlockAcceptance` | — | `false` | Whether warning-severity governance failures block |
| `Stewie:JwtSecret` | `Stewie__JwtSecret` | **required** | JWT signing key (min 32 chars) |
| `Stewie:EncryptionKey` | `Stewie__EncryptionKey` | **required** | AES-256 key for credential encryption |
| `Stewie:AdminPassword` | `Stewie__AdminPassword` | **required** | Initial admin password (first startup only) |
| `Stewie:AdminUsername` | `Stewie__AdminUsername` | `admin` | Initial admin username |
| `RabbitMQ:HostName` | — | `localhost` | RabbitMQ host |
| `RabbitMQ:Port` | — | `5672` | RabbitMQ AMQP port |
| `RabbitMQ:UserName` | — | `stewie` | RabbitMQ username |
| `RabbitMQ:Password` | — | `Stewie_Dev_R@bbit1` | RabbitMQ password |
| `RabbitMQ:VirtualHost` | — | `stewie` | RabbitMQ virtual host |

## Runtime Contract

Workers communicate with Stewie via JSON files mounted in the container.

### task.json (input → `/workspace/input/task.json`)

```json
{
  "taskId": "guid",
  "jobId": "guid",
  "role": "developer",
  "objective": "Implement feature X",
  "scope": "src/services/",
  "repoUrl": "https://github.com/org/repo.git",
  "branch": "stewie/run-id",
  "script": ["npm install", "npm test"],
  "acceptanceCriteria": ["All tests pass", "No lint errors"],
  "projectConfig": { "stack": "node", "language": "typescript" }
}
```

### result.json (output → `/workspace/output/result.json`)

```json
{
  "taskId": "guid",
  "status": "success",
  "summary": "Implemented feature X with 3 new files",
  "filesChanged": ["src/services/foo.ts", "src/services/bar.ts"],
  "testsPassed": true,
  "errors": [],
  "notes": "Added unit tests for edge cases",
  "nextAction": "review"
}
```

Full contract: `CODEX/20_BLUEPRINTS/CON-001_Runtime_Contract.md`

## Testing

```bash
# Run all tests (260 passing)
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Frontend build verification
cd src/Stewie.Web/ClientApp && npm run build
```

## Contracts

| Contract | Version | Description |
|:---------|:--------|:------------|
| CON-001 | v1.6.0 | Runtime Contract — task.json / result.json / projectConfig |
| CON-002 | v2.0.0 | API Contract — HTTP endpoints, SignalR hub, schemas, error codes |
| CON-003 | v1.1.0 | Project Configuration — stewie.json file format |
| CON-004 | v1.1.0 | Agent Messaging Contract — RabbitMQ exchange topology and message types |

## Roadmap

| Phase | Name | Status |
|:------|:-----|:-------|
| 0 | Foundation | ✅ Complete |
| 1 | Core Orchestration MVP | ✅ Complete |
| 2 | Real Repo Interaction | ✅ Complete |
| 2.5 | GitHub Integration + Auth | ✅ Complete |
| 2.75 | Repository Automation + Platform Abstraction | ✅ Complete |
| 3 | Governance Engine | ✅ Complete |
| 4 | Multi-Task Jobs (DAG, Parallel, Analytics) | ✅ Complete |
| 5a | Chat + Real-Time UI (SignalR, Chat, Streaming) | ✅ Complete |
| 5b | Message Bus + Agent Lifecycle (RabbitMQ, IAgentRuntime) | ✅ Complete |
| 6 | AI Agent Intelligence (OpenCode, Architect Loop, E2E) | ✅ Complete |
| **7** | **Design System Foundation (Tailwind v4, Component Library)** | ✅ Complete |
| 8 | App Shell + Role-Based Architecture | ✅ Complete |
| 9 | Code Explorer + Premium Polish | 📋 Planned |
| 10 | Production Hardening | 📋 Planned |

Full roadmap: `CODEX/05_PROJECT/PRJ-001_Roadmap.md`

## License

Private — all rights reserved.
