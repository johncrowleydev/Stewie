/// <summary>
/// Projects API controller — CRUD endpoints for Project entities.
/// REF: CON-002 §4.1, §5.1
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If project creation fails    → check POST action, validation, or DB connectivity
/// 2. If projects list is empty    → check GetAllAsync or DB migration status
/// 3. If 404 on GET by ID          → check GetByIdAsync return value handling
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes CRUD endpoints for managing Projects.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProjectsController> _logger;

    /// <summary>Initializes the ProjectsController with required dependencies.</summary>
    public ProjectsController(
        IProjectRepository projectRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProjectsController> logger)
    {
        _projectRepository = projectRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Lists all projects, ordered by creation time descending.
    /// </summary>
    /// <returns>200 OK with array of project objects per CON-002 §5.1.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("Listing all projects");

        var projects = await _projectRepository.GetAllAsync();

        var response = projects.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            repoUrl = p.RepoUrl,
            repoProvider = p.RepoProvider,
            createdAt = p.CreatedAt.ToString("o")
        });

        return Ok(response);
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    /// <param name="request">The project creation request containing name and repoUrl.</param>
    /// <returns>201 Created with the new project object per CON-002 §5.1.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        // Guard: validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Project name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepoUrl))
        {
            throw new ArgumentException("Repository URL is required.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            RepoUrl = request.RepoUrl.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _projectRepository.SaveAsync(project);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created project {ProjectId} with name {Name}",
            project.Id, project.Name);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, new
        {
            id = project.Id,
            name = project.Name,
            repoUrl = project.RepoUrl,
            repoProvider = project.RepoProvider,
            createdAt = project.CreatedAt.ToString("o")
        });
    }

    /// <summary>
    /// Gets a single project by ID.
    /// </summary>
    /// <param name="id">The project's GUID.</param>
    /// <returns>200 OK with the project object, or 404 if not found.</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        _logger.LogInformation("Getting project {ProjectId}", id);

        var project = await _projectRepository.GetByIdAsync(id);

        if (project is null)
        {
            throw new KeyNotFoundException($"Project with ID '{id}' was not found.");
        }

        return Ok(new
        {
            id = project.Id,
            name = project.Name,
            repoUrl = project.RepoUrl,
            repoProvider = project.RepoProvider,
            createdAt = project.CreatedAt.ToString("o")
        });
    }
}

/// <summary>Request body for creating a new project.</summary>
public class CreateProjectRequest
{
    /// <summary>Human-readable project name. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Repository URL associated with this project. Required.</summary>
    public string RepoUrl { get; set; } = string.Empty;
}
