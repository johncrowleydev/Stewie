using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>Repository for InviteCode entities.</summary>
public interface IInviteCodeRepository
{
    Task SaveAsync(InviteCode inviteCode);
    Task<InviteCode?> GetByCodeAsync(string code);
    Task<IList<InviteCode>> GetAllAsync();
}
