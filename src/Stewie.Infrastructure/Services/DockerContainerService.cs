/// <summary>
/// Docker container service — launches worker containers for task execution.
/// REF: BLU-001 §3.3, GOV-008
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IContainerService"/> using Docker CLI via Process.Start.
/// </summary>
public class DockerContainerService : IContainerService
{
    private readonly string _defaultImageName;
    private readonly ILogger<DockerContainerService> _logger;

    /// <summary>Initializes the Docker container service.</summary>
    public DockerContainerService(string defaultImageName, ILogger<DockerContainerService> logger)
    {
        _defaultImageName = defaultImageName;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task)
    {
        return LaunchWorkerInternalAsync(task, _defaultImageName, repoWritable: false);
    }

    /// <inheritdoc/>
    public Task<int> LaunchWorkerAsync(WorkTask task, string imageName)
    {
        return LaunchWorkerInternalAsync(task, imageName, repoWritable: true);
    }

    /// <summary>
    /// Internal launcher that supports both read-only and writable repo mounts.
    /// Script workers need write access to repo/ so they can modify files.
    /// </summary>
    private async Task<int> LaunchWorkerInternalAsync(WorkTask task, string imageName, bool repoWritable)
    {
        var workspacePath = Path.GetFullPath(task.WorkspacePath);
        var inputMount = Path.Combine(workspacePath, "input");
        var outputMount = Path.Combine(workspacePath, "output");
        var repoMount = Path.Combine(workspacePath, "repo");

        Directory.CreateDirectory(outputMount);

        var repoMountFlag = repoWritable ? "" : ":ro";

        var arguments = string.Join(" ",
            "run", "--rm",
            "-v", $"\"{inputMount}:/workspace/input:ro\"",
            "-v", $"\"{outputMount}:/workspace/output\"",
            "-v", $"\"{repoMount}:/workspace/repo{repoMountFlag}\"",
            imageName);

        _logger.LogInformation("Launching container: docker {Arguments}", arguments);

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

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogInformation("Container stdout:\n{Stdout}", stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning("Container stderr:\n{Stderr}", stderr);
        }

        _logger.LogInformation("Container exited with code {ExitCode}", process.ExitCode);

        return process.ExitCode;
    }
}
