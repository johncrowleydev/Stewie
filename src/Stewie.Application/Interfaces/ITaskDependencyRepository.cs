/// <summary>
/// Repository interface for TaskDependency entity persistence.
/// REF: JOB-009 T-082
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines persistence operations for task dependency edges in the DAG.
/// </summary>
public interface ITaskDependencyRepository
{
    /// <summary>Persists a new task dependency edge.</summary>
    /// <param name="dependency">The dependency to save.</param>
    Task SaveAsync(TaskDependency dependency);

    /// <summary>Retrieves all dependency edges for tasks belonging to a specific job.</summary>
    /// <param name="jobId">The job's GUID.</param>
    /// <returns>All TaskDependency records whose TaskId belongs to the specified job.</returns>
    Task<IList<TaskDependency>> GetByJobIdAsync(Guid jobId);

    /// <summary>Retrieves all upstream dependencies for a specific task.</summary>
    /// <param name="taskId">The task's GUID (the downstream task).</param>
    /// <returns>All TaskDependency records where TaskId matches.</returns>
    Task<IList<TaskDependency>> GetByTaskIdAsync(Guid taskId);

    /// <summary>Deletes all dependency edges for tasks belonging to a specific job.</summary>
    /// <param name="jobId">The job's GUID.</param>
    Task DeleteByJobIdAsync(Guid jobId);
}
