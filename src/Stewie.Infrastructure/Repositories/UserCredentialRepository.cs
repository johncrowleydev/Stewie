using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

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
    public async Task DeleteAsync(UserCredential credential) => await _uow.Session.DeleteAsync(credential);
}
