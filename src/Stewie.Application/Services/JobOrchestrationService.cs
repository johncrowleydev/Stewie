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
        string scriptWorkerImage = "stewie-script-worker")
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
        _scriptWorkerImage = scriptWorkerImage;
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

        var task = tasks[0]; // 1 Job = 1 Task for now

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

            // Update final statuses
            var isSuccess = string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase);
            task.Status = isSuccess ? WorkTaskStatus.Completed : WorkTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            job.Status = isSuccess ? JobStatus.Completed : JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;

            if (!isSuccess)
            {
                task.FailureReason = TaskFailureReason.WorkerReportedFailure.ToString();
            }

            await _workTaskRepository.SaveAsync(task);
            await _jobRepository.SaveAsync(job);

            if (isSuccess)
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskCompleted,
                    new { taskId = task.Id, summary = result.Summary });
                await EmitEventAsync("Job", job.Id, EventType.JobCompleted,
                    new { jobId = job.Id, summary = result.Summary, commitSha });
            }
            else
            {
                await EmitEventAsync("Task", task.Id, EventType.TaskFailed,
                    new { taskId = task.Id, reason = result.Summary, failureReason = task.FailureReason });
                await EmitEventAsync("Job", job.Id, EventType.JobFailed,
                    new { jobId = job.Id, reason = result.Summary, failureReason = task.FailureReason });
            }

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
