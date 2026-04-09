/// <summary>
/// GovernanceReport entity — stores the result of governance checks run by the tester worker.
/// REF: JOB-007 T-067, CON-001 §6
/// </summary>
namespace Stewie.Domain.Entities;

/// <summary>
/// Represents the outcome of a governance check cycle for a tester task.
/// One report per tester task execution.
/// </summary>
public class GovernanceReport
{
    /// <summary>Unique identifier for this report.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>FK to the tester task that produced this report.</summary>
    public virtual Guid TaskId { get; set; }

    /// <summary>Whether all error-severity checks passed.</summary>
    public virtual bool Passed { get; set; }

    /// <summary>Total number of governance checks executed.</summary>
    public virtual int TotalChecks { get; set; }

    /// <summary>Number of checks that passed.</summary>
    public virtual int PassedChecks { get; set; }

    /// <summary>Number of checks that failed.</summary>
    public virtual int FailedChecks { get; set; }

    /// <summary>JSON array of GovernanceCheckResult objects — the detailed check results.</summary>
    public virtual string CheckResultsJson { get; set; } = "[]";

    /// <summary>Timestamp when the report was created.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
