/// <summary>
/// EventType enum — classifies audit trail events for state changes.
/// REF: BLU-001 §3.2
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Defines the types of events tracked in the audit trail.
/// Each value corresponds to a lifecycle state change on a Run or WorkTask.
/// </summary>
public enum EventType
{
    /// <summary>A new run was created.</summary>
    RunCreated = 0,

    /// <summary>A run transitioned to the Running state.</summary>
    RunStarted = 1,

    /// <summary>A run completed successfully.</summary>
    RunCompleted = 2,

    /// <summary>A run failed.</summary>
    RunFailed = 3,

    /// <summary>A new task was created within a run.</summary>
    TaskCreated = 4,

    /// <summary>A task transitioned to the Running state.</summary>
    TaskStarted = 5,

    /// <summary>A task completed successfully.</summary>
    TaskCompleted = 6,

    /// <summary>A task failed.</summary>
    TaskFailed = 7
}
