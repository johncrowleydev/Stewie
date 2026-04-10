/// <summary>
/// Agents API controller — manages agent container lifecycle (launch, terminate, status).
/// REF: JOB-017 T-166, CON-002
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Services;

namespace Stewie.Api.Controllers;

/// <summary>
/// REST endpoints for agent container lifecycle management.
/// POST /api/agents/launch — Launch a new agent container
/// DELETE /api/agents/{id} — Terminate an agent session
/// GET /api/agents/{id}/status — Get agent session status
/// GET /api/projects/{projectId}/agents — List agents for a project
/// </summary>
[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly AgentLifecycleService _lifecycle;
    private readonly ILogger<AgentsController> _logger;

    /// <summary>Initializes the agents controller with required dependencies.</summary>
    public AgentsController(
        AgentLifecycleService lifecycle,
        ILogger<AgentsController> logger)
    {
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>Launch a new agent container for a project.</summary>
    /// <param name="request">Launch parameters.</param>
    /// <returns>201 Created with the agent session details.</returns>
    [HttpPost("launch")]
    public async Task<IActionResult> LaunchAgent([FromBody] LaunchAgentRequest request)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { error = "ProjectId is required." });

        if (string.IsNullOrWhiteSpace(request.AgentRole))
            return BadRequest(new { error = "AgentRole is required (architect, developer, tester)." });

        if (string.IsNullOrWhiteSpace(request.RuntimeName))
            return BadRequest(new { error = "RuntimeName is required (e.g. 'stub')." });

        var validRoles = new[] { "architect", "developer", "tester" };
        if (!validRoles.Contains(request.AgentRole.ToLowerInvariant()))
            return BadRequest(new { error = $"AgentRole must be one of: {string.Join(", ", validRoles)}." });

        try
        {
            var session = await _lifecycle.LaunchAgentAsync(
                request.ProjectId,
                request.AgentRole.ToLowerInvariant(),
                request.RuntimeName.ToLowerInvariant(),
                request.TaskId,
                request.WorkspacePath);

            _logger.LogInformation("Agent session {SessionId} launched via API", session.Id);

            return StatusCode(201, MapSession(session));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Terminate a running agent session.</summary>
    /// <param name="id">Agent session ID.</param>
    /// <param name="request">Optional termination details.</param>
    /// <returns>200 OK with updated session.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> TerminateAgent(Guid id, [FromBody] TerminateAgentRequest? request = null)
    {
        try
        {
            var reason = request?.Reason ?? "User requested termination";
            await _lifecycle.TerminateAgentAsync(id, reason);

            var session = await _lifecycle.GetStatusAsync(id);
            return Ok(MapSession(session));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Agent session '{id}' not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Get the current status of an agent session.</summary>
    /// <param name="id">Agent session ID.</param>
    /// <returns>200 OK with session details.</returns>
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        try
        {
            var session = await _lifecycle.GetStatusAsync(id);
            return Ok(MapSession(session));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Agent session '{id}' not found." });
        }
    }

    /// <summary>List all agent sessions for a project.</summary>
    /// <param name="projectId">Project ID.</param>
    /// <returns>200 OK with session list.</returns>
    [HttpGet("/api/projects/{projectId}/agents")]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var sessions = await _lifecycle.GetSessionsByProjectAsync(projectId);
        return Ok(new
        {
            sessions = sessions.Select(MapSession),
            total = sessions.Count
        });
    }

    /// <summary>Maps an AgentSession entity to an API response object.</summary>
    private static object MapSession(Domain.Entities.AgentSession session) => new
    {
        id = session.Id,
        projectId = session.ProjectId,
        taskId = session.TaskId,
        containerId = session.ContainerId,
        runtimeName = session.RuntimeName,
        agentRole = session.AgentRole,
        status = session.Status.ToString(),
        startedAt = session.StartedAt.ToString("O"),
        stoppedAt = session.StoppedAt?.ToString("O"),
        stopReason = session.StopReason
    };

    // ========================================
    // Architect session management (T-172)
    // ========================================

    /// <summary>Start an Architect agent for a project.</summary>
    /// <param name="projectId">The project to attach the Architect to.</param>
    /// <param name="request">Optional launch configuration.</param>
    /// <returns>201 Created with architect session details.</returns>
    [HttpPost("/api/projects/{projectId}/architect/start")]
    public async Task<IActionResult> StartArchitect(
        Guid projectId,
        [FromBody] StartArchitectRequest? request = null)
    {
        if (projectId == Guid.Empty)
            return BadRequest(new { error = "ProjectId is required." });

        var runtimeName = request?.RuntimeName ?? "stub";

        try
        {
            var session = await _lifecycle.LaunchAgentAsync(
                projectId,
                "architect",
                runtimeName.ToLowerInvariant(),
                taskId: null,
                workspacePath: request?.WorkspacePath);

            _logger.LogInformation(
                "Architect session {SessionId} started for project {ProjectId}",
                session.Id, projectId);

            return StatusCode(201, MapSession(session));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Terminate the active Architect for a project.</summary>
    /// <param name="projectId">The project whose Architect to stop.</param>
    /// <param name="request">Optional termination details.</param>
    /// <returns>200 OK with updated session, or 404 if no active Architect.</returns>
    [HttpDelete("/api/projects/{projectId}/architect")]
    public async Task<IActionResult> StopArchitect(
        Guid projectId,
        [FromBody] TerminateAgentRequest? request = null)
    {
        var session = await _lifecycle.GetActiveArchitectAsync(projectId);
        if (session is null)
            return NotFound(new { error = $"No active Architect session for project '{projectId}'." });

        try
        {
            var reason = request?.Reason ?? "User stopped Architect";
            await _lifecycle.TerminateAgentAsync(session.Id, reason);

            var updated = await _lifecycle.GetStatusAsync(session.Id);
            return Ok(MapSession(updated));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Get the active Architect session status for a project.</summary>
    /// <param name="projectId">The project to query.</param>
    /// <returns>200 OK with session details, or 404 if no active Architect.</returns>
    [HttpGet("/api/projects/{projectId}/architect/status")]
    public async Task<IActionResult> GetArchitectStatus(Guid projectId)
    {
        var session = await _lifecycle.GetActiveArchitectAsync(projectId);
        if (session is null)
            return Ok(new { active = false, session = (object?)null });

        return Ok(new { active = true, session = MapSession(session) });
    }
}

/// <summary>Request body for launching an agent.</summary>
public class LaunchAgentRequest
{
    /// <summary>Project ID the agent will work on. Required.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Agent role: "architect", "developer", or "tester". Required.</summary>
    public string AgentRole { get; set; } = string.Empty;

    /// <summary>Name of the runtime to use (e.g. "stub"). Required.</summary>
    public string RuntimeName { get; set; } = string.Empty;

    /// <summary>Optional task ID for task-scoped agents (developer, tester).</summary>
    public Guid? TaskId { get; set; }

    /// <summary>Optional workspace path to mount into the container.</summary>
    public string? WorkspacePath { get; set; }
}

/// <summary>Request body for terminating an agent.</summary>
public class TerminateAgentRequest
{
    /// <summary>Human-readable reason for termination.</summary>
    public string Reason { get; set; } = "User requested termination";
}

/// <summary>Request body for starting an Architect agent. All fields are optional.</summary>
public class StartArchitectRequest
{
    /// <summary>Runtime to use for the Architect container (default: "stub").</summary>
    public string RuntimeName { get; set; } = "stub";

    /// <summary>Optional workspace path to mount into the container.</summary>
    public string? WorkspacePath { get; set; }
}
