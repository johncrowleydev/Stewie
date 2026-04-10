/// <summary>
/// Docker container service — launches worker containers for task execution.
/// Enforces configurable timeout (default 300s) per CON-001 §7.
/// REF: BLU-001 §3.3, GOV-008, SPR-005 T-051, JOB-014 T-141
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IContainerService"/> using Docker CLI via Process.Start.
/// Enforces a hard timeout on container execution — returns exit code 124 on timeout.
/// Supports optional line-by-line stdout/stderr streaming via callback (JOB-014).
/// </summary>
public class DockerContainerService : IContainerService
{
    private readonly string _defaultImageName;
    private readonly int _timeoutSeconds;
    private readonly ILogger<DockerContainerService> _logger;

    /// <summary>Initializes the Docker container service with timeout configuration.</summary>
    /// <param name="defaultImageName">Default Docker image for dummy workers.</param>
    /// <param name="timeoutSeconds">Hard timeout in seconds for container execution. Default: 300.</param>
    /// <param name="logger">Logger instance.</param>
    public DockerContainerService(string defaultImageName, int timeoutSeconds, ILogger<DockerContainerService> logger)
    {
        _defaultImageName = defaultImageName;
        _timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 300;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        return LaunchWorkerInternalAsync(task, _defaultImageName, repoWritable: false, onOutputLine: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task, string imageName, CancellationToken cancellationToken = default)
    {
        return LaunchWorkerInternalAsync(task, imageName, repoWritable: true, onOutputLine: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default)
    {
        return LaunchWorkerInternalAsync(task, _defaultImageName, repoWritable: false, onOutputLine, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task, string imageName, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default)
    {
        return LaunchWorkerInternalAsync(task, imageName, repoWritable: true, onOutputLine, cancellationToken);
    }

    /// <summary>
    /// Internal launcher with timeout enforcement and optional streaming.
    /// Creates a linked CancellationTokenSource combining the caller's token and the configured timeout.
    /// On timeout: kills the Docker container and returns exit code 124 (Unix timeout convention).
    /// When onOutputLine is provided, stdout/stderr are streamed line-by-line via async events.
    /// When onOutputLine is null, output is read to completion (legacy behavior).
    /// REF: JOB-014 T-141
    /// </summary>
    private async Task<int> LaunchWorkerInternalAsync(
        WorkTask task, string imageName, bool repoWritable,
        Func<string, Task>? onOutputLine, CancellationToken cancellationToken)
    {
        var workspacePath = Path.GetFullPath(task.WorkspacePath);
        var inputMount = Path.Combine(workspacePath, "input");
        var outputMount = Path.Combine(workspacePath, "output");
        var repoMount = Path.Combine(workspacePath, "repo");

        Directory.CreateDirectory(outputMount);

        var repoMountFlag = repoWritable ? "" : ":ro";

        // Use a unique container name so we can kill it by name on timeout
        var containerName = $"stewie-worker-{task.Id:N}";

        var arguments = string.Join(" ",
            "run", "--rm",
            "--name", $"\"{containerName}\"",
            "-v", $"\"{inputMount}:/workspace/input:ro\"",
            "-v", $"\"{outputMount}:/workspace/output\"",
            "-v", $"\"{repoMount}:/workspace/repo{repoMountFlag}\"",
            imageName);

        _logger.LogInformation("Launching container {ContainerName}: docker {Arguments}", containerName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Docker process");

        // Link caller's cancellation token with our timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        try
        {
            if (onOutputLine is not null)
            {
                // Streaming mode — read stdout/stderr line-by-line as they arrive (JOB-014)
                var stdoutTask = StreamOutputAsync(process.StandardOutput, isStderr: false, onOutputLine, timeoutCts.Token);
                var stderrTask = StreamOutputAsync(process.StandardError, isStderr: true, onOutputLine, timeoutCts.Token);

                await Task.WhenAll(stdoutTask, stderrTask);
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            else
            {
                // Legacy mode — buffer output until exit
                var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
                var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);

                await process.WaitForExitAsync(timeoutCts.Token);

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    _logger.LogInformation("Container stdout:\n{Stdout}", stdout);
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    _logger.LogWarning("Container stderr:\n{Stderr}", stderr);
                }
            }

            _logger.LogInformation("Container {ContainerName} exited with code {ExitCode}",
                containerName, process.ExitCode);

            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            // Timeout or external cancellation — force-kill the container
            _logger.LogWarning("Container {ContainerName} timed out after {TimeoutSeconds}s, killing",
                containerName, _timeoutSeconds);

            await ForceKillContainerAsync(containerName);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            _logger.LogError("Task {TaskId} timed out after {TimeoutSeconds}s in container {ContainerName}",
                task.Id, _timeoutSeconds, containerName);

            return 124; // Unix timeout convention
        }
    }

    /// <summary>
    /// Reads lines from a stream reader and invokes the callback for each line.
    /// Stderr lines are prefixed with [stderr].
    /// Callback exceptions are caught and logged — they must never crash the container process.
    /// REF: JOB-014 T-141
    /// </summary>
    private async Task StreamOutputAsync(
        StreamReader reader, bool isStderr,
        Func<string, Task> onOutputLine, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break; // EOF

            var outputLine = isStderr ? $"[stderr] {line}" : line;

            if (isStderr)
            {
                _logger.LogWarning("Container stderr: {Line}", line);
            }

            try
            {
                await onOutputLine(outputLine);
            }
            catch (Exception ex)
            {
                // Callback failure (e.g., SignalR push error) must never kill the container
                _logger.LogWarning(ex, "Output line callback failed for line: {Line}", outputLine);
            }
        }
    }

    /// <summary>Kills a Docker container by name using docker kill.</summary>
    private async Task ForceKillContainerAsync(string containerName)
    {
        try
        {
            var killPsi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"kill \"{containerName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var killProcess = Process.Start(killPsi);
            if (killProcess is not null)
            {
                await killProcess.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill container {ContainerName}", containerName);
        }
    }
}
