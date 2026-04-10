/// <summary>
/// Core orchestration service — executes jobs by creating tasks, launching containers,
/// and ingesting results. Emits audit trail events and tracks workspace lifecycle.
///
/// REF: BLU-001 §4, CON-001, CON-002 §3.1, §4.2
/// </summary>
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Application.Services;

/// <summary>
/// Orchestrates the execution of jobs: creates entities, clones repos, launches containers,
/// ingests results, captures diffs, auto-commits, emits audit events, and tracks workspace lifecycle.
/// </summary>
public partial class JobOrchestrationService
{
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IContainerService _containerService;
    private readonly IGitPlatformService _gitPlatformService;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobOrchestrationService> _logger;
    private readonly string _scriptWorkerImage;
    private readonly IGovernanceReportRepository _governanceReportRepository;
    private readonly ITaskDependencyRepository _taskDependencyRepository;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly string _governanceWorkerImage;
    private readonly int _maxGovernanceRetries;
    private readonly SemaphoreSlim _taskSemaphore;

    /// <summary>Initializes the orchestration service with all required dependencies.</summary>
    public JobOrchestrationService(
        IJobRepository jobRepository,
        IWorkTaskRepository workTaskRepository,
        IArtifactRepository artifactRepository,
        IEventRepository eventRepository,
        IWorkspaceRepository workspaceRepository,
        IProjectRepository projectRepository,
        IUserCredentialRepository credentialRepository,
        IWorkspaceService workspaceService,
        IContainerService containerService,
        IGitPlatformService gitPlatformService,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        ILogger<JobOrchestrationService> logger,
        IGovernanceReportRepository governanceReportRepository,
        ITaskDependencyRepository taskDependencyRepository,
        IRealTimeNotifier realTimeNotifier,
        string scriptWorkerImage = "stewie-script-worker",
        string governanceWorkerImage = "stewie-governance-worker",
        int maxGovernanceRetries = 2,
        int maxConcurrentTasks = 5)
    {
        _jobRepository = jobRepository;
        _workTaskRepository = workTaskRepository;
        _artifactRepository = artifactRepository;
        _eventRepository = eventRepository;
        _workspaceRepository = workspaceRepository;
        _projectRepository = projectRepository;
        _credentialRepository = credentialRepository;
        _workspaceService = workspaceService;
        _containerService = containerService;
        _gitPlatformService = gitPlatformService;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _governanceReportRepository = governanceReportRepository;
        _taskDependencyRepository = taskDependencyRepository;
        _realTimeNotifier = realTimeNotifier;
        _scriptWorkerImage = scriptWorkerImage;
        _governanceWorkerImage = governanceWorkerImage;
        _maxGovernanceRetries = maxGovernanceRetries;
        _taskSemaphore = new SemaphoreSlim(maxConcurrentTasks);
    }

    /// <summary>
    /// Executes a real job: clones repo, creates branch, launches script worker,
    /// captures diff, auto-commits results.
    /// </summary>
    /// <param name="jobId">The ID of a pre-created Job (from POST /api/jobs).</param>
    /// <returns>A <see cref="TestJobResult"/> describing the outcome.</returns>
    public async Task<TestJobResult> ExecuteJobAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        var tasks = await _workTaskRepository.GetByJobIdAsync(jobId);
        if (tasks.Count == 0)
        {
            throw new InvalidOperationException($"Job '{jobId}' has no tasks.");
        }

        // Delegate to multi-task path if >1 developer task
        var devTasks = tasks.Where(t => t.Role == "developer").ToList();
        if (devTasks.Count > 1)
        {
            return await ExecuteMultiTaskJobAsync(jobId);
        }

        var task = tasks[0]; // Single-task legacy path

        // Load project for repoUrl
        Project? project = null;
        if (job.ProjectId.HasValue)
        {
            project = await _projectRepository.GetByIdAsync(job.ProjectId.Value);
        }

        // Deserialize JSON fields from task
        List<string>? script = null;
        if (!string.IsNullOrWhiteSpace(task.ScriptJson))
        {
            script = JsonSerializer.Deserialize<List<string>>(task.ScriptJson);
        }

        List<string>? criteria = null;
        if (!string.IsNullOrWhiteSpace(task.AcceptanceCriteriaJson))
        {
            criteria = JsonSerializer.Deserialize<List<string>>(task.AcceptanceCriteriaJson);
        }

        // Generate branch name
        string? branchName = null;
        if (project?.RepoUrl is not null)
        {
            var shortId = job.Id.ToString()[..8];
            var sanitized = SanitizeBranchName(task.Objective ?? "task");
            branchName = $"stewie/{shortId}/{sanitized}";
        }

        _unitOfWork.BeginTransaction();
        await EmitEventAsync("Job", job.Id, EventType.JobCreated, new { jobId = job.Id });

        // Prepare workspace with full fields
        var workspacePath = _workspaceService.PrepareWorkspaceForRun(
            task, job, project?.RepoUrl, branchName, script, criteria);
        task.WorkspacePath = workspacePath;
        await _workTaskRepository.SaveAsync(task);

