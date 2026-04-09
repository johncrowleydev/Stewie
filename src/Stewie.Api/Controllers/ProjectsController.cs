/// <summary>
/// Projects API controller — CRUD endpoints for Project entities.
/// Supports two creation modes: link an existing repo, or create a new one via the user's git platform PAT.
/// REF: CON-002 §4.1, §5.1 (v1.4.0)
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If project creation fails    → check POST action, validation, PAT lookup, or DB connectivity
/// 2. If projects list is empty    → check GetAllAsync or DB migration status
/// 3. If 404 on GET by ID          → check GetByIdAsync return value handling
/// 4. If create-mode fails         → check IGitPlatformService, encryption, or user credential
/// </summary>
using System.Security.Claims;
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
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IGitPlatformService _gitPlatformService;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProjectsController> _logger;

    /// <summary>Initializes the ProjectsController with required dependencies.</summary>
    public ProjectsController(
        IProjectRepository projectRepository,
        IUserCredentialRepository credentialRepository,
        IGitPlatformService gitPlatformService,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        ILogger<ProjectsController> logger)
    {
        _projectRepository = projectRepository;
        _credentialRepository = credentialRepository;
        _gitPlatformService = gitPlatformService;
        _encryptionService = encryptionService;
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
    /// Creates a new project. Supports two modes:
    /// - Link mode (default): provide name + repoUrl to link an existing repo.
    /// - Create mode (createRepo=true): provide name + repoName to provision a new repo via the user's PAT.
    /// </summary>
    /// <param name="request">The project creation request.</param>
    /// <returns>201 Created with the new project object per CON-002 §5.1.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Project name is required.");
        }

        if (request.CreateRepo)
        {
            return await CreateWithNewRepoAsync(request);
        }

        return await CreateWithLinkedRepoAsync(request);
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

        return Ok(ProjectResponse(project));
    }

    /// <summary>
    /// Link mode: validates repoUrl, auto-detects provider, persists project.
    /// Backward-compatible with the original POST /api/projects behavior.
    /// </summary>
    private async Task<IActionResult> CreateWithLinkedRepoAsync(CreateProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
        {
            throw new ArgumentException("Repository URL is required when not creating a new repo.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            RepoUrl = request.RepoUrl.Trim(),
            RepoProvider = DetectProvider(request.RepoUrl),
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _projectRepository.SaveAsync(project);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Created project {ProjectId} (link mode) with repo {RepoUrl}",
            project.Id, project.RepoUrl);

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, ProjectResponse(project));
    }

    /// <summary>
    /// Create mode: validates repoName, looks up PAT, provisions repo, persists project.
    /// If repo creation fails after project insert, rolls back by deleting the project.
    /// </summary>
    private async Task<IActionResult> CreateWithNewRepoAsync(CreateProjectRequest request)
    {
        // Validate create-mode fields
        if (string.IsNullOrWhiteSpace(request.RepoName))
        {
            throw new ArgumentException("Repository name is required when creating a new repo.");
        }

        if (!string.IsNullOrWhiteSpace(request.RepoUrl))
        {
            throw new ArgumentException("Cannot specify repoUrl when createRepo is true. Provide repoName instead.");
        }

        // Get current user ID from JWT
        var userId = GetCurrentUserId();

        // Look up PAT for the current platform
        var credential = await _credentialRepository.GetByUserAndProviderAsync(userId, _gitPlatformService.Provider);
        if (credential is null)
        {
            throw new ArgumentException(
                "GitHub PAT not configured. Visit Settings to connect your GitHub account.");
        }

        // Decrypt PAT
        var pat = _encryptionService.Decrypt(credential.EncryptedToken);

        // Create project entity first (Option A — rollback on failure)
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            RepoUrl = null, // Will be set after repo creation
            RepoProvider = _gitPlatformService.Provider,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _projectRepository.SaveAsync(project);
        await _unitOfWork.CommitAsync();

        try
        {
            // Create repo on platform
            _logger.LogInformation("Creating repo {RepoName} for project {ProjectId}",
                request.RepoName, project.Id);

            var cloneUrl = await _gitPlatformService.CreateRepositoryAsync(
                request.RepoName.Trim(),
                request.Description ?? $"Repository for {request.Name.Trim()}",
                request.IsPrivate,
                pat);

            // Update project with the new repo URL
            _unitOfWork.BeginTransaction();
            project.RepoUrl = cloneUrl;
            await _projectRepository.SaveAsync(project);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Created project {ProjectId} (create mode) with repo {RepoUrl}",
                project.Id, cloneUrl);

            return CreatedAtAction(nameof(GetById), new { id = project.Id }, ProjectResponse(project));
        }
        catch (Exception ex)
        {
            // Rollback: delete the project entity
            _logger.LogError(ex, "Repo creation failed for project {ProjectId}, rolling back", project.Id);

            try
            {
                _unitOfWork.BeginTransaction();
                await _projectRepository.DeleteAsync(project.Id);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback failed for project {ProjectId}", project.Id);
            }

            throw new InvalidOperationException(
                $"Failed to create repository '{request.RepoName}': {ex.Message}", ex);
        }
    }

    /// <summary>Extracts the current user's GUID from JWT claims.</summary>
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User identity not found in token.");
        return Guid.Parse(sub);
    }

    /// <summary>Auto-detects the git platform provider from a repository URL.</summary>
    private static string? DetectProvider(string? repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl)) return null;

        var lower = repoUrl.ToLowerInvariant();
        if (lower.Contains("github.com")) return "github";
        if (lower.Contains("gitlab.com")) return "gitlab";
        if (lower.Contains("bitbucket.org")) return "bitbucket";
        if (lower.Contains("dev.azure.com") || lower.Contains("visualstudio.com")) return "azure-devops";
        return null;
    }

    /// <summary>Builds the standard project response DTO per CON-002 §5.1.</summary>
    private static object ProjectResponse(Project p) => new
    {
        id = p.Id,
        name = p.Name,
        repoUrl = p.RepoUrl,
        repoProvider = p.RepoProvider,
        createdAt = p.CreatedAt.ToString("o")
    };
}

/// <summary>Request body for creating a new project. Supports link mode and create mode.</summary>
public class CreateProjectRequest
{
    /// <summary>Human-readable project name. Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Repository URL to link. Required when createRepo is false. Must be null when createRepo is true.</summary>
    public string? RepoUrl { get; set; }

    /// <summary>When true, create a new repo on the user's git platform instead of linking an existing one.</summary>
    public bool CreateRepo { get; set; }

    /// <summary>Name for the new repo. Required when createRepo is true.</summary>
    public string? RepoName { get; set; }

    /// <summary>Whether the new repo should be private. Default: true. Only used when createRepo is true.</summary>
    public bool IsPrivate { get; set; } = true;

    /// <summary>Description for the new repo. Only used when createRepo is true.</summary>
    public string? Description { get; set; }
}
