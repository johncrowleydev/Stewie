/// <summary>
/// Repository interface for Project entity persistence.
/// REF: BLU-001 §7.2 (new entity pattern)
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Project"/> entities.
/// </summary>
public interface IProjectRepository
{
    /// <summary>Retrieves a project by its unique identifier.</summary>
    /// <param name="id">The project's GUID.</param>
    /// <returns>The project, or null if not found.</returns>
    Task<Project?> GetByIdAsync(Guid id);

    /// <summary>Retrieves all projects.</summary>
    /// <returns>A list of all projects.</returns>
    Task<IList<Project>> GetAllAsync();

    /// <summary>Persists a new or updated project.</summary>
    /// <param name="project">The project entity to save.</param>
    Task SaveAsync(Project project);
}
