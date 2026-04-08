using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Services;

public class RunOrchestrationService
{
    private readonly IRunRepository _runRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IContainerService _containerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RunOrchestrationService> _logger;

    public RunOrchestrationService(
        IRunRepository runRepository,
        IWorkTaskRepository workTaskRepository,
        IArtifactRepository artifactRepository,
        IWorkspaceService workspaceService,
        IContainerService containerService,
        IUnitOfWork unitOfWork,
        ILogger<RunOrchestrationService> logger)
    {
        _runRepository = runRepository;
        _workTaskRepository = workTaskRepository;
        _artifactRepository = artifactRepository;
        _workspaceService = workspaceService;
        _containerService = containerService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TestRunResult> ExecuteTestRunAsync()
    {
        // 1. Create Run
        var run = new Run
        {
            Id = Guid.NewGuid(),
            Status = RunStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _runRepository.SaveAsync(run);
        _logger.LogInformation("Created Run {RunId} with status {Status}", run.Id, run.Status);

        // 2. Create Task
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Run = run,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _workTaskRepository.SaveAsync(task);
        _logger.LogInformation("Created Task {TaskId} for Run {RunId}", task.Id, run.Id);

        // 3. Prepare workspace & write task.json
        var workspacePath = _workspaceService.PrepareWorkspace(task, run);
        task.WorkspacePath = workspacePath;
        await _workTaskRepository.SaveAsync(task);
        _logger.LogInformation("Prepared workspace at {WorkspacePath}", workspacePath);

        // 4. Update statuses to Running
        run.Status = RunStatus.Running;
        task.Status = WorkTaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        await _runRepository.SaveAsync(run);
        await _workTaskRepository.SaveAsync(task);
        await _unitOfWork.CommitAsync();
        _logger.LogInformation("Run and Task set to Running");

        try
        {
            // 5. Launch container
            _logger.LogInformation("Launching worker container for Task {TaskId}", task.Id);
            var exitCode = await _containerService.LaunchWorkerAsync(task);

            if (exitCode != 0)
            {
                _logger.LogError("Container exited with non-zero code {ExitCode}", exitCode);
                await MarkFailedAsync(run, task, $"Container exited with code {exitCode}");
                return new TestRunResult
                {
                    RunId = run.Id,
                    TaskId = task.Id,
                    Status = "Failed",
                    Summary = $"Container exited with code {exitCode}"
                };
            }

            // 6. Read result
            var result = _workspaceService.ReadResult(task);
            _logger.LogInformation("Ingested result: status={Status}, summary={Summary}",
                result.Status, result.Summary);

            // 7. Create artifact
            _unitOfWork.BeginTransaction();

            var artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                WorkTask = task,
                Type = "result",
                ContentJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTime.UtcNow
            };

            await _artifactRepository.SaveAsync(artifact);
            _logger.LogInformation("Stored Artifact {ArtifactId} for Task {TaskId}", artifact.Id, task.Id);

            // 8. Update final statuses
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            run.Status = isSuccess ? RunStatus.Completed : RunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;

            await _workTaskRepository.SaveAsync(task);
            await _runRepository.SaveAsync(run);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Run {RunId} completed with status {Status}", run.Id, run.Status);

            return new TestRunResult
            {
                RunId = run.Id,
                TaskId = task.Id,
                ArtifactId = artifact.Id,
                Status = run.Status.ToString(),
                Summary = result.Summary,
                ResultPayload = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during task execution for Run {RunId}", run.Id);
            await MarkFailedAsync(run, task, ex.Message);
            return new TestRunResult
            {
                RunId = run.Id,
                TaskId = task.Id,
                Status = "Failed",
                Summary = $"Error: {ex.Message}"
            };
        }
    }

    private async Task MarkFailedAsync(Run run, WorkTask task, string reason)
    {
        try
        {
            _unitOfWork.BeginTransaction();
            task.Status = WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            run.Status = RunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            await _workTaskRepository.SaveAsync(task);
            await _runRepository.SaveAsync(run);
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark run/task as failed: {Reason}", reason);
        }
    }
}

public class TestRunResult
{
    public Guid RunId { get; set; }
    public Guid TaskId { get; set; }
    public Guid? ArtifactId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public object? ResultPayload { get; set; }
}
