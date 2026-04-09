/// <summary>
/// Tasks API controller — endpoints for querying WorkTask entities.
/// REF: CON-002 §4.3, §5.3
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If task detail missing artifacts → check GetByTaskIdAsync in ArtifactRepository
/// 2. If 404 on task lookup           → check GetByIdAsync return value
/// 3. If tasks for job are empty      → check GetByJobIdAsync filter logic
/// </summary>
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes read-only endpoints for WorkTask entities.
/// Tasks are created by the orchestration service, not directly via API.
/// </summary>
[ApiController]
public class TasksController : ControllerBase
{
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ILogger<TasksController> _logger;

    /// <summary>Initializes the TasksController with required dependencies.</summary>
    public TasksController(
        IWorkTaskRepository workTaskRepository,
        IArtifactRepository artifactRepository,
        ILogger<TasksController> logger)
    {
        _workTaskRepository = workTaskRepository;
        _artifactRepository = artifactRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets a single task by ID, including its artifacts.
    /// </summary>
    /// <param name="id">The task's GUID.</param>
    /// <returns>200 OK with task + artifacts per CON-002 §5.3, or 404 if not found.</returns>
    [HttpGet("api/tasks/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Getting task {TaskId}", id);

        var task = await _workTaskRepository.GetByIdAsync(id);

        if (task is null)
        {
            throw new KeyNotFoundException($"Task with ID '{id}' was not found.");
        }

        var artifacts = await _artifactRepository.GetByTaskIdAsync(id);

        return Ok(new
        {
            id = task.Id,
            jobId = task.JobId,
            role = task.Role,
            status = task.Status.ToString(),
            workspacePath = task.WorkspacePath,
            createdAt = task.CreatedAt.ToString("o"),
            startedAt = task.StartedAt?.ToString("o"),
            completedAt = task.CompletedAt?.ToString("o"),
            artifacts = artifacts.Select(a => new
            {
                id = a.Id,
                type = a.Type,
                createdAt = a.CreatedAt.ToString("o")
            })
        });
    }

    /// <summary>
    /// Lists all tasks for a specific job.
    /// </summary>
    /// <param name="jobId">The job's GUID.</param>
    /// <returns>200 OK with array of task objects per CON-002 §5.3.</returns>
    [HttpGet("api/jobs/{jobId:guid}/tasks")]
    public async Task<IActionResult> GetByJobId(Guid jobId)
    {
        _logger.LogInformation("Listing tasks for job {JobId}", jobId);

        var tasks = await _workTaskRepository.GetByJobIdAsync(jobId);

        var response = tasks.Select(t => new
        {
            id = t.Id,
            jobId = t.JobId,
            role = t.Role,
            status = t.Status.ToString(),
            workspacePath = t.WorkspacePath,
            createdAt = t.CreatedAt.ToString("o"),
            startedAt = t.StartedAt?.ToString("o"),
            completedAt = t.CompletedAt?.ToString("o")
        });

        return Ok(response);
    }
}
