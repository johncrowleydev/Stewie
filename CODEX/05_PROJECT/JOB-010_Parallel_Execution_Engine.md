---
id: JOB-010
title: "Job 010 — Parallel Execution Engine + API"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-4, parallel-execution]
related: [PRJ-001, CON-001, CON-002, BLU-001, JOB-009]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Replace the single-task execution path with a parallel task scheduler that respects DAG dependencies. Update `POST /api/jobs` to accept multi-task definitions with dependency edges. Each developer task gets its own governance cycle. Bump CON-002 to v1.7.0 and CON-001 to v1.5.0.

# Job 010 — Parallel Execution Engine + API

---

## 1. Context

JOB-009 built the graph primitives (TaskDependency entity, TaskGraph service, Blocked/Cancelled states). JOB-010 activates them — replacing the `tasks[0]` single-task path in `JobOrchestrationService.ExecuteJobAsync` with a scheduler loop that evaluates the DAG, launches ready tasks in parallel, runs governance per-task, and computes aggregate job status.

**Current state:** `ExecuteJobAsync` hardcodes `var task = tasks[0]` (line 96 of JobOrchestrationService.cs).

**Target state:** `ExecuteMultiTaskJobAsync` iterates the DAG, launching N tasks concurrently bounded by `SemaphoreSlim`.

> ⚠️ **Risk:** This is the highest-risk job in Phase 4 — it changes core execution behavior. Backward compatibility with single-task `POST /api/jobs` must be preserved.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Execution engine, API controller, workspace service, contract docs | `feature/JOB-010-exec-engine` |
| Dev B | Integration tests, backward compatibility tests | `feature/JOB-010-exec-tests` |

**Separate branches per agent (GOV-005). Merge order: Dev A first, Dev B rebases onto main after Dev A merges.**

---

## 3. Dependencies

- **Requires JOB-009 merged to main** — TaskDependency entity, TaskGraph service, Blocked/Cancelled/PartiallyCompleted enums.
- **ITaskDependencyRepository** DI registration.

---

## 4. Tasks

### Dev A — Execution Engine + API

#### T-090: ExecuteMultiTaskJobAsync Method

**Add to** `JobOrchestrationService.cs`:

```csharp
/// <summary>
/// Executes a multi-task job using the DAG scheduler.
/// Each task gets its own workspace, container launch, and governance cycle.
/// </summary>
public async Task<TestJobResult> ExecuteMultiTaskJobAsync(Guid jobId)
```

**Core logic:**
1. Load job + all tasks + all TaskDependencies for this job
2. Build `TaskGraph` from tasks + deps
3. Validate acyclic (throw if cycle detected)
4. Set tasks with unmet deps to `Blocked`
5. Enter scheduler loop (T-091)
6. Return aggregate result

**Acceptance criteria:**
- [ ] Single-task job (no deps) executes identically to current behavior
- [ ] Multi-task job executes tasks in dependency order
- [ ] `ExecuteJobAsync` delegates to `ExecuteMultiTaskJobAsync` for jobs with >1 task

---

#### T-091: Task Scheduler Loop

**Add to** `JobOrchestrationService.cs`:

```csharp
/// <summary>
/// Scheduler loop: poll ready tasks, launch in parallel, process completions, repeat.
/// Exits when all tasks are terminal (Completed/Failed/Cancelled).
/// </summary>
private async Task ScheduleTasks(Job job, TaskGraph graph, Project? project)
```

**Scheduler pseudocode:**
```
while (!graph.IsComplete)
{
    var ready = graph.GetReadyTasks();
    if (ready.Count == 0 && !graph.IsComplete)
    {
        // Deadlock: all remaining tasks are Blocked but no tasks are Running
        // This shouldn't happen with a valid DAG — fail the job
        break;
    }

    // Launch all ready tasks in parallel
    var launchTasks = ready.Select(t => ExecuteSingleTaskAsync(job, t, project));
    await Task.WhenAll(launchTasks);

    // Re-evaluate graph after batch completes
}
```

