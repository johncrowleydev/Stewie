using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>Repository for UserCredential entities.</summary>
public interface IUserCredentialRepository
{
    Task SaveAsync(UserCredential credential);
    Task<UserCredential?> GetByUserAndProviderAsync(Guid userId, string provider);
    Task DeleteAsync(UserCredential credential);
}
