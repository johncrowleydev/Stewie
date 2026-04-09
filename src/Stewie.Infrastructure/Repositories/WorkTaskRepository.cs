/// <summary>
/// NHibernate-backed repository for WorkTask entities.
/// REF: BLU-001 §7.2, CON-002 §4.3
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IWorkTaskRepository"/> using NHibernate ISession.
/// </summary>
public class WorkTaskRepository : IWorkTaskRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkTaskRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public WorkTaskRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<WorkTask?> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<WorkTask>(id);
    }

    /// <inheritdoc/>
    public async Task<IList<WorkTask>> GetByRunIdAsync(Guid runId)
    {
        return await _unitOfWork.Session.Query<WorkTask>()
            .Where(t => t.RunId == runId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WorkTask workTask)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(workTask);
    }
}
