using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a top-level execution unit that orchestrates one or more WorkTasks.
/// Optionally associated with a Project for grouping.
/// REF: CON-002 §5.2, BLU-001 §3.1
/// </summary>
public class Run
{
    /// <summary>Unique identifier for the run.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Optional project this run belongs to. Null for standalone runs.</summary>
    public virtual Guid? ProjectId { get; set; }

    /// <summary>Navigation to the associated project. Null for standalone runs.</summary>
    public virtual Project? Project { get; set; }

    /// <summary>Current execution status.</summary>
    public virtual RunStatus Status { get; set; }

    /// <summary>Timestamp when the run was created.</summary>
    public virtual DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the run completed. Null if still in progress.</summary>
    public virtual DateTime? CompletedAt { get; set; }
}

