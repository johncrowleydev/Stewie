using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Repositories;

/// <summary>NHibernate implementation of <see cref="IUserCredentialRepository"/>.</summary>
public class UserCredentialRepository : IUserCredentialRepository
{
    private readonly IUnitOfWork _uow;
    public UserCredentialRepository(IUnitOfWork uow) => _uow = uow;
    public async Task SaveAsync(UserCredential credential) => await _uow.Session.SaveOrUpdateAsync(credential);
    public async Task<UserCredential?> GetByUserAndProviderAsync(Guid userId, string provider) =>
        await _uow.Session.Query<UserCredential>()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider);

    /// <inheritdoc/>
    public async Task<UserCredential?> GetByTypeAsync(Guid userId, CredentialType type) =>
        await _uow.Session.Query<UserCredential>()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CredentialType == type);

    public async Task DeleteAsync(UserCredential credential) => await _uow.Session.DeleteAsync(credential);
}

