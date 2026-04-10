namespace Stewie.Domain.Enums;

/// <summary>
/// Aggregate status for a Job.
/// Values 0-3 are Phase 1 originals. Value 4 is a Phase 4 addition for multi-task DAG jobs
/// where some tasks succeed and others fail.
/// Stored as int in SQL — no migration needed for new values.
/// REF: JOB-009 T-087
/// </summary>
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    PartiallyCompleted = 4
}
