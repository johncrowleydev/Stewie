using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Services;

public class DockerContainerService : IContainerService
{
    private readonly string _imageName;
    private readonly ILogger<DockerContainerService> _logger;

    public DockerContainerService(string imageName, ILogger<DockerContainerService> logger)
    {
        _imageName = imageName;
        _logger = logger;
    }

    public async Task<int> LaunchWorkerAsync(WorkTask task)
    {
        var workspacePath = Path.GetFullPath(task.WorkspacePath);
        var inputMount = Path.Combine(workspacePath, "input");
        var outputMount = Path.Combine(workspacePath, "output");
        var repoMount = Path.Combine(workspacePath, "repo");

        // Ensure output directory exists
        Directory.CreateDirectory(outputMount);

        var arguments = string.Join(" ",
            "run", "--rm",
            "-v", $"\"{inputMount}:/workspace/input:ro\"",
            "-v", $"\"{outputMount}:/workspace/output\"",
            "-v", $"\"{repoMount}:/workspace/repo:ro\"",
            _imageName);

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
