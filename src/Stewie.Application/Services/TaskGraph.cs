/// <summary>
/// Task dependency graph evaluator — topological sort, readiness, cycle detection.
/// Uses Kahn's algorithm for both topological ordering and cycle validation.
/// REF: JOB-009 T-084, T-085
/// </summary>
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Services;

/// <summary>
/// Evaluates a directed acyclic graph of WorkTasks and their dependencies.
/// Provides topological ordering, readiness evaluation, cycle detection,
/// and aggregate status computation.
/// </summary>
public class TaskGraph
{
    private readonly IReadOnlyDictionary<Guid, WorkTask> _taskMap;
    private readonly IReadOnlyDictionary<Guid, List<Guid>> _dependenciesOf; // taskId → list of upstream taskIds
    private readonly IReadOnlyDictionary<Guid, List<Guid>> _dependentsOf;  // taskId → list of downstream taskIds
    private readonly IReadOnlyList<WorkTask> _topologicalOrder;

    private TaskGraph(
        IReadOnlyDictionary<Guid, WorkTask> taskMap,
        IReadOnlyDictionary<Guid, List<Guid>> dependenciesOf,
        IReadOnlyDictionary<Guid, List<Guid>> dependentsOf,
        IReadOnlyList<WorkTask> topologicalOrder)
    {
        _taskMap = taskMap;
        _dependenciesOf = dependenciesOf;
        _dependentsOf = dependentsOf;
        _topologicalOrder = topologicalOrder;
    }

    /// <summary>
    /// Build graph from tasks and their dependencies.
    /// Does NOT validate acyclicity — call <see cref="ValidateAcyclic"/> explicitly
    /// during job creation validation.
    /// </summary>
    /// <param name="tasks">All tasks in the job.</param>
    /// <param name="deps">All dependency edges for the job's tasks.</param>
    /// <returns>A constructed TaskGraph ready for queries.</returns>
    /// <exception cref="ArgumentException">Thrown if tasks list is empty.</exception>
    public static TaskGraph Build(IList<WorkTask> tasks, IList<TaskDependency> deps)
    {
        if (tasks == null || tasks.Count == 0)
        {
            throw new ArgumentException("Tasks list cannot be null or empty.", nameof(tasks));
        }

        var taskMap = tasks.ToDictionary(t => t.Id);

        // Build adjacency lists
        var dependenciesOf = new Dictionary<Guid, List<Guid>>();
        var dependentsOf = new Dictionary<Guid, List<Guid>>();

        foreach (var task in tasks)
        {
            dependenciesOf[task.Id] = new List<Guid>();
            dependentsOf[task.Id] = new List<Guid>();
        }

        foreach (var dep in deps)
        {
            // Only include edges where both tasks exist in our task set
            if (taskMap.ContainsKey(dep.TaskId) && taskMap.ContainsKey(dep.DependsOnTaskId))
            {
                dependenciesOf[dep.TaskId].Add(dep.DependsOnTaskId);
                dependentsOf[dep.DependsOnTaskId].Add(dep.TaskId);
            }
        }

        // Compute topological order via Kahn's algorithm
        var topOrder = KahnTopologicalSort(tasks, dependenciesOf);

        return new TaskGraph(taskMap, dependenciesOf, dependentsOf, topOrder);
    }

    /// <summary>
    /// Get tasks whose dependencies are all Completed and which are currently Pending or Blocked.
    /// These are the tasks ready to be dispatched to workers.
    /// </summary>
    /// <returns>Tasks ready for execution.</returns>
    public IReadOnlyList<WorkTask> GetReadyTasks()
    {
        var ready = new List<WorkTask>();

        foreach (var task in _taskMap.Values)
        {
            // Only Pending or Blocked tasks can become ready
            if (task.Status != WorkTaskStatus.Pending && task.Status != WorkTaskStatus.Blocked)
            {
                continue;
            }

            // Check if all upstream dependencies are Completed
            var deps = _dependenciesOf[task.Id];
            if (deps.Count == 0 || deps.All(depId => _taskMap[depId].Status == WorkTaskStatus.Completed))
            {
                ready.Add(task);
            }
        }

        return ready;
    }

    /// <summary>
    /// Return tasks in topological order (dependencies first).
    /// If the graph has a cycle, this will return a partial order
    /// containing only the non-cyclic portion.
    /// </summary>
    /// <returns>Tasks ordered so that each task appears after all its dependencies.</returns>
    public IReadOnlyList<WorkTask> GetTopologicalOrder()
    {
        return _topologicalOrder;
    }

    /// <summary>
    /// True if all tasks are in a terminal state (Completed, Failed, Cancelled).
    /// </summary>
    public bool IsComplete
    {
        get
        {
            return _taskMap.Values.All(t =>
                t.Status == WorkTaskStatus.Completed ||
                t.Status == WorkTaskStatus.Failed ||
                t.Status == WorkTaskStatus.Cancelled);
        }
    }

