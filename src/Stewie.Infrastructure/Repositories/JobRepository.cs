/// <summary>
/// NHibernate-backed repository for Job entities.
/// REF: BLU-001 §7.2, CON-002 §4.2
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IJobRepository"/> using NHibernate ISession.
/// </summary>
public class JobRepository : IJobRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="JobRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public JobRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<Job?> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<Job>(id);
    }

    /// <inheritdoc/>
    public async Task<IList<Job>> GetAllAsync()
    {
        return await _unitOfWork.Session.Query<Job>()
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<Job>> GetByProjectIdAsync(Guid projectId)
    {
        return await _unitOfWork.Session.Query<Job>()
            .Where(j => j.ProjectId == projectId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Job job)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(job);
    }
}
