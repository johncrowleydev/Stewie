/// <summary>
/// Repository interface for Workspace entity persistence.
/// REF: BLU-001 §7.2
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Workspace"/> entities.
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>Retrieves a workspace by its unique identifier.</summary>
    /// <param name="id">The workspace's GUID.</param>
    /// <returns>The workspace, or null if not found.</returns>
    Task<Workspace?> GetByIdAsync(Guid id);

    /// <summary>Retrieves the workspace for a specific task.</summary>
    /// <param name="taskId">The task's GUID.</param>
    /// <returns>The workspace, or null if not found.</returns>
    Task<Workspace?> GetByTaskIdAsync(Guid taskId);

    /// <summary>Persists a new or updated workspace.</summary>
    /// <param name="workspace">The workspace entity to save.</param>
    Task SaveAsync(Workspace workspace);
}
