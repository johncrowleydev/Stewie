/// <summary>
/// Pluggable abstraction for launching and managing agent containers.
/// Each implementation wraps a specific agentic framework (Claude Code, Aider, etc.).
/// REF: BLU-001 §7, JOB-017 T-162, PRJ-001 Phase 5b
/// </summary>
namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines the contract for agent container runtimes. The Application layer
/// uses this interface to launch, terminate, and query agent containers
/// without coupling to any specific container technology or agentic framework.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>Human-readable name of this runtime (e.g., "stub", "claude-code", "aider").</summary>
    string RuntimeName { get; }

    /// <summary>
    /// Launch an agent container. Returns the container ID.
    /// The container should connect to RabbitMQ using the connection info in the request
    /// and begin consuming from its assigned command queue.
    /// </summary>
    /// <param name="request">Launch configuration including RabbitMQ connection details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Container ID that can be used with TerminateAsync and IsRunningAsync.</returns>
    Task<string> LaunchAsync(Models.AgentLaunchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Terminate a running agent container.
    /// Should stop the container gracefully and remove it.
    /// </summary>
    /// <param name="containerId">Container ID returned by LaunchAsync.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TerminateAsync(string containerId, CancellationToken ct = default);

    /// <summary>
    /// Check if a container is still running.
    /// </summary>
    /// <param name="containerId">Container ID returned by LaunchAsync.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the container is currently running.</returns>
    Task<bool> IsRunningAsync(string containerId, CancellationToken ct = default);
}
