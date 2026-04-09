/// <summary>
/// NHibernate-backed repository for Artifact entities.
/// REF: BLU-001 §7.2, CON-002 §4.3
/// </summary>
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Implements <see cref="IArtifactRepository"/> using NHibernate ISession.
/// </summary>
public class ArtifactRepository : IArtifactRepository
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of <see cref="ArtifactRepository"/>.
    /// </summary>
    /// <param name="unitOfWork">The NHibernate unit of work providing the session.</param>
    public ArtifactRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Artifact artifact)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(artifact);
    }

    /// <inheritdoc/>
    public async Task<IList<Artifact>> GetByTaskIdAsync(Guid taskId)
    {
        return await _unitOfWork.Session.Query<Artifact>()
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }
}
