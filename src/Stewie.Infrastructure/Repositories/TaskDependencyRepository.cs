/// <summary>
/// NHibernate implementation of ITaskDependencyRepository.
/// REF: JOB-009 T-082
/// </summary>
using NHibernate;
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Persists and retrieves TaskDependency entities via NHibernate.
/// </summary>
public class TaskDependencyRepository : ITaskDependencyRepository
{
    private readonly ISession _session;

    /// <summary>Initializes repository with NHibernate session from UnitOfWork.</summary>
    public TaskDependencyRepository(IUnitOfWork unitOfWork)
    {
        _session = unitOfWork.Session;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(TaskDependency dependency)
    {
        await _session.SaveOrUpdateAsync(dependency);
    }

    /// <inheritdoc/>
    public async Task<IList<TaskDependency>> GetByJobIdAsync(Guid jobId)
    {
        // Join through Tasks table to find dependencies for tasks belonging to this job
        var taskIds = await _session.Query<WorkTask>()
            .Where(t => t.JobId == jobId)
            .Select(t => t.Id)
            .ToListAsync();

        return await _session.Query<TaskDependency>()
            .Where(d => taskIds.Contains(d.TaskId))
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<TaskDependency>> GetByTaskIdAsync(Guid taskId)
    {
        return await _session.Query<TaskDependency>()
            .Where(d => d.TaskId == taskId)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task DeleteByJobIdAsync(Guid jobId)
    {
        var taskIds = await _session.Query<WorkTask>()
            .Where(t => t.JobId == jobId)
            .Select(t => t.Id)
            .ToListAsync();

        var deps = await _session.Query<TaskDependency>()
            .Where(d => taskIds.Contains(d.TaskId) || taskIds.Contains(d.DependsOnTaskId))
            .ToListAsync();

        foreach (var dep in deps)
        {
            await _session.DeleteAsync(dep);
        }
    }
}
