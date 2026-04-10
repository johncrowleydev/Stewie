---
id: JOB-009
title: "Job 009 — Task DAG Infrastructure"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-4, dag]
related: [PRJ-001, CON-001, CON-002, BLU-001, JOB-007]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Extend the Stewie data model for task dependency graphs. Add `TaskDependency` entity, `Blocked`/`Cancelled` WorkTask states, `PartiallyCompleted` Job state, and a `TaskGraph` service that performs topological sort, cycle detection, and readiness evaluation. No execution changes — existing 1-task jobs continue identically.

# Job 009 — Task DAG Infrastructure

---

## 1. Context

Phase 3 introduced sequential task chains (dev → tester → retry). Phase 4 generalizes this into a full task dependency DAG where N tasks can execute in parallel, with explicit dependencies controlling execution order.

JOB-009 is **purely additive** — it builds the graph infrastructure without changing any execution behavior. The scheduler (JOB-010) will use these primitives.

**Current state:** 1 Job = 1 developer task + 0-N governance tester tasks (linear chain).

**Target state:** 1 Job = N tasks with directed acyclic dependency graph.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Domain entities, migrations, repositories, `TaskGraph` service | `feature/JOB-009-dag-infra` |
| Dev B | Unit tests for `TaskGraph`, integration tests for new entities | `feature/JOB-009-dag-infra` |

**Single branch, granular commits per task. Merge order: Dev A first, Dev B second.**

---

## 3. Tasks

### Dev A — Backend Infrastructure

#### T-081: TaskDependency Entity + Migration

**Create** `Stewie.Domain/Entities/TaskDependency.cs`:
```csharp
public class TaskDependency
{
    public virtual Guid Id { get; set; }
    public virtual Guid TaskId { get; set; }
    public virtual Guid DependsOnTaskId { get; set; }
    public virtual DateTime CreatedAt { get; set; }
}
```

**Create** `Stewie.Infrastructure/Mappings/TaskDependencyMap.cs`.

**Create** `Stewie.Infrastructure/Migrations/Migration_015_CreateTaskDependencies.cs`:
- Table: `TaskDependencies`
- Columns: `Id` (PK), `TaskId` (FK → WorkTasks), `DependsOnTaskId` (FK → WorkTasks), `CreatedAt`
- Unique constraint on `(TaskId, DependsOnTaskId)` to prevent duplicate edges

**Acceptance criteria:**
- [x] Entity compiles
- [x] Migration runs cleanly on existing DB
- [x] SchemaExport works for SQLite test factory

---

#### T-082: TaskDependency Repository

**Create** `Stewie.Application/Interfaces/ITaskDependencyRepository.cs`:
```csharp
public interface ITaskDependencyRepository
{
    Task SaveAsync(TaskDependency dependency);
    Task<IList<TaskDependency>> GetByJobIdAsync(Guid jobId);
    Task<IList<TaskDependency>> GetByTaskIdAsync(Guid taskId);
    Task DeleteByJobIdAsync(Guid jobId);
}
```

**Create** `Stewie.Infrastructure/Repositories/TaskDependencyRepository.cs`.

**Register** in `Program.cs` DI container.

**Acceptance criteria:**
- [x] CRUD operations work against SQLite
- [x] `GetByJobIdAsync` returns all deps for a job's tasks

---

#### T-083: WorkTask — DependsOn Field

**No migration needed** — dependencies are modeled in the `TaskDependencies` junction table (T-081). The `TaskGraph` service joins tasks and dependencies at runtime.

**No changes to WorkTask entity.**

> Note: This task was originally planned as a JSON field on WorkTask. Using the junction table (T-081) is cleaner and more queryable. This task is effectively absorbed by T-081.

---

#### T-084: TaskGraph — Topological Sort + Readiness

**Create** `Stewie.Application/Services/TaskGraph.cs`:

```csharp
/// <summary>
/// Evaluates a directed acyclic graph of WorkTasks and their dependencies.
/// Provides topological ordering, readiness evaluation, and aggregate status.
/// </summary>
public class TaskGraph
{
    /// <summary>Build graph from tasks and their dependencies.</summary>
    public static TaskGraph Build(IList<WorkTask> tasks, IList<TaskDependency> deps);

    /// <summary>Get tasks whose dependencies are all Completed and which are Pending/Blocked.</summary>
    public IReadOnlyList<WorkTask> GetReadyTasks();

    /// <summary>Return tasks in topological order (dependencies first).</summary>
    public IReadOnlyList<WorkTask> GetTopologicalOrder();

    /// <summary>True if all tasks are in a terminal state (Completed, Failed, Cancelled).</summary>
    public bool IsComplete { get; }

    /// <summary>Aggregate status from constituent tasks.</summary>
    public JobStatus GetAggregateStatus();
}
```

