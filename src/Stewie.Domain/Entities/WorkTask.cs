/// <summary>
/// Represents a single unit of work within a Run, assigned to a worker container.
/// REF: CON-002 §5.3, BLU-001 §3.1
/// </summary>
using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a single unit of work within a Run, assigned to a worker container.
/// </summary>
public class WorkTask
{
    /// <summary>Unique identifier for the task.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Parent run identifier.</summary>
    public virtual Guid RunId { get; set; }

    /// <summary>Navigation to the parent run.</summary>
    public virtual Run Run { get; set; } = null!;

    /// <summary>Agent role executing this task (developer, tester, researcher).</summary>
    public virtual string Role { get; set; } = string.Empty;

    /// <summary>Current execution status.</summary>
    public virtual WorkTaskStatus Status { get; set; }

    /// <summary>What the worker should accomplish. Null for legacy test runs.</summary>
    public virtual string? Objective { get; set; }

    /// <summary>Boundaries of the work. Null for legacy test runs.</summary>
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

    /// <summary>Timestamp when the task completed. Null if still in progress.</summary>
    public virtual DateTime? CompletedAt { get; set; }
}
