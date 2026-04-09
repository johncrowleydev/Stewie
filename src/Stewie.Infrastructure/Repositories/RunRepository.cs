/// <summary>
/// NHibernate-backed repository for Run entities.
/// REF: BLU-001 §7.2, CON-002 §4.2
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IRunRepository"/> using NHibernate ISession.
/// </summary>
public class RunRepository : IRunRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="RunRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public RunRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<Run?> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<Run>(id);
    }

    /// <inheritdoc/>
    public async Task<IList<Run>> GetAllAsync()
    {
        return await _unitOfWork.Session.Query<Run>()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<Run>> GetByProjectIdAsync(Guid projectId)
    {
        return await _unitOfWork.Session.Query<Run>()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Run run)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(run);
    }
}
