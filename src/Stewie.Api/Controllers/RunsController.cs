/// <summary>
/// Runs API controller — CRUD endpoints and the existing test-run trigger.
/// REF: CON-002 §4.2, §5.2
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If test run fails            → check ExecuteTestRunAsync in RunOrchestrationService
/// 2. If runs list is empty        → check GetAllAsync or DB migration status
/// 3. If tasks missing in detail   → check GetByRunIdAsync in WorkTaskRepository
/// </summary>
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes endpoints for managing Runs — the top-level execution units.
/// Combines the existing test-run trigger with new CRUD endpoints.
/// </summary>
[ApiController]
public class RunsController : ControllerBase
{
    private readonly RunOrchestrationService _orchestrationService;
    private readonly IRunRepository _runRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RunsController> _logger;

    /// <summary>Initializes the RunsController with required dependencies.</summary>
    public RunsController(
        RunOrchestrationService orchestrationService,
        IRunRepository runRepository,
        IWorkTaskRepository workTaskRepository,
        IUnitOfWork unitOfWork,
        ILogger<RunsController> logger)
    {
        _orchestrationService = orchestrationService;
        _runRepository = runRepository;
        _workTaskRepository = workTaskRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a test run using the dummy worker container.
    /// This is the original Milestone 0 endpoint — preserved for backward compatibility.
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
            createdAt = r.CreatedAt.ToString("o"),
            completedAt = r.CompletedAt?.ToString("o")
        });

        return Ok(response);
    }

    /// <summary>
    /// Creates a new run, optionally associated with a project.
    /// </summary>
    /// <param name="request">The run creation request with optional projectId.</param>
    /// <returns>201 Created with the new run object per CON-002 §5.2.</returns>
    [HttpPost("api/runs")]
    public async Task<IActionResult> Create([FromBody] CreateRunRequest request)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Status = RunStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _runRepository.SaveAsync(run);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created run {RunId} for project {ProjectId}",
            run.Id, run.ProjectId);

        return CreatedAtAction(nameof(GetById), new { id = run.Id }, new
        {
            id = run.Id,
            projectId = run.ProjectId,
            status = run.Status.ToString(),
            createdAt = run.CreatedAt.ToString("o"),
            completedAt = (string?)null,
            tasks = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Gets a single run by ID, including its nested tasks.
    /// </summary>
    /// <param name="id">The run's GUID.</param>
    /// <returns>200 OK with run + tasks, or 404 if not found.</returns>
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
            createdAt = run.CreatedAt.ToString("o"),
            completedAt = run.CompletedAt?.ToString("o"),
            tasks = tasks.Select(t => new
            {
                id = t.Id,
                runId = t.RunId,
                role = t.Role,
                status = t.Status.ToString(),
                workspacePath = t.WorkspacePath,
                createdAt = t.CreatedAt.ToString("o"),
                startedAt = t.StartedAt?.ToString("o"),
                completedAt = t.CompletedAt?.ToString("o")
            })
        });
    }
}

/// <summary>Request body for creating a new run.</summary>
public class CreateRunRequest
{
    /// <summary>Optional project ID to associate this run with.</summary>
    public Guid? ProjectId { get; set; }
}
