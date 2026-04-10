/// <summary>
/// NHibernate implementation of IAgentSessionRepository.
/// REF: JOB-017 T-164
/// </summary>
using NHibernate;
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Persists and retrieves AgentSession entities via NHibernate.
/// </summary>
public class AgentSessionRepository : IAgentSessionRepository
{
    private readonly ISession _session;

    /// <summary>Initializes repository with NHibernate session from UnitOfWork.</summary>
    public AgentSessionRepository(IUnitOfWork unitOfWork)
    {
        _session = unitOfWork.Session;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> SaveAsync(AgentSession session)
    {
        await _session.SaveOrUpdateAsync(session);
        return session;
    }

    /// <inheritdoc/>
    public async Task<AgentSession?> GetByIdAsync(Guid id)
    {
        return await _session.GetAsync<AgentSession>(id);
    }

    /// <inheritdoc/>
    public async Task<IList<AgentSession>> GetByProjectIdAsync(Guid projectId)
    {
        return await _session.Query<AgentSession>()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<AgentSession>> GetActiveSessionsAsync()
    {
        var activeStatuses = new[]
        {
            AgentSessionStatus.Pending,
            AgentSessionStatus.Starting,
            AgentSessionStatus.Active
        };

        return await _session.Query<AgentSession>()
            .Where(s => activeStatuses.Contains(s.Status))
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<AgentSession?> GetActiveByProjectAndRoleAsync(Guid projectId, string agentRole)
    {
        var activeStatuses = new[]
        {
            AgentSessionStatus.Pending,
            AgentSessionStatus.Starting,
            AgentSessionStatus.Active
        };

        return await _session.Query<AgentSession>()
            .Where(s => s.ProjectId == projectId
                     && s.AgentRole == agentRole
                     && activeStatuses.Contains(s.Status))
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();
    }
}
