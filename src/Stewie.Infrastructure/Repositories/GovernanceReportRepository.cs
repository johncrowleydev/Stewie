/// <summary>
/// NHibernate implementation of IGovernanceReportRepository.
/// REF: JOB-007 T-067
/// </summary>
using NHibernate;
using NHibernate.Linq;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

/// <summary>
/// Persists and retrieves GovernanceReport entities via NHibernate.
/// </summary>
public class GovernanceReportRepository : IGovernanceReportRepository
{
    private readonly ISession _session;

    /// <summary>Initializes repository with NHibernate session from UnitOfWork.</summary>
    public GovernanceReportRepository(IUnitOfWork unitOfWork)
    {
        _session = unitOfWork.Session;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(GovernanceReport report)
    {
        await _session.SaveOrUpdateAsync(report);
    }

    /// <inheritdoc/>
    public async Task<GovernanceReport?> GetByTaskIdAsync(Guid taskId)
    {
        return await _session.Query<GovernanceReport>()
            .Where(r => r.TaskId == taskId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<GovernanceReport?> GetLatestByJobIdAsync(Guid jobId)
    {
        // Join through Tasks table to find reports for tasks belonging to this job
        return await _session.Query<GovernanceReport>()
            .Where(r => _session.Query<WorkTask>()
                .Where(t => t.JobId == jobId && t.Role == "tester")
                .Select(t => t.Id)
                .Contains(r.TaskId))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<IList<GovernanceReport>> GetAllSinceAsync(DateTime since)
    {
        return await _session.Query<GovernanceReport>()
            .Where(r => r.CreatedAt >= since)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}
