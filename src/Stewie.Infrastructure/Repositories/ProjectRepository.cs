/// <summary>
/// NHibernate-backed repository for Project entities.
/// REF: BLU-001 §7.2 (new entity pattern)
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IProjectRepository"/> using NHibernate ISession.
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="ProjectRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public ProjectRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<Project>(id);
    }

    /// <inheritdoc/>
    public async Task<IList<Project>> GetAllAsync()
    {
        return await _unitOfWork.Session.Query<Project>()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Project project)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(project);
    }
}
