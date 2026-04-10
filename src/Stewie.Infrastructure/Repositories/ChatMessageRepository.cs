/// <summary>
/// NHibernate implementation of IChatMessageRepository.
/// REF: JOB-013 T-132
/// </summary>
using NHibernate;
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Persists and retrieves ChatMessage entities via NHibernate.
/// </summary>
public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ISession _session;

    /// <summary>Initializes repository with NHibernate session from UnitOfWork.</summary>
    public ChatMessageRepository(IUnitOfWork unitOfWork)
    {
        _session = unitOfWork.Session;
    }

    /// <inheritdoc/>
    public async Task<ChatMessage> SaveAsync(ChatMessage message)
    {
        await _session.SaveOrUpdateAsync(message);
        return message;
    }

    /// <inheritdoc/>
    public async Task<IList<ChatMessage>> GetByProjectIdAsync(Guid projectId, int limit = 100, int offset = 0)
    {
        return await _session.Query<ChatMessage>()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<int> GetCountByProjectIdAsync(Guid projectId)
    {
        return await _session.Query<ChatMessage>()
            .Where(m => m.ProjectId == projectId)
            .CountAsync();
    }
}
