/// <summary>
/// Repository interface for Run entity persistence.
/// REF: BLU-001 §7.2, CON-002 §4.2
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Run"/> entities.
/// </summary>
public interface IRunRepository
{
    /// <summary>Retrieves a run by its unique identifier.</summary>
    /// <param name="id">The run's GUID.</param>
    /// <returns>The run, or null if not found.</returns>
    Task<Run?> GetByIdAsync(Guid id);

    /// <summary>Retrieves all runs, ordered by creation time descending.</summary>
    /// <returns>A list of all runs.</returns>
    Task<IList<Run>> GetAllAsync();

    /// <summary>Retrieves all runs for a specific project.</summary>
    /// <param name="projectId">The project's GUID.</param>
    /// <returns>Runs belonging to the project, ordered by creation time descending.</returns>
    Task<IList<Run>> GetByProjectIdAsync(Guid projectId);

    /// <summary>Persists a new or updated run.</summary>
    /// <param name="run">The run entity to save.</param>
    Task SaveAsync(Run run);
}
