/// <summary>
/// Core orchestration service — executes runs by creating tasks, launching containers,
/// and ingesting results. Emits audit trail events and tracks workspace lifecycle.
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If events not emitted       → check EmitEventAsync calls and IEventRepository
/// 2. If workspace not tracked    → check Workspace entity creation in step 3
/// 3. If run stuck in Running     → check MarkFailedAsync and container exit code handling
///
/// REF: BLU-001 §4, CON-001, CON-002 §3.1
/// </summary>
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Services;

/// <summary>
/// Orchestrates the execution of runs: creates entities, launches containers,
/// ingests results, emits audit events, and tracks workspace lifecycle.
/// </summary>
public class RunOrchestrationService
{
    private readonly IRunRepository _runRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IContainerService _containerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RunOrchestrationService> _logger;

    /// <summary>
    /// Initializes the orchestration service with all required dependencies.
    /// </summary>
    public RunOrchestrationService(
        IRunRepository runRepository,
        IWorkTaskRepository workTaskRepository,
        IArtifactRepository artifactRepository,
        IEventRepository eventRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceService workspaceService,
        IContainerService containerService,
        IUnitOfWork unitOfWork,
        ILogger<RunOrchestrationService> logger)
    {
        _runRepository = runRepository;
        _workTaskRepository = workTaskRepository;
        _artifactRepository = artifactRepository;
        _eventRepository = eventRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceService = workspaceService;
        _containerService = containerService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Executes a test run: creates Run + Task, prepares workspace, launches container,
    /// ingests result, emits events for all state transitions, and tracks workspace lifecycle.
    /// </summary>
    /// <returns>A <see cref="TestRunResult"/> describing the outcome.</returns>
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
        await EmitEventAsync("Run", run.Id, EventType.RunCreated,
            new { runId = run.Id });
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
        await EmitEventAsync("Task", task.Id, EventType.TaskCreated,
            new { taskId = task.Id, runId = run.Id, role = task.Role });
        _logger.LogInformation("Created Task {TaskId} for Run {RunId}", task.Id, run.Id);

        // 3. Prepare workspace & write task.json, then track workspace entity
        var workspacePath = _workspaceService.PrepareWorkspace(task, run);
        task.WorkspacePath = workspacePath;
        await _workTaskRepository.SaveAsync(task);

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Path = workspacePath,
            Status = WorkspaceStatus.Created,
            CreatedAt = DateTime.UtcNow
        };
        await _workspaceRepository.SaveAsync(workspace);
        _logger.LogInformation("Prepared workspace at {WorkspacePath}, tracked as {WorkspaceId}",
            workspacePath, workspace.Id);

        // 4. Update statuses to Running — emit events for both Run and Task
        run.Status = RunStatus.Running;
        task.Status = WorkTaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        await _runRepository.SaveAsync(run);
        await _workTaskRepository.SaveAsync(task);

        await EmitEventAsync("Run", run.Id, EventType.RunStarted,
            new { runId = run.Id, taskCount = 1 });
        await EmitEventAsync("Task", task.Id, EventType.TaskStarted,
            new { taskId = task.Id, role = task.Role, workspacePath });

        // Update workspace to Mounted status
        workspace.Status = WorkspaceStatus.Mounted;
        workspace.MountedAt = DateTime.UtcNow;
        await _workspaceRepository.SaveAsync(workspace);

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

            // 8. Update final statuses and emit completion/failure events
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            run.Status = isSuccess ? RunStatus.Completed : RunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;

            await _workTaskRepository.SaveAsync(task);
            await _runRepository.SaveAsync(run);

            if (isSuccess)
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskCompleted,
                    new { taskId = task.Id, summary = result.Summary });
                await EmitEventAsync("Run", run.Id, EventType.RunCompleted,
                    new { runId = run.Id, summary = result.Summary });
            }
            else
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = result.Summary });
                await EmitEventAsync("Run", run.Id, EventType.RunFailed,
                    new { runId = run.Id, reason = result.Summary });
            }

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

    /// <summary>
    /// Marks a run and task as failed, emitting failure events for both.
    /// FAILURE MODE: If this method itself fails, the error is logged but not propagated
    /// to avoid masking the original failure.
    /// </summary>
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

            await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                new { taskId = task.Id, reason });
            await EmitEventAsync("Run", run.Id, EventType.RunFailed,
                new { runId = run.Id, reason });

            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark run/task as failed: {Reason}", reason);
        }
    }

    /// <summary>
    /// Emits an audit trail event. Serializes the payload to JSON.
    /// This is an internal helper — events are always emitted within an existing transaction.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g. "Run", "Task").</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="eventType">The classification of the event.</param>
    /// <param name="payload">An object to serialize as the event payload.</param>
    private async Task EmitEventAsync(string entityType, Guid entityId, EventType eventType, object payload)
    {
        var eventRecord = new Event
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            Timestamp = DateTime.UtcNow
        };

        await _eventRepository.SaveAsync(eventRecord);
        _logger.LogDebug("Emitted {EventType} for {EntityType} {EntityId}",
            eventType, entityType, entityId);
    }
}

/// <summary>Result DTO returned by ExecuteTestRunAsync.</summary>
public class TestRunResult
{
    /// <summary>The run's unique identifier.</summary>
    public Guid RunId { get; set; }

    /// <summary>The task's unique identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The artifact ID, if one was created.</summary>
    public Guid? ArtifactId { get; set; }

    /// <summary>Human-readable status string.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Summary of the run outcome.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The full result payload from the worker.</summary>
    public object? ResultPayload { get; set; }
}
