/// <summary>
/// NHibernate-backed repository for Event entities.
/// REF: BLU-001 §7.2, CON-002 §4.5
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IEventRepository"/> using NHibernate ISession.
/// </summary>
public class EventRepository : IEventRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="EventRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public EventRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Event eventRecord)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(eventRecord);
    }

    /// <inheritdoc/>
    public async Task<IList<Event>> GetByEntityAsync(string entityType, Guid entityId)
    {
        return await _unitOfWork.Session.Query<Event>()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<Event>> GetRecentAsync(int limit = 100)
    {
        // Clamp limit to valid range per CON-002 §4.5
        var clampedLimit = Math.Clamp(limit, 1, 500);

        return await _unitOfWork.Session.Query<Event>()
            .OrderByDescending(e => e.Timestamp)
            .Take(clampedLimit)
            .ToListAsync();
    }
}
