/// <summary>
/// Unit tests for <see cref="TaskGraph"/> — DAG infrastructure (JOB-009 T-088 + T-089).
///
/// Covers:
///   T-088: Topological sort, readiness evaluation, IsComplete, aggregate status, forest graphs
///   T-089: Cycle detection for direct, indirect, self-referencing, valid DAG, and forest graphs
///
/// NOTE: These tests are written against the TaskGraph API contract defined in JOB-009 §T-084/T-085.
/// Dev A implements TaskGraph, TaskDependency entity, and the new enum values.
/// Until Dev A's code is merged, this file will not compile — this is expected per the
/// two-agent parallel workflow (Dev A first, Dev B rebases).
///
/// REF: GOV-002, JOB-009 T-088, T-089
/// </summary>
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Validates TaskGraph behavior: topological ordering, readiness evaluation,
/// completion detection, aggregate status computation, and cycle detection.
/// Uses plain WorkTask/TaskDependency instances (no mocks needed — pure logic).
/// </summary>
public class TaskGraphTests
{
    // ---------------------------------------------------------------
    // Helper: create a WorkTask with minimal fields for graph testing
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a WorkTask with the given ID, job ID, and status.
    /// Only fields relevant to graph evaluation are populated.
    /// </summary>
    private static WorkTask MakeTask(Guid id, Guid jobId, WorkTaskStatus status = WorkTaskStatus.Pending)
    {
        return new WorkTask
        {
            Id = id,
            JobId = jobId,
            Status = status,
            Role = "developer",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a TaskDependency edge: taskId depends on dependsOnTaskId.
    /// </summary>
    private static TaskDependency MakeDep(Guid taskId, Guid dependsOnTaskId)
    {
        return new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ===================================================================
    // T-088: Unit Tests for TaskGraph — topological sort + readiness
    // ===================================================================

    /// <summary>
    /// A single task with no dependencies should be immediately ready.
    /// </summary>
    [Fact]
    public void SingleTask_NoDependencies_IsReady()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var taskA = MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Pending);
        var tasks = new List<WorkTask> { taskA };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var ready = graph.GetReadyTasks();

        // Assert
        Assert.Single(ready);
        Assert.Equal(taskA.Id, ready[0].Id);
    }

    /// <summary>
    /// Linear chain A→B→C should produce topological order [A, B, C].
    /// </summary>
    [Fact]
    public void LinearChain_TopologicalOrder()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        var taskA = MakeTask(idA, jobId);
        var taskB = MakeTask(idB, jobId);
        var taskC = MakeTask(idC, jobId);

        var tasks = new List<WorkTask> { taskA, taskB, taskC };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA), // B depends on A
            MakeDep(idC, idB)  // C depends on B
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var order = graph.GetTopologicalOrder();

        // Assert — A must come before B, B must come before C
        var indexA = IndexOf(order, idA);
        var indexB = IndexOf(order, idB);
        var indexC = IndexOf(order, idC);

