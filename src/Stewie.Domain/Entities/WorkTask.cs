/// <summary>
/// Represents a single unit of work within a Job, assigned to a worker container.
/// REF: CON-002 §5.3, BLU-001 §3.1
/// </summary>
using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a single unit of work within a Job, assigned to a worker container.
/// </summary>
public class WorkTask
{
    /// <summary>Unique identifier for the task.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Parent job identifier.</summary>
    public virtual Guid JobId { get; set; }

    /// <summary>Navigation to the parent job.</summary>
    public virtual Job Job { get; set; } = null!;

    /// <summary>Agent role executing this task (developer, tester, researcher).</summary>
    public virtual string Role { get; set; } = string.Empty;

    /// <summary>Current execution status.</summary>
    public virtual WorkTaskStatus Status { get; set; }

    /// <summary>What the worker should accomplish. Null for legacy test jobs.</summary>
    public virtual string? Objective { get; set; }

    /// <summary>Boundaries of the work. Null for legacy test jobs.</summary>
    public virtual string? Scope { get; set; }

    /// <summary>JSON array of bash commands for the script worker. Null if not provided.</summary>
    public virtual string? ScriptJson { get; set; }

    /// <summary>JSON array of acceptance criteria. Null if not provided.</summary>
    public virtual string? AcceptanceCriteriaJson { get; set; }

    /// <summary>Filesystem path to the workspace directory.</summary>
    public virtual string WorkspacePath { get; set; } = string.Empty;

    /// <summary>Timestamp when the task was created.</summary>
    public virtual DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the task started execution. Null if not yet started.</summary>
    public virtual DateTime? StartedAt { get; set; }

    /// <summary>Classified failure reason from TaskFailureReason enum. Null for successful tasks.</summary>
    public virtual string? FailureReason { get; set; }

    /// <summary>FK to parent task — tester task points to its dev task. Null for root tasks.</summary>
    public virtual Guid? ParentTaskId { get; set; }

    /// <summary>Which retry iteration this task belongs to. Starts at 1.</summary>
    public virtual int AttemptNumber { get; set; } = 1;

    /// <summary>JSON array of governance violations from prior tester, injected for retry feedback. Null if first attempt.</summary>
    public virtual string? GovernanceViolationsJson { get; set; }

    /// <summary>Timestamp when the task completed. Null if still in progress.</summary>
    public virtual DateTime? CompletedAt { get; set; }
}
