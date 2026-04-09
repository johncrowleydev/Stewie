/// <summary>
/// Repository interface for Job entity persistence.
/// REF: BLU-001 §7.2, CON-002 §4.2
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Job"/> entities.
/// </summary>
public interface IJobRepository
{
    /// <summary>Retrieves a job by its unique identifier.</summary>
    /// <param name="id">The job's GUID.</param>
    /// <returns>The job, or null if not found.</returns>
    Task<Job?> GetByIdAsync(Guid id);

    /// <summary>Retrieves all jobs, ordered by creation time descending.</summary>
    /// <returns>A list of all jobs.</returns>
    Task<IList<Job>> GetAllAsync();

    /// <summary>Retrieves all jobs for a specific project.</summary>
    /// <param name="projectId">The project's GUID.</param>
    /// <returns>Jobs belonging to the project, ordered by creation time descending.</returns>
    Task<IList<Job>> GetByProjectIdAsync(Guid projectId);

    /// <summary>Persists a new or updated job.</summary>
    /// <param name="job">The job entity to save.</param>
    Task SaveAsync(Job job);
}
