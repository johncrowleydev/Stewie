using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>Repository for InviteCode entities.</summary>
public interface IInviteCodeRepository
{
    Task SaveAsync(InviteCode inviteCode);
    Task<InviteCode?> GetByCodeAsync(string code);
    Task<InviteCode?> GetByIdAsync(Guid id);
    Task<IList<InviteCode>> GetAllAsync();
    Task DeleteAsync(InviteCode inviteCode);
}
