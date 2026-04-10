/// <summary>
/// Jobs API controller — CRUD endpoints, test-job trigger, and real job execution.
/// REF: CON-002 §4.2, §5.2, JOB-010 T-095
/// </summary>
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes endpoints for managing Jobs — the top-level execution units.
/// Combines the existing test-job trigger with full CRUD and real job execution.
/// </summary>
[ApiController]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly JobOrchestrationService _orchestrationService;
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskDependencyRepository _taskDependencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobsController> _logger;

    /// <summary>Initializes the JobsController with required dependencies.</summary>
    public JobsController(
        JobOrchestrationService orchestrationService,
        IJobRepository jobRepository,
        IWorkTaskRepository workTaskRepository,
        IProjectRepository projectRepository,
        ITaskDependencyRepository taskDependencyRepository,
        IUnitOfWork unitOfWork,
        ILogger<JobsController> logger)
    {
        _orchestrationService = orchestrationService;
        _jobRepository = jobRepository;
        _workTaskRepository = workTaskRepository;
        _projectRepository = projectRepository;
        _taskDependencyRepository = taskDependencyRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a test job using the dummy worker container.
    /// Backward-compatible Milestone 0 endpoint.
    /// </summary>
    /// <returns>200 OK with test job result per CON-002 §3.1.</returns>
    [HttpPost("jobs/test")]
    public async Task<IActionResult> TriggerTestJob()
    {
        _logger.LogInformation("Test job triggered");
        var result = await _orchestrationService.ExecuteTestJobAsync();
        _logger.LogInformation("Test job completed: {Status}", result.Status);
        return Ok(result);
    }

    /// <summary>
    /// Lists all jobs, optionally filtered by projectId query parameter.
    /// </summary>
    /// <param name="projectId">Optional project ID to filter jobs.</param>
    /// <returns>200 OK with array of job objects per CON-002 §5.2.</returns>
    [HttpGet("api/jobs")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId = null)
    {
        _logger.LogInformation("Listing jobs, projectId filter: {ProjectId}", projectId);

        IList<Job> jobs;

        if (projectId.HasValue)
        {
            jobs = await _jobRepository.GetByProjectIdAsync(projectId.Value);
        }
        else
        {
            jobs = await _jobRepository.GetAllAsync();
        }

        var response = jobs.Select(j => new
        {
            id = j.Id,
            projectId = j.ProjectId,
            status = j.Status.ToString(),
            branch = j.Branch,
            diffSummary = j.DiffSummary,
            commitSha = j.CommitSha,
            pullRequestUrl = j.PullRequestUrl,
            createdAt = j.CreatedAt.ToString("o"),
            completedAt = j.CompletedAt?.ToString("o")
        });

        return Ok(response);
    }

    /// <summary>
    /// Creates a new job with task definition(s), validates project, and triggers execution.
    /// Supports both legacy single-task and multi-task DAG requests.
    /// REF: CON-002 v1.7.0, JOB-010 T-095
    /// </summary>
    /// <param name="request">The job creation request.</param>
    /// <returns>201 Created with the job, then execution proceeds asynchronously.</returns>
    [HttpPost("api/jobs")]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request)
    {
        // Validate required fields
        if (!request.ProjectId.HasValue)
        {
            throw new ArgumentException("projectId is required.");
        }

        // Route: multi-task or single-task
        if (request.Tasks is { Count: > 0 })
        {
            return await CreateMultiTaskJob(request);
        }

        if (!string.IsNullOrWhiteSpace(request.Objective))
        {
            return await CreateSingleTaskJob(request);
        }

        throw new ArgumentException("Either 'objective' (single-task) or 'tasks' (multi-task) is required.");
    }

    /// <summary>Creates a single-task job (legacy backward-compatible path).</summary>
    private async Task<IActionResult> CreateSingleTaskJob(CreateJobRequest request)
    {
        // Validate project exists
        var project = await _projectRepository.GetByIdAsync(request.ProjectId!.Value);
        if (project is null)
        {
            throw new KeyNotFoundException($"Project with ID '{request.ProjectId.Value}' was not found.");
        }

        // Serialize optional array fields to JSON for storage
        string? scriptJson = request.Script is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Script)
            : null;
        string? criteriaJson = request.AcceptanceCriteria is { Count: > 0 }
            ? JsonSerializer.Serialize(request.AcceptanceCriteria)
            : null;

        // Get current user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        Guid? createdByUserId = userIdClaim is not null ? Guid.Parse(userIdClaim) : null;

        // Create Job
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Status = JobStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Create Task with provided fields
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            Objective = request.Objective!.Trim(),
            Scope = request.Scope?.Trim(),
            ScriptJson = scriptJson,
            AcceptanceCriteriaJson = criteriaJson,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _jobRepository.SaveAsync(job);
        await _workTaskRepository.SaveAsync(task);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created job {JobId} with task {TaskId} for project {ProjectId}",
            job.Id, task.Id, job.ProjectId);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, new
        {
            id = job.Id,
            projectId = job.ProjectId,
            status = job.Status.ToString(),
            branch = job.Branch,
            diffSummary = job.DiffSummary,
            commitSha = job.CommitSha,
            pullRequestUrl = job.PullRequestUrl,
            createdAt = job.CreatedAt.ToString("o"),
            completedAt = (string?)null,
            tasks = new[]
            {
                new
                {
                    id = task.Id,
                    jobId = task.JobId,
                    role = task.Role,
                    status = task.Status.ToString(),
                    objective = task.Objective,
                    scope = task.Scope,
                    workspacePath = task.WorkspacePath,
                    createdAt = task.CreatedAt.ToString("o"),
                    startedAt = (string?)null,
                    completedAt = (string?)null
                }
            }
        });
    }

    /// <summary>
    /// Creates a multi-task DAG job. Maps client-side dependency references to
    /// server-side TaskDependency entities and validates the graph is acyclic.
    /// REF: JOB-010 T-095
    /// </summary>
    private async Task<IActionResult> CreateMultiTaskJob(CreateJobRequest request)
    {
        // Validate project exists
        var project = await _projectRepository.GetByIdAsync(request.ProjectId!.Value);
        if (project is null)
        {
            throw new KeyNotFoundException($"Project with ID '{request.ProjectId.Value}' was not found.");
        }

        // Get current user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        Guid? createdByUserId = userIdClaim is not null ? Guid.Parse(userIdClaim) : null;

        // Create Job
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Status = JobStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Map clientId → server-side Guid for dependency resolution
        var clientIdToTaskId = new Dictionary<string, Guid>();
        var createdTasks = new List<WorkTask>();

        foreach (var taskDef in request.Tasks!)
        {
            var taskId = Guid.NewGuid();
            var clientId = taskDef.ClientId ?? taskId.ToString();
            clientIdToTaskId[clientId] = taskId;

            string? scriptJson = taskDef.Script is { Count: > 0 }
                ? JsonSerializer.Serialize(taskDef.Script)
                : null;
            string? criteriaJson = taskDef.AcceptanceCriteria is { Count: > 0 }
                ? JsonSerializer.Serialize(taskDef.AcceptanceCriteria)
                : null;

            var task = new WorkTask
            {
                Id = taskId,
                JobId = job.Id,
                Job = job,
                Role = "developer",
                Status = WorkTaskStatus.Pending,
                Objective = taskDef.Objective.Trim(),
                Scope = taskDef.Scope?.Trim(),
                ScriptJson = scriptJson,
                AcceptanceCriteriaJson = criteriaJson,
                WorkspacePath = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            createdTasks.Add(task);
        }

        // Resolve dependency edges
        var dependencies = new List<TaskDependency>();
        for (int i = 0; i < request.Tasks!.Count; i++)
        {
            var taskDef = request.Tasks[i];
            if (taskDef.DependsOn is not { Count: > 0 }) continue;

            var taskId = createdTasks[i].Id;
            foreach (var depClientId in taskDef.DependsOn)
            {
                if (!clientIdToTaskId.TryGetValue(depClientId, out var depTaskId))
                {
                    throw new ArgumentException(
                        $"Invalid dependency reference '{depClientId}' in task '{taskDef.ClientId ?? taskId.ToString()}'.");
                }

                dependencies.Add(new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    DependsOnTaskId = depTaskId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Validate DAG before persisting
        var graph = TaskGraph.Build(createdTasks, dependencies);
        try
        {
            graph.ValidateAcyclic();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException($"Dependency cycle detected: {ex.Message}");
        }

        // Persist
        _unitOfWork.BeginTransaction();
        await _jobRepository.SaveAsync(job);
        foreach (var task in createdTasks)
        {
            await _workTaskRepository.SaveAsync(task);
        }
        foreach (var dep in dependencies)
        {
            await _taskDependencyRepository.SaveAsync(dep);
        }
        await _unitOfWork.CommitAsync();

        _logger.LogInformation(
            "Created multi-task job {JobId} with {TaskCount} tasks and {DepCount} dependencies",
            job.Id, createdTasks.Count, dependencies.Count);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, new
        {
            id = job.Id,
            projectId = job.ProjectId,
            status = job.Status.ToString(),
            createdAt = job.CreatedAt.ToString("o"),
            completedAt = (string?)null,
            taskCount = createdTasks.Count,
            tasks = createdTasks.Select(t => new
            {
                id = t.Id,
                jobId = t.JobId,
                role = t.Role,
                status = t.Status.ToString(),
                objective = t.Objective,
                scope = t.Scope,
                createdAt = t.CreatedAt.ToString("o")
            })
        });
    }

    /// <summary>
    /// Gets a single job by ID, including its nested tasks.
    /// </summary>
    /// <param name="id">The job's GUID.</param>
    /// <returns>200 OK with job + tasks per CON-002 §5.2, or 404 if not found.</returns>
    [HttpGet("api/jobs/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Getting job {JobId}", id);

        var job = await _jobRepository.GetByIdAsync(id);

        if (job is null)
        {
            throw new KeyNotFoundException($"Job with ID '{id}' was not found.");
        }

        var tasks = await _workTaskRepository.GetByJobIdAsync(id);

        return Ok(new
        {
            id = job.Id,
            projectId = job.ProjectId,
            status = job.Status.ToString(),
            branch = job.Branch,
            diffSummary = job.DiffSummary,
            commitSha = job.CommitSha,
            pullRequestUrl = job.PullRequestUrl,
            createdAt = job.CreatedAt.ToString("o"),
            completedAt = job.CompletedAt?.ToString("o"),
            taskCount = tasks.Count(t => t.Role == "developer"),
            completedTaskCount = tasks.Count(t => t.Role == "developer" && t.Status == WorkTaskStatus.Completed),
            failedTaskCount = tasks.Count(t => t.Role == "developer" && t.Status == WorkTaskStatus.Failed),
            tasks = tasks.OrderBy(t => t.CreatedAt).Select(t => new
            {
                id = t.Id,
                jobId = t.JobId,
                parentTaskId = t.ParentTaskId,
                attemptNumber = t.AttemptNumber,
                role = t.Role,
                status = t.Status.ToString(),
                objective = t.Objective,
                scope = t.Scope,
                workspacePath = t.WorkspacePath,
                failureReason = t.FailureReason,
                createdAt = t.CreatedAt.ToString("o"),
                startedAt = t.StartedAt?.ToString("o"),
                completedAt = t.CompletedAt?.ToString("o")
            })
        });
    }
}

/// <summary>
/// Request body for creating a new job. Supports both single-task (legacy) and multi-task (DAG) modes.
/// REF: CON-002 v1.7.0, JOB-010 T-095
/// </summary>
public class CreateJobRequest
{
    /// <summary>Project ID to associate this job with. Required.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>What the worker should accomplish. Required for single-task jobs.</summary>
    public string? Objective { get; set; }

    /// <summary>Boundaries of the work. Optional (single-task only).</summary>
    public string? Scope { get; set; }

    /// <summary>Bash commands for script worker. Optional (single-task only).</summary>
    public List<string>? Script { get; set; }

    /// <summary>Conditions that must be met for success. Optional (single-task only).</summary>
    public List<string>? AcceptanceCriteria { get; set; }

    /// <summary>Task definitions for multi-task DAG jobs. Mutually exclusive with Objective.</summary>
    public List<TaskDefinition>? Tasks { get; set; }
}

/// <summary>
/// Defines a single task within a multi-task job creation request.
/// REF: JOB-010 T-095
/// </summary>
public class TaskDefinition
{
    /// <summary>Client-generated identifier for cross-referencing in DependsOn arrays.</summary>
    public string? ClientId { get; set; }

    /// <summary>What the worker should accomplish. Required.</summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>Boundaries of the work. Optional.</summary>
    public string? Scope { get; set; }

    /// <summary>Bash commands for script worker. Optional.</summary>
    public List<string>? Script { get; set; }

    /// <summary>Conditions that must be met for success. Optional.</summary>
    public List<string>? AcceptanceCriteria { get; set; }

    /// <summary>ClientIds of upstream tasks that must complete before this task can execute.</summary>
    public List<string>? DependsOn { get; set; }
}