        Assert.True(indexA < indexB, "A must appear before B in topological order");
        Assert.True(indexB < indexC, "B must appear before C in topological order");
        Assert.Equal(3, order.Count);
    }

    /// <summary>
    /// Diamond DAG: A→B, A→C, B→D, C→D should produce a valid topological ordering
    /// where A comes first and D comes last.
    /// </summary>
    [Fact]
    public void Diamond_TopologicalOrder()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();
        var idD = Guid.NewGuid();

        var tasks = new List<WorkTask>
        {
            MakeTask(idA, jobId),
            MakeTask(idB, jobId),
            MakeTask(idC, jobId),
            MakeTask(idD, jobId)
        };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA), // B depends on A
            MakeDep(idC, idA), // C depends on A
            MakeDep(idD, idB), // D depends on B
            MakeDep(idD, idC)  // D depends on C
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var order = graph.GetTopologicalOrder();

        // Assert — A must be first, D must be last, B and C in between
        Assert.Equal(4, order.Count);
        Assert.Equal(idA, order[0].Id); // A has no deps, must be first
        Assert.Equal(idD, order[3].Id); // D depends on both B and C, must be last

        // B and C can be in either order (both only depend on A)
        var middleIds = new[] { order[1].Id, order[2].Id };
        Assert.Contains(idB, middleIds);
        Assert.Contains(idC, middleIds);
    }

    /// <summary>
    /// After A completes, B (which depends on A) should become ready.
    /// C (which depends on B) should NOT be ready yet.
    /// </summary>
    [Fact]
    public void GetReadyTasks_OnlyPendingWithCompletedDeps()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        var taskA = MakeTask(idA, jobId, WorkTaskStatus.Completed);
        var taskB = MakeTask(idB, jobId, WorkTaskStatus.Pending);
        var taskC = MakeTask(idC, jobId, WorkTaskStatus.Pending);

        var tasks = new List<WorkTask> { taskA, taskB, taskC };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA), // B depends on A
            MakeDep(idC, idB)  // C depends on B
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var ready = graph.GetReadyTasks();

        // Assert — only B is ready (A is completed, C is blocked by B)
        Assert.Single(ready);
        Assert.Equal(idB, ready[0].Id);
    }

    /// <summary>
    /// B stays blocked while A is still Running — it should NOT appear in ready tasks.
    /// </summary>
    [Fact]
    public void GetReadyTasks_BlockedStaysBlocked()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var taskA = MakeTask(idA, jobId, WorkTaskStatus.Running);
        var taskB = MakeTask(idB, jobId, WorkTaskStatus.Blocked);

        var tasks = new List<WorkTask> { taskA, taskB };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA) // B depends on A
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var ready = graph.GetReadyTasks();

        // Assert — no tasks are ready (A is Running, B is Blocked)
        Assert.Empty(ready);
    }

    /// <summary>
    /// IsComplete is true when all tasks are in a terminal state (Completed, Failed, Cancelled).
    /// </summary>
    [Fact]
    public void IsComplete_AllTerminal()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Failed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Cancelled)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert
        Assert.True(graph.IsComplete);
    }

    /// <summary>
    /// IsComplete is false when any task is still Running.
    /// </summary>
    [Fact]
    public void IsComplete_HasRunning_False()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Running),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Pending)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert
        Assert.False(graph.IsComplete);
    }

    /// <summary>
    /// Aggregate status is Completed when all tasks are Completed.
    /// </summary>
    [Fact]
    public void AggregateStatus_AllCompleted()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var status = graph.GetAggregateStatus();

        // Assert
        Assert.Equal(JobStatus.Completed, status);
    }

    /// <summary>
    /// Aggregate status is PartiallyCompleted when some tasks Failed and remaining are Blocked/Cancelled.
    /// Per JOB-009: Any Failed + remaining Blocked/Cancelled → PartiallyCompleted.
    /// </summary>
    [Fact]
    public void AggregateStatus_MixedFailBlocked()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Completed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Failed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Blocked),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Cancelled)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var status = graph.GetAggregateStatus();

        // Assert
        Assert.Equal(JobStatus.PartiallyCompleted, status);
    }

    /// <summary>
    /// Aggregate status is Failed when ALL tasks are Failed.
    /// </summary>
    [Fact]
    public void AggregateStatus_AllFailed()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Failed),
            MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Failed)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var status = graph.GetAggregateStatus();

        // Assert
        Assert.Equal(JobStatus.Failed, status);
    }

    /// <summary>
    /// Three independent tasks (no dependencies) should all be ready immediately.
    /// This validates the "forest" case — disconnected nodes in the graph.
    /// </summary>
    [Fact]
    public void Forest_IndependentTasks_AllReady()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var taskA = MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Pending);
        var taskB = MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Pending);
        var taskC = MakeTask(Guid.NewGuid(), jobId, WorkTaskStatus.Pending);

        var tasks = new List<WorkTask> { taskA, taskB, taskC };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);
        var ready = graph.GetReadyTasks();

        // Assert — all three should be ready
        Assert.Equal(3, ready.Count);
        var readyIds = ready.Select(t => t.Id).ToHashSet();
        Assert.Contains(taskA.Id, readyIds);
        Assert.Contains(taskB.Id, readyIds);
        Assert.Contains(taskC.Id, readyIds);
    }

    // ===================================================================
    // T-089: Unit Tests for Cycle Detection
    // ===================================================================

    /// <summary>
    /// A direct cycle A→B→A should throw InvalidOperationException.
    /// </summary>
    [Fact]
    public void CycleDetection_DirectCycle_Throws()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var tasks = new List<WorkTask>
        {
            MakeTask(idA, jobId),
            MakeTask(idB, jobId)
        };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA), // B depends on A
            MakeDep(idA, idB)  // A depends on B → cycle!
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert
        Assert.Throws<InvalidOperationException>(() => graph.ValidateAcyclic());
    }

    /// <summary>
    /// An indirect cycle A→B→C→A should throw InvalidOperationException.
    /// </summary>
    [Fact]
    public void CycleDetection_IndirectCycle_Throws()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        var tasks = new List<WorkTask>
        {
            MakeTask(idA, jobId),
            MakeTask(idB, jobId),
            MakeTask(idC, jobId)
        };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA), // B depends on A
            MakeDep(idC, idB), // C depends on B
            MakeDep(idA, idC)  // A depends on C → cycle!
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert
        Assert.Throws<InvalidOperationException>(() => graph.ValidateAcyclic());
    }

    /// <summary>
    /// A self-referencing cycle A→A should throw InvalidOperationException.
    /// </summary>
    [Fact]
    public void CycleDetection_SelfCycle_Throws()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();

        var tasks = new List<WorkTask>
        {
            MakeTask(idA, jobId)
        };
        var deps = new List<TaskDependency>
        {
            MakeDep(idA, idA) // A depends on itself → cycle!
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert
        Assert.Throws<InvalidOperationException>(() => graph.ValidateAcyclic());
    }

    /// <summary>
    /// A valid diamond DAG should NOT throw on ValidateAcyclic.
    /// </summary>
    [Fact]
    public void CycleDetection_ValidDag_NoThrow()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();
        var idD = Guid.NewGuid();

        var tasks = new List<WorkTask>
        {
            MakeTask(idA, jobId),
            MakeTask(idB, jobId),
            MakeTask(idC, jobId),
            MakeTask(idD, jobId)
        };
        var deps = new List<TaskDependency>
        {
            MakeDep(idB, idA),
            MakeDep(idC, idA),
            MakeDep(idD, idB),
            MakeDep(idD, idC)
        };

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert — should not throw
        var exception = Record.Exception(() => graph.ValidateAcyclic());
        Assert.Null(exception);
    }

    /// <summary>
    /// Disconnected tasks (forest — no edges) should NOT throw on ValidateAcyclic.
    /// </summary>
    [Fact]
    public void CycleDetection_Forest_NoThrow()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tasks = new List<WorkTask>
        {
            MakeTask(Guid.NewGuid(), jobId),
            MakeTask(Guid.NewGuid(), jobId),
            MakeTask(Guid.NewGuid(), jobId)
        };
        var deps = new List<TaskDependency>();

        // Act
        var graph = TaskGraph.Build(tasks, deps);

        // Assert — should not throw
        var exception = Record.Exception(() => graph.ValidateAcyclic());
        Assert.Null(exception);
    }

    // ===================================================================
    // Helper
    // ===================================================================

    /// <summary>
    /// Finds the index of a task with the given ID in the ordered list.
    /// Throws if not found — test will fail with a clear message.
    /// </summary>
    private static int IndexOf(IReadOnlyList<WorkTask> order, Guid taskId)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i].Id == taskId)
                return i;
        }

        throw new InvalidOperationException(
            $"Task {taskId} not found in topological order. " +
            $"Order contains: [{string.Join(", ", order.Select(t => t.Id))}]");
    }
}
