/// <summary>
/// Workspace filesystem service — manages workspace directories, task.json I/O,
/// git repository operations (clone, branch, diff, commit).
///
/// REF: BLU-001 §3.2, CON-001 §3, §4, CON-002 §5.6
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

    /// <summary>Initializes the workspace service.</summary>
    public WorkspaceService(string workspaceRoot, ILogger<WorkspaceService> logger)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string PrepareWorkspace(WorkTask task, Job job)
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
            JobId = job.Id,
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

        WriteTaskJson(taskDir, taskPacket);
        return taskDir;
    }

    /// <inheritdoc/>
    public string PrepareWorkspaceForRun(WorkTask task, Job job, string? repoUrl,
        string? branch, List<string>? script, List<string>? acceptanceCriteria)
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
            JobId = job.Id,
            Role = task.Role,
            Objective = task.Objective ?? string.Empty,
            Scope = task.Scope ?? string.Empty,
            AllowedPaths = [],
            ForbiddenPaths = [],
            AcceptanceCriteria = acceptanceCriteria ?? [],
            RepoUrl = repoUrl,
            Branch = branch,
            Script = script
        };

        WriteTaskJson(taskDir, taskPacket);
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

        if (Directory.Exists(repoDir) && Directory.EnumerateFileSystemEntries(repoDir).Any())
        {
            Directory.Delete(repoDir, recursive: true);
            Directory.CreateDirectory(repoDir);
        }

        var exitCode = await RunGitCommandAsync($"clone \"{repoUrl}\" \"{repoDir}\"", workspacePath);

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

        var exitCode = await RunGitCommandAsync($"checkout -b \"{branchName}\"", repoDir);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"git checkout -b failed with exit code {exitCode} for branch '{branchName}'");
        }

        _logger.LogInformation("Successfully created branch {BranchName}", branchName);
    }

    /// <inheritdoc/>
    public async Task<DiffResult> CaptureDiffAsync(string workspacePath)
    {
        var repoDir = Path.Combine(workspacePath, "repo");

        if (!Directory.Exists(Path.Combine(repoDir, ".git")))
        {
            _logger.LogWarning("No git repo at {RepoDir}, returning empty diff", repoDir);
            return new DiffResult();
        }

        _logger.LogInformation("Capturing diff in {RepoDir}", repoDir);

        var (statExit, statOutput) = await RunGitCommandWithOutputAsync("diff --stat", repoDir);
        var (patchExit, patchOutput) = await RunGitCommandWithOutputAsync("diff", repoDir);

        return new DiffResult
        {
            DiffStat = statExit == 0 ? statOutput : string.Empty,
            DiffPatch = patchExit == 0 ? patchOutput : string.Empty
        };
    }

    /// <inheritdoc/>
    public async Task<string?> CommitChangesAsync(string workspacePath, string message)
    {
        var repoDir = Path.Combine(workspacePath, "repo");

        if (!Directory.Exists(Path.Combine(repoDir, ".git")))
        {
            _logger.LogWarning("No git repo at {RepoDir}, nothing to commit", repoDir);
            return null;
        }

        // Configure git user for the commit
        await RunGitCommandAsync("config user.email \"stewie@stewie.dev\"", repoDir);
        await RunGitCommandAsync("config user.name \"Stewie\"", repoDir);

        // Stage all changes
        var addExit = await RunGitCommandAsync("add -A", repoDir);
        if (addExit != 0)
        {
            _logger.LogWarning("git add -A failed with exit code {ExitCode}", addExit);
            return null;
        }

        // Check if there's anything to commit
        var (statusExit, statusOutput) = await RunGitCommandWithOutputAsync("status --porcelain", repoDir);
        if (statusExit != 0 || string.IsNullOrWhiteSpace(statusOutput))
        {
            _logger.LogInformation("Nothing to commit in {RepoDir}", repoDir);
            return null;
        }

        // Commit
        var commitExit = await RunGitCommandAsync($"commit -m \"{message}\"", repoDir);
        if (commitExit != 0)
        {
            _logger.LogWarning("git commit failed with exit code {ExitCode}", commitExit);
            return null;
        }

        // Get commit SHA
        var (shaExit, sha) = await RunGitCommandWithOutputAsync("rev-parse HEAD", repoDir);
        if (shaExit != 0)
        {
            _logger.LogWarning("Failed to get commit SHA");
            return null;
        }

        var commitSha = sha.Trim();
        _logger.LogInformation("Committed changes in {RepoDir}: {CommitSha}", repoDir, commitSha);
        return commitSha;
    }

    /// <summary>Writes task.json to the workspace input directory.</summary>
    private void WriteTaskJson(string taskDir, TaskPacket taskPacket)
    {
        var inputDir = Path.Combine(taskDir, "input");
        var taskJsonPath = Path.Combine(inputDir, "task.json");
        var json = JsonSerializer.Serialize(taskPacket, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(taskJsonPath, json);
        _logger.LogInformation("Wrote task.json to {Path}", taskJsonPath);
    }

    /// <summary>Runs a git command and returns the exit code.</summary>
    private async Task<int> RunGitCommandAsync(string arguments, string workingDirectory)
    {
        var (exitCode, _) = await RunGitCommandWithOutputAsync(arguments, workingDirectory);
        return exitCode;
    }

    /// <summary>Runs a git command and returns both exit code and stdout.</summary>
    private async Task<(int ExitCode, string Output)> RunGitCommandWithOutputAsync(
        string arguments, string workingDirectory)
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

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git {Arguments} failed (exit {ExitCode}): {Stderr}",
                arguments, process.ExitCode, stderr);
        }

        return (process.ExitCode, stdout);
    }
}