**Acceptance criteria:**
- [ ] Linear DAG (A→B→C) executes in correct order
- [ ] Diamond DAG (A→B, A→C, B→D, C→D) launches B and C in parallel after A
- [ ] Loop terminates when all tasks reach terminal state
- [ ] Deadlock detection triggers job failure

---

#### T-092: Parallel Container Launcher (SemaphoreSlim)

**Add to** `JobOrchestrationService.cs`:

```csharp
private readonly SemaphoreSlim _taskSemaphore;

// In constructor:
_taskSemaphore = new SemaphoreSlim(maxConcurrentTasks); // default: 5
```

Wrap each `ExecuteSingleTaskAsync` call in a semaphore acquire/release:

```csharp
await _taskSemaphore.WaitAsync();
try
{
    await ExecuteSingleTaskAsync(job, task, project);
}
finally
{
    _taskSemaphore.Release();
}
```

**Config:** `Stewie:MaxConcurrentTasks` in `appsettings.json` (default: 5).

**Acceptance criteria:**
- [ ] At most N tasks run concurrently (N = MaxConcurrentTasks)
- [ ] Semaphore is released even on task failure
- [ ] Config value read from `IConfiguration`

---

#### T-093: Per-Task Workspace Isolation

**Modify** workspace preparation to support per-task workspaces within a multi-task job:

Each task in a multi-task job gets its own workspace directory:
```
workspaces/{jobId}/{taskId}/
    input/
    output/
    repo/
```

**Changes to** `WorkspaceService`:
- Add `PrepareWorkspaceForTask(WorkTask task, Job job, string? repoUrl, string? branch)` that creates the task-scoped workspace
- Each task clones the repo independently (avoid shared-workspace race conditions)

**Acceptance criteria:**
- [ ] Each task has its own workspace path
- [ ] Parallel tasks do not share filesystem state
- [ ] Single-task jobs continue to work with existing workspace layout

---

#### T-094: Aggregated Job Status Computation

After each task completes (or fails), call `TaskGraph.GetAggregateStatus()` to update the job:

```csharp
// After task completes, re-evaluate job status
var graph = TaskGraph.Build(tasks, deps);
var aggregateStatus = graph.GetAggregateStatus();
job.Status = aggregateStatus;
```

**When a task fails:**
1. Mark downstream tasks as `Cancelled` (they can never execute)
2. If remaining running tasks exist, wait for them to complete
3. Final job status: `PartiallyCompleted` if some succeeded, `Failed` if all failed

**Acceptance criteria:**
- [ ] All tasks succeed → Job `Completed`
- [ ] 2 of 3 succeed, 1 fails → Job `PartiallyCompleted`
- [ ] All tasks fail → Job `Failed`
- [ ] Failed task's downstream deps → `Cancelled`

---

#### T-095: Update `POST /api/jobs` for Multi-Task

**Modify** `JobsController.cs` and `CreateJobRequest`:

**Extended request model:**
```csharp
public class CreateJobRequest
{
    // Legacy single-task fields (backward compatible)
    public Guid ProjectId { get; set; }
    public string? Objective { get; set; }
    public string? Scope { get; set; }
    public List<string>? Script { get; set; }
    public List<string>? AcceptanceCriteria { get; set; }

    // New multi-task fields
    public List<TaskDefinition>? Tasks { get; set; }
}

public class TaskDefinition
{
    public string? ClientId { get; set; }  // Client-generated ID for dep references
    public string Objective { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public List<string>? Script { get; set; }
    public List<string>? AcceptanceCriteria { get; set; }
    public List<string>? DependsOn { get; set; }  // ClientIds of upstream tasks
}
```

**Routing logic:**
- If `request.Tasks` is non-null and non-empty → multi-task flow
- If `request.Objective` is non-null → legacy single-task flow (wrap in 1-task job)
- Both null → 400 validation error

**Acceptance criteria:**
- [ ] Old single-task request format still works
- [ ] Multi-task request creates N WorkTask entities + TaskDependency edges
- [ ] Invalid dependency references → 400 error
- [ ] Cycle in deps → 400 error (validated before saving)

---

#### T-096: CON-002 v1.7.0 — Multi-Task API Schema

