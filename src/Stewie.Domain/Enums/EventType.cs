/// <summary>
/// EventType enum — classifies audit trail events for state changes.
/// REF: BLU-001 §3.2
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Defines the types of events tracked in the audit trail.
/// Each value corresponds to a lifecycle state change on a Job or WorkTask.
/// </summary>
public enum EventType
{
    /// <summary>A new job was created.</summary>
    JobCreated = 0,

    /// <summary>A job transitioned to the Running state.</summary>
    JobStarted = 1,

    /// <summary>A job completed successfully.</summary>
    JobCompleted = 2,

    /// <summary>A job failed.</summary>
    JobFailed = 3,

    /// <summary>A new task was created within a job.</summary>
    TaskCreated = 4,

    /// <summary>A task transitioned to the Running state.</summary>
    TaskStarted = 5,

    /// <summary>A task completed successfully.</summary>
    TaskCompleted = 6,

    /// <summary>A task failed.</summary>
    TaskFailed = 7,

    /// <summary>Governance checks started for a job.</summary>
    GovernanceStarted = 8,

    /// <summary>Governance checks passed — job accepted.</summary>
    GovernancePassed = 9,

    /// <summary>Governance checks failed — job rejected or exhausted retries.</summary>
    GovernanceFailed = 10,

    /// <summary>Governance checks failed — retrying with violation feedback.</summary>
    GovernanceRetry = 11,

    /// <summary>An agent container session has started. (JOB-017)</summary>
    AgentStarted = 12,

    /// <summary>An agent container session has been terminated. (JOB-017)</summary>
    AgentTerminated = 13
}
