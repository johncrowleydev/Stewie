/// <summary>
/// Repository interface for AgentSession persistence.
/// REF: JOB-017 T-164
/// </summary>
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for agent session lifecycle tracking.
/// </summary>
public interface IAgentSessionRepository
{
    /// <summary>Persists a new or updated agent session.</summary>
    Task<AgentSession> SaveAsync(AgentSession session);

    /// <summary>Retrieves an agent session by its unique ID.</summary>
    Task<AgentSession?> GetByIdAsync(Guid id);

    /// <summary>Retrieves all sessions for a project, ordered by StartedAt descending.</summary>
    Task<IList<AgentSession>> GetByProjectIdAsync(Guid projectId);

    /// <summary>Retrieves all active sessions (status is Pending, Starting, or Active).</summary>
    Task<IList<AgentSession>> GetActiveSessionsAsync();

    /// <summary>Retrieves the active session for a specific project and role, if any.</summary>
    Task<AgentSession?> GetActiveByProjectAndRoleAsync(Guid projectId, string agentRole);
}
