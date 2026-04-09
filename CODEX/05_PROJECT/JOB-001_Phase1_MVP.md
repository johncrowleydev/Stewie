---
id: JOB-001
title: "Phase 1 MVP — Core Entities, API Endpoints, Dashboard & Tests"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, sprint, workflow]
related: [BCK-001, BLU-001, CON-001, CON-002, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Sprint 001 delivers the Phase 1 MVP foundation. Two developer agents work **in parallel**: Agent A builds backend entities + API endpoints, Agent B builds React dashboard + test infrastructure. Each agent works in non-overlapping file territories to minimize merge conflicts. Agent A's branch merges first.

# Sprint 001: Phase 1 MVP — Core Entities, API, Dashboard & Tests

**Phase:** Phase 1 — Core Orchestration (MVP)
**Target:** Scope-bounded (AI-agent pace)
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** None (Phase 0 complete)
**Contracts:** CON-001 (Runtime Contract), CON-002 (API Contract)

---

## ⚠️ Mandatory Compliance — Every Task

> All tasks in this sprint MUST incorporate these governance standards. They are not optional and not deferred.

| Governance Doc | Sprint Requirement |
|:---------------|:-------------------|
| **GOV-001** | XML doc comments on all public classes and methods |
| **GOV-002** | Agent B creates test project; both agents write tests for their code |
| **GOV-003** | C# coding standards, no dead code, complexity limits |
| **GOV-004** | Standardized error responses via error middleware (Agent A) |
| **GOV-005** | Agent A: `feature/JOB-001-backend-api`. Agent B: `feature/JOB-001-frontend-tests`. Commits: `feat(JOB-001): T-XXX description` |
| **GOV-006** | Structured `ILogger` logging on all new services and controllers |
| **GOV-007** | Task status updated in this doc. Blockers → `DEF-` doc |
| **GOV-008** | All infrastructure per GOV-008 (SQL Server, Docker, local-first) |

**Acceptance gate:** No task is considered complete unless ALL applicable governance requirements are met.

---

## Parallel Execution Plan

```
                    JOB-001 Timeline
                    ================

Agent A (Backend)              Agent B (Frontend + Tests)
─────────────────              ─────────────────────────
T-001: Project entity          T-010: Test project setup
T-002: Event entity            T-011: Unit tests (orchestration)
T-003: Workspace entity        T-012: Unit tests (workspace)
T-004: Link Run→Project        T-013: Dashboard layout + routing
T-005: Error middleware        T-014: Runs list page
T-006: Health endpoint         T-015: Run detail page
T-007: Project CRUD            T-016: Projects page
T-008: Run endpoints
T-009: Task endpoints
        │                              │
        ▼                              ▼
   Merge A first              Merge B second (rebase on A)
```

**Conflict minimization:** Agent A works exclusively in `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, and `src/Stewie.Api/`. Agent B works exclusively in `src/Stewie.Web/ClientApp/` and creates a new `src/Stewie.Tests/` project. The only shared touchpoint is `Stewie.slnx` (Agent B adds the test project).

**Merge order:** Agent A merges first. Agent B rebases onto the updated main, then merges. This ensures the API endpoints exist before the frontend can be fully integration-tested.

---

## Dev Agent A Tasks (Backend API)

> **Branch:** `feature/JOB-001-backend-api`
> **File territory:** `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, `src/Stewie.Api/`

### T-001: Project Entity
- **Dependencies:** None
- **Contracts:** CON-002 §5.1, BLU-001 §3.2
- **Deliverable:**
  - `Stewie.Domain/Entities/Project.cs` — `Id`, `Name`, `RepoUrl`, `CreatedAt`
  - `Stewie.Infrastructure/Mappings/ProjectMap.cs`
  - `Stewie.Infrastructure/Migrations/Migration_004_CreateProjectsTable.cs`
  - `Stewie.Application/Interfaces/IProjectRepository.cs`
  - `Stewie.Infrastructure/Repositories/ProjectRepository.cs`
  - DI registration in `Program.cs`
- **Acceptance criteria:**
  - Project entity persists to SQL Server
  - Build succeeds: `dotnet build src/Stewie.Api/Stewie.Api.csproj`
- **Status:** [x] Complete — merged to main

### T-002: Event Entity
- **Dependencies:** None
- **Contracts:** BLU-001 §3.2
- **Deliverable:**
  - `Stewie.Domain/Entities/Event.cs` — `Id`, `EntityType`, `EntityId`, `EventType`, `Payload` (JSON string), `Timestamp`
  - `Stewie.Domain/Enums/EventType.cs` — `RunCreated`, `RunStarted`, `RunCompleted`, `RunFailed`, `TaskCreated`, `TaskStarted`, `TaskCompleted`, `TaskFailed`
  - `Stewie.Infrastructure/Mappings/EventMap.cs`
  - `Stewie.Infrastructure/Migrations/Migration_005_CreateEventsTable.cs`
  - `Stewie.Application/Interfaces/IEventRepository.cs`
  - `Stewie.Infrastructure/Repositories/EventRepository.cs`
  - DI registration in `Program.cs`
- **Acceptance criteria:**
  - Event entity persists to SQL Server
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-003: Workspace Entity
- **Dependencies:** None
- **Contracts:** BLU-001 §3.2
- **Deliverable:**
  - `Stewie.Domain/Entities/Workspace.cs` — `Id`, `TaskId`, `Path`, `Status`, `CreatedAt`, `MountedAt`, `CleanedAt`
  - `Stewie.Domain/Enums/WorkspaceStatus.cs` — `Created`, `Mounted`, `Cleaned`
  - `Stewie.Infrastructure/Mappings/WorkspaceMap.cs`
  - `Stewie.Infrastructure/Migrations/Migration_006_CreateWorkspacesTable.cs`
  - `Stewie.Application/Interfaces/IWorkspaceRepository.cs`
  - `Stewie.Infrastructure/Repositories/WorkspaceRepository.cs`
  - DI registration in `Program.cs`
- **Acceptance criteria:**
  - Workspace entity persists to SQL Server
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-004: Link Run to Project
- **Dependencies:** T-001
- **Contracts:** CON-002 §5.2
- **Deliverable:**
  - Add nullable `ProjectId` property + `Project` navigation to `Run.cs`
  - `Migration_007_AddProjectIdToRuns.cs` — nullable FK
  - Update `RunMap.cs` with Project reference
- **Acceptance criteria:**
  - Run can optionally be associated with a Project
  - Existing runs (null ProjectId) still work
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-005: Standardized Error Middleware
- **Dependencies:** None
- **Contracts:** CON-002 §6, GOV-004
- **Deliverable:**
  - `Stewie.Api/Middleware/ErrorHandlingMiddleware.cs` — catches exceptions, returns `{ error: { code, message, details } }`
  - Register in pipeline in `Program.cs`
- **Acceptance criteria:**
  - Unhandled exceptions return structured JSON error response
  - 404s return `NOT_FOUND` error format
  - Validation errors return `VALIDATION_ERROR` format
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-006: Health Endpoint
- **Dependencies:** None
- **Contracts:** CON-002 §4.4
- **Deliverable:**
  - `Stewie.Api/Controllers/HealthController.cs` — `GET /health`
  - Returns `{ status: "healthy", version: "...", timestamp: "..." }`
  - Version from assembly metadata
- **Acceptance criteria:**
  - `GET /health` returns 200 with correct JSON shape
  - No authentication required
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-007: Project CRUD Endpoints
- **Dependencies:** T-001
- **Contracts:** CON-002 §4.1, §5.1
- **Deliverable:**
  - `Stewie.Api/Controllers/ProjectsController.cs`
  - `GET /api/projects` — list all
  - `POST /api/projects` — create (name, repoUrl)
  - `GET /api/projects/{id}` — get by ID
  - Structured logging on all actions
- **Acceptance criteria:**
  - All 3 endpoints return correct JSON per CON-002 §5.1
  - POST validates required fields
  - GET /{id} returns 404 for missing projects (using error format from T-005)
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-008: Run Endpoints (Enhanced)
- **Dependencies:** T-004
- **Contracts:** CON-002 §4.2, §5.2
- **Deliverable:**
  - Add to `RunsController.cs` or create new controller:
  - `GET /api/jobs` — list all runs (optionally filter by projectId query param)
  - `POST /api/jobs` — create a run (optionally with projectId)
  - `GET /api/jobs/{id}` — get run by ID, include nested tasks
  - Keep existing `POST /runs/test` working
- **Acceptance criteria:**
  - All endpoints return correct JSON per CON-002 §5.2
  - GET /{id} includes tasks array
  - Build succeeds
- **Status:** [x] Complete — merged to main

### T-009: Task Endpoints
- **Dependencies:** None (uses existing WorkTask entity)
- **Contracts:** CON-002 §4.3, §5.3
- **Deliverable:**
  - `Stewie.Api/Controllers/TasksController.cs`
  - `GET /api/tasks/{id}` — get task by ID, include artifacts
  - `GET /api/jobs/{runId}/tasks` — list tasks for a run
- **Acceptance criteria:**
  - All endpoints return correct JSON per CON-002 §5.3
  - Build succeeds
- **Status:** [x] Complete — merged to main

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/JOB-001-frontend-tests`
> **File territory:** `src/Stewie.Web/ClientApp/`, `src/Stewie.Tests/` (new project)

### T-010: Test Project Setup
- **Dependencies:** None
- **Contracts:** GOV-002
- **Deliverable:**
  - Create `src/Stewie.Tests/Stewie.Tests.csproj` (xUnit + Moq/NSubstitute)
  - Add project to `Stewie.slnx`
  - Reference `Stewie.Domain`, `Stewie.Application`, `Stewie.Infrastructure`
  - Verify: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` runs (even with 0 tests)
- **Acceptance criteria:**
  - Test project builds and runs
  - Added to solution file
- **Status:** [x] Complete — merged to main

### T-011: Unit Tests — RunOrchestrationService
- **Dependencies:** T-010
- **Contracts:** GOV-002
- **Deliverable:**
  - `Stewie.Tests/Services/RunOrchestrationServiceTests.cs`
  - Test cases:
    - Happy path: run completes successfully
    - Container fails (non-zero exit code)
    - Result.json missing after container exit
    - Exception during execution
  - Mock all dependencies (IRunRepository, IWorkTaskRepository, IArtifactRepository, IWorkspaceService, IContainerService, IUnitOfWork)
- **Acceptance criteria:**
  - All tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
  - Covers success, failure, and exception paths
- **Status:** [x] Complete — merged to main

### T-012: Unit Tests — WorkspaceService
- **Dependencies:** T-010
- **Contracts:** GOV-002
- **Deliverable:**
  - `Stewie.Tests/Services/WorkspaceServiceTests.cs`
  - Test cases:
    - PrepareWorkspace creates correct directory structure
    - PrepareWorkspace writes valid task.json
    - ReadResult deserializes result.json correctly
    - ReadResult throws when file missing
  - Use temporary directories for filesystem tests
- **Acceptance criteria:**
  - All tests pass
  - Covers normal and error paths
- **Status:** [x] Complete — merged to main

### T-013: Dashboard Layout & Routing
- **Dependencies:** None
- **Contracts:** None (design follows branding in AGENTS.md §2)
- **Deliverable:**
  - Install React Router (`react-router-dom`)
  - Create layout component: header (Stewie logo + wordmark), sidebar navigation, main content area
  - Routes: `/` (dashboard home), `/runs` (runs list), `/runs/:id` (run detail), `/projects` (projects)
  - Use Stewie branding: primary `#6fac50`, secondary `#767573`
  - Dark theme preferred
  - CSS file for global styles
- **Acceptance criteria:**
  - Navigation between routes works
  - Layout renders correctly with Stewie branding
  - `npm run build` succeeds (in ClientApp)
- **Status:** [x] Complete — merged to main

### T-014: Runs List Page
- **Dependencies:** T-013
- **Contracts:** CON-002 §4.2, §5.2
- **Deliverable:**
  - `src/pages/RunsPage.tsx` — fetches `GET /api/jobs`, displays as a table/list
  - Status badges (color-coded: Pending=gray, Running=blue, Completed=green, Failed=red)
  - Click a run → navigates to `/runs/{id}`
  - Loading and empty states
  - API service module for HTTP calls
- **Acceptance criteria:**
  - Runs display with correct status badges
  - Click navigates to detail page
  - Handles loading and empty states gracefully
  - `npm run build` succeeds
- **Status:** [x] Complete — merged to main

### T-015: Run Detail Page
- **Dependencies:** T-013
- **Contracts:** CON-002 §4.2, §4.3, §5.2, §5.3
- **Deliverable:**
  - `src/pages/RunDetailPage.tsx` — fetches `GET /api/jobs/{id}`, displays Run info + Tasks table
  - Shows: Run status, created/completed timestamps, list of tasks with their statuses
  - Each task shows: role, status, workspace path, timestamps
- **Acceptance criteria:**
  - Run detail displays all fields from CON-002 §5.2
  - Tasks listed within the run detail
  - Handles 404 (run not found) gracefully
  - `npm run build` succeeds
- **Status:** [x] Complete — merged to main

### T-016: Projects Page
- **Dependencies:** T-013
- **Contracts:** CON-002 §4.1, §5.1
- **Deliverable:**
  - `src/pages/ProjectsPage.tsx` — fetches `GET /api/projects`, displays as list/cards
  - "Create Project" form (name, repoUrl) → `POST /api/projects`
  - Success/error feedback
- **Acceptance criteria:**
  - Projects list displays
  - Create form works and refreshes list
  - Validation errors shown to user
  - `npm run build` succeeds
- **Status:** [x] Complete — merged to main

---

## Sprint Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-001 | A | [x] | Project entity |
| T-002 | A | [x] | Event entity |
| T-003 | A | [x] | Workspace entity |
| T-004 | A | [x] | Link Run→Project |
| T-005 | A | [x] | Error middleware |
| T-006 | A | [x] | Health endpoint |
| T-007 | A | [x] | Project CRUD |
| T-008 | A | [x] | Run endpoints |
| T-009 | A | [x] | Task endpoints |
| T-010 | B | [x] | Test project setup |
| T-011 | B | [x] | Tests: RunOrchestrationService |
| T-012 | B | [x] | Tests: WorkspaceService |
| T-013 | B | [x] | Dashboard layout |
| T-014 | B | [x] | Runs list page |
| T-015 | B | [x] | Run detail page |
| T-016 | B | [x] | Projects page |

---

## Merge Strategy

1. **Agent A completes** → Architect audits → merge `feature/JOB-001-backend-api` to `main`
2. **Agent B completes** → rebase `feature/JOB-001-frontend-tests` onto updated `main` → Architect audits → merge
3. Architect verifies end-to-end: dashboard shows data from API

**Expected conflict:** `Stewie.slnx` (Agent B adds test project). Trivial to resolve.

---

## Blockers

| # | Blocker | Filed by | DEF/EVO ID | Status |
|:--|:--------|:---------|:-----------|:-------|
| — | None | — | — | — |

---

## Sprint Completion Criteria

- [ ] All 16 tasks pass acceptance criteria
- [ ] All GOV compliance checks pass (Architect audit)
- [ ] `dotnet build src/Stewie.slnx` succeeds with 0 errors
- [ ] `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` passes
- [ ] `npm run build` succeeds in `src/Stewie.Web/ClientApp/`
- [ ] Architect audit complete
- [ ] No open `DEF-` reports against this sprint

---

## Audit Notes (Architect)

### Agent A Audit (2026-04-09)
- **Audit report:** `40_VERIFICATION/VER-001_JOB-001_Agent_A_Audit.md`
- Build: ✅ 0 errors
- Governance: ✅ All 8 GOV docs compliant
- Contract: ✅ All 10 CON-002 endpoints verified
- Code quality: High — consistent patterns, full XML doc coverage, structured logging
- **Verdict:** PASS
- **Merged to main:** commit `979425c`

### Agent B Audit (2026-04-09)
- **Audit report:** `40_VERIFICATION/VER-002_JOB-001_Agent_B_Audit.md`
- Build: ✅ Test project 0 errors, frontend 48 modules compiled
- Tests: ✅ 8/8 pass (131ms)
- Governance: ✅ All applicable GOV docs compliant, zero `any` types
- Contract: ✅ TypeScript types match all CON-002 schemas
- UI: ✅ Stewie branding, dark theme, Inter font, 754-line design system
- Rebase: ✅ Clean rebase on Agent A code, combined build verified
- **Verdict:** PASS
- **Merged to main:** merge commit on `main`

**Sprint Verdict:** CLOSED ✅
**Deploy approved:** YES (pending infrastructure verification)
