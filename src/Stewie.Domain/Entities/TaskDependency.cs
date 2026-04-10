/// <summary>
/// Represents an edge in the task dependency DAG — "TaskId depends on DependsOnTaskId".
/// REF: JOB-009 T-081, BLU-001
/// </summary>
namespace Stewie.Domain.Entities;

/// <summary>
/// Models a directed dependency edge between two WorkTasks within the same Job.
/// The task identified by <see cref="TaskId"/> cannot execute until
/// the task identified by <see cref="DependsOnTaskId"/> has completed.
/// </summary>
public class TaskDependency
{
    /// <summary>Unique identifier for this dependency edge.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>The task that has the dependency (the downstream task).</summary>
    public virtual Guid TaskId { get; set; }

    /// <summary>The task that must complete first (the upstream task).</summary>
    public virtual Guid DependsOnTaskId { get; set; }

    /// <summary>Timestamp when the dependency was created.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
