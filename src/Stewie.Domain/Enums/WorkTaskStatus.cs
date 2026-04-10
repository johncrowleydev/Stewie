namespace Stewie.Domain.Enums;

/// <summary>
/// Execution status values for a WorkTask.
/// Values 0-3 are Phase 1 originals. Values 4-5 are Phase 4 additions for DAG support.
/// Stored as int in SQL — no migration needed for new values.
/// REF: JOB-009 T-086
/// </summary>
public enum WorkTaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Blocked = 4,
    Cancelled = 5
}
