/// <summary>
/// Runs API controller — CRUD endpoints, test-run trigger, and real run execution.
/// REF: CON-002 §4.2, §5.2
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
/// Exposes endpoints for managing Runs — the top-level execution units.
/// Combines the existing test-run trigger with full CRUD and real run execution.
/// </summary>
[ApiController]
[Authorize]
public class RunsController : ControllerBase
{
    private readonly RunOrchestrationService _orchestrationService;
    private readonly IRunRepository _runRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RunsController> _logger;

    /// <summary>Initializes the RunsController with required dependencies.</summary>
    public RunsController(
        RunOrchestrationService orchestrationService,
        IRunRepository runRepository,
        IWorkTaskRepository workTaskRepository,
        IProjectRepository projectRepository,
        IUnitOfWork unitOfWork,
        ILogger<RunsController> logger)
    {
        _orchestrationService = orchestrationService;
        _runRepository = runRepository;
        _workTaskRepository = workTaskRepository;
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a test run using the dummy worker container.
    /// Backward-compatible Milestone 0 endpoint.
    /// </summary>
    /// <returns>200 OK with test run result per CON-002 §3.1.</returns>
    [HttpPost("runs/test")]
    public async Task<IActionResult> TriggerTestRun()
    {
        _logger.LogInformation("Test run triggered");
        var result = await _orchestrationService.ExecuteTestRunAsync();
        _logger.LogInformation("Test run completed: {Status}", result.Status);
        return Ok(result);
    }

    /// <summary>
    /// Lists all runs, optionally filtered by projectId query parameter.
    /// </summary>
    /// <param name="projectId">Optional project ID to filter runs.</param>
    /// <returns>200 OK with array of run objects per CON-002 §5.2.</returns>
    [HttpGet("api/runs")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId = null)
    {
        _logger.LogInformation("Listing runs, projectId filter: {ProjectId}", projectId);

        IList<Run> runs;

        if (projectId.HasValue)
        {
            runs = await _runRepository.GetByProjectIdAsync(projectId.Value);
        }
        else
        {
            runs = await _runRepository.GetAllAsync();
        }

        var response = runs.Select(r => new
        {
            id = r.Id,
            projectId = r.ProjectId,
            status = r.Status.ToString(),
            branch = r.Branch,
            diffSummary = r.DiffSummary,
            commitSha = r.CommitSha,
            pullRequestUrl = r.PullRequestUrl,
            createdAt = r.CreatedAt.ToString("o"),
            completedAt = r.CompletedAt?.ToString("o")
        });

        return Ok(response);
    }

    /// <summary>
    /// Creates a new run with task definition, validates project, and triggers execution.
    /// Per CON-002 v1.2.0: projectId and objective are required.
    /// </summary>
    /// <param name="request">The run creation request.</param>
    /// <returns>201 Created with the run, then execution proceeds asynchronously.</returns>
    [HttpPost("api/runs")]
    public async Task<IActionResult> Create([FromBody] CreateRunRequest request)
    {
        // Validate required fields
        if (!request.ProjectId.HasValue)
        {
            throw new ArgumentException("projectId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Objective))
        {
            throw new ArgumentException("objective is required.");
        }

        // Validate project exists
        var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value);
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

        // Create Run
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Status = RunStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Create Task with provided fields
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Run = run,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            Objective = request.Objective.Trim(),
            Scope = request.Scope?.Trim(),
            ScriptJson = scriptJson,
            AcceptanceCriteriaJson = criteriaJson,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _runRepository.SaveAsync(run);
        await _workTaskRepository.SaveAsync(task);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created run {RunId} with task {TaskId} for project {ProjectId}",
            run.Id, task.Id, run.ProjectId);

        return CreatedAtAction(nameof(GetById), new { id = run.Id }, new
        {
            id = run.Id,
            projectId = run.ProjectId,
            status = run.Status.ToString(),
            branch = run.Branch,
            diffSummary = run.DiffSummary,
            commitSha = run.CommitSha,
            pullRequestUrl = run.PullRequestUrl,
            createdAt = run.CreatedAt.ToString("o"),
            completedAt = (string?)null,
            tasks = new[]
            {
                new
                {
                    id = task.Id,
                    runId = task.RunId,
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
    /// Gets a single run by ID, including its nested tasks.
    /// </summary>
    /// <param name="id">The run's GUID.</param>
    /// <returns>200 OK with run + tasks per CON-002 §5.2, or 404 if not found.</returns>
    [HttpGet("api/runs/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Getting run {RunId}", id);

        var run = await _runRepository.GetByIdAsync(id);

        if (run is null)
        {
            throw new KeyNotFoundException($"Run with ID '{id}' was not found.");
        }

        var tasks = await _workTaskRepository.GetByRunIdAsync(id);

        return Ok(new
        {
            id = run.Id,
            projectId = run.ProjectId,
            status = run.Status.ToString(),
            branch = run.Branch,
            diffSummary = run.DiffSummary,
            commitSha = run.CommitSha,
            pullRequestUrl = run.PullRequestUrl,
            createdAt = run.CreatedAt.ToString("o"),
            completedAt = run.CompletedAt?.ToString("o"),
            tasks = tasks.Select(t => new
            {
                id = t.Id,
                runId = t.RunId,
                role = t.Role,
                status = t.Status.ToString(),
                objective = t.Objective,
                scope = t.Scope,
                workspacePath = t.WorkspacePath,
                createdAt = t.CreatedAt.ToString("o"),
                startedAt = t.StartedAt?.ToString("o"),
                completedAt = t.CompletedAt?.ToString("o")
            })
        });
    }
}

/// <summary>Request body for creating a new run per CON-002 §4.2.</summary>
public class CreateRunRequest
{
    /// <summary>Project ID to associate this run with. Required.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>What the worker should accomplish. Required.</summary>
    public string? Objective { get; set; }

    /// <summary>Boundaries of the work. Optional.</summary>
    public string? Scope { get; set; }

    /// <summary>Bash commands for script worker. Optional.</summary>
    public List<string>? Script { get; set; }

    /// <summary>Conditions that must be met for success. Optional.</summary>
    public List<string>? AcceptanceCriteria { get; set; }
}
