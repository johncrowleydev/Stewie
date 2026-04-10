/// <summary>
/// Enum representing the runtime-reported status of an agent container.
/// Used by IAgentRuntime.GetStatusAsync to report container state.
/// REF: JOB-017 T-162
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Runtime status of an agent container as reported by the container runtime.
/// This is the "physical" state of the container, not the logical session state.
/// </summary>
public enum AgentRuntimeStatus
{
    /// <summary>Container is running and responsive.</summary>
    Running = 0,

    /// <summary>Container has stopped normally.</summary>
    Stopped = 1,

    /// <summary>Container has stopped due to an error.</summary>
    Failed = 2,

    /// <summary>Container status cannot be determined.</summary>
    Unknown = 3
}
