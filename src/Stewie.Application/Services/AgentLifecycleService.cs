/// <summary>
/// AgentLifecycleService — orchestrates agent container launch, termination,
/// and status queries. Creates AgentSession records, delegates to IAgentRuntime
/// implementations, and emits events/SignalR notifications.
/// REF: JOB-017 T-165
/// </summary>
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stewie.Application.Configuration;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;

namespace Stewie.Application.Services;

/// <summary>
/// Manages the full lifecycle of agent containers: launch → monitor → terminate.
/// Acts as the bridge between REST API requests and the pluggable IAgentRuntime implementations.
/// </summary>
public class AgentLifecycleService
{
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IAgentRuntime> _runtimes;
    private readonly RabbitMqOptions _mqOptions;
    private readonly ILogger<AgentLifecycleService> _logger;

    /// <summary>Creates a new AgentLifecycleService instance.</summary>
    public AgentLifecycleService(
        IAgentSessionRepository sessionRepo,
        IEventRepository eventRepo,
        IRealTimeNotifier notifier,
        IRabbitMqService rabbitMq,
        IUnitOfWork unitOfWork,
        IEnumerable<IAgentRuntime> runtimes,
        IOptions<RabbitMqOptions> mqOptions,
        ILogger<AgentLifecycleService> logger)
    {
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _notifier = notifier;
        _rabbitMq = rabbitMq;
        _unitOfWork = unitOfWork;
        _runtimes = runtimes;
        _mqOptions = mqOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Launches a new agent container for the specified project and role.
    /// Creates an AgentSession record, resolves the runtime, and delegates the launch.
    /// </summary>
    /// <param name="projectId">Project ID the agent will work on.</param>
    /// <param name="agentRole">Role: "architect", "developer", "tester".</param>
    /// <param name="runtimeName">Name of the IAgentRuntime to use (e.g. "stub").</param>
    /// <param name="taskId">Optional task ID for task-scoped agents.</param>
    /// <param name="workspacePath">Workspace path to mount into the container.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created AgentSession with ContainerId populated on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an active session already exists for the same project+role, or if the runtime is not registered.</exception>
    public async Task<AgentSession> LaunchAgentAsync(
        Guid projectId,
        string agentRole,
        string runtimeName,
        Guid? taskId = null,
        string? workspacePath = null,
        CancellationToken ct = default)
    {
        // Prevent duplicate active sessions for the same project + role
        var existing = await _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, agentRole);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"An active {agentRole} session already exists for project {projectId} (session {existing.Id}).");
        }

        // Resolve runtime by name
        var runtime = _runtimes.FirstOrDefault(r => r.RuntimeName == runtimeName)
            ?? throw new InvalidOperationException($"No IAgentRuntime registered with name '{runtimeName}'.");

        // Create session record
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TaskId = taskId,
            RuntimeName = runtimeName,
            AgentRole = agentRole,
            Status = AgentSessionStatus.Starting,
            StartedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _sessionRepo.SaveAsync(session);

        // Emit session started event
        var startEvent = new Event
        {
            Id = Guid.NewGuid(),
            EntityType = "AgentSession",
            EntityId = session.Id,
            EventType = EventType.AgentStarted,
            Payload = $"{{\"agentRole\":\"{agentRole}\",\"runtimeName\":\"{runtimeName}\",\"projectId\":\"{projectId}\"}}",
            Timestamp = DateTime.UtcNow
        };
        await _eventRepo.SaveAsync(startEvent);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Launching agent session {SessionId} ({Role}/{Runtime}) for project {ProjectId}",
            session.Id, agentRole, runtimeName, projectId);

        try
        {
            // Build launch request
            var request = new AgentLaunchRequest
            {
                SessionId = session.Id,
                ProjectId = projectId,
                TaskId = taskId,
                AgentRole = agentRole,
                WorkspacePath = workspacePath ?? string.Empty,
                RabbitMqHost = _mqOptions.HostName,
                RabbitMqPort = _mqOptions.Port,
                RabbitMqVHost = _mqOptions.VirtualHost,
                RabbitMqUser = _mqOptions.UserName,
                RabbitMqPassword = _mqOptions.Password,
                CommandQueueName = $"agent.{session.Id}.commands"
            };

            // Delegate to runtime
            var containerId = await runtime.LaunchAgentAsync(request, ct);

            // Update session with container ID and Active status
            session.ContainerId = containerId;
            session.Status = AgentSessionStatus.Active;

            _unitOfWork.BeginTransaction();
            await _sessionRepo.SaveAsync(session);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Agent session {SessionId} is now active (container {ContainerId})",
                session.Id, containerId);

            // Notify via SignalR
            await _notifier.NotifyAgentStatusChangedAsync(session.ProjectId, session.Id, "Active");

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch agent session {SessionId}", session.Id);

            session.Status = AgentSessionStatus.Failed;
            session.StoppedAt = DateTime.UtcNow;
            session.StopReason = $"Launch failed: {ex.Message}";

            _unitOfWork.BeginTransaction();
            await _sessionRepo.SaveAsync(session);
            await _unitOfWork.CommitAsync();

            throw;
        }
    }

    /// <summary>
    /// Terminates a running agent session. Stops the container and updates the session record.
    /// </summary>
    /// <param name="sessionId">The session to terminate.</param>
    /// <param name="reason">Human-readable reason for termination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the session does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the session is not in a terminable state.</exception>
    public async Task TerminateAgentAsync(Guid sessionId, string reason = "User requested", CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Agent session {sessionId} not found.");

        if (session.Status is AgentSessionStatus.Completed or AgentSessionStatus.Failed or AgentSessionStatus.Terminated)
        {
            throw new InvalidOperationException(
                $"Agent session {sessionId} is already in terminal state {session.Status}.");
        }

        _logger.LogInformation("Terminating agent session {SessionId} (reason: {Reason})", sessionId, reason);

        // Terminate the container if we have a container ID
        if (!string.IsNullOrEmpty(session.ContainerId))
        {
            var runtime = _runtimes.FirstOrDefault(r => r.RuntimeName == session.RuntimeName);
            if (runtime is not null)
            {
                try
                {
                    await runtime.TerminateAgentAsync(session.ContainerId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to terminate container {ContainerId} for session {SessionId}",
                        session.ContainerId, sessionId);
                    // Continue — we still mark the session as terminated even if container cleanup fails
                }
            }
        }

        session.Status = AgentSessionStatus.Terminated;
        session.StoppedAt = DateTime.UtcNow;
        session.StopReason = reason;

        _unitOfWork.BeginTransaction();
        await _sessionRepo.SaveAsync(session);

        var terminateEvent = new Event
        {
            Id = Guid.NewGuid(),
            EntityType = "AgentSession",
            EntityId = sessionId,
            EventType = EventType.AgentTerminated,
            Payload = $"{{\"reason\":\"{reason}\",\"projectId\":\"{session.ProjectId}\"}}",
            Timestamp = DateTime.UtcNow
        };
        await _eventRepo.SaveAsync(terminateEvent);
        await _unitOfWork.CommitAsync();

        await _notifier.NotifyAgentStatusChangedAsync(session.ProjectId, sessionId, "Terminated");
    }

    /// <summary>
    /// Gets the current status of an agent session, including runtime container status if available.
    /// </summary>
    /// <param name="sessionId">The session to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent session with current status.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the session does not exist.</exception>
    public async Task<AgentSession> GetStatusAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Agent session {sessionId} not found.");

        return session;
    }

    /// <summary>
    /// Lists all sessions for a project.
    /// </summary>
    /// <param name="projectId">Project ID to query.</param>
    /// <returns>List of agent sessions ordered by StartedAt descending.</returns>
    public async Task<IList<AgentSession>> GetSessionsByProjectAsync(Guid projectId)
    {
        return await _sessionRepo.GetByProjectIdAsync(projectId);
    }

    /// <summary>
    /// Gets the active Architect session for a project, if one exists.
    /// REF: JOB-018 T-172
    /// </summary>
    /// <param name="projectId">Project ID to query.</param>
    /// <returns>The active Architect session, or null if none exists.</returns>
    public async Task<AgentSession?> GetActiveArchitectAsync(Guid projectId)
    {
        return await _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect");
    }
}
