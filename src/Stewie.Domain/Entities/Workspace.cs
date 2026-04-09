/// <summary>
/// Workspace entity — tracks the lifecycle of a task workspace on the host filesystem.
/// Used by: WorkspaceService, RunOrchestrationService.
/// REF: BLU-001 §3.2
/// </summary>
using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a task workspace directory on the host filesystem.
/// Tracks when the workspace was created, mounted into a container, and cleaned up.
/// </summary>
public class Workspace
{
    /// <summary>Unique identifier for this workspace record.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Identifier of the task this workspace belongs to.</summary>
    public virtual Guid TaskId { get; set; }

    /// <summary>Filesystem path to the workspace directory.</summary>
    public virtual string Path { get; set; } = string.Empty;

    /// <summary>Current lifecycle state of the workspace.</summary>
    public virtual WorkspaceStatus Status { get; set; }

    /// <summary>Timestamp when the workspace record was created.</summary>
    public virtual DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the workspace was mounted into a container. Null if not yet mounted.</summary>
    public virtual DateTime? MountedAt { get; set; }

    /// <summary>Timestamp when the workspace was cleaned up. Null if not yet cleaned.</summary>
    public virtual DateTime? CleanedAt { get; set; }
}