**Update** `CON-002_API_Contract.md`:
- Bump version to `1.7.0`
- Add multi-task `POST /api/jobs` request body spec
- Add `taskCount`, `completedTaskCount`, `failedTaskCount` to `GET /api/jobs/{id}` response
- Add `dependsOn` field to Task response schema
- Add `PartiallyCompleted` to Job status enum doc
- Add `Blocked`, `Cancelled` to Task status enum doc

**Acceptance criteria:**
- [ ] Contract accurately reflects the implementation
- [ ] Backward compatibility documented
- [ ] Version bumped

---

#### T-097: CON-001 v1.5.0 — Per-Task Workspace Mount

**Update** `CON-001_Runtime_Contract.md`:
- Bump version to `1.5.0`
- Document per-task workspace isolation within multi-task jobs
- Update filesystem layout section for `workspaces/{jobId}/{taskId}/` pattern

**Acceptance criteria:**
- [ ] New workspace layout documented
- [ ] Backward compatibility with single-task layout noted

---

#### T-101: Per-Task Governance Cycle

**Modify** the governance cycle to run independently per task:

Each developer task in a multi-task job gets its own tester task and governance cycle. The existing `RunGovernanceCycleAsync` method works per-task already — ensure it works correctly when called from the parallel scheduler.

**Key consideration:** Governance retries happen within the task's own lifecycle. If task A's governance fails and retries, it creates a new dev task (retry) that re-enters the scheduler as a new task in the graph.

**Acceptance criteria:**
- [ ] Each dev task gets its own governance tester
- [ ] Governance retry creates a new dev task within the same job
- [ ] Parallel governance cycles do not interfere with each other
- [ ] All governance passes → Job can complete

---

### Dev B — Integration Tests

#### T-098: Integration Tests — 2-Task Parallel Job

**Create** `Stewie.Tests/Integration/MultiTaskJobTests.cs`:

| Test | Description |
|:-----|:------------|
| `TwoParallelTasks_BothComplete` | 2 tasks with no deps → both execute → Job Completed |
| `TwoParallelTasks_OneFailsOneFails` | Job = Failed |
| `TwoParallelTasks_OneFailsOneSucceeds` | Job = PartiallyCompleted |

**Acceptance criteria:**
- [ ] All tests pass
- [ ] Tests use `StewieWebApplicationFactory` with mock container service

---

#### T-099: Integration Tests — 3-Task DAG (A→B, A→C)

| Test | Description |
|:-----|:------------|
| `FanOutDag_ExecutesInOrder` | A first, then B and C in parallel |
| `FanOutDag_AFailsCascade` | A fails → B and C are Cancelled → Job Failed |
| `FanOutDag_BFailsCContinues` | B and C are independent; B fails, C succeeds → PartiallyCompleted |

---

#### T-100: Integration Tests — Dependency Failure Cascade

| Test | Description |
|:-----|:------------|
| `LinearChain_MiddleTaskFails` | A→B→C: B fails → C is Cancelled → PartiallyCompleted |
| `Diamond_ConvergenceFailure` | A→B, A→C, B+C→D: B fails → D cannot execute → PartiallyCompleted |
| `BackwardCompat_SingleTaskJob` | Old POST /api/jobs format with `objective` → works as before |

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Multi-task POST /api/jobs, extended response, new status values | → v1.7.0 |
| CON-001 | Per-task workspace mount documentation | → v1.5.0 |

---

## 6. Verification

```bash
# Build all affected projects
dotnet build src/Stewie.Application/Stewie.Application.csproj
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Full test suite (existing + new)
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Multi-task-specific tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --filter "FullyQualifiedName~MultiTask"
```

**Exit criteria:**
- All existing 76+ tests pass (no regressions)
- All new MultiTaskJob tests pass
- Old single-task `POST /api/jobs` still works (backward compat)
- No migration conflicts
- CON-002 v1.7.0 and CON-001 v1.5.0 accurate

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-010 created for Phase 4 Parallel Execution Engine |
| 2026-04-10 | JOB-010 CLOSED — 101 tests pass (9 new), merged to main |
