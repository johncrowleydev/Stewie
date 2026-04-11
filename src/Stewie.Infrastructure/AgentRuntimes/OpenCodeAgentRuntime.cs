/// <summary>
/// OpenCode agent runtime — launches a Docker container running the OpenCode CLI
/// backed by a configurable LLM provider. Uses file-based secret injection for
/// API keys rather than environment variables.
/// REF: JOB-021 T-180, CON-004
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;

namespace Stewie.Infrastructure.AgentRuntimes;

/// <summary>
/// Implements <see cref="IAgentRuntime"/> by launching the <c>stewie-opencode-agent</c>
/// Docker image. The container connects to RabbitMQ, invokes the OpenCode CLI to execute
/// tasks, and publishes results. Supports file-based secret mounting for LLM API keys
/// and a mock LLM mode for CI testing.
///
/// Uses Docker CLI via <see cref="Process.Start"/> (same pattern as
/// <see cref="StubAgentRuntime"/>).
/// </summary>
public class OpenCodeAgentRuntime : IAgentRuntime
{
    /// <summary>Default Docker image name for the OpenCode agent.</summary>
    public const string DefaultImageName = "stewie-opencode-agent";

    private readonly string _imageName;
    private readonly ILogger<OpenCodeAgentRuntime> _logger;

    /// <summary>Initializes the OpenCode runtime with an optional custom image name.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="imageName">Docker image name. Defaults to <c>stewie-opencode-agent</c>.</param>
    public OpenCodeAgentRuntime(ILogger<OpenCodeAgentRuntime> logger, string? imageName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageName = imageName ?? DefaultImageName;
    }

    /// <inheritdoc/>
    public string RuntimeName => "opencode";

    /// <inheritdoc/>
    public async Task<string> LaunchAgentAsync(AgentLaunchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var containerName = FormatContainerName(request.SessionId);

        // Build base env vars (RabbitMQ, agent identity)
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

        // LLM configuration (non-secret, passed as env vars)
        if (!string.IsNullOrWhiteSpace(request.LlmProvider))
        {
            envArgs += $" -e LLM_PROVIDER={request.LlmProvider}";
        }

        if (!string.IsNullOrWhiteSpace(request.ModelName))
        {
            envArgs += $" -e MODEL_NAME={request.ModelName}";
        }

        // Volume mounts
        var volumeArgs = string.Empty;

        // Workspace mount
        if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
        {
            var workspacePath = Path.GetFullPath(request.WorkspacePath);
            volumeArgs = $"-v \"{workspacePath}:/workspace\"";
        }

        // Secret file mount — file-based injection for API keys
        if (!string.IsNullOrWhiteSpace(request.SecretsMountPath))
        {
            var secretsPath = Path.GetFullPath(request.SecretsMountPath);
            volumeArgs += $" -v \"{secretsPath}:/run/secrets:ro\"";
        }

        var arguments = $"run -d --network host --name \"{containerName}\" {envArgs} {volumeArgs} {_imageName}".Trim();

        _logger.LogInformation(
            "Launching OpenCode agent container {ContainerName} for session {SessionId}, role={Role}, provider={Provider}, model={Model}",
            containerName, request.SessionId, request.AgentRole, request.LlmProvider, request.ModelName);

        var (exitCode, stdout, stderr) = await RunDockerCommandAsync(arguments, ct);

        if (exitCode != 0)
        {
            _logger.LogError(
                "Failed to launch OpenCode agent container: exit={ExitCode}, stderr={Stderr}",
                exitCode, stderr);
            throw new InvalidOperationException(
                $"Docker run failed with exit code {exitCode}: {stderr}");
        }

        // docker run -d returns the full container ID on stdout
        var containerId = stdout.Trim();

        _logger.LogInformation(
            "OpenCode agent container {ContainerName} launched: containerId={ContainerId}",
            containerName, containerId.Length > 12 ? containerId[..12] : containerId);

        return containerId;
    }

    /// <inheritdoc/>
    public async Task TerminateAgentAsync(string containerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        _logger.LogInformation("Terminating OpenCode agent container {ContainerId}",
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

        _logger.LogInformation("OpenCode agent container {ContainerId} terminated",
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
    /// Creates the secrets directory and writes the LLM API key to a file.
    /// Called by <see cref="Application.Services.AgentLifecycleService"/> before launching.
    /// </summary>
    /// <param name="sessionId">Session ID used to create a unique secrets directory.</param>
    /// <param name="apiKey">Decrypted LLM API key.</param>
    /// <returns>Path to the secrets directory for mounting into the container.</returns>
    public static string WriteSecretFile(Guid sessionId, string apiKey)
    {
        var secretsDir = Path.Combine(Path.GetTempPath(), $"stewie-secrets-{sessionId:N}");
        Directory.CreateDirectory(secretsDir);
        File.WriteAllText(Path.Combine(secretsDir, "llm_api_key"), apiKey);
        return secretsDir;
    }

    /// <summary>
    /// Deletes the secrets directory created for a session.
    /// Safe to call even if the directory has already been deleted.
    /// </summary>
    /// <param name="sessionId">Session ID whose secrets directory to clean up.</param>
    public static void CleanupSecretFile(Guid sessionId)
    {
        var secretsDir = Path.Combine(Path.GetTempPath(), $"stewie-secrets-{sessionId:N}");
        try
        {
            if (Directory.Exists(secretsDir))
            {
                Directory.Delete(secretsDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort — directory may already be gone
        }
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
