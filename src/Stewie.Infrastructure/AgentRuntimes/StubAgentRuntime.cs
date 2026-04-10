/// <summary>
/// Stub agent runtime — launches a lightweight Python container that speaks
/// the CON-004 protocol for end-to-end testing of the messaging loop.
/// REF: JOB-017 T-167, CON-004
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;

namespace Stewie.Infrastructure.AgentRuntimes;

/// <summary>
/// Implements <see cref="IAgentRuntime"/> by launching the <c>stewie-stub-agent</c>
/// Docker image. The container connects to RabbitMQ, publishes events, and responds
/// to commands — validating the entire messaging loop without any LLM dependency.
///
/// Uses Docker CLI via <see cref="Process.Start"/> (same pattern as
/// <see cref="Services.DockerContainerService"/>).
/// </summary>
public class StubAgentRuntime : IAgentRuntime
{
    /// <summary>Default Docker image name for the stub agent.</summary>
    public const string DefaultImageName = "stewie-stub-agent";

    private readonly string _imageName;
    private readonly ILogger<StubAgentRuntime> _logger;

    /// <summary>Initializes the stub runtime with an optional custom image name.</summary>
    /// <param name="imageName">Docker image name. Defaults to <c>stewie-stub-agent</c>.</param>
    /// <param name="logger">Logger instance.</param>
    public StubAgentRuntime(ILogger<StubAgentRuntime> logger, string? imageName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageName = imageName ?? DefaultImageName;
    }

    /// <inheritdoc/>
    public string RuntimeName => "stub";

    /// <inheritdoc/>
    public async Task<string> LaunchAgentAsync(AgentLaunchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var containerName = FormatContainerName(request.SessionId);

        var envArgs = string.Join(" ",
            $"-e RABBITMQ_HOST={request.RabbitMqHost}",
            $"-e RABBITMQ_PORT={request.RabbitMqPort}",
            $"-e RABBITMQ_USER={request.RabbitMqUser}",
            $"-e RABBITMQ_PASS={request.RabbitMqPassword}",
            $"-e RABBITMQ_VHOST={request.RabbitMqVHost}",
            $"-e AGENT_QUEUE={request.CommandQueueName}",
            $"-e AGENT_ID={request.SessionId}",
            $"-e PROJECT_ID={request.ProjectId}",
            $"-e AGENT_ROLE={request.AgentRole}");

        // Add workspace mount if specified
        var volumeArgs = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            var workspacePath = Path.GetFullPath(request.WorkspacePath);
            volumeArgs = $"-v \"{workspacePath}:/workspace\"";
        }

        var arguments = $"run -d --name \"{containerName}\" {envArgs} {volumeArgs} {_imageName}".Trim();

        _logger.LogInformation(
            "Launching stub agent container {ContainerName} for session {SessionId}, role={Role}",
            containerName, request.SessionId, request.AgentRole);

        var (exitCode, stdout, stderr) = await RunDockerCommandAsync(arguments, ct);

        if (exitCode != 0)
        {
            _logger.LogError(
                "Failed to launch stub agent container: exit={ExitCode}, stderr={Stderr}",
                exitCode, stderr);
            throw new InvalidOperationException(
                $"Docker run failed with exit code {exitCode}: {stderr}");
        }

        // docker run -d returns the full container ID on stdout
        var containerId = stdout.Trim();

        _logger.LogInformation(
            "Stub agent container {ContainerName} launched: containerId={ContainerId}",
            containerName, containerId.Length > 12 ? containerId[..12] : containerId);

        return containerId;
    }

    /// <inheritdoc/>
    public async Task TerminateAgentAsync(string containerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        _logger.LogInformation("Terminating stub agent container {ContainerId}",
            containerId.Length > 12 ? containerId[..12] : containerId);

        // Stop with 10s grace period (allows SIGTERM handler to run)
        var (stopExit, _, stopErr) = await RunDockerCommandAsync(
            $"stop --time 10 \"{containerId}\"", ct);

        if (stopExit != 0)
        {
            _logger.LogWarning("docker stop failed: exit={ExitCode}, stderr={Stderr}",
                stopExit, stopErr);
        }

        // Remove the stopped container
        var (rmExit, _, rmErr) = await RunDockerCommandAsync(
            $"rm -f \"{containerId}\"", ct);

        if (rmExit != 0)
        {
            _logger.LogWarning("docker rm failed: exit={ExitCode}, stderr={Stderr}",
                rmExit, rmErr);
        }

        _logger.LogInformation("Stub agent container {ContainerId} terminated",
            containerId.Length > 12 ? containerId[..12] : containerId);
    }

    /// <inheritdoc/>
    public async Task<AgentRuntimeStatus> GetStatusAsync(string containerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        var (exitCode, stdout, _) = await RunDockerCommandAsync(
            $"inspect -f \"{{{{.State.Running}}}}\" \"{containerId}\"", ct);

        if (exitCode != 0)
            return AgentRuntimeStatus.Unknown;

        return stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            ? AgentRuntimeStatus.Running
            : AgentRuntimeStatus.Stopped;
    }

    /// <summary>
    /// Generates a deterministic container name from a session ID.
    /// Pattern: <c>stewie-agent-{sessionId:N}</c> (no hyphens in GUID for Docker name safety).
    /// </summary>
    internal static string FormatContainerName(Guid sessionId)
    {
        return $"stewie-agent-{sessionId:N}";
    }

    /// <summary>
    /// Runs a Docker CLI command and captures stdout, stderr, and exit code.
    /// Times out after 30 seconds to prevent zombie processes.
    /// </summary>
    private async Task<(int ExitCode, string Stdout, string Stderr)> RunDockerCommandAsync(
        string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("docker {Arguments}", arguments);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Docker process");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            return (process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }
}
