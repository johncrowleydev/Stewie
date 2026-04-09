/// <summary>
/// Repository interface for Artifact entity persistence.
/// REF: BLU-001 §7.2, CON-002 §4.3
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Artifact"/> entities.
/// </summary>
public interface IArtifactRepository
{
    /// <summary>Persists a new artifact.</summary>
    /// <param name="artifact">The artifact entity to save.</param>
    Task SaveAsync(Artifact artifact);

    /// <summary>Retrieves all artifacts for a specific task.</summary>
    /// <param name="taskId">The task's GUID.</param>
    /// <returns>Artifacts belonging to the task.</returns>
    Task<IList<Artifact>> GetByTaskIdAsync(Guid taskId);
}
