---
id: SPR-003
title: "Real Repo Interaction"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [project-management, sprint, workflow]
related: [BCK-001, BLU-001, CON-001, CON-002, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Sprint 003 delivers Phase 2 — real repo interaction. Stewie clones repos, creates branches, runs script workers, captures diffs, and auto-commits results. Two agents work in parallel. Agent A merges first.

# Sprint 003: Real Repo Interaction

**Phase:** Phase 2 — Real Repo Interaction
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** SPR-002 complete (merged)
**Contracts:** CON-001 v1.2.0 (script field), CON-002 v1.2.0 (Run creation body, diff/branch fields)

---

## ⚠️ Mandatory Compliance — Every Task

| Governance Doc | Sprint Requirement |
|:---------------|:-------------------|
| **GOV-001** | XML doc comments (C#), JSDoc (TS) on all public/exported members |
| **GOV-002** | All new code must have tests |
| **GOV-003** | C# coding standards; TS strict mode, no `any` types |
| **GOV-004** | Error middleware for API; error/loading states for frontend |
| **GOV-005** | Branch: `feature/SPR-003-backend` (A), `feature/SPR-003-frontend-tests` (B). Commits: `feat(SPR-003): T-XXX description` |
| **GOV-006** | Structured `ILogger` logging on all new services and controllers |
| **GOV-008** | All infrastructure per GOV-008 |

---

## Parallel Execution Plan

```
Agent A (Backend)                    Agent B (Frontend + Tests)
─────────────────                    ─────────────────────────
T-027: Run creation API              T-032: Create Run form
T-028: Wire git into loop            T-033: Run detail git/diff viewer
T-029: Script worker container       T-034: Dashboard auto-refresh
T-030: Diff ingestion                T-035: Integration tests (Run creation)
T-031: Auto-commit                   T-036: Unit tests (diff/commit)
        │                                    │
        ▼                                    ▼
   Merge A first                    Merge B second (rebase on A)
```

---

## Dev Agent A Tasks (Backend)

> **Branch:** `feature/SPR-003-backend`
> **File territory:** `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, `src/Stewie.Api/`, `workers/`

### T-027: Extended Run Creation API
- **Dependencies:** None
- **Contracts:** CON-002 §4.2 (updated), §5.2, §5.3
- **Deliverable:**
  - Modify `POST /api/runs` to accept JSON body: `{ projectId, objective, scope, script, acceptanceCriteria }`
  - Validate `projectId` exists (return 404 if not), `objective` is non-empty (return 400 if missing)
  - Create Run linked to Project, create Task with provided fields
  - Add `Objective` (string), `Scope` (string), `ScriptJson` (string, JSON array), `AcceptanceCriteriaJson` (string, JSON array) fields to WorkTask entity
  - Add `Branch` (string), `DiffSummary` (string), `CommitSha` (string) fields to Run entity
  - FluentMigrator migration for new columns
  - NHibernate mapping updates
  - `POST /runs/test` remains backward-compatible
- **Acceptance criteria:**
  - `POST /api/runs { projectId, objective }` creates Run + Task with correct fields
  - Validation returns 400/404 for bad input
  - Build succeeds
- **Status:** [ ] Not Started

### T-028: Wire Git Operations into Execution Loop
- **Dependencies:** T-027 (new fields exist)
- **Contracts:** CON-001 §3, §4.1
- **Deliverable:**
  - In `RunOrchestrationService`, after workspace preparation:
    - If Run's Project has a `RepoUrl`, call `CloneRepositoryAsync(repoUrl, workspacePath)`
    - Generate branch name: `stewie/{runId-short}/{sanitized-objective}`
    - Call `CreateBranchAsync(workspacePath, branchName)`
    - Store branch name on Run entity
  - Update `PrepareWorkspace` to write `repoUrl`, `branch`, `script`, `objective`, `scope` into task.json
  - Create a new `ExecuteRunAsync(Guid runId)` method (or extend existing) that processes real runs
  - Keep `ExecuteTestRunAsync()` as-is for backward compatibility
- **Acceptance criteria:**
  - Creating a Run against a Project with repoUrl → repo cloned, branch created
  - task.json contains all fields from CON-001 v1.2.0
  - Build succeeds
- **Status:** [ ] Not Started

### T-029: Script Worker Container
- **Dependencies:** None (can be built independently)
- **Contracts:** CON-001 §4, §5
- **Deliverable:**
  - `workers/script-worker/Dockerfile` (Alpine + bash + git, lightweight)
  - `workers/script-worker/entrypoint.sh`:
    1. Read task.json
    2. If `script` array exists, execute each command via `bash -c` in `/workspace/repo/`
    3. Capture stdout/stderr per command
    4. Write result.json (status=success if all exit 0, failure otherwise)
    5. Include command output in result `notes` field
  - Keep the worker language-agnostic (shell script, not C#) for portability
  - Update `appsettings.json`: add `Stewie:ScriptWorkerImage` = `stewie-script-worker`
  - Docker build produces `stewie-script-worker:latest`
- **Acceptance criteria:**
  - `docker build -t stewie-script-worker workers/script-worker/` succeeds
  - Worker runs, reads task.json, executes script, writes valid result.json
  - Worker handles missing script (reports "no script provided")
  - Worker handles failing commands (reports failure with stderr)
- **Status:** [ ] Not Started

### T-030: Diff Ingestion
- **Dependencies:** T-028 (git operations wired in)
- **Contracts:** CON-002 §5.6
- **Deliverable:**
  - After worker container exits successfully:
    - Run `git diff --stat` in workspace repo → capture summary
    - Run `git diff` in workspace repo → capture full patch
    - Store as artifact: type="diff", contentJson = `{ diffStat, diffPatch }`
  - Store `DiffSummary` on Run entity
  - Add `CaptureDiffAsync(string workspacePath)` to `IWorkspaceService`
  - Returns a `DiffResult { DiffStat, DiffPatch }` DTO
- **Acceptance criteria:**
  - After a script worker modifies files, `GET /api/runs/{id}` includes `diffSummary`
  - Diff artifact exists in artifacts array
  - Build succeeds
- **Status:** [ ] Not Started

### T-031: Auto-Commit Worker Changes
- **Dependencies:** T-030 (diff captured first)
- **Contracts:** CON-002 §5.2 (commitSha field)
- **Deliverable:**
  - After diff capture, run `git add -A && git commit -m "..."` in workspace repo
  - Commit message: `feat(stewie): {objective} [Run {runId-short}]`
  - Store commit SHA on Run entity (`CommitSha` field)
  - Add `CommitChangesAsync(string workspacePath, string message)` to `IWorkspaceService`
  - Returns the commit SHA string
  - This is a LOCAL commit only — push to remote deferred to SPR-004
- **Acceptance criteria:**
  - After script worker runs, workspace repo has a new commit
  - `GET /api/runs/{id}` includes `commitSha`
  - Build succeeds
- **Status:** [ ] Not Started

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/SPR-003-frontend-tests`
> **File territory:** `src/Stewie.Web/ClientApp/`, `src/Stewie.Tests/`

### T-032: Create Run Form
- **Dependencies:** None (build against CON-002 §4.2 contract)
- **Contracts:** CON-002 §4.2
- **Deliverable:**
  - Accessible from Dashboard ("New Run" button) and Runs page
  - Form fields:
    - Project selector (dropdown populated from `GET /api/projects`)
    - Objective (text area, required)
    - Scope (text input, optional)
    - Script commands (multi-line text area, one command per line, optional)
    - Acceptance criteria (multi-line text area, one per line, optional)
  - Submit calls `POST /api/runs` with JSON body
  - Loading state during submission
  - On success: redirect to Run detail page
  - Form validation (project + objective required)
  - Add `createRun()` function to `api/client.ts`
- **Acceptance criteria:**
  - Form renders, validates, submits successfully
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-033: Run Detail — Git Info + Diff Viewer
- **Dependencies:** Soft dep on T-030 (diff artifact)
- **Contracts:** CON-002 §5.2, §5.6
- **Deliverable:**
  - Show branch name badge on Run detail page
  - Show commit SHA (truncated, monospaced)
  - Show diff summary (files changed, +/- lines)
  - Expandable diff viewer with monospaced text (full patch)
  - Add new types to `types/index.ts` for the extended Run/Artifact schemas
  - Color-code diff lines (green for +, red for -)
- **Acceptance criteria:**
  - Run detail shows git info when available
  - Diff viewer renders patch with color coding
  - Handles runs without git info gracefully (null states)
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-034: Dashboard Auto-Refresh + Run Status Polling
- **Dependencies:** None
- **Contracts:** None (UX improvement)
- **Deliverable:**
  - Dashboard page: poll `GET /api/runs` every 5s (configurable via constant)
  - Runs list page: same polling
  - Run detail page: poll `GET /api/runs/{id}` every 3s while status is `Running` or `Pending`
  - Stop polling once status reaches `Completed` or `Failed`
  - Create `usePolling` custom hook for reusable polling logic
  - Subtle "live" indicator (green dot + pulse) when polling is active
  - Clean up intervals on unmount
- **Acceptance criteria:**
  - Dashboard updates automatically when a run changes state
  - Polling stops for completed/failed runs
  - No memory leaks (intervals cleaned up)
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-035: Integration Tests — Run Creation
- **Dependencies:** None (tests define expected behavior)
- **Contracts:** CON-002 §4.2, GOV-002
- **Deliverable:**
  - `Stewie.Tests/Integration/RunCreationTests.cs`
  - Test cases:
    - `POST /api/runs` with valid `{ projectId, objective }` → 200/201
    - `POST /api/runs` with missing `projectId` → 400
    - `POST /api/runs` with missing `objective` → 400
    - `POST /api/runs` with non-existent `projectId` → 404
    - Response body includes `branch` field (null or populated)
  - Reuse `StewieWebApplicationFactory` from SPR-002
- **Acceptance criteria:**
  - All integration tests pass
  - Build succeeds
- **Status:** [ ] Not Started

### T-036: Unit Tests — Diff/Commit Services
- **Dependencies:** None
- **Contracts:** GOV-002
- **Deliverable:**
  - `Stewie.Tests/Services/WorkspaceServiceGitTests.cs`
  - Test cases:
    - `CaptureDiffAsync` with modified files returns non-empty DiffStat + DiffPatch
    - `CaptureDiffAsync` with no changes returns empty
    - `CommitChangesAsync` creates a commit and returns SHA
    - `CommitChangesAsync` with no changes throws or returns null
  - These test the actual git operations (create temp git repo in test)
- **Acceptance criteria:**
  - All unit tests pass
  - Build succeeds
- **Status:** [ ] Not Started

---

## Sprint Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-027 | A | [ ] | Run creation API |
| T-028 | A | [ ] | Wire git into loop |
| T-029 | A | [ ] | Script worker container |
| T-030 | A | [ ] | Diff ingestion |
| T-031 | A | [ ] | Auto-commit |
| T-032 | B | [ ] | Create Run form |
| T-033 | B | [ ] | Run detail git/diff viewer |
| T-034 | B | [ ] | Dashboard auto-refresh |
| T-035 | B | [ ] | Integration tests (Run creation) |
| T-036 | B | [ ] | Unit tests (diff/commit) |

---

## Merge Strategy

1. **Agent A completes** → Architect audits → merge `feature/SPR-003-backend` to `main`
2. **Agent B completes** → rebase `feature/SPR-003-frontend-tests` onto updated `main` → Architect audits → merge
3. Build script worker image: `docker build -t stewie-script-worker workers/script-worker/`
4. E2E: Create project → create run with script → verify repo cloned, script executed, diff captured, committed

---

## Sprint Completion Criteria

- [ ] All 10 tasks pass acceptance criteria
- [ ] `dotnet build src/Stewie.slnx` succeeds with 0 errors
- [ ] `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` passes (all unit + integration)
- [ ] `npm run build` succeeds in `src/Stewie.Web/ClientApp/`
- [ ] Script worker container builds and runs correctly
- [ ] E2E: real repo cloned, script executed, diff captured, changes committed
- [ ] No open `DEF-` reports against this sprint
- [ ] Phase 2 exit criteria met (per PRJ-001)

---

## Audit Notes (Architect)

[Architect fills this in during audit.]

**Verdict:** PENDING
**Deploy approved:** NO
