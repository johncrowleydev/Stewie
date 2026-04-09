/// <summary>
/// Workspace filesystem service — manages workspace directories, task.json I/O,
/// and git repository operations (clone, branch).
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If task.json missing        → check PrepareWorkspace directory creation
/// 2. If result.json not found    → check WorkspacePath on the task entity
/// 3. If git clone fails          → check repoUrl format and network access
/// 4. If git branch fails         → check that repo was cloned first
///
/// REF: BLU-001 §3.2, CON-001 §3, §4
/// </summary>
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IWorkspaceService"/> — manages workspace filesystem operations
/// and git repository interactions via Process.Start shell commands.
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly string _workspaceRoot;
    private readonly ILogger<WorkspaceService> _logger;

    /// <summary>
    /// Initializes the workspace service.
    /// </summary>
    /// <param name="workspaceRoot">Root directory for all task workspaces.</param>
    /// <param name="logger">Structured logger.</param>
    public WorkspaceService(string workspaceRoot, ILogger<WorkspaceService> logger)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string PrepareWorkspace(WorkTask task, Run run)
    {
        var taskDir = Path.Combine(_workspaceRoot, task.Id.ToString());
        var repoDir = Path.Combine(taskDir, "repo");
        var inputDir = Path.Combine(taskDir, "input");
        var outputDir = Path.Combine(taskDir, "output");

        Directory.CreateDirectory(repoDir);
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        _logger.LogInformation("Created workspace directories at {TaskDir}", taskDir);

        var taskPacket = new TaskPacket
        {
            TaskId = task.Id,
            RunId = run.Id,
            Role = task.Role,
            Objective = "Execute the first Stewie worker runtime contract",
            Scope = "Read this task packet and produce a valid result packet",
            AllowedPaths = [],
            ForbiddenPaths = [],
            AcceptanceCriteria =
            [
                "Worker reads task.json",
                "Worker writes result.json",
                "Result can be ingested by Stewie"
            ]
        };

        var taskJsonPath = Path.Combine(inputDir, "task.json");
        var json = JsonSerializer.Serialize(taskPacket, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(taskJsonPath, json);

        _logger.LogInformation("Wrote task.json to {Path}", taskJsonPath);

        return taskDir;
    }

    /// <inheritdoc/>
    public ResultPacket ReadResult(WorkTask task)
    {
        var resultPath = Path.Combine(task.WorkspacePath, "output", "result.json");

        if (!File.Exists(resultPath))
        {
            throw new FileNotFoundException($"result.json not found at {resultPath}");
        }

        var json = File.ReadAllText(resultPath);
        _logger.LogInformation("Read result.json from {Path}", resultPath);

        return JsonSerializer.Deserialize<ResultPacket>(json)
            ?? throw new InvalidOperationException("Failed to deserialize result.json");
    }

    /// <inheritdoc/>
    public async Task CloneRepositoryAsync(string repoUrl, string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            throw new ArgumentException("Repository URL is required.", nameof(repoUrl));
        }

        var repoDir = Path.Combine(workspacePath, "repo");
        _logger.LogInformation("Cloning {RepoUrl} into {RepoDir}", repoUrl, repoDir);

        // Clean out the repo dir if it already has content (fresh clone)
        if (Directory.Exists(repoDir) && Directory.EnumerateFileSystemEntries(repoDir).Any())
        {
            Directory.Delete(repoDir, recursive: true);
            Directory.CreateDirectory(repoDir);
        }

        var exitCode = await RunGitCommandAsync(
            $"clone \"{repoUrl}\" \"{repoDir}\"",
            workspacePath);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"git clone failed with exit code {exitCode} for repo '{repoUrl}'");
        }

        _logger.LogInformation("Successfully cloned {RepoUrl} into {RepoDir}", repoUrl, repoDir);
    }

    /// <inheritdoc/>
    public async Task CreateBranchAsync(string workspacePath, string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new ArgumentException("Branch name is required.", nameof(branchName));
        }

        var repoDir = Path.Combine(workspacePath, "repo");

        if (!Directory.Exists(Path.Combine(repoDir, ".git")))
        {
            throw new InvalidOperationException(
                $"No git repository found at '{repoDir}'. Clone the repository first.");
        }

        _logger.LogInformation("Creating branch {BranchName} in {RepoDir}", branchName, repoDir);

        var exitCode = await RunGitCommandAsync(
            $"checkout -b \"{branchName}\"",
            repoDir);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"git checkout -b failed with exit code {exitCode} for branch '{branchName}'");
        }

        _logger.LogInformation("Successfully created branch {BranchName}", branchName);
    }

    /// <summary>
    /// Runs a git command via Process.Start and returns the exit code.
    /// Uses GIT_TERMINAL_PROMPT=0 to prevent interactive auth prompts from hanging.
    /// </summary>
    /// <param name="arguments">The git command arguments (without "git" prefix).</param>
    /// <param name="workingDirectory">The working directory for the git command.</param>
    /// <returns>The process exit code (0 = success).</returns>
    private async Task<int> RunGitCommandAsync(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Prevent interactive prompts per safe_commands workflow
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read output streams to prevent deadlocks
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git {Arguments} failed (exit {ExitCode}): {Stderr}",
                arguments, process.ExitCode, stderr);
        }
        else if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogDebug("git {Arguments} output: {Stdout}", arguments, stdout);
        }

        return process.ExitCode;
    }
}
