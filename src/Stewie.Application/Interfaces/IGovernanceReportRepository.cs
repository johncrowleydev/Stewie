/// <summary>
/// Repository interface for GovernanceReport entity persistence.
/// REF: JOB-007 T-067
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines persistence operations for governance reports.
/// </summary>
public interface IGovernanceReportRepository
{
    /// <summary>Persists a governance report.</summary>
    Task SaveAsync(GovernanceReport report);

    /// <summary>Gets the governance report for a specific tester task.</summary>
    Task<GovernanceReport?> GetByTaskIdAsync(Guid taskId);

    /// <summary>Gets the latest governance report for a job (across all tester tasks).</summary>
    Task<GovernanceReport?> GetLatestByJobIdAsync(Guid jobId);
}
