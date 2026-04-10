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

    /// <summary>Gets all governance reports created within the specified date range.</summary>
    /// <param name="since">Start of the date range (inclusive).</param>
    /// <returns>All reports created on or after the specified date.</returns>
    Task<IList<GovernanceReport>> GetAllSinceAsync(DateTime since);
}