    /// <summary>
    /// Aggregate status from constituent tasks.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item>All Completed → Completed</item>
    /// <item>All Failed → Failed</item>
    /// <item>Any Failed + remaining Blocked/Cancelled → PartiallyCompleted</item>
    /// <item>Any Running or Pending → Running</item>
    /// </list>
    /// </returns>
    public JobStatus GetAggregateStatus()
    {
        var statuses = _taskMap.Values.Select(t => t.Status).ToList();

        // If any task is Running or Pending, the job is still running
        if (statuses.Any(s => s == WorkTaskStatus.Running || s == WorkTaskStatus.Pending))
        {
            return JobStatus.Running;
        }

        // All tasks must be in terminal states below this point
        if (statuses.All(s => s == WorkTaskStatus.Completed))
        {
            return JobStatus.Completed;
        }

        if (statuses.All(s => s == WorkTaskStatus.Failed))
        {
            return JobStatus.Failed;
        }

        // Mix of Failed/Cancelled/Completed/Blocked — partial success
        return JobStatus.PartiallyCompleted;
    }

    /// <summary>
    /// Validates the graph is acyclic. Throws InvalidOperationException if a cycle is detected.
    /// Call this during job creation validation.
    /// Uses Kahn's algorithm — if not all nodes are visited, a cycle exists.
    /// REF: JOB-009 T-085
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a cycle is detected in the dependency graph.</exception>
    public void ValidateAcyclic()
    {
        if (_topologicalOrder.Count < _taskMap.Count)
        {
            // Find tasks involved in the cycle for diagnostic purposes
            var visited = new HashSet<Guid>(_topologicalOrder.Select(t => t.Id));
            var cycleTasks = _taskMap.Keys.Where(id => !visited.Contains(id)).ToList();
            throw new InvalidOperationException(
                $"Cycle detected in task dependency graph. Tasks involved: [{string.Join(", ", cycleTasks)}]");
        }
    }

    /// <summary>
    /// Returns all direct downstream dependents of the given task.
    /// Used for failure cascade — when a task fails, its dependents are cancelled.
    /// REF: JOB-010 T-094
    /// </summary>
    /// <param name="taskId">The upstream task ID.</param>
    /// <returns>Tasks that directly depend on the given task.</returns>
    public IReadOnlyList<WorkTask> GetDependentsOf(Guid taskId)
    {
        if (!_dependentsOf.TryGetValue(taskId, out var dependentIds))
        {
            return Array.Empty<WorkTask>();
        }

        return dependentIds
            .Where(id => _taskMap.ContainsKey(id))
            .Select(id => _taskMap[id])
            .ToList();
    }

    /// <summary>
    /// Returns all transitive downstream dependents of the given task (recursive).
    /// Used for failure cascade — all tasks downstream of a failure are cancelled.
    /// REF: JOB-010 T-094
    /// </summary>
    /// <param name="taskId">The upstream task ID.</param>
    /// <returns>All tasks transitively dependent on the given task.</returns>
    public IReadOnlyList<WorkTask> GetAllDownstream(Guid taskId)
    {
        var result = new List<WorkTask>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(taskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_dependentsOf.TryGetValue(current, out var dependentIds))
            {
                continue;
            }

            foreach (var depId in dependentIds)
            {
                if (visited.Add(depId) && _taskMap.TryGetValue(depId, out var task))
                {
                    result.Add(task);
                    queue.Enqueue(depId);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Kahn's algorithm for topological sorting.
    /// Returns as many nodes as possible in topological order.
    /// If the graph has cycles, nodes in cycles will be excluded from the result.
    /// </summary>
    private static IReadOnlyList<WorkTask> KahnTopologicalSort(
        IList<WorkTask> tasks,
        Dictionary<Guid, List<Guid>> dependenciesOf)
    {
        // Compute in-degree for each node
        var inDegree = new Dictionary<Guid, int>();
        foreach (var task in tasks)
        {
            inDegree[task.Id] = dependenciesOf[task.Id].Count;
        }

        // Seed queue with zero-dependency tasks (sorted by Id for deterministic ordering)
        var queue = new Queue<Guid>();
        foreach (var id in inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(id => id))
        {
            queue.Enqueue(id);
        }

        var taskMap = tasks.ToDictionary(t => t.Id);
        var result = new List<WorkTask>();

        // Build reverse adjacency for decrementing in-degree
        var dependentsOf = new Dictionary<Guid, List<Guid>>();
        foreach (var task in tasks)
        {
            dependentsOf[task.Id] = new List<Guid>();
        }
        foreach (var task in tasks)
        {
            foreach (var depId in dependenciesOf[task.Id])
            {
                if (dependentsOf.ContainsKey(depId))
                {
                    dependentsOf[depId].Add(task.Id);
                }
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(taskMap[current]);

            // For each downstream task, decrement in-degree
            foreach (var downstream in dependentsOf[current].OrderBy(id => id))
            {
                inDegree[downstream]--;
                if (inDegree[downstream] == 0)
                {
                    queue.Enqueue(downstream);
                }
            }
        }

        return result;
    }
}
