/// <summary>
/// NHibernate-backed repository for Workspace entities.
/// REF: BLU-001 §7.2
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IWorkspaceRepository"/> using NHibernate ISession.
/// </summary>
public class WorkspaceEntityRepository : IWorkspaceRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkspaceEntityRepository"/>.
    /// DECISION: Named WorkspaceEntityRepository (not WorkspaceRepository) to avoid
    /// collision with the existing IWorkspaceService/WorkspaceService that handles
    /// filesystem operations. This repository handles only DB persistence.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public WorkspaceEntityRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task<Workspace?> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<Workspace>(id);
    }

    /// <inheritdoc/>
    public async Task<Workspace?> GetByTaskIdAsync(Guid taskId)
    {
        return await _unitOfWork.Session.Query<Workspace>()
            .Where(w => w.TaskId == taskId)
            .SingleOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Workspace workspace)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(workspace);
    }
}
