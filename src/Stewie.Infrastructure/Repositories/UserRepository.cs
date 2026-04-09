using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>NHibernate implementation of <see cref="IUserRepository"/>.</summary>
public class UserRepository : IUserRepository
{
    private readonly IUnitOfWork _uow;
    public UserRepository(IUnitOfWork uow) => _uow = uow;
    public async Task SaveAsync(User user) => await _uow.Session.SaveOrUpdateAsync(user);
    public async Task<User?> GetByIdAsync(Guid id) => await _uow.Session.GetAsync<User>(id);
    public async Task<User?> GetByUsernameAsync(string username) =>
        await _uow.Session.Query<User>().FirstOrDefaultAsync(u => u.Username == username);
    public async Task<bool> ExistsAsync() =>
        await _uow.Session.Query<User>().AnyAsync();
}
