using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>Repository for User entities.</summary>
public interface IUserRepository
{
    Task SaveAsync(User user);
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByUsernameAsync(string username);
    Task<IList<User>> GetAllAsync();
    Task DeleteAsync(User user);
    Task<bool> ExistsAsync();
}
