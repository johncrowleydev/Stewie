---
id: GOV-008
title: "Infrastructure & Operations Standard"
type: reference
status: APPROVED
owner: architect
agents: [all]
tags: [governance, standards, infrastructure, deployment, operations]
related: [GOV-007, BLU-001, PRJ-001]
created: 2026-03-24
updated: 2026-04-09
version: 2.0.0
---

> **BLUF:** Stewie runs local-first as a Docker Compose stack: .NET 10 API (Stewie.Api), React frontend (Stewie.Web), SQL Server 2022, and ephemeral Docker worker containers. Monorepo structure. No cloud services in MVP. All infrastructure decisions are final and override any BLU- assumptions.

# Infrastructure & Operations Standard

> **"Architecture assumes. Infrastructure decides."**

---

## 1. Deployment Model

| Decision | Value |
|:---------|:------|
| **Deployment target** | Docker Compose (local-first) |
| **Cloud provider** | None (local development only for MVP) |
| **Environment count** | dev only (staging/prod deferred) |
| **Production hostname** | N/A (local-first MVP) |

### Adaptation Table

| Blueprint Assumption | Actual (GOV-008) |
|:--------------------|:-----------------|
| Cloud hosting | Local Docker Compose |
| Managed database | SQL Server 2022 in Docker container |
| External file storage | Local disk (`workspaces/` directory) |
| Message queue | Deferred (RabbitMQ planned for future) |

---

## 2. Repository Structure

| Decision | Value |
|:---------|:------|
| **Structure** | Monorepo |
| **CODEX location** | `CODEX/` at repository root |

### Repository Layout

```
architect/                  # Repository root
├── CODEX/                  # Governance, blueprints, contracts, project mgmt
├── src/
│   ├── Stewie.Domain/      # Entities, enums, contracts (DTOs)
│   ├── Stewie.Application/ # Service interfaces, orchestration use cases
│   ├── Stewie.Infrastructure/ # NHibernate, migrations, Docker, filesystem
│   ├── Stewie.Api/         # ASP.NET Core API host (port 5275)
│   └── Stewie.Web/         # React frontend (Vite, port 5173)
├── workers/
│   └── dummy-worker/       # Dummy worker runtime (container)
├── workspaces/             # Runtime-created per-task directories (ephemeral)
└── docker-compose.yml      # SQL Server 2022
```

---

## 3. Service Architecture

Two separate host processes:

| Service | Project | Port | Purpose |
|:--------|:--------|:-----|:--------|
| **API** | `Stewie.Api` | 5275 | Orchestration API, business logic, DB access |
| **Frontend** | `Stewie.Web` | 5173 | React SPA (Vite dev server), proxies `/api` to API |

These are intentionally separate projects. The frontend calls the API over HTTP.

---

## 4. Database

| Decision | Value |
|:---------|:------|
| **Engine** | SQL Server 2022 (Docker: `mcr.microsoft.com/mssql/server:2022-latest`) |
| **ORM** | NHibernate |
| **Migrations** | FluentMigrator (runs on API startup) |
| **Port** | 1433 |
| **Database name** | `Stewie` (auto-created on first run) |
| **Schema owner** | `Stewie.Api` (single service owns all tables) |

### Tables (Milestone 0)

| Table | Entity | Owner |
|:------|:-------|:------|
| `Runs` | `Run` | Stewie.Api |
| `Tasks` | `WorkTask` | Stewie.Api |
| `Artifacts` | `Artifact` | Stewie.Api |

---

## 5. File Storage

| Decision | Value |
|:---------|:------|
| **Storage model** | Local disk |
| **Storage path** | `./workspaces/{taskId}/` |
| **Structure** | `input/` (task.json), `output/` (result.json), `repo/` (cloned source) |
| **Lifecycle** | Ephemeral — created per task, not auto-cleaned |

---

## 6. Worker Container Runtime

| Decision | Value |
|:---------|:------|
| **Container runtime** | Docker (local Docker daemon) |
| **Image** | `stewie-dummy-worker` (MVP) |
| **Execution model** | `docker run --rm` with volume mounts |
| **I/O contract** | Input: `/workspace/input/task.json` → Output: `/workspace/output/result.json` |
| **Isolation** | Each task runs in its own container |
| **Networking** | No network access (containers are compute-only) |

---

## 7. Service Communication

| Decision | Value |
|:---------|:------|
| **Frontend ↔ API** | HTTP (Vite proxy in dev) |
| **API ↔ Worker** | Docker volumes (filesystem I/O, not network) |
| **Authentication** | None (MVP — local-first) |
| **Inter-service auth** | N/A (single API service) |

---

## 8. Development Environment

| Component | Spec |
|:----------|:-----|
| **OS** | Linux (Ubuntu) |
| **.NET** | 10 SDK |
| **Node.js** | 20+ LTS |
| **Docker** | Docker Engine (required for SQL Server + workers) |
| **IDE** | Any (VS Code recommended) |

### Startup Sequence

1. `docker compose up -d` — Start SQL Server
2. `docker build -t stewie-dummy-worker workers/dummy-worker/` — Build worker image
3. `dotnet run --project src/Stewie.Api/Stewie.Api.csproj` — Start API (auto-migrates DB)
4. `cd src/Stewie.Web/ClientApp && npm run dev` — Start React frontend

---

## 9. Backup & Recovery

| Decision | Value |
|:---------|:------|
| **Backup method** | N/A (local dev only) |
| **Frequency** | N/A |
| **Recovery** | `docker compose down -v && docker compose up -d` (fresh start) |

---

## 10. Monitoring & Observability

| Decision | Value |
|:---------|:------|
| **Error tracking** | Console logging (structured, via `ILogger`) |
| **Log aggregation** | None (local dev — console output) |
| **Health checks** | Deferred (will add `GET /health` in future sprint) |
| **Uptime monitoring** | N/A |