**Aggregate status logic:**
- All `Completed` → `JobStatus.Completed`
- Any `Failed` + remaining `Blocked`/`Cancelled` → `JobStatus.PartiallyCompleted`
- Any `Running` or `Pending` → `JobStatus.Running`
- All `Failed` → `JobStatus.Failed`

**Acceptance criteria:**
- [x] Linear chain (A→B→C) produces correct topological order
- [x] Diamond DAG (A→B, A→C, B→D, C→D) produces valid topological order
- [x] `GetReadyTasks()` returns only tasks whose deps are all Completed
- [x] Aggregate status computed correctly for all scenarios

---

#### T-085: TaskGraph — Cycle Detection

**Add to** `TaskGraph.cs`:

```csharp
/// <summary>
/// Validates the graph is acyclic. Throws InvalidOperationException if a cycle is detected.
/// Call this during job creation validation.
/// </summary>
public void ValidateAcyclic();
```

Uses Kahn's algorithm (already needed for topological sort) — if not all nodes are visited, a cycle exists.

**Acceptance criteria:**
- [x] Throws on A→B→A cycle
- [x] Throws on A→B→C→A cycle
- [x] Passes on valid DAGs (chain, diamond, forest)

---

#### T-086: WorkTaskStatus — Add Blocked + Cancelled

**Modify** `Stewie.Domain/Enums/WorkTaskStatus.cs`:
```csharp
public enum WorkTaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Blocked = 4,
    Cancelled = 5
}
```

No migration needed — stored as int, new values fit existing column.

**Acceptance criteria:**
- [x] Existing tests pass (no behavior change)
- [x] New values serialize/deserialize correctly via NHibernate

---

#### T-087: JobStatus — Add PartiallyCompleted

**Modify** `Stewie.Domain/Enums/JobStatus.cs`:
```csharp
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    PartiallyCompleted = 4
}
```

No migration needed — stored as int.

**Acceptance criteria:**
- [x] Existing tests pass (no behavior change)
- [x] API serializes `PartiallyCompleted` correctly

---

### Dev B — Tests

#### T-088: Unit Tests for TaskGraph

**Create** `Stewie.Tests/Services/TaskGraphTests.cs`:

| Test | Description |
|:-----|:------------|
| `SingleTask_NoDepencies_IsReady` | One task, no deps → ready immediately |
| `LinearChain_TopologicalOrder` | A→B→C → order is [A, B, C] |
| `Diamond_TopologicalOrder` | A→B, A→C, B→D, C→D → valid ordering |
| `GetReadyTasks_OnlyPendingWithCompletedDeps` | After A completes, B (depends on A) becomes ready |
| `GetReadyTasks_BlockedStaysBlocked` | B stays blocked while A is Running |
| `IsComplete_AllTerminal` | True when all tasks are Completed/Failed/Cancelled |
| `IsComplete_HasRunning_False` | False when any task is Running |
| `AggregateStatus_AllCompleted` | Returns Completed |
| `AggregateStatus_MixedFailBlocked` | Returns PartiallyCompleted |
| `AggregateStatus_AllFailed` | Returns Failed |
| `Forest_IndependentTasks_AllReady` | 3 tasks with no deps → all ready |

---

#### T-089: Unit Tests for Cycle Detection

**Add to** `TaskGraphTests.cs`:

| Test | Description |
|:-----|:------------|
| `CycleDetection_DirectCycle_Throws` | A→B→A throws |
| `CycleDetection_IndirectCycle_Throws` | A→B→C→A throws |
| `CycleDetection_SelfCycle_Throws` | A→A throws |
| `CycleDetection_ValidDag_NoThrow` | Diamond DAG does not throw |
| `CycleDetection_Forest_NoThrow` | Disconnected tasks do not throw |

---

## 4. Contracts Affected

**None in this job.** JOB-009 is purely internal infrastructure. Contract changes (CON-001 v1.5.0, CON-002 v1.7.0) happen in JOB-010.

---

## 5. Verification

```bash
# Build
dotnet build src/Stewie.Domain/Stewie.Domain.csproj
dotnet build src/Stewie.Application/Stewie.Application.csproj
dotnet build src/Stewie.Infrastructure/Stewie.Infrastructure.csproj

# Full test suite (existing + new)
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# TaskGraph-specific tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --filter "FullyQualifiedName~TaskGraph"
```

**Exit criteria:**
- All existing 76 tests pass (no regressions)
- All new TaskGraph tests pass
- Build succeeds with no warnings
- No migration conflicts

---

## 6. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-009 created for Phase 4 Task DAG Infrastructure |
