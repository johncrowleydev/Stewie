/// <summary>
/// Enum representing the lifecycle status of an AgentSession.
/// Tracks the logical state of an agent from launch through termination.
/// REF: JOB-017 T-163
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Logical lifecycle status of an agent session.
/// Stored as int in SQL — no migration needed for new values.
/// </summary>
public enum AgentSessionStatus
{
    /// <summary>Agent session has been created but the container is not yet running.</summary>
    Pending = 0,

    /// <summary>Agent container is launching.</summary>
    Starting = 1,

    /// <summary>Agent container is running and processing work.</summary>
    Active = 2,

    /// <summary>Agent has completed its work successfully.</summary>
    Completed = 3,

    /// <summary>Agent has failed.</summary>
    Failed = 4,

    /// <summary>Agent was terminated by the API (user-initiated or timeout).</summary>
    Terminated = 5
}
