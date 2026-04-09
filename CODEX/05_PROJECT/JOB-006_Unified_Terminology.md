---
id: JOB-006
title: "Unified Terminology — Run → Job, SPR- → JOB-"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, refactor]
related: [BCK-001, CON-001, CON-002, GOV-005, GOV-007]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Rename the `Run` entity to `Job` throughout the codebase and rename all `SPR-` document prefixes to `JOB-` in CODEX. Unifies orchestration engine terminology with governance system — "Job" is the single concept for a batch of work containing tasks. Pre-Phase 3 cleanup.

# Job 006: Unified Terminology — Run → Job, SPR- → JOB-

**Phase:** Pre-Phase 3 cleanup
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** JOB-005 complete (merged)
**Contracts:** CON-001 v1.3.0 (jobId), CON-002 v1.5.0 (/api/jobs)

---

## ⚠️ Critical: This is a RENAME-ONLY refactor

No new features. No behavior changes. Every file rename, class rename, and content replacement must produce identical functionality with different names.

**Use file-redirect workflow** for `dotnet build` / `dotnet test` / `npm run build`.

---

## Dev Agent A Tasks (Backend)

> **Branch:** `feature/JOB-006-backend`

### T-057: Database Migration (Runs→Jobs, RunId→JobId)
- New FluentMigrator migration using `sp_rename`
- Rename `Runs` table → `Jobs`
- Rename `Tasks.RunId` column → `Tasks.JobId`
- Rename any FK constraints or indexes referencing "Run"
- **AC:** Migration runs on startup, existing data preserved

### T-058: Domain Entity Renames
- `Run.cs` → `Job.cs` (class `Run` → `Job`)
- `RunStatus.cs` → `JobStatus.cs` (enum `RunStatus` → `JobStatus`)
- `WorkTask.cs`: `RunId` → `JobId`, `Run` nav → `Job`
- `TaskPacket.cs`: `runId` → `jobId` (CON-001)
- **AC:** Build succeeds, no references to `Run` entity remain

### T-059: Application Layer Renames
- `IRunRepository.cs` → `IJobRepository.cs`
- `RunOrchestrationService.cs` → `JobOrchestrationService.cs`
- `IWorkTaskRepository`: `GetByRunIdAsync` → `GetByJobIdAsync`
- Update all internal variable names: `run` → `job`
- **AC:** Build succeeds

### T-060: API Layer Renames
- `RunsController.cs` → `JobsController.cs`
- Routes: `/api/runs` → `/api/jobs`, `/runs/test` → `/jobs/test`
- `CreateRunRequest` → `CreateJobRequest`
- `Program.cs`: DI registration updates
- **AC:** `POST /api/jobs` returns 201, `POST /api/runs` returns 404

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/JOB-006-frontend-tests`

### T-061: Frontend Renames
- `RunsPage.tsx` → `JobsPage.tsx`
- `CreateRunPage.tsx` → `CreateJobPage.tsx`
- `RunDetailPage.tsx` → `JobDetailPage.tsx`
- `App.tsx`: routes `/runs` → `/jobs`
- `types/index.ts`: `Run` type → `Job`, `runId` → `jobId`
- `api/client.ts`: endpoints and function names
- `Layout.tsx`: nav label "Runs" → "Jobs"
- All other pages with run references
- **AC:** `npm run build` succeeds, nav shows "Jobs"

### T-062: Test Renames
- `RunsControllerTests.cs` → `JobsControllerTests.cs`
- `RunCreationTests.cs` → `JobCreationTests.cs`
- `RunOrchestrationServiceTests.cs` → `JobOrchestrationServiceTests.cs`
- Update internal references in ContainerTimeoutTests, RetryLogicTests, WorkspaceServiceTests
- **AC:** All tests pass

---

## Job Checklist

| Task | Agent | Status |
|:-----|:------|:-------|
| T-057 | A | [x] |
| T-058 | A | [x] |
| T-059 | A | [x] |
| T-060 | A | [x] |
| T-061 | B | [x] |
| T-062 | B | [x] |
| T-063 | Architect | [x] |
| T-064 | Architect | [x] |
| T-065 | Architect | [x] |

---

## Merge Strategy

1. Agent A merges `feature/JOB-006-backend` first
2. Agent B rebases `feature/JOB-006-frontend-tests` on updated main, then merges
3. Verify: `grep -r "IRunRepository\|RunStatus\|RunsController\|/api/runs\|SPR-" src/ CODEX/` returns 0 results

---

## Audit Notes (Architect)

### Combined Audit (2026-04-09)
- **Audit report:** `40_VERIFICATION/VER-007_JOB-006_Audit.md`
- Build: ✅ 0 errors, frontend clean, 54/54 tests pass (3 skipped)
- Merge conflicts: 4 test files (both agents renamed same files), resolved by accepting Agent A's versions
- Stale reference scan: ✅ ZERO residual `Run`, `SPR-`, `IRunRepository`, `/api/runs` references
- **Verdict:** PASS

**Job Verdict:** CLOSED ✅
**Deploy approved:** YES
