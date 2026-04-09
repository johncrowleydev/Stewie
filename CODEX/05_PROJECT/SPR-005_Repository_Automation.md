---
id: SPR-005
title: "Repository Automation + Platform Abstraction"
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

> **BLUF:** Sprint 005 delivers Phase 3 — platform abstraction, project-level repo creation, and worker pipeline hardening. The GitHub-specific interface becomes a platform-agnostic abstraction. Project creation supports both linking existing repos and creating new ones. Worker containers get timeout enforcement and retry logic. Two agents work in parallel. Agent A merges first.

# Sprint 005: Repository Automation + Platform Abstraction

**Phase:** Phase 3 — Repository Automation + Platform Abstraction
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** SPR-004 complete (merged)
**Contracts:** CON-002 v1.4.0 (extended project creation, repoProvider field)

---

## ⚠️ Mandatory Compliance — Every Task

| Governance Doc | Sprint Requirement |
|:---------------|:-------------------|
| **GOV-001** | XML doc comments (C#), JSDoc (TS) on all public/exported members |
| **GOV-002** | All new code must have tests |
| **GOV-003** | C# coding standards; TS strict mode, no `any` types |
| **GOV-004** | Error middleware for API; error/loading states for frontend |
| **GOV-005** | Branch: `feature/SPR-005-backend` (A), `feature/SPR-005-frontend-tests` (B). Commits: `feat(SPR-005): T-XXX description` |
| **GOV-006** | Structured `ILogger` logging on all new services and controllers |
| **GOV-008** | All infrastructure per GOV-008 |

---

## ⚠️ Critical Process Rules (read FIRST)

1. **Use file-redirect workflow for all heavy commands** — `dotnet build`, `dotnet test`, `npm run build` MUST redirect output to `/tmp/` and read with `tail`/`grep`. Do NOT use pipe chains. See `.agent/workflows/safe_commands.md` Rule 9.
2. **Test factory auth** — `StewieWebApplicationFactory` auto-seeds a test user. Use `factory.GetAuthToken()` for all authenticated requests. Credentials: `admin` / `Admin@Stewie123!`.

---

## Parallel Execution Plan

```
Agent A (Backend)                    Agent B (Frontend + Tests)
─────────────────                    ─────────────────────────
T-048: IGitPlatformService refactor  T-053: Project creation form
T-049: Project entity + migration   T-054: Integration tests (projects)
T-050: ProjectsController create     T-055: Unit tests (timeout + retry)
T-051: Container timeout
T-052: Retry + error taxonomy
        │                                    │
        ▼                                    ▼
   Merge A first                    Merge B second (rebase on A)
```

---

## Dev Agent A Tasks (Backend)

> **Branch:** `feature/SPR-005-backend`
> **File territory:** `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, `src/Stewie.Api/`

### T-048: Rename IGitHubService → IGitPlatformService
- **Dependencies:** None (prerequisite for all other tasks)
- **Contracts:** Internal refactor — no API surface change
- **Deliverable:**
  - Rename `IGitHubService` → `IGitPlatformService` in `src/Stewie.Application/Interfaces/`
  - Rename file: `IGitHubService.cs` → `IGitPlatformService.cs`
  - Rename DTO: `GitHubUserInfo` → `PlatformUserInfo`
  - Add read-only property: `string Provider { get; }` to the interface
  - Update `GitHubService` to implement `IGitPlatformService`:
    - Add `public string Provider => "github";`
    - Return `PlatformUserInfo` instead of `GitHubUserInfo`
    - Class name stays `GitHubService` — it IS the GitHub implementation
  - Update all consumers:
    - `RunOrchestrationService`: field + constructor param
    - `Program.cs`: DI registration `AddScoped<IGitPlatformService, GitHubService>()`
    - `UsersController` (if it uses `IGitHubService` for token validation)
    - `RunOrchestrationServiceTests`: mock type
  - Delete old `IGitHubService.cs` file
- **Acceptance criteria:**
  - `dotnet build src/Stewie.slnx` succeeds with 0 errors
  - `dotnet test` passes (all 40 existing tests)
  - No references to `IGitHubService` remain in codebase
- **Status:** [ ] Not Started

### T-049: Project Entity + RepoProvider Migration
- **Dependencies:** None (can be done in parallel with T-048)
- **Contracts:** CON-002 §5.1
- **Deliverable:**
  - Add `RepoProvider` (string?, nullable) field to `Project` entity
  - FluentMigrator migration (next sequence number) adding `RepoProvider` column to Projects table
  - NHibernate mapping update for `Project` to include `RepoProvider`
  - Update all Project response DTOs (in `ProjectsController`) to include `repoProvider`
- **Acceptance criteria:**
  - Migration runs on startup without errors
  - `GET /api/projects` response includes `repoProvider` field (null for existing projects)
  - Build succeeds
- **Status:** [ ] Not Started

### T-050: ProjectsController — Link or Create Repo Flow
- **Dependencies:** T-048 (IGitPlatformService), T-049 (RepoProvider field)
- **Contracts:** CON-002 §4.1 (updated v1.4.0)
- **Deliverable:**
  - Inject `IGitPlatformService`, `IUserCredentialRepository`, `IEncryptionService` into `ProjectsController`
  - Extend `CreateProjectRequest` with: `CreateRepo` (bool, default false), `RepoName` (string?), `IsPrivate` (bool, default true), `Description` (string?)
  - **Link mode** (`createRepo == false`, default):
    - Existing behavior — `repoUrl` required, persisted as-is
    - Auto-detect `repoProvider` from URL (parse "github.com" → "github", "gitlab.com" → "gitlab", etc.)
  - **Create mode** (`createRepo == true`):
    - Validate `repoName` is non-empty (400 if missing)
    - Validate `repoUrl` is null/absent (400 if provided — conflicting)
    - Look up current user's PAT from `IUserCredentialRepository` via JWT claim → `GetByUserAndProviderAsync(userId, "github")`
    - Fast-fail with 400 if no PAT: `"GitHub PAT not configured. Visit Settings to connect your GitHub account."`
    - Decrypt PAT via `IEncryptionService`
    - Create project entity in DB first with `repoUrl = null` (Option A — rollback on failure)
    - Call `IGitPlatformService.CreateRepositoryAsync(repoName, description, isPrivate, pat)` → returns clone URL
    - Update project's `repoUrl` with returned URL
    - Set `repoProvider` to `IGitPlatformService.Provider` (currently `"github"`)
    - If repo creation fails: delete the project from DB, return 500 with error details
  - Update response DTO to include `repoProvider`
- **Acceptance criteria:**
  - `POST /api/projects { name, repoUrl }` → 201 (backward compatible, link mode)
  - `POST /api/projects { name, createRepo: true, repoName: "new-repo" }` → 201 with GitHub-created repoUrl
  - `POST /api/projects { name, createRepo: true, repoName: "x" }` without PAT → 400
  - `POST /api/projects { name, createRepo: true, repoUrl: "..." }` → 400 (conflicting)
  - Build succeeds
- **Status:** [ ] Not Started

### T-051: Container Timeout Enforcement
- **Dependencies:** None
- **Contracts:** CON-001 §7 (300s timeout — "not yet enforced")
- **Deliverable:**
  - Add `CancellationToken` parameter to `IContainerService.LaunchWorkerAsync` overloads
  - In `DockerContainerService` implementation:
    - Create `CancellationTokenSource` with 300-second timeout (configurable via `Stewie:TaskTimeoutSeconds` setting)
    - On timeout: force-kill the container via `docker kill`
    - Return exit code `124` for timeout (Unix `timeout` convention)
    - Log timeout with structured fields: `{TaskId, TimeoutSeconds, ContainerId}`
  - Update `RunOrchestrationService` to pass `CancellationToken` to container launch
  - Add `Stewie:TaskTimeoutSeconds` config key (default: 300)
- **Acceptance criteria:**
  - Container running >300s is killed and returns exit code 124
  - Exit code 124 is reported as "Task timed out after 300s"
  - Configurable via appsettings
  - Build succeeds
- **Status:** [ ] Not Started

### T-052: Retry Logic + Error Taxonomy
- **Dependencies:** T-051 (timeout exit code)
- **Contracts:** GOV-004 (error handling)
- **Deliverable:**
  - Define `TaskFailureReason` enum in `Stewie.Domain.Enums`:
    - `WorkerCrash` — non-zero exit code (not timeout)
    - `Timeout` — exit code 124
    - `ContainerError` — Docker daemon error (image not found, socket error)
    - `ResultMissing` — exit 0 but no result.json
    - `ResultInvalid` — result.json exists but fails deserialization
    - `WorkerReportedFailure` — result.json with status != "success"
  - Add `FailureReason` (string?, nullable) field to `WorkTask` entity + migration + mapping
  - In `RunOrchestrationService.ExecuteRunAsync`:
    - Classify failures into taxonomy categories
    - Set `task.FailureReason` to the enum name
    - **Retry logic:** If failure is `Timeout` or `ContainerError` (transient):
      - Retry once with the same parameters
      - Log: "Retrying task {TaskId} due to transient failure: {Reason} (attempt 2/2)"
      - If retry also fails: mark as failed with both failure reasons in event payload
    - If failure is permanent (`WorkerCrash`, `ResultMissing`, `ResultInvalid`, `WorkerReportedFailure`): no retry
  - Include `failureReason` in Event payload for failed tasks
- **Acceptance criteria:**
  - Transient failures (timeout, container error) trigger exactly one retry
  - Permanent failures do not trigger retry
  - `failureReason` is set on failed tasks
  - Event payloads include failure taxonomy
  - Build succeeds
- **Status:** [ ] Not Started

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/SPR-005-frontend-tests`
> **File territory:** `src/Stewie.Web/ClientApp/`, `src/Stewie.Tests/`

### T-053: Project Creation Form — Link or Create Toggle
- **Dependencies:** None (build against CON-002 §4.1 v1.4.0 contract)
- **Contracts:** CON-002 §4.1
- **Deliverable:**
  - Update the existing "Create Project" form (or dialog) with a toggle:
    - **"Link Existing Repository"** (default) — shows `repoUrl` text input (current behavior)
    - **"Create New Repository"** — shows:
      - `repoName` text input (required)
      - `description` text area (optional)
      - `isPrivate` checkbox/toggle (default: checked = private)
  - Conditional validation:
    - Link mode: `name` + `repoUrl` required
    - Create mode: `name` + `repoName` required
  - Update `api/client.ts`:
    - Modify `createProject()` to accept extended request body
  - Update `types/index.ts`:
    - Add `repoProvider` to `Project` type
  - Show `repoProvider` badge on project list/detail (e.g., GitHub logo for "github")
  - Handle 400 error for missing PAT: display friendly message
  - Stewie branding: use primary green (#6fac50) for toggle, consistent with existing UI
- **Acceptance criteria:**
  - Toggle switches between link/create modes
  - Form validates correctly in both modes
  - API client sends correct payloads
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-054: Integration Tests — Project Creation Flows
- **Dependencies:** None (tests define expected behavior)
- **Contracts:** CON-002 §4.1, GOV-002
- **Deliverable:**
  - `Stewie.Tests/Integration/ProjectCreationTests.cs`
  - Test cases:
    - `POST /api/projects { name, repoUrl }` → 201 (link mode, backward compatible)
    - `POST /api/projects { name }` missing repoUrl and createRepo false → 400
    - `POST /api/projects { name, createRepo: true, repoName }` without PAT → 400 with clear message
    - `POST /api/projects { name, createRepo: true }` missing repoName → 400
    - `POST /api/projects { name, createRepo: true, repoUrl: "..." }` conflicting → 400
    - Response includes `repoProvider` field
  - Reuse `StewieWebApplicationFactory` with `GetAuthToken()`
  - Note: tests for `createRepo: true` with a PAT will need a mock `IGitPlatformService` — register a mock in the test factory that returns a fake URL
- **Acceptance criteria:**
  - All integration tests pass
  - Build succeeds
- **Status:** [ ] Not Started

### T-055: Unit Tests — Timeout + Retry
- **Dependencies:** None
- **Contracts:** GOV-002
- **Deliverable:**
  - `Stewie.Tests/Services/ContainerTimeoutTests.cs` (or extend existing):
    - `LaunchWorkerAsync` with timeout → returns 124
    - `LaunchWorkerAsync` under timeout → returns actual exit code
  - `Stewie.Tests/Services/RetryLogicTests.cs` (or extend `RunOrchestrationServiceTests.cs`):
    - Transient failure (exit code 124) → retry once → success → task marked Completed
    - Transient failure → retry → second failure → task marked Failed with reason
    - Permanent failure (exit code 1, valid result.json with status=failure) → no retry → task marked Failed
    - Verify `FailureReason` is set correctly on task entity
  - Use NSubstitute mocks for `IContainerService`
- **Acceptance criteria:**
  - All unit tests pass
  - Build succeeds
- **Status:** [ ] Not Started

---

## Sprint Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-048 | A | [x] | IGitPlatformService refactor |
| T-049 | A | [x] | Project entity + RepoProvider migration |
| T-050 | A | [x] | ProjectsController link-or-create |
| T-051 | A | [x] | Container timeout enforcement |
| T-052 | A | [x] | Retry logic + error taxonomy |
| T-053 | B | [x] | Project creation form (frontend) |
| T-054 | B | [x] | Integration tests (project creation) |
| T-055 | B | [x] | Unit tests (timeout + retry) |

---

## Merge Strategy

1. **Agent A completes** → Architect audits → merge `feature/SPR-005-backend` to `main`
2. **Agent B completes** → rebase `feature/SPR-005-frontend-tests` onto updated `main` → Architect audits → merge
3. E2E: Create project (link mode) → verify backward compatible
4. E2E: Create project (create mode with PAT) → verify GitHub repo created, repoUrl populated
5. E2E: Create run → verify timeout enforced, retry on transient failure

---

## Configuration Requirements

New/updated configuration for Sprint 005:

| Variable | Description | Default (dev) |
|:---------|:------------|:--------------|
| `Stewie:TaskTimeoutSeconds` | Hard timeout for container execution | `300` |

> All existing SPR-004 env vars (`STEWIE_JWT_SECRET`, `STEWIE_ENCRYPTION_KEY`, `STEWIE_ADMIN_PASSWORD`, `STEWIE_ADMIN_USERNAME`) remain unchanged.

---

## Sprint Completion Criteria

- [ ] All 8 tasks pass acceptance criteria
- [ ] `dotnet build src/Stewie.slnx` succeeds with 0 errors
- [ ] `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` passes all tests (existing 40 + new)
- [ ] `npm run build` succeeds in `src/Stewie.Web/ClientApp/`
- [ ] No references to `IGitHubService` remain in codebase
- [ ] Link-mode project creation is backward compatible
- [ ] Create-mode project creation provisions GitHub repo
- [ ] Container timeout enforced at 300s
- [ ] Transient failures trigger exactly one retry
- [ ] No open `DEF-` reports against this sprint

---

## Audit Notes (Architect)

### Combined Audit (2026-04-09)
- **Audit report:** `40_VERIFICATION/VER-006_SPR-005_Audit.md`
- Build: ✅ API 0 errors, frontend 57 modules, 54/54 tests pass (3 skipped)
- Merge fix: Applied IGitHubService → IGitPlatformService rename in Agent B's test files (parallel-agent merge artifact)
- Governance: ✅ All GOV docs compliant
- Contract: ✅ CON-002 v1.4.0, CON-001 §7 timeout enforced
- **Verdict:** PASS

**Sprint Verdict:** CLOSED ✅
**Phase 3:** COMPLETE ✅
**Deploy approved:** YES
