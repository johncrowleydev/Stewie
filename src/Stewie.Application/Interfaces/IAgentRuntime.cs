/// <summary>
/// IAgentRuntime — abstraction for pluggable agent container runtimes.
/// Each implementation knows how to launch, terminate, and query containers
/// using a specific agent framework (stub, Claude Code, OpenCode, etc.).
/// REF: JOB-017 T-162, BLU-001 §7
/// </summary>
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines the contract for launching and managing agent containers.
/// Implementations are registered in DI and selected by runtime name.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Unique name identifying this runtime (e.g. "stub", "claude-code", "open-code").
    /// Used to match sessions to their runtime and for configuration lookup.
    /// </summary>
    string RuntimeName { get; }

    /// <summary>
    /// Launches an agent container with the specified configuration.
    /// Returns the Docker container ID on success.
    /// </summary>
    /// <param name="request">Launch configuration including project, task, workspace, and RabbitMQ settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Docker container ID of the launched container.</returns>
    Task<string> LaunchAgentAsync(AgentLaunchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Terminates a running agent container.
    /// </summary>
    /// <param name="containerId">Docker container ID to terminate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TerminateAgentAsync(string containerId, CancellationToken ct = default);

    /// <summary>
    /// Queries the runtime status of an agent container.
    /// </summary>
    /// <param name="containerId">Docker container ID to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current runtime status of the container.</returns>
    Task<AgentRuntimeStatus> GetStatusAsync(string containerId, CancellationToken ct = default);
}