        // Track workspace entity
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Path = workspacePath,
            Status = WorkspaceStatus.Created,
            CreatedAt = DateTime.UtcNow
        };
        await _workspaceRepository.SaveAsync(workspace);

        await EmitEventAsync("Task", task.Id, EventType.TaskCreated,
            new { taskId = task.Id, jobId = job.Id, role = task.Role });

        // Clone repo and create branch if project has repoUrl
        if (!string.IsNullOrWhiteSpace(project?.RepoUrl))
        {
            _logger.LogInformation("Cloning repo {RepoUrl} for job {JobId}", project.RepoUrl, job.Id);
            await _workspaceService.CloneRepositoryAsync(project.RepoUrl, workspacePath);

            if (branchName is not null)
            {
                await _workspaceService.CreateBranchAsync(workspacePath, branchName);
                job.Branch = branchName;
                await _jobRepository.SaveAsync(job);
            }
        }

        // Transition to Running
        job.Status = JobStatus.Running;
        task.Status = WorkTaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        await _jobRepository.SaveAsync(job);
        await _workTaskRepository.SaveAsync(task);

        await EmitEventAsync("Job", job.Id, EventType.JobStarted,
            new { jobId = job.Id, taskCount = 1 });
        await EmitEventAsync("Task", task.Id, EventType.TaskStarted,
            new { taskId = task.Id, role = task.Role, workspacePath });

        workspace.Status = WorkspaceStatus.Mounted;
        workspace.MountedAt = DateTime.UtcNow;
        await _workspaceRepository.SaveAsync(workspace);

        await _unitOfWork.CommitAsync();
        _logger.LogInformation("Job {JobId} set to Running", job.Id);

        try
        {
            // Launch script worker container (writable repo mount) — with retry for transient failures
            var (exitCode, failureReason) = await LaunchWithRetryAsync(task, job.Id);

            if (exitCode != 0)
            {
                task.FailureReason = failureReason?.ToString();
                _logger.LogError("Script worker failed with exit code {ExitCode}, reason: {FailureReason}",
                    exitCode, failureReason);
                await MarkFailedAsync(job, task,
                    $"Script worker exited with code {exitCode} ({failureReason})");
                return new TestJobResult
                {
                    JobId = job.Id, TaskId = task.Id,
                    Status = "Failed",
                    Summary = $"Script worker exited with code {exitCode} ({failureReason})"
                };
            }

            // Read result — classify read failures
            Domain.Contracts.ResultPacket result;
            try
            {
                result = _workspaceService.ReadResult(task);
            }
            catch (FileNotFoundException)
            {
                task.FailureReason = TaskFailureReason.ResultMissing.ToString();
                await MarkFailedAsync(job, task, "result.json not found after successful container exit");
                return new TestJobResult
                {
                    JobId = job.Id, TaskId = task.Id,
                    Status = "Failed",
                    Summary = "result.json not found after successful container exit (ResultMissing)"
                };
            }
            catch (JsonException ex)
            {
                task.FailureReason = TaskFailureReason.ResultInvalid.ToString();
                await MarkFailedAsync(job, task, $"result.json deserialization failed: {ex.Message}");
                return new TestJobResult
                {
                    JobId = job.Id, TaskId = task.Id,
                    Status = "Failed",
                    Summary = $"result.json deserialization failed (ResultInvalid): {ex.Message}"
                };
            }

            _logger.LogInformation("Result: status={Status}, summary={Summary}",
                result.Status, result.Summary);

            _unitOfWork.BeginTransaction();

            // Store result artifact
            var resultArtifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                WorkTask = task,
                Type = "result",
                ContentJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTime.UtcNow
            };
            await _artifactRepository.SaveAsync(resultArtifact);

            // Capture diff (T-030)
            var diff = await _workspaceService.CaptureDiffAsync(workspacePath);
            if (!string.IsNullOrWhiteSpace(diff.DiffStat))
            {
                job.DiffSummary = diff.DiffStat;
                var diffArtifact = new Artifact
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    WorkTask = task,
                    Type = "diff",
                    ContentJson = JsonSerializer.Serialize(new
                    {
                        diffStat = diff.DiffStat,
                        diffPatch = diff.DiffPatch
                    }),
                    CreatedAt = DateTime.UtcNow
                };
                await _artifactRepository.SaveAsync(diffArtifact);
                _logger.LogInformation("Stored diff artifact for task {TaskId}", task.Id);
            }

            // Auto-commit (T-031)
            var objective = task.Objective ?? "stewie task";
            var shortJobId = job.Id.ToString()[..8];
            var commitMessage = $"feat(stewie): {objective} [Job {shortJobId}]";
            var commitSha = await _workspaceService.CommitChangesAsync(workspacePath, commitMessage);
            if (commitSha is not null)
            {
                job.CommitSha = commitSha;
                _logger.LogInformation("Auto-committed changes: {CommitSha}", commitSha);
            }

            // GitHub push + PR (T-041) — only if user has a PAT
            if (commitSha is not null && job.CreatedByUserId.HasValue && project?.RepoUrl is not null)
            {
                await TryGitHubPushAndPrAsync(job, task, project, workspacePath);
            }

            // Update dev task final status
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;

            if (!isSuccess)
            {
                task.FailureReason = TaskFailureReason.WorkerReportedFailure.ToString();
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
            }

            await _workTaskRepository.SaveAsync(task);
            await _jobRepository.SaveAsync(job);

            if (isSuccess)
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskCompleted,
                    new { taskId = task.Id, summary = result.Summary });

                // Dev task succeeded — enter governance cycle
                await _unitOfWork.CommitAsync();

                var governanceResult = await RunGovernanceCycleAsync(
                    job, task, workspacePath, project, task.AttemptNumber);

                return new TestJobResult
                {
                    JobId = job.Id,
                    TaskId = task.Id,
                    ArtifactId = resultArtifact.Id,
                    Status = job.Status.ToString(),
                    Summary = governanceResult,
                    ResultPayload = result
                };
            }
            else
            {
                // Dev task failed — mark job failed
                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = result.Summary, failureReason = task.FailureReason });
                await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                    new { jobId = job.Id, reason = result.Summary, failureReason = task.FailureReason });

                await _unitOfWork.CommitAsync();

                return new TestJobResult
                {
                    JobId = job.Id,
                    TaskId = task.Id,
                    ArtifactId = resultArtifact.Id,
                    Status = job.Status.ToString(),
                    Summary = result.Summary,
                    ResultPayload = result
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job execution for Job {JobId}", job.Id);
            task.FailureReason ??= TaskFailureReason.ContainerError.ToString();
            await MarkFailedAsync(job, task, ex.Message);
            return new TestJobResult
            {
                JobId = job.Id, TaskId = task.Id,
                Status = "Failed",
                Summary = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Executes a multi-task job using the DAG scheduler.
    /// Each task gets its own workspace, container launch, and governance cycle.
    /// REF: JOB-010 T-090
    /// </summary>
    /// <param name="jobId">The ID of a pre-created Job with multiple tasks.</param>
    /// <returns>A <see cref="TestJobResult"/> describing the aggregate outcome.</returns>
    public async Task<TestJobResult> ExecuteMultiTaskJobAsync(Guid jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        var tasks = await _workTaskRepository.GetByJobIdAsync(jobId);
        if (tasks.Count == 0)
        {
            throw new InvalidOperationException($"Job '{jobId}' has no tasks.");
        }

        // Load dependencies
        var deps = await _taskDependencyRepository.GetByJobIdAsync(jobId);

        // Build and validate graph
        var graph = TaskGraph.Build(tasks, deps);
        graph.ValidateAcyclic();

        // Load project
        Project? project = null;
        if (job.ProjectId.HasValue)
        {
            project = await _projectRepository.GetByIdAsync(job.ProjectId.Value);
        }

        // Set tasks with unmet dependencies to Blocked
        _unitOfWork.BeginTransaction();
        foreach (var task in tasks.Where(t => t.Role == "developer"))
        {
            var readyTasks = graph.GetReadyTasks();
            if (!readyTasks.Any(r => r.Id == task.Id) && task.Status == WorkTaskStatus.Pending)
            {
                task.Status = WorkTaskStatus.Blocked;
                await _workTaskRepository.SaveAsync(task);
            }
        }

        // Transition job to Running
        job.Status = JobStatus.Running;
        await _jobRepository.SaveAsync(job);
        await EmitEventAsync("Job", job.Id, EventType.JobStarted,
            new { jobId = job.Id, taskCount = tasks.Count(t => t.Role == "developer") });
        await _unitOfWork.CommitAsync();

        _logger.LogInformation(
            "Starting multi-task execution for job {JobId} with {TaskCount} developer tasks",
            job.Id, tasks.Count(t => t.Role == "developer"));

        // Enter scheduler loop
        await ScheduleTasksAsync(job, tasks, deps, project);

        // Re-load tasks to get final states
        tasks = await _workTaskRepository.GetByJobIdAsync(jobId);
        deps = await _taskDependencyRepository.GetByJobIdAsync(jobId);
        var finalGraph = TaskGraph.Build(tasks, deps);
        var aggregateStatus = finalGraph.GetAggregateStatus();

        // Update final job status
        _unitOfWork.BeginTransaction();
        job.Status = aggregateStatus;
        job.CompletedAt = DateTime.UtcNow;
        await _jobRepository.SaveAsync(job);

        var statusEvent = aggregateStatus == JobStatus.Completed
            ? EventType.JobCompleted
            : EventType.JobFailed;
        await EmitEventAsync("Job", job.Id, statusEvent,
            new { jobId = job.Id, status = aggregateStatus.ToString() });
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Multi-task job {JobId} completed with status {Status}",
            job.Id, aggregateStatus);

        var completedCount = tasks.Count(t => t.Status == WorkTaskStatus.Completed);
        var failedCount = tasks.Count(t => t.Status == WorkTaskStatus.Failed);

        return new TestJobResult
        {
            JobId = job.Id,
            TaskId = tasks.First(t => t.Role == "developer").Id,
            Status = aggregateStatus.ToString(),
            Summary = $"Multi-task job: {completedCount} completed, {failedCount} failed, " +
                      $"{tasks.Count(t => t.Status == WorkTaskStatus.Cancelled)} cancelled"
        };
    }

    /// <summary>
    /// Scheduler loop: poll ready tasks, launch in parallel, process completions, repeat.
    /// Exits when all tasks are in terminal state (Completed/Failed/Cancelled).
    /// REF: JOB-010 T-091
    /// </summary>
    private async Task ScheduleTasksAsync(Job job, IList<WorkTask> tasks,
        IList<TaskDependency> deps, Project? project)
    {
        while (true)
        {
            // Rebuild graph with current task states
            var graph = TaskGraph.Build(tasks, deps);

            if (graph.IsComplete)
            {
                _logger.LogInformation("All tasks for job {JobId} are in terminal state", job.Id);
                break;
            }

            var ready = graph.GetReadyTasks()
                .Where(t => t.Role == "developer")
                .ToList();

            if (ready.Count == 0)
            {
                // Check if any tasks are still running
                var hasRunning = tasks.Any(t => t.Status == WorkTaskStatus.Running);
                if (!hasRunning)
                {
                    // Deadlock: blocked tasks but nothing running — cancel remaining
                    _logger.LogWarning(
                        "Deadlock detected in job {JobId}: blocked tasks with no running tasks", job.Id);

                    _unitOfWork.BeginTransaction();
                    foreach (var blocked in tasks.Where(t =>
                        t.Status == WorkTaskStatus.Blocked || t.Status == WorkTaskStatus.Pending))
                    {
                        blocked.Status = WorkTaskStatus.Cancelled;
                        blocked.CompletedAt = DateTime.UtcNow;
                        await _workTaskRepository.SaveAsync(blocked);
                    }
                    await _unitOfWork.CommitAsync();
                    break;
                }

                // Tasks are running, wait briefly before re-checking
                await Task.Delay(100);
                // Re-read tasks to get updated states
                tasks = await _workTaskRepository.GetByJobIdAsync(job.Id);
                continue;
            }

            _logger.LogInformation(
                "Launching {ReadyCount} ready tasks for job {JobId}", ready.Count, job.Id);

            // Launch all ready tasks in parallel, bounded by semaphore
            var launchTasks = ready.Select(t =>
                ExecuteSingleTaskWithGovernanceAsync(job, t, project, tasks, deps));
            await Task.WhenAll(launchTasks);

            // Re-read tasks to get updated states after batch completes
            tasks = await _workTaskRepository.GetByJobIdAsync(job.Id);
            deps = await _taskDependencyRepository.GetByJobIdAsync(job.Id);
        }
    }

    /// <summary>
    /// Executes a single developer task within a multi-task job: workspace setup,
    /// container launch, result ingestion, governance cycle. Bounded by SemaphoreSlim.
    /// REF: JOB-010 T-090, T-092, T-101
    /// </summary>
    private async Task ExecuteSingleTaskWithGovernanceAsync(Job job, WorkTask task,
        Project? project, IList<WorkTask> allTasks, IList<TaskDependency> allDeps)
    {
        await _taskSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Executing task {TaskId} for job {JobId}", task.Id, job.Id);

            // Deserialize JSON fields
            List<string>? script = null;
            if (!string.IsNullOrWhiteSpace(task.ScriptJson))
            {
                script = JsonSerializer.Deserialize<List<string>>(task.ScriptJson);
            }

            List<string>? criteria = null;
            if (!string.IsNullOrWhiteSpace(task.AcceptanceCriteriaJson))
            {
                criteria = JsonSerializer.Deserialize<List<string>>(task.AcceptanceCriteriaJson);
            }

            // Generate branch name — use job-level branch, per-task suffix for multi-task
            string? branchName = null;
            if (project?.RepoUrl is not null)
            {
                var shortId = job.Id.ToString()[..8];
                var sanitized = SanitizeBranchName(task.Objective ?? "task");
                branchName = $"stewie/{shortId}/{sanitized}";
            }

            // Prepare per-task workspace
            var workspacePath = _workspaceService.PrepareWorkspaceForRun(
                task, job, project?.RepoUrl, branchName, script, criteria);
            task.WorkspacePath = workspacePath;

            _unitOfWork.BeginTransaction();
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

            await EmitEventAsync("Task", task.Id, EventType.TaskCreated,
                new { taskId = task.Id, jobId = job.Id, role = task.Role });

            // Clone repo if project has repoUrl
            if (!string.IsNullOrWhiteSpace(project?.RepoUrl))
            {
                await _workspaceService.CloneRepositoryAsync(project.RepoUrl, workspacePath);
                if (branchName is not null)
                {
                    await _workspaceService.CreateBranchAsync(workspacePath, branchName);
                }
            }

            // Transition to Running
            task.Status = WorkTaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;
            await _workTaskRepository.SaveAsync(task);

            workspace.Status = WorkspaceStatus.Mounted;
            workspace.MountedAt = DateTime.UtcNow;
            await _workspaceRepository.SaveAsync(workspace);

            await EmitEventAsync("Task", task.Id, EventType.TaskStarted,
                new { taskId = task.Id, role = task.Role, workspacePath });
            await _unitOfWork.CommitAsync();

            // Launch container
            var (exitCode, failureReason) = await LaunchWithRetryAsync(task, job.Id);

            if (exitCode != 0)
            {
                task.FailureReason = failureReason?.ToString();
                await MarkTaskFailedAsync(task,
                    $"Script worker exited with code {exitCode} ({failureReason})");

                // Cancel downstream tasks
                var graph = TaskGraph.Build(allTasks, allDeps);
                await CancelDownstreamTasksAsync(graph, task);
                return;
            }

            // Read result
            Domain.Contracts.ResultPacket result;
            try
            {
                result = _workspaceService.ReadResult(task);
            }
            catch (Exception ex)
            {
                task.FailureReason = TaskFailureReason.ResultMissing.ToString();
                await MarkTaskFailedAsync(task, $"result.json error: {ex.Message}");

                var graph = TaskGraph.Build(allTasks, allDeps);
                await CancelDownstreamTasksAsync(graph, task);
                return;
            }

            _unitOfWork.BeginTransaction();

            // Store result artifact
            var resultArtifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                WorkTask = task,
                Type = "result",
                ContentJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTime.UtcNow
            };
            await _artifactRepository.SaveAsync(resultArtifact);

            // Capture diff
            var diff = await _workspaceService.CaptureDiffAsync(workspacePath);
            if (!string.IsNullOrWhiteSpace(diff.DiffStat))
            {
                var diffArtifact = new Artifact
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    WorkTask = task,
                    Type = "diff",
                    ContentJson = JsonSerializer.Serialize(new
                    {
                        diffStat = diff.DiffStat,
                        diffPatch = diff.DiffPatch
                    }),
                    CreatedAt = DateTime.UtcNow
                };
                await _artifactRepository.SaveAsync(diffArtifact);
            }

            // Auto-commit
            var objective = task.Objective ?? "stewie task";
            var shortJobId = job.Id.ToString()[..8];
            var commitMessage = $"feat(stewie): {objective} [Job {shortJobId}]";
            await _workspaceService.CommitChangesAsync(workspacePath, commitMessage);

            // Update dev task final status
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            if (!isSuccess)
            {
                task.FailureReason = TaskFailureReason.WorkerReportedFailure.ToString();
            }
            await _workTaskRepository.SaveAsync(task);

            if (isSuccess)
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskCompleted,
                    new { taskId = task.Id, summary = result.Summary });
                await _unitOfWork.CommitAsync();

                // Per-task governance cycle (T-101)
                await RunGovernanceCycleAsync(job, task, workspacePath, project, task.AttemptNumber);
            }
            else
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = result.Summary });
                await _unitOfWork.CommitAsync();

                // Cancel downstream tasks on failure
                var graph = TaskGraph.Build(allTasks, allDeps);
                await CancelDownstreamTasksAsync(graph, task);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task {TaskId} for job {JobId}", task.Id, job.Id);
            task.FailureReason ??= TaskFailureReason.ContainerError.ToString();
            await MarkTaskFailedAsync(task, ex.Message);

            try
            {
                var graph = TaskGraph.Build(allTasks, allDeps);
                await CancelDownstreamTasksAsync(graph, task);
            }
            catch (Exception cascadeEx)
            {
                _logger.LogError(cascadeEx, "Failed to cascade cancellation for task {TaskId}", task.Id);
            }
        }
        finally
        {
            _taskSemaphore.Release();
        }
    }

    /// <summary>
    /// Cancels all transitive downstream tasks of a failed task.
    /// REF: JOB-010 T-094
    /// </summary>
    private async Task CancelDownstreamTasksAsync(TaskGraph graph, WorkTask failedTask)
    {
        var downstream = graph.GetAllDownstream(failedTask.Id);
        if (downstream.Count == 0) return;

        _logger.LogInformation(
            "Cancelling {Count} downstream tasks after failure of task {TaskId}",
            downstream.Count, failedTask.Id);

        _unitOfWork.BeginTransaction();
        foreach (var task in downstream)
        {
            if (task.Status is WorkTaskStatus.Pending or WorkTaskStatus.Blocked)
            {
                task.Status = WorkTaskStatus.Cancelled;
                task.CompletedAt = DateTime.UtcNow;
                task.FailureReason = $"Upstream task {failedTask.Id} failed";
                await _workTaskRepository.SaveAsync(task);

                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = $"Cancelled: upstream task {failedTask.Id} failed" });
            }
        }
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Executes a test job using the dummy worker. Backward-compatible Milestone 0 flow.
    /// </summary>
    public async Task<TestJobResult> ExecuteTestJobAsync()
    {
        // 1. Create Job
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _jobRepository.SaveAsync(job);
        await EmitEventAsync("Job", job.Id, EventType.JobCreated, new { jobId = job.Id });
        _logger.LogInformation("Created Job {JobId} with status {Status}", job.Id, job.Status);

        // 2. Create Task
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _workTaskRepository.SaveAsync(task);
        await EmitEventAsync("Task", task.Id, EventType.TaskCreated,
            new { taskId = task.Id, jobId = job.Id, role = task.Role });
        _logger.LogInformation("Created Task {TaskId} for Job {JobId}", task.Id, job.Id);

        // 3. Prepare workspace
        var workspacePath = _workspaceService.PrepareWorkspace(task, job);
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

        // 4. Running
        job.Status = JobStatus.Running;
        task.Status = WorkTaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        await _jobRepository.SaveAsync(job);
        await _workTaskRepository.SaveAsync(task);

        await EmitEventAsync("Job", job.Id, EventType.JobStarted,
            new { jobId = job.Id, taskCount = 1 });
        await EmitEventAsync("Task", task.Id, EventType.TaskStarted,
            new { taskId = task.Id, role = task.Role, workspacePath });

        workspace.Status = WorkspaceStatus.Mounted;
        workspace.MountedAt = DateTime.UtcNow;
        await _workspaceRepository.SaveAsync(workspace);

        await _unitOfWork.CommitAsync();

        try
        {
            // 5. Launch dummy worker
            var exitCode = await _containerService.LaunchWorkerAsync(task);

            if (exitCode != 0)
            {
                await MarkFailedAsync(job, task, $"Container exited with code {exitCode}");
                return new TestJobResult
                {
                    JobId = job.Id, TaskId = task.Id,
                    Status = "Failed",
                    Summary = $"Container exited with code {exitCode}"
                };
            }

            // 6. Read result
            var result = _workspaceService.ReadResult(task);

            // 7. Store artifact
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

            // 8. Final statuses
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            job.Status = isSuccess ? JobStatus.Completed : JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;

            await _workTaskRepository.SaveAsync(task);
            await _jobRepository.SaveAsync(job);

            if (isSuccess)
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskCompleted,
                    new { taskId = task.Id, summary = result.Summary });
                await EmitEventAsync("Job", job.Id, EventType.JobCompleted,
                    new { jobId = job.Id, summary = result.Summary });
            }
            else
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = result.Summary });
                await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                    new { jobId = job.Id, reason = result.Summary });
            }

            await _unitOfWork.CommitAsync();

            return new TestJobResult
            {
                JobId = job.Id, TaskId = task.Id, ArtifactId = artifact.Id,
                Status = job.Status.ToString(),
                Summary = result.Summary,
                ResultPayload = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test job for Job {JobId}", job.Id);
            await MarkFailedAsync(job, task, ex.Message);
            return new TestJobResult
            {
                JobId = job.Id, TaskId = task.Id,
                Status = "Failed",
                Summary = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Launches a worker container with retry for transient failures (Timeout, ContainerError).
    /// Retries exactly once. Permanent failures (WorkerCrash) are not retried.
    /// REF: JOB-005 T-052
    /// </summary>
    private async Task<(int ExitCode, TaskFailureReason? FailureReason)> LaunchWithRetryAsync(
        WorkTask task, Guid jobId)
    {
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation("Launching script worker for task {TaskId} (attempt {Attempt}/{MaxAttempts})",
                task.Id, attempt, maxAttempts);

            int exitCode;
            try
            {
                exitCode = await _containerService.LaunchWorkerAsync(task, _scriptWorkerImage);
            }
            catch (Exception ex)
            {
                // Docker daemon errors (image not found, socket error, etc.)
                var reason = TaskFailureReason.ContainerError;

                if (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex,
                        "Retrying task {TaskId} due to transient failure: {Reason} (attempt {Next}/{Max})",
                        task.Id, reason, attempt + 1, maxAttempts);
                    continue;
                }

                _logger.LogError(ex,
                    "Task {TaskId} failed after {MaxAttempts} attempts due to {Reason}",
                    task.Id, maxAttempts, reason);
                return (-1, reason);
            }

            if (exitCode == 0)
            {
                return (0, null); // Success
            }

            var failureReason = ClassifyContainerFailure(exitCode);

            // Only retry transient failures
            if (IsTransient(failureReason) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Retrying task {TaskId} due to transient failure: {Reason} (attempt {Next}/{Max})",
                    task.Id, failureReason, attempt + 1, maxAttempts);
                continue;
            }

            return (exitCode, failureReason);
        }

        // Should not reach here, but guard
        return (-1, TaskFailureReason.WorkerCrash);
    }

    /// <summary>Classifies a non-zero container exit code into the failure taxonomy.</summary>
    private static TaskFailureReason ClassifyContainerFailure(int exitCode) => exitCode switch
    {
        124 => TaskFailureReason.Timeout,
        125 or 126 or 127 => TaskFailureReason.ContainerError, // Docker run failures
        _ => TaskFailureReason.WorkerCrash
    };

    /// <summary>Returns true if the failure reason is transient and eligible for retry.</summary>
    private static bool IsTransient(TaskFailureReason reason) =>
        reason is TaskFailureReason.Timeout or TaskFailureReason.ContainerError;

    /// <summary>
    /// Runs the governance cycle after a dev task completes successfully.
    /// Spawns a tester task, launches governance container, ingests report,
    /// and decides: pass → complete, fail + retries left → retry, fail + no retries → fail.
    /// REF: JOB-007 T-069
    /// </summary>
    private async Task<string> RunGovernanceCycleAsync(
        Job job, WorkTask devTask, string workspacePath, Project? project, int currentAttempt)
    {
        _logger.LogInformation(
            "Starting governance cycle for job {JobId}, dev task {DevTaskId}, attempt {Attempt}",
            job.Id, devTask.Id, currentAttempt);

        _unitOfWork.BeginTransaction();

        // 1. Create tester task
        var testerTask = new WorkTask
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            Role = "tester",
            Status = WorkTaskStatus.Pending,
            ParentTaskId = devTask.Id,
            AttemptNumber = currentAttempt,
            Objective = "Run governance checks against developer output",
            Scope = "Validate code quality, testing, and security compliance",
            WorkspacePath = workspacePath,
            CreatedAt = DateTime.UtcNow
        };

        await _workTaskRepository.SaveAsync(testerTask);
        await EmitEventAsync("Task", testerTask.Id, EventType.TaskCreated,
            new { taskId = testerTask.Id, jobId = job.Id, role = "tester", parentTaskId = devTask.Id });
        await EmitEventAsync("Job", job.Id, EventType.GovernanceStarted,
            new { jobId = job.Id, testerTaskId = testerTask.Id, attempt = currentAttempt });

        // 2. Write governance task.json to the workspace
        var governanceTaskPacket = new Domain.Contracts.TaskPacket
        {
            TaskId = testerTask.Id,
            JobId = job.Id,
            Role = "tester",
            Objective = "Run governance checks against developer output",
            Scope = workspacePath,
            ParentTaskId = devTask.Id,
            AttemptNumber = currentAttempt,
            AllowedPaths = [],
            ForbiddenPaths = [],
            AcceptanceCriteria = []
        };
        _workspaceService.WriteTaskJson(workspacePath, governanceTaskPacket);

        // 3. Transition tester task to Running
        testerTask.Status = WorkTaskStatus.Running;
        testerTask.StartedAt = DateTime.UtcNow;
        await _workTaskRepository.SaveAsync(testerTask);
        await EmitEventAsync("Task", testerTask.Id, EventType.TaskStarted,
            new { taskId = testerTask.Id, role = "tester", workspacePath });

        await _unitOfWork.CommitAsync();

        // 4. Launch governance container
        try
        {
            var exitCode = await _containerService.LaunchWorkerAsync(testerTask, _governanceWorkerImage);

            if (exitCode != 0)
            {
                _logger.LogError("Governance worker failed with exit code {ExitCode}", exitCode);
                testerTask.FailureReason = TaskFailureReason.WorkerCrash.ToString();
                await MarkTaskFailedAsync(testerTask, $"Governance worker exited with code {exitCode}");
                await MarkJobFailedAsync(job, $"Governance worker crashed (exit code {exitCode})");
                return $"Governance worker failed (exit code {exitCode})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Governance container error for job {JobId}", job.Id);
            testerTask.FailureReason = TaskFailureReason.ContainerError.ToString();
            await MarkTaskFailedAsync(testerTask, $"Governance container error: {ex.Message}");
            await MarkJobFailedAsync(job, $"Governance container error: {ex.Message}");
            return $"Governance container error: {ex.Message}";
        }

        // 5. Ingest governance-report.json
        GovernanceReport report;
        try
        {
            report = await IngestGovernanceReportAsync(testerTask, workspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest governance report for job {JobId}", job.Id);
            testerTask.FailureReason = TaskFailureReason.ResultMissing.ToString();
            await MarkTaskFailedAsync(testerTask, $"Governance report error: {ex.Message}");
            await MarkJobFailedAsync(job, $"Governance report ingest failed: {ex.Message}");
            return $"Governance report error: {ex.Message}";
        }

        // 6. Evaluate verdict
        if (report.Passed)
        {
            _logger.LogInformation("Governance PASSED for job {JobId}", job.Id);

            _unitOfWork.BeginTransaction();

            testerTask.Status = WorkTaskStatus.Completed;
            testerTask.CompletedAt = DateTime.UtcNow;
            await _workTaskRepository.SaveAsync(testerTask);

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _jobRepository.SaveAsync(job);

            await EmitEventAsync("Task", testerTask.Id, EventType.TaskCompleted,
                new { taskId = testerTask.Id, summary = $"{report.PassedChecks}/{report.TotalChecks} checks passed" });
            await EmitEventAsync("Job", job.Id, EventType.GovernancePassed,
                new { jobId = job.Id, totalChecks = report.TotalChecks, passedChecks = report.PassedChecks });
            await EmitEventAsync("Job", job.Id, EventType.JobCompleted,
                new { jobId = job.Id, summary = $"Governance passed ({report.PassedChecks}/{report.TotalChecks})" });

            await _unitOfWork.CommitAsync();
            return $"Governance passed: {report.PassedChecks}/{report.TotalChecks} checks passed";
        }

        // Governance FAILED
        _logger.LogWarning("Governance FAILED for job {JobId}: {PassedChecks}/{TotalChecks}",
            job.Id, report.PassedChecks, report.TotalChecks);

        _unitOfWork.BeginTransaction();

        testerTask.Status = WorkTaskStatus.Completed;
        testerTask.CompletedAt = DateTime.UtcNow;
        await _workTaskRepository.SaveAsync(testerTask);

        await EmitEventAsync("Task", testerTask.Id, EventType.TaskCompleted,
            new { taskId = testerTask.Id, summary = $"{report.FailedChecks} checks failed" });

        if (currentAttempt < _maxGovernanceRetries)
        {
            // Retry: spawn new dev task with violation feedback
            await EmitEventAsync("Job", job.Id, EventType.GovernanceRetry,
                new { jobId = job.Id, attempt = currentAttempt, nextAttempt = currentAttempt + 1,
                      failedChecks = report.FailedChecks });

            await _unitOfWork.CommitAsync();

            return await SpawnRetryDevTaskAsync(job, devTask, report, workspacePath, project, currentAttempt + 1);
        }
        else
        {
            // Max retries exhausted — fail the job
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            await _jobRepository.SaveAsync(job);

            await EmitEventAsync("Job", job.Id, EventType.GovernanceFailed,
                new { jobId = job.Id, totalAttempts = currentAttempt, failedChecks = report.FailedChecks });
            await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                new { jobId = job.Id, reason = $"Governance failed after {currentAttempt} attempts",
                      failureReason = TaskFailureReason.GovernanceFailed.ToString() });

            await _unitOfWork.CommitAsync();
            return $"Governance failed after {currentAttempt} attempts: {report.FailedChecks}/{report.TotalChecks} checks failed";
        }
    }

    /// <summary>
    /// Reads governance-report.json, creates GovernanceReport entity, persists it.
    /// </summary>
    private async Task<GovernanceReport> IngestGovernanceReportAsync(WorkTask testerTask, string workspacePath)
    {
        var reportPacket = _workspaceService.ReadGovernanceReport(workspacePath);

        var report = new GovernanceReport
        {
            Id = Guid.NewGuid(),
            TaskId = testerTask.Id,
            Passed = string.Equals(reportPacket.Status, "pass", StringComparison.OrdinalIgnoreCase),
            TotalChecks = reportPacket.TotalChecks,
            PassedChecks = reportPacket.PassedChecks,
            FailedChecks = reportPacket.FailedChecks,
            CheckResultsJson = JsonSerializer.Serialize(reportPacket.Checks),
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _governanceReportRepository.SaveAsync(report);

        // Store raw governance report as artifact
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TaskId = testerTask.Id,
            WorkTask = testerTask,
            Type = "governance-report",
            ContentJson = JsonSerializer.Serialize(reportPacket),
            CreatedAt = DateTime.UtcNow
        };
        await _artifactRepository.SaveAsync(artifact);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Ingested governance report for task {TaskId}: passed={Passed}, {Passed}/{Total}",
            testerTask.Id, report.Passed, report.PassedChecks, report.TotalChecks);

        return report;
    }

    /// <summary>
    /// Spawns a new dev task with governance violation feedback for retry.
    /// Re-enters the ExecuteJobAsync flow with the new dev task.
    /// </summary>
    private async Task<string> SpawnRetryDevTaskAsync(
        Job job, WorkTask originalDevTask, GovernanceReport report,
        string workspacePath, Project? project, int nextAttempt)
    {
        _logger.LogInformation("Spawning retry dev task for job {JobId}, attempt {Attempt}",
            job.Id, nextAttempt);

        // Extract violations from failed checks
        var failedChecks = JsonSerializer.Deserialize<List<Domain.Contracts.GovernanceCheckResult>>(
            report.CheckResultsJson) ?? [];
        var violations = failedChecks
            .Where(c => !c.Passed && string.Equals(c.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .Select(c => new Domain.Contracts.GovernanceViolation
            {
                RuleId = c.RuleId,
                RuleName = c.RuleName,
                Details = c.Details ?? string.Empty
            })
            .ToList();

        var violationsJson = JsonSerializer.Serialize(violations);

        _unitOfWork.BeginTransaction();

        // Create retry dev task
        var retryTask = new WorkTask
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            ParentTaskId = originalDevTask.Id,
            AttemptNumber = nextAttempt,
            Objective = originalDevTask.Objective,
            Scope = originalDevTask.Scope,
            ScriptJson = originalDevTask.ScriptJson,
            AcceptanceCriteriaJson = originalDevTask.AcceptanceCriteriaJson,
            GovernanceViolationsJson = violationsJson,
            WorkspacePath = workspacePath,
            CreatedAt = DateTime.UtcNow
        };

        await _workTaskRepository.SaveAsync(retryTask);

        // Write updated task.json with violation feedback
        List<string>? script = null;
        if (!string.IsNullOrWhiteSpace(retryTask.ScriptJson))
        {
            script = JsonSerializer.Deserialize<List<string>>(retryTask.ScriptJson);
        }

        var retryPacket = new Domain.Contracts.TaskPacket
        {
            TaskId = retryTask.Id,
            JobId = job.Id,
            Role = "developer",
            Objective = retryTask.Objective ?? string.Empty,
            Scope = retryTask.Scope ?? string.Empty,
            ParentTaskId = originalDevTask.Id,
            AttemptNumber = nextAttempt,
            GovernanceViolations = violations,
            Script = script,
            AllowedPaths = [],
            ForbiddenPaths = [],
            AcceptanceCriteria = []
        };
        _workspaceService.WriteTaskJson(workspacePath, retryPacket);

        await EmitEventAsync("Task", retryTask.Id, EventType.TaskCreated,
            new { taskId = retryTask.Id, jobId = job.Id, role = "developer",
                  attempt = nextAttempt, violationCount = violations.Count });

        // Transition to Running
        retryTask.Status = WorkTaskStatus.Running;
        retryTask.StartedAt = DateTime.UtcNow;
        await _workTaskRepository.SaveAsync(retryTask);

        await EmitEventAsync("Task", retryTask.Id, EventType.TaskStarted,
            new { taskId = retryTask.Id, role = "developer", workspacePath, attempt = nextAttempt });

        await _unitOfWork.CommitAsync();

        // Launch the script worker for the retry task
        try
        {
            var (exitCode, failureReason) = await LaunchWithRetryAsync(retryTask, job.Id);

            if (exitCode != 0)
            {
                retryTask.FailureReason = failureReason?.ToString();
                await MarkTaskFailedAsync(retryTask,
                    $"Retry script worker exited with code {exitCode} ({failureReason})");
                await MarkJobFailedAsync(job,
                    $"Retry dev task failed (attempt {nextAttempt}): exit code {exitCode}");
                return $"Retry dev task failed (exit code {exitCode})";
            }

            // Read result
            Domain.Contracts.ResultPacket result;
            try
            {
                result = _workspaceService.ReadResult(retryTask);
            }
            catch (Exception ex)
            {
                retryTask.FailureReason = TaskFailureReason.ResultMissing.ToString();
                await MarkTaskFailedAsync(retryTask, $"result.json error: {ex.Message}");
                await MarkJobFailedAsync(job, $"Retry result ingest failed: {ex.Message}");
                return $"Retry result.json error: {ex.Message}";
            }

            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);

            _unitOfWork.BeginTransaction();

            retryTask.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            retryTask.CompletedAt = DateTime.UtcNow;
            if (!isSuccess)
            {
                retryTask.FailureReason = TaskFailureReason.WorkerReportedFailure.ToString();
            }
            await _workTaskRepository.SaveAsync(retryTask);

            if (isSuccess)
            {
                await EmitEventAsync("Task", retryTask.Id, EventType.TaskCompleted,
                    new { taskId = retryTask.Id, summary = result.Summary });
                await _unitOfWork.CommitAsync();

                // Re-enter governance cycle
                return await RunGovernanceCycleAsync(job, retryTask, workspacePath, project, nextAttempt);
            }
            else
            {
                // Retry dev task failed
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                await _jobRepository.SaveAsync(job);

                await EmitEventAsync("Task", retryTask.Id, EventType.TaskFailed,
                    new { taskId = retryTask.Id, reason = result.Summary });
                await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                    new { jobId = job.Id, reason = $"Retry dev task failed (attempt {nextAttempt})" });

                await _unitOfWork.CommitAsync();
                return $"Retry dev task failed (attempt {nextAttempt}): {result.Summary}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during retry dev task for Job {JobId}", job.Id);
            retryTask.FailureReason ??= TaskFailureReason.ContainerError.ToString();
            await MarkTaskFailedAsync(retryTask, ex.Message);
            await MarkJobFailedAsync(job, $"Retry error: {ex.Message}");
            return $"Retry error: {ex.Message}";
        }
    }

    /// <summary>Marks a single task as failed without affecting the job.</summary>
    private async Task MarkTaskFailedAsync(WorkTask task, string reason)
    {
        try
        {
            _unitOfWork.BeginTransaction();
            task.Status = WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            await _workTaskRepository.SaveAsync(task);
            await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                new { taskId = task.Id, reason, failureReason = task.FailureReason });
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark task {TaskId} as failed: {Reason}", task.Id, reason);
        }
    }

    /// <summary>Marks a job as failed without affecting any specific task.</summary>
    private async Task MarkJobFailedAsync(Job job, string reason)
    {
        try
        {
            _unitOfWork.BeginTransaction();
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            await _jobRepository.SaveAsync(job);
            await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                new { jobId = job.Id, reason });
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark job {JobId} as failed: {Reason}", job.Id, reason);
        }
    }

    /// <summary>Marks a job and task as failed, emitting failure events.</summary>
    private async Task MarkFailedAsync(Job job, WorkTask task, string reason)
    {
        try
        {
            _unitOfWork.BeginTransaction();
            task.Status = WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            await _workTaskRepository.SaveAsync(task);
            await _jobRepository.SaveAsync(job);

            await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                new { taskId = task.Id, reason, failureReason = task.FailureReason });
            await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                new { jobId = job.Id, reason, failureReason = task.FailureReason });

            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark job/task as failed: {Reason}", reason);
        }
    }

    /// <summary>Emits an audit trail event within the current transaction.</summary>
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

        // Push real-time notification to connected clients
        await PushRealTimeNotificationAsync(entityType, entityId, eventType);
    }

    /// <summary>
    /// Pushes a real-time notification via SignalR for the given event.
    /// Failures are swallowed and logged — never breaks orchestration.
    /// REF: JOB-012 T-124
    /// </summary>
    private async Task PushRealTimeNotificationAsync(string entityType, Guid entityId, EventType eventType)
    {
        try
        {
            var statusString = eventType.ToString();
            if (entityType == "Job")
            {
                var job = await _jobRepository.GetByIdAsync(entityId);
                await _realTimeNotifier.NotifyJobUpdatedAsync(job?.ProjectId, entityId, statusString);
            }
            else if (entityType == "Task")
            {
                var task = await _workTaskRepository.GetByIdAsync(entityId);
                if (task != null)
                    await _realTimeNotifier.NotifyTaskUpdatedAsync(task.JobId, entityId, statusString);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push real-time notification for {EntityType} {EntityId}",
                entityType, entityId);
        }
    }

    /// <summary>
    /// Pushes the branch and creates a PR if the user has a GitHub PAT stored.
    /// Fails silently — GitHub integration is best-effort, not blocking.
    /// </summary>
    private async Task TryGitHubPushAndPrAsync(Job job, WorkTask task, Project project, string workspacePath)
    {
        try
        {
            var credential = await _credentialRepository.GetByUserAndProviderAsync(
                job.CreatedByUserId!.Value, "github");

            if (credential is null)
            {
                _logger.LogInformation("No GitHub PAT for user {UserId}, skipping push/PR",
                    job.CreatedByUserId);
                return;
            }

            var pat = _encryptionService.Decrypt(credential.EncryptedToken);

            // Push branch
            if (job.Branch is not null)
            {
                await _gitPlatformService.PushBranchAsync(workspacePath, project.RepoUrl, job.Branch, pat);

                // Parse owner/repo from URL for PR creation
                var (owner, repo) = ParseOwnerRepo(project.RepoUrl);
                if (owner is not null && repo is not null)
                {
                    var prTitle = task.Objective ?? "Stewie automated changes";
                    var prBody = $"**Job:** `{job.Id}`\n\n**Objective:** {task.Objective}\n\n**Diff Summary:**\n```\n{job.DiffSummary ?? "No changes"}\n```";
                    var prUrl = await _gitPlatformService.CreatePullRequestAsync(
                        owner, repo, job.Branch, prTitle, prBody, pat);

                    job.PullRequestUrl = prUrl;
                    await _jobRepository.SaveAsync(job);
                    _logger.LogInformation("Created PR: {PrUrl}", prUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub push/PR failed for job {JobId} — continuing", job.Id);
        }
    }

    /// <summary>Parses owner and repo from a GitHub URL.</summary>
    private static (string? Owner, string? Repo) ParseOwnerRepo(string repoUrl)
    {
        try
        {
            var uri = new Uri(repoUrl.TrimEnd('/'));
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
            {
                var repo = segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1];
                return (segments[0], repo);
            }
        }
        catch { /* best-effort parsing */ }
        return (null, null);
    }

    /// <summary>
    /// Sanitizes a string for use as a git branch name segment.
    /// Replaces spaces/special chars with hyphens, lowercases, truncates to 50 chars.
    /// </summary>
    private static string SanitizeBranchName(string input)
    {
        var sanitized = BranchNameRegex().Replace(input.ToLowerInvariant(), "-").Trim('-');
        return sanitized.Length > 50 ? sanitized[..50].TrimEnd('-') : sanitized;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex BranchNameRegex();
}

/// <summary>Result DTO returned by ExecuteTestJobAsync and ExecuteJobAsync.</summary>
public class TestJobResult
{
    /// <summary>The job's unique identifier.</summary>
    public Guid JobId { get; set; }

    /// <summary>The task's unique identifier.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The artifact ID, if one was created.</summary>
    public Guid? ArtifactId { get; set; }

    /// <summary>Human-readable status string.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Summary of the job outcome.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The full result payload from the worker.</summary>
    public object? ResultPayload { get; set; }
}
