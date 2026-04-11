using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Interfaces;

/// <summary>Repository for UserCredential entities.</summary>
public interface IUserCredentialRepository
{
    /// <summary>Persists a credential (insert or update).</summary>
    Task SaveAsync(UserCredential credential);

    /// <summary>Retrieves a credential by user and provider name.</summary>
    Task<UserCredential?> GetByUserAndProviderAsync(Guid userId, string provider);

    /// <summary>
    /// Retrieves a credential by user and credential type.
    /// REF: JOB-021 T-183.
    /// </summary>
    Task<UserCredential?> GetByTypeAsync(Guid userId, CredentialType type);

    /// <summary>Deletes a credential.</summary>
    Task DeleteAsync(UserCredential credential);
}

