/// <summary>
/// Repository interface for ChatMessage persistence.
/// REF: JOB-013 T-132
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines data access operations for project chat messages.
/// </summary>
public interface IChatMessageRepository
{
    /// <summary>Persists a new chat message.</summary>
    Task<ChatMessage> SaveAsync(ChatMessage message);

    /// <summary>
    /// Retrieves chat messages for a project, ordered oldest-first (ascending CreatedAt).
    /// Supports pagination via limit/offset.
    /// </summary>
    Task<IList<ChatMessage>> GetByProjectIdAsync(Guid projectId, int limit = 100, int offset = 0);

    /// <summary>Returns the total count of messages in a project.</summary>
    Task<int> GetCountByProjectIdAsync(Guid projectId);
}
