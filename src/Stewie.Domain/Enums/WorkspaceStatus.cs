/// <summary>
/// WorkspaceStatus enum — tracks workspace lifecycle states.
/// REF: BLU-001 §3.2
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Defines the lifecycle states of a task workspace on the host filesystem.
/// </summary>
public enum WorkspaceStatus
{
    /// <summary>Workspace directory has been created but not yet mounted.</summary>
    Created = 0,

    /// <summary>Workspace has been mounted into a worker container.</summary>
    Mounted = 1,

    /// <summary>Workspace has been cleaned up after task completion.</summary>
    Cleaned = 2
}
