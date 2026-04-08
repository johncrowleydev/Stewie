using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly string _workspaceRoot;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(string workspaceRoot, ILogger<WorkspaceService> logger)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _logger = logger;
    }

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
}
