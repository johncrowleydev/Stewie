---
id: BLU-001
title: "Stewie System Blueprint"
type: reference
status: DRAFT
owner: architect
agents: [all]
tags: [architecture, standards, specification, governance]
related: [PRJ-001, CON-001, CON-002, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Stewie is a 4-layer .NET application (Domain → Application → Infrastructure → API) that orchestrates worker containers through structured I/O. This blueprint defines the entity model, execution loop, layer responsibilities, and extension points.

# Stewie System Blueprint

> **"Stewie does not write code. Stewie orchestrates agents that do."**

---

## 1. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                        HUMAN                                  │
│                 (vision, decisions, approval)                  │
└──────────────────────┬───────────────────────────────────────┘
                       │ real-time interaction
┌──────────────────────▼───────────────────────────────────────┐
│               ARCHITECT AGENT                                 │
│          (creates tasks, reviews results)                      │
└──────────────────────┬───────────────────────────────────────┘
                       │ API calls
┌──────────────────────▼───────────────────────────────────────┐
│                   STEWIE.API                                  │
│        ┌─────────────────────────────────┐                    │
│        │    RunOrchestrationService       │                    │
│        │    ┌───────┐  ┌──────────────┐  │                    │
│        │    │ Run   │  │ WorkTask     │  │                    │
│        │    └───┬───┘  └──────┬───────┘  │                    │
│        │        │             │           │                    │
│        │   ┌────▼─────────────▼─────┐    │                    │
│        │   │   WorkspaceService     │    │                    │
│        │   │   (filesystem I/O)     │    │                    │
│        │   └────────────┬───────────┘    │                    │
│        │                │                │                    │
│        │   ┌────────────▼───────────┐    │                    │
│        │   │ DockerContainerService │    │                    │
│        │   │ (docker run --rm)      │    │                    │
│        │   └────────────┬───────────┘    │                    │
│        └────────────────│────────────────┘                    │
└─────────────────────────│────────────────────────────────────┘
                          │ volume mounts
┌─────────────────────────▼────────────────────────────────────┐
│                  WORKER CONTAINER                              │
│            reads task.json → writes result.json               │
│            (stateless, ephemeral, isolated)                    │
└──────────────────────────────────────────────────────────────┘
```

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

**Stewie.Web** is a separate host (React SPA). It calls Stewie.Api over HTTP and does not reference any .NET layers directly.

---

## 3. Entity Model

### 3.1 Current Entities (Milestone 0)

```
┌─────────┐       ┌──────────┐       ┌──────────┐
│   Run   │ 1───* │ WorkTask │ 1───* │ Artifact │
└─────────┘       └──────────┘       └──────────┘
```

| Entity | Key Fields | Purpose |
|:-------|:-----------|:--------|
| **Run** | Id, Status, CreatedAt, CompletedAt | Top-level execution unit |
| **WorkTask** | Id, RunId, Role, Status, WorkspacePath, timestamps | Individual unit of work within a Run |
| **Artifact** | Id, TaskId, Type, ContentJson, CreatedAt | Stored output from a completed task |

### 3.2 Planned Entities (Phase 1)

| Entity | Purpose | Status |
|:-------|:--------|:-------|
| **Project** | Groups Runs under a repository/project context | Not implemented |
| **Workspace** | Tracks workspace lifecycle (created, mounted, cleaned) | Not implemented |
| **Event** | Audit trail for all state changes | Not implemented |

### 3.3 Status Enums

**RunStatus:** `Pending` → `Running` → `Completed` | `Failed`

**WorkTaskStatus:** `Pending` → `Running` → `Completed` | `Failed`

---

## 4. Execution Loop

The core loop that Stewie executes for every task:

```
1. Create Run (status: Pending)
       ↓
2. Create WorkTask (status: Pending, linked to Run)
       ↓
3. Prepare Workspace
   - Create directories: input/, output/, repo/
   - Write task.json to input/ (per CON-001)
       ↓
4. Update statuses to Running
       ↓
5. Launch Worker Container
   - docker run --rm with volume mounts
   - Mount input/ (ro), output/ (rw), repo/ (ro)
       ↓
6. Wait for container exit
       ↓
7. Read result.json from output/ (per CON-001)
       ↓
8. Create Artifact (stores serialized result)
       ↓
9. Update statuses to Completed/Failed
       ↓
10. Return result to caller
```

---

## 5. Key Services

### 5.1 RunOrchestrationService (Application Layer)

The central orchestration service. Coordinates the entire execution loop.

- Creates Run and WorkTask entities
- Delegates workspace preparation to `IWorkspaceService`
- Delegates container execution to `IContainerService`
- Ingests results and creates Artifacts
- Manages status transitions

### 5.2 WorkspaceService (Infrastructure Layer)

Manages the filesystem workspace for each task.

- Creates directory structure (`input/`, `output/`, `repo/`)
- Serializes `TaskPacket` to `task.json`
- Deserializes `ResultPacket` from `result.json`

### 5.3 DockerContainerService (Infrastructure Layer)

Manages Docker container lifecycle.

- Executes `docker run --rm` with volume mounts
- Captures stdout/stderr
- Returns exit code

### 5.4 UnitOfWork (Infrastructure Layer)

NHibernate transaction boundary. Wraps all persistence operations.

---

## 6. Persistence

| Technology | Purpose |
|:-----------|:--------|
| **SQL Server 2022** | Primary data store |
| **NHibernate** | ORM (XML-free, FluentNHibernate mappings) |
| **FluentMigrator** | Schema migrations (run on startup) |

### 6.1 Migration Strategy

Migrations live in `Stewie.Infrastructure/Migrations/` and run automatically when the API starts. The `DatabaseInitializer` creates the database if it doesn't exist.

Current migrations:
- `Migration_001_CreateRunsTable`
- `Migration_002_CreateTasksTable`
- `Migration_003_CreateArtifactsTable`

---

## 7. Extension Points

### 7.1 New Worker Types

Workers are pluggable. To add a new worker:
1. Create a Docker image that reads `/workspace/input/task.json` and writes `/workspace/output/result.json`
2. Configure the image name in `appsettings.json` (`Stewie:DockerImageName`)
3. Conform to `CON-001`

### 7.2 New Entities

To add a new entity:
1. Add class to `Stewie.Domain/Entities/`
2. Add FluentNHibernate mapping to `Stewie.Infrastructure/Mappings/`
3. Add FluentMigrator migration to `Stewie.Infrastructure/Migrations/`
4. Add repository interface to `Stewie.Application/Interfaces/`
5. Add repository implementation to `Stewie.Infrastructure/Repositories/`
6. Register in DI container (`Stewie.Api/Program.cs`)

### 7.3 New API Endpoints

To add a new endpoint:
1. Add controller to `Stewie.Api/Controllers/`
2. Document in `CON-002`
3. Register any new services in DI

---

## 8. Configuration

All configuration lives in `src/Stewie.Api/appsettings.json`:

| Key | Default | Description |
|:----|:--------|:------------|
| `ConnectionStrings:Stewie` | `Server=localhost,1433;...` | SQL Server connection |
| `Stewie:WorkspaceRoot` | `./workspaces` | Base directory for task workspaces |
| `Stewie:DockerImageName` | `stewie-dummy-worker` | Docker image for worker containers |
