/// <summary>
/// Repository interface for WorkTask entity persistence.
/// REF: BLU-001 §7.2, CON-002 §4.3
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="WorkTask"/> entities.
/// </summary>
public interface IWorkTaskRepository
{
    /// <summary>Retrieves a task by its unique identifier.</summary>
    /// <param name="id">The task's GUID.</param>
    /// <returns>The task, or null if not found.</returns>
    Task<WorkTask?> GetByIdAsync(Guid id);

    /// <summary>Retrieves all tasks for a specific run.</summary>
    /// <param name="runId">The run's GUID.</param>
    /// <returns>Tasks belonging to the run, ordered by creation time ascending.</returns>
    Task<IList<WorkTask>> GetByRunIdAsync(Guid runId);

    /// <summary>Persists a new or updated task.</summary>
    /// <param name="workTask">The task entity to save.</param>
    Task SaveAsync(WorkTask workTask);
}
