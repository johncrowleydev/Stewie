---
id: JOB-007
title: "Sequential Task Chains + Governance Infrastructure"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, governance]
related: [BCK-001, CON-001, CON-002, GOV-001, GOV-002, GOV-003, GOV-004, GOV-005, GOV-006, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Build the infrastructure for Stewie's governance engine: sequential task chains (dev → tester → accept/reject/retry), GovernanceReport entity, governance worker Docker image, and orchestration-level chaining logic. This is the plumbing — JOB-008 adds the rule engine and frontend on top.

# Job 007: Sequential Task Chains + Governance Infrastructure

**Phase:** Phase 3 — Governance Engine
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Backend + Worker), Dev Agent B (API)
**Dependencies:** JOB-006 complete (merged)
**Contracts:** CON-001 v1.4.0 (governance-report.json, TaskPacket extensions), CON-002 v1.6.0 (/api/jobs/{id}/governance)

---

## Design Decisions (Approved)

1. **Secret scanning:** YES — governance worker includes credential leak detection (SEC-001 category)
2. **Warning severity:** User-configurable setting. Default: warnings do NOT block acceptance. Only errors trigger rejection.
3. **Stack detection:** File-based heuristic for now (`*.csproj` → dotnet, `package.json` → react). Future: `stewie.json` project config override.
4. **Max retries:** Configurable via `Stewie:MaxGovernanceRetries`, default 2.

---

## Dev Agent A Tasks (Backend + Worker Image)

> **Branch:** `feature/JOB-007-backend`

### T-066: WorkTask Entity Extensions + Migrations
- Add to `WorkTask.cs`:
  - `ParentTaskId` (Guid?, FK to self — tester points to dev task)
  - `AttemptNumber` (int, default 1)
  - `GovernanceViolationsJson` (string?, nvarchar(max) — violations from prior tester for retry feedback)
- New migration: `Migration_013_AddTaskChainFields.cs`
- Update `WorkTaskMap.cs` for new columns
- **AC:** Build succeeds, migration runs without error

### T-067: GovernanceReport Entity + Migration + Repository
- New entity `GovernanceReport.cs`:
  - `Id`, `TaskId` (FK), `Passed` (bool), `TotalChecks`, `PassedChecks`, `FailedChecks`, `CheckResultsJson` (nvarchar(max)), `CreatedAt`
- New DTO `GovernanceCheckResult.cs` (serialized as JSON array in `CheckResultsJson`)
  - `RuleId`, `RuleName`, `Category`, `Passed`, `Details`, `Severity`
- New migration: `Migration_014_CreateGovernanceReports.cs`
- `IGovernanceReportRepository` + `GovernanceReportRepository`
- NHibernate mapping
- **AC:** Build succeeds, `GovernanceReport` can be persisted and retrieved

### T-068: TaskPacket Extensions
- Add to `TaskPacket.cs`:
  - `parentTaskId` (Guid?)
  - `governanceViolations` (List\<GovernanceViolation\>?)
  - `attemptNumber` (int, default 1)
- New DTO `GovernanceViolation.cs`:
  - `ruleId`, `ruleName`, `details`
- **AC:** TaskPacket serializes correctly with new fields

### T-069: JobOrchestrationService Sequential Chaining
The core orchestration change:
1. After dev task completes successfully → spawn tester task (role=tester, ParentTaskId=devTask.Id)
2. Launch governance container against same workspace
3. Ingest `governance-report.json` from output directory
4. Create GovernanceReport entity
5. If PASS → mark job Complete, emit GovernancePassed
6. If FAIL + attempts < max → spawn new dev task with violation feedback, emit GovernanceRetry
7. If FAIL + attempts >= max → mark job Failed (GovernanceFailed), emit GovernanceFailed
- Config: `Stewie:MaxGovernanceRetries` (default 2), `Stewie:GovernanceWorkerImage` (default "stewie-governance-worker")
- **AC:** Job with passing worker → 2 tasks created (dev + tester), job completes. Job with failing governance → retry cycle respects max attempts.

### T-070: Governance Worker Docker Image (Skeleton)
- Create `workers/governance-worker/`:
  - `Dockerfile` (dotnet-sdk:10.0-alpine + nodejs + npm + bash + git + jq)
  - `entrypoint.sh`: reads task.json, detects stack, writes minimal governance-report.json
  - `rules/` directory (empty scripts for now — JOB-008 populates)
- Image should build and run, producing a valid governance-report.json (all checks pass as placeholder)
- **AC:** `docker build -t stewie-governance-worker workers/governance-worker/` succeeds. Container runs and produces valid report.

### T-071: EventType Extensions + Governance Events
- Add to `EventType.cs`: GovernanceStarted=8, GovernancePassed=9, GovernanceFailed=10, GovernanceRetry=11
- Add to `TaskFailureReason.cs`: GovernanceFailed
- Emit events from orchestration service at appropriate points
- **AC:** Events table records governance lifecycle events for a job

---

## Dev Agent B Tasks (API)

> **Branch:** `feature/JOB-007-api`

### T-072: GovernanceController + API Response Updates
- New `GovernanceController.cs`:
  - `GET /api/jobs/{jobId}/governance` → latest GovernanceReport
  - `GET /api/tasks/{taskId}/governance` → GovernanceReport for specific tester task
- Modify `GET /api/jobs/{id}` response to include all tasks in chain (ordered by CreatedAt)
- Task response gains: `parentTaskId`, `attemptNumber`
- **AC:** API returns governance report data per CON-002 v1.6.0

---

## Job Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-066 | A | [ ] | WorkTask extensions + migration |
| T-067 | A | [ ] | GovernanceReport entity + migration |
| T-068 | A | [ ] | TaskPacket extensions |
| T-069 | A | [ ] | Orchestration sequential chaining |
| T-070 | A | [ ] | Governance worker Docker image |
| T-071 | A | [ ] | EventType extensions + events |
| T-072 | B | [ ] | GovernanceController + API |
| T-073 | Architect | [ ] | CON-001 v1.4.0 + CON-002 v1.6.0 |

---

## Merge Strategy

1. Agent A merges `feature/JOB-007-backend` first (entities, migrations, orchestration, worker)
2. Agent B rebases `feature/JOB-007-api` on updated main, then merges (API layer depends on entities)
3. Verify: build + tests + governance worker image builds

---

## Audit Notes (Architect)

_[Pending — will be filled after developer work completes]_
