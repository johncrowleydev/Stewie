/// <summary>
/// Repository interface for Event entity persistence.
/// REF: BLU-001 §7.2, CON-002 §4.5
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for <see cref="Event"/> entities.
/// </summary>
public interface IEventRepository
{
    /// <summary>Persists a new event record.</summary>
    /// <param name="eventRecord">The event entity to save.</param>
    Task SaveAsync(Event eventRecord);

    /// <summary>Retrieves all events for a specific entity.</summary>
    /// <param name="entityType">The type name of the entity (e.g. "Run").</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <returns>Events ordered by timestamp ascending.</returns>
    Task<IList<Event>> GetByEntityAsync(string entityType, Guid entityId);

    /// <summary>Retrieves the most recent events, ordered by timestamp descending.</summary>
    /// <param name="limit">Maximum number of events to return (default 100, max 500).</param>
    /// <returns>Events ordered by timestamp descending.</returns>
    Task<IList<Event>> GetRecentAsync(int limit = 100);
}
