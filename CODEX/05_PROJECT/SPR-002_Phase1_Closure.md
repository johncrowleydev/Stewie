---
id: SPR-002
title: "Phase 1 Closure + Phase 2 Plumbing"
type: how-to
status: PLANNING
owner: architect
agents: [coder]
tags: [project-management, sprint, workflow]
related: [BCK-001, BLU-001, CON-001, CON-002, DEF-001, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Sprint 002 closes out Phase 1 (event emission, workspace tracking, integration tests, DEF-001 light theme) and starts Phase 2 plumbing (git clone into workspaces, branch creation per task). Two agents work in parallel. Agent A merges first.

# Sprint 002: Phase 1 Closure + Phase 2 Plumbing

**Phase:** Phase 1 closure + Phase 2 start
**Target:** Scope-bounded (AI-agent pace)
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** SPR-001 complete (merged)
**Contracts:** CON-001 v1.1.0 (updated with git fields), CON-002 v1.1.0 (updated with Events endpoints)

---

## ⚠️ Mandatory Compliance — Every Task

> All tasks in this sprint MUST incorporate these governance standards. They are not optional and not deferred.

| Governance Doc | Sprint Requirement |
|:---------------|:-------------------|
| **GOV-001** | XML doc comments on all public classes/methods (C#), JSDoc on all exported functions (TS) |
| **GOV-002** | All new code must have tests. Integration tests use SQLite in-memory via WebApplicationFactory. |
| **GOV-003** | C# coding standards; TypeScript strict mode, no `any` types |
| **GOV-004** | Error middleware for API; error boundaries/states for frontend |
| **GOV-005** | Agent A: `feature/SPR-002-backend`. Agent B: `feature/SPR-002-frontend-tests`. Commits: `feat(SPR-002): T-XXX description` |
| **GOV-006** | Structured `ILogger` logging on all new services and controllers |
| **GOV-007** | Task status updated in this doc. Blockers → `DEF-` doc |
| **GOV-008** | All infrastructure per GOV-008 |

---

## Parallel Execution Plan

```
Agent A (Backend)                Agent B (Frontend + Tests)
─────────────────                ─────────────────────────
T-017: Event emission (Run)      T-022: DEF-001 theme toggle
T-018: Event emission (Task)     T-023: Integration tests (Projects)
T-019: Workspace tracking        T-024: Integration tests (Runs/Tasks/Health)
T-020: Events API endpoint       T-025: Events timeline page ← soft dep on T-020
T-021: Git clone/branch          T-026: Run detail events ← soft dep on T-020
        │                                │
        ▼                                ▼
   Merge A first                Merge B second (rebase on A)
```

**Soft dependency:** T-025 and T-026 reference the Events API shape from CON-002 §4.5/§5.5. Agent B can build the UI against the contract definition without needing Agent A's actual endpoint code. Wire-up happens after rebase.

---

## Dev Agent A Tasks (Backend)

> **Branch:** `feature/SPR-002-backend`
> **File territory:** `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, `src/Stewie.Api/`

### T-017: Event Emission — Run Lifecycle
- **Dependencies:** None
- **Contracts:** BLU-001 §4, CON-002 §5.5
- **Deliverable:**
  - Modify `RunOrchestrationService.ExecuteTestRunAsync()` to emit events via `IEventRepository`
  - Emit `RunCreated` when Run entity is created
  - Emit `RunStarted` when task execution begins
  - Emit `RunCompleted` when run finishes successfully
  - Emit `RunFailed` when run fails
  - Payload captures relevant context (failure reason, task count, etc.)
- **Acceptance criteria:**
  - After `POST /runs/test`, Events table contains RunCreated + RunStarted + RunCompleted/RunFailed
  - Build succeeds
- **Status:** [ ] Not Started

### T-018: Event Emission — Task Lifecycle
- **Dependencies:** T-017 (pattern established)
- **Contracts:** BLU-001 §4, CON-002 §5.5
- **Deliverable:**
  - Emit `TaskCreated` when WorkTask is created
  - Emit `TaskStarted` when container launches
  - Emit `TaskCompleted` when task succeeds
  - Emit `TaskFailed` when task fails
  - Payload captures role, workspace path, exit code on failure
- **Acceptance criteria:**
  - After `POST /runs/test`, Events table contains TaskCreated + TaskStarted + TaskCompleted/TaskFailed
  - Build succeeds
- **Status:** [ ] Not Started

### T-019: Workspace Lifecycle Tracking
- **Dependencies:** None
- **Contracts:** BLU-001 §3.2
- **Deliverable:**
  - Modify `RunOrchestrationService.ExecuteTestRunAsync()` to save `Workspace` entity records
  - `WorkspaceStatus.Created` → after `PrepareWorkspace()` returns
  - `WorkspaceStatus.Mounted` → after container launches
  - Update Workspace status transitions via `IWorkspaceRepository.SaveAsync()`
- **Acceptance criteria:**
  - After `POST /runs/test`, Workspaces table has a record with correct status
  - Build succeeds
- **Status:** [ ] Not Started

### T-020: Events API Endpoint
- **Dependencies:** T-017, T-018 (events exist to query)
- **Contracts:** CON-002 §4.5, §5.5
- **Deliverable:**
  - `Stewie.Api/Controllers/EventsController.cs`
  - `GET /api/events` — list recent events (most recent first, default limit 100)
  - `GET /api/events?entityType=Run&entityId={id}` — filter by entity
  - `GET /api/events?limit={n}` — configurable limit (max 500)
  - Add `GetRecentAsync(int limit)` and `GetAllAsync()` to `IEventRepository`
  - Implement in `EventRepository`
  - DI registration if needed
- **Acceptance criteria:**
  - `GET /api/events` returns recent events per CON-002 §5.5
  - Filtering by entityType + entityId works
  - Build succeeds
- **Status:** [ ] Not Started

### T-021: Git Clone + Branch in WorkspaceService
- **Dependencies:** None
- **Contracts:** CON-001 §4.1 (updated with repoUrl/branch fields)
- **Deliverable:**
  - Add `CloneRepositoryAsync(string repoUrl, string workspacePath)` to `IWorkspaceService`
  - Add `CreateBranchAsync(string workspacePath, string branchName)` to `IWorkspaceService`
  - Implementation: shell out to `git clone` and `git checkout -b` via `Process.Start`
  - Add optional `RepoUrl` and `Branch` fields to `TaskPacket` C# class
  - Add unit tests for the new methods (test that commands are formed correctly)
  - Does NOT modify `ExecuteTestRunAsync()` — plumbing for future use
- **Acceptance criteria:**
  - `CloneRepositoryAsync` clones a real repo into workspace/repo/
  - `CreateBranchAsync` creates a new branch in the cloned repo
  - Unit tests pass
  - Build succeeds
- **Status:** [ ] Not Started

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/SPR-002-frontend-tests`
> **File territory:** `src/Stewie.Web/ClientApp/`, `src/Stewie.Tests/`

### T-022: DEF-001 — Light/Dark Theme Toggle
- **Dependencies:** None
- **Contracts:** None (UX improvement)
- **Defect ref:** DEF-001
- **Deliverable:**
  - Create `src/hooks/useTheme.ts` — manages theme state, persists to `localStorage`
  - Default: respect `prefers-color-scheme` media query, fallback to dark
  - Add `[data-theme="light"]` CSS custom properties to `index.css` — full light palette
  - Add theme toggle button to sidebar footer in `Layout.tsx`
  - Sun/moon icon toggle with smooth transition
- **Acceptance criteria:**
  - Toggle switches between light and dark themes
  - Theme persists across page reloads (localStorage)
  - Default respects OS preference
  - All pages render correctly in both themes
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-023: Integration Tests — Project Endpoints
- **Dependencies:** None
- **Contracts:** GOV-002, CON-002 §4.1
- **Deliverable:**
  - Add `Stewie.Api` project reference to `Stewie.Tests.csproj`
  - Create test fixture: `WebApplicationFactory<Program>` with SQLite in-memory database
  - `Stewie.Tests/Integration/ProjectsControllerTests.cs`
  - Test cases:
    - `GET /api/projects` returns 200 with empty array
    - `POST /api/projects` creates project, returns 201
    - `GET /api/projects/{id}` returns 200 for existing project
    - `GET /api/projects/{id}` returns 404 for missing project
    - `POST /api/projects` with missing name returns 400
  - Verify response schemas match CON-002 §5.1
- **Acceptance criteria:**
  - All integration tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
  - Tests run without Docker (SQLite in-memory)
  - Build succeeds
- **Status:** [ ] Not Started

### T-024: Integration Tests — Run, Task, Health Endpoints
- **Dependencies:** T-023 (test fixture established)
- **Contracts:** GOV-002, CON-002 §4.2, §4.3, §4.4
- **Deliverable:**
  - `Stewie.Tests/Integration/RunsControllerTests.cs`
  - `Stewie.Tests/Integration/HealthControllerTests.cs`
  - Test cases:
    - `GET /api/runs` returns 200 with empty array
    - `POST /api/runs` creates run, returns 201
    - `GET /api/runs/{id}` returns 200 with tasks array
    - `GET /api/runs/{id}` returns 404 for missing run
    - `GET /api/tasks/{nonexistent}` returns 404
    - `GET /health` returns 200 with status/version/timestamp
  - Verify error middleware returns structured error format per CON-002 §6
- **Acceptance criteria:**
  - All integration tests pass
  - Error response format verified
  - Build succeeds
- **Status:** [ ] Not Started

### T-025: Events Timeline Page
- **Dependencies:** Soft dependency on T-020 (Agent A creates endpoint); build against CON-002 §4.5/§5.5 contract
- **Contracts:** CON-002 §4.5, §5.5
- **Deliverable:**
  - Add `Event` type to `types/index.ts`
  - Add `fetchEvents()` and `fetchEventsByEntity()` to `api/client.ts`
  - New route: `/events` in `App.tsx`
  - `src/pages/EventsPage.tsx` — vertical timeline of events
  - Color-coded by event type (Created=blue, Started=amber, Completed=green, Failed=red)
  - Filter controls: by entity type (Run/Task)
  - Add "Events" nav link with icon to sidebar in `Layout.tsx`
- **Acceptance criteria:**
  - Events page renders (may show empty/error state if backend not yet merged)
  - `npm run build` succeeds
  - Color coding and timeline layout work
- **Status:** [ ] Not Started

### T-026: Run Detail — Events Mini-Timeline
- **Dependencies:** T-025 (shared types/API functions)
- **Contracts:** CON-002 §4.5
- **Deliverable:**
  - On `RunDetailPage.tsx`, fetch `GET /api/events?entityType=Run&entityId={id}`
  - Display as a compact horizontal or vertical timeline below the tasks table
  - Shows lifecycle: Created → Started → Completed/Failed with timestamps
- **Acceptance criteria:**
  - Run detail page shows event timeline
  - Handles missing/empty events gracefully
  - `npm run build` succeeds
- **Status:** [ ] Not Started

---

## Sprint Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-017 | A | [ ] | Event emission (Run) |
| T-018 | A | [ ] | Event emission (Task) |
| T-019 | A | [ ] | Workspace tracking |
| T-020 | A | [ ] | Events API endpoint |
| T-021 | A | [ ] | Git clone/branch plumbing |
| T-022 | B | [ ] | DEF-001 theme toggle |
| T-023 | B | [ ] | Integration tests (Projects) |
| T-024 | B | [ ] | Integration tests (Runs/Tasks/Health) |
| T-025 | B | [ ] | Events timeline page |
| T-026 | B | [ ] | Run detail events |

---

## Merge Strategy

1. **Agent A completes** → Architect audits → merge `feature/SPR-002-backend` to `main`
2. **Agent B completes** → rebase `feature/SPR-002-frontend-tests` onto updated `main` → Architect audits → merge
3. Architect verifies end-to-end: test run → events appear → events timeline shows them

---

## Blockers

| # | Blocker | Filed by | DEF/EVO ID | Status |
|:--|:--------|:---------|:-----------|:-------|
| 1 | Dark-mode-only dashboard | Human | DEF-001 | OPEN — fixed by T-022 |

---

## Sprint Completion Criteria

- [ ] All 10 tasks pass acceptance criteria
- [ ] All GOV compliance checks pass (Architect audit)
- [ ] `dotnet build src/Stewie.slnx` succeeds with 0 errors
- [ ] `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` passes (unit + integration)
- [ ] `npm run build` succeeds in `src/Stewie.Web/ClientApp/`
- [ ] `POST /runs/test` → events visible in `GET /api/events`
- [ ] Light/dark theme toggle works
- [ ] DEF-001 resolved
- [ ] No open `DEF-` reports against this sprint

---

## Audit Notes (Architect)

[Architect fills this in during audit.]

**Verdict:** PENDING
**Deploy approved:** NO
