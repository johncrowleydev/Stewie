---
id: BCK-001
title: "Development Backlog"
type: planning
status: ACTIVE
owner: architect
agents: [all]
tags: [project-management, backlog, agentic-development]
related: [PRJ-001, BCK-002, GOV-007]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Prioritized development backlog for Stewie. Items are pulled from here into sprint documents. Two developer agents will execute against this backlog. Items are ordered by dependency and priority.

# Development Backlog

---

## Priority Legend

| Priority | Definition |
|:---------|:-----------|
| **P0** | Blocking — must be done before anything else |
| **P1** | High — core MVP functionality |
| **P2** | Medium — important but not blocking |
| **P3** | Low — nice to have |

---

## Phase 1: Core Orchestration (MVP)

### P0 — Foundation

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-001 | **Project entity** | Add `Project` entity with `Id`, `Name`, `RepoUrl`, `CreatedAt`. Migration, mapping, repository, DI registration. | CON-002 §5.1 | Small |
| B-002 | **Event entity** | Add `Event` entity for audit trail: `Id`, `EntityType`, `EntityId`, `EventType`, `Payload`, `Timestamp`. Migration, mapping, repository. | BLU-001 §3.2 | Small |
| B-003 | **Workspace entity** | Add `Workspace` entity: `Id`, `TaskId`, `Path`, `Status` (Created/Mounted/Cleaned), timestamps. Migration, mapping, repository. | BLU-001 §3.2 | Small |
| B-004 | **Link Run to Project** | Add nullable `ProjectId` FK to `Run`. Update Run creation to optionally associate with a Project. Migration. | CON-002 §5.2 | Small |

### P1 — API Endpoints

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-010 | **Health endpoint** | `GET /health` returning `{ status, version, timestamp }`. No auth. | CON-002 §4.4 | Trivial |
| B-011 | **Project CRUD** | `GET /api/projects`, `POST /api/projects`, `GET /api/projects/{id}` | CON-002 §4.1 | Small |
| B-012 | **Run endpoints** | `GET /api/runs`, `POST /api/runs`, `GET /api/runs/{id}` with nested tasks | CON-002 §4.2 | Medium |
| B-013 | **Task endpoints** | `GET /api/tasks/{id}`, `GET /api/runs/{runId}/tasks` | CON-002 §4.3 | Small |
| B-014 | **Standardized error responses** | Implement error middleware returning `{ error: { code, message, details } }` per GOV-004 | CON-002 §6 | Medium |

### P1 — Event Emission

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-020 | **Emit events on state changes** | `RunOrchestrationService` emits Events on Run/Task status transitions | BLU-001 §4 | Medium |

### P2 — React Dashboard

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-030 | **Dashboard layout** | Navigation, header with Stewie branding, sidebar | — | Medium |
| B-031 | **Runs list page** | Fetch and display all Runs with status badges | CON-002 §4.2 | Medium |
| B-032 | **Run detail page** | Show Run with its Tasks and Artifacts | CON-002 §4.2, §4.3 | Medium |
| B-033 | **Projects page** | List projects, create new project form | CON-002 §4.1 | Medium |

### P2 — Test Infrastructure

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-040 | **Test project setup** | Create `Stewie.Tests` project with xUnit. Add to solution. | GOV-002 | Small |
| B-041 | **Unit tests for RunOrchestrationService** | Test the execution loop with mocked dependencies | GOV-002 | Medium |
| B-042 | **Unit tests for WorkspaceService** | Test workspace creation and result reading | GOV-002 | Small |
| B-043 | **Integration tests for API endpoints** | Test controllers with WebApplicationFactory | GOV-002 | Medium |

---

## Phase 2: Real Repo Interaction (Future)

| ID | Task | Description | Priority |
|:---|:-----|:------------|:---------|
| B-100 | Git clone into workspace | WorkspaceService clones target repo into `repo/` | P1 |
| B-101 | Branch creation for tasks | Create feature branch per task | P1 |
| B-102 | Diff ingestion from result | Parse `filesChanged` into actual git diffs | P2 |
| B-103 | Commit worker changes | Auto-commit worker file mutations | P2 |

---

## Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-09 | Initial backlog created from PRJ-001 Phase 1 |
