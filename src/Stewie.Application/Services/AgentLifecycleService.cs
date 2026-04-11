/// <summary>
/// AgentLifecycleService — orchestrates agent container launch, termination,
/// and status queries. Creates AgentSession records, delegates to IAgentRuntime
/// implementations, and emits events/SignalR notifications.
/// REF: JOB-017 T-165, JOB-021 T-184
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
    private readonly IUserCredentialRepository? _credentialRepo;
    private readonly IEncryptionService? _encryptionService;

    /// <summary>Creates a new AgentLifecycleService instance.</summary>
    /// <param name="sessionRepo">Agent session repository.</param>
    /// <param name="eventRepo">Event repository.</param>
    /// <param name="notifier">Real-time notification service (SignalR).</param>
    /// <param name="rabbitMq">RabbitMQ messaging service.</param>
    /// <param name="unitOfWork">Unit of work for transactions.</param>
    /// <param name="runtimes">All registered agent runtime implementations.</param>
    /// <param name="mqOptions">RabbitMQ configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="credentialRepo">Optional credential repository for LLM API key resolution. REF: JOB-021 T-184.</param>
    /// <param name="encryptionService">Optional encryption service for credential decryption. REF: JOB-021 T-184.</param>
    public AgentLifecycleService(
        IAgentSessionRepository sessionRepo,
        IEventRepository eventRepo,
        IRealTimeNotifier notifier,
        IRabbitMqService rabbitMq,
        IUnitOfWork unitOfWork,
        IEnumerable<IAgentRuntime> runtimes,
        IOptions<RabbitMqOptions> mqOptions,
        ILogger<AgentLifecycleService> logger,
        IUserCredentialRepository? credentialRepo = null,
        IEncryptionService? encryptionService = null)
    {
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _notifier = notifier;
        _rabbitMq = rabbitMq;
        _unitOfWork = unitOfWork;
        _runtimes = runtimes;
        _mqOptions = mqOptions.Value;
        _logger = logger;
        _credentialRepo = credentialRepo;
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Launches a new agent container for the specified project and role.
    /// Creates an AgentSession record, resolves the runtime, and delegates the launch.
    /// For LLM-backed runtimes (e.g. "opencode"), resolves the API key from the
    /// credential store and injects it via file-based secret mounting.
    /// REF: JOB-021 T-184.
    /// </summary>
    /// <param name="projectId">Project ID the agent will work on.</param>
    /// <param name="agentRole">Role: "architect", "developer", "tester".</param>
    /// <param name="runtimeName">Name of the IAgentRuntime to use (e.g. "stub").</param>
    /// <param name="taskId">Optional task ID for task-scoped agents.</param>
    /// <param name="workspacePath">Workspace path to mount into the container.</param>
    /// <param name="userId">Optional user ID for credential resolution. REF: JOB-021 T-184.</param>
    /// <param name="llmProvider">Optional LLM provider name (e.g. "google", "anthropic"). REF: JOB-021 T-184.</param>
    /// <param name="modelName">Optional model name (e.g. "gemini-2.0-flash"). REF: JOB-021 T-184.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created AgentSession with ContainerId populated on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an active session already exists for the same project+role, or if the runtime is not registered.</exception>
    public async Task<AgentSession> LaunchAgentAsync(
        Guid projectId,
        string agentRole,
        string runtimeName,
        Guid? taskId = null,
        string? workspacePath = null,
        Guid? userId = null,
        string? llmProvider = null,
        string? modelName = null,
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
            // Resolve LLM API key for LLM-backed runtimes (JOB-021 T-184)
            string? secretsMountPath = null;
            if (userId.HasValue && !string.IsNullOrWhiteSpace(llmProvider)
                && _credentialRepo is not null && _encryptionService is not null)
            {
                secretsMountPath = await ResolveLlmSecretAsync(
                    userId.Value, llmProvider, session.Id);
            }

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
                CommandQueueName = $"agent.{session.Id}.commands",
                LlmProvider = llmProvider ?? string.Empty,
                ModelName = modelName ?? string.Empty,
                SecretsMountPath = secretsMountPath ?? string.Empty
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

        await VerifyAndHealSessionAsync(session, ct);

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
        var session = await _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect");
        if (session is not null)
        {
            await VerifyAndHealSessionAsync(session, default);
            if (session.Status is AgentSessionStatus.Terminated or AgentSessionStatus.Failed)
            {
                return null;
            }
        }
        return session;
    }

    private async Task VerifyAndHealSessionAsync(AgentSession session, CancellationToken ct)
    {
        if (session.Status is AgentSessionStatus.Active or AgentSessionStatus.Starting && !string.IsNullOrEmpty(session.ContainerId))
        {
            var runtime = _runtimes.FirstOrDefault(r => r.RuntimeName == session.RuntimeName);
            if (runtime is not null)
            {
                try
                {
                    var containerStatus = await runtime.GetStatusAsync(session.ContainerId, ct);
                    if (containerStatus is AgentRuntimeStatus.Stopped or AgentRuntimeStatus.Failed)
                    {
                        _logger.LogWarning("Agent session {SessionId} (Container {ContainerId}) found offline during health check. Auto-healing.", session.Id, session.ContainerId);
                        
                        session.Status = AgentSessionStatus.Terminated;
                        session.StoppedAt = DateTime.UtcNow;
                        session.StopReason = "Auto-healed: Container process died unexpectedly.";
                        
                        _unitOfWork.BeginTransaction();
                        await _sessionRepo.SaveAsync(session);
                        
                        var terminateEvent = new Event
                        {
                            Id = Guid.NewGuid(),
                            EntityType = "AgentSession",
                            EntityId = session.Id,
                            EventType = EventType.AgentTerminated,
                            Payload = $"{{\"reason\":\"{session.StopReason}\",\"projectId\":\"{session.ProjectId}\"}}",
                            Timestamp = DateTime.UtcNow
                        };
                        await _eventRepo.SaveAsync(terminateEvent);
                        
                        await _unitOfWork.CommitAsync();
                        
                        await _notifier.NotifyAgentStatusChangedAsync(session.ProjectId, session.Id, "Terminated");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify status for container {ContainerId} of session {SessionId}", session.ContainerId, session.Id);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the LLM API key from the credential store and writes it to a temp file.
    /// REF: JOB-021 T-184.
    /// </summary>
    /// <param name="userId">User ID to look up credentials for.</param>
    /// <param name="llmProvider">LLM provider name (e.g. "google", "anthropic").</param>
    /// <param name="sessionId">Session ID for unique secret directory naming.</param>
    /// <returns>Path to the secrets directory, or null if no credential found.</returns>
    private async Task<string?> ResolveLlmSecretAsync(Guid userId, string llmProvider, Guid sessionId)
    {
        var credentialType = MapProviderToCredentialType(llmProvider);
        if (credentialType is null)
        {
            _logger.LogWarning("Unknown LLM provider '{Provider}' — skipping credential resolution", llmProvider);
            return null;
        }

        var credential = await _credentialRepo!.GetByTypeAsync(userId, credentialType.Value);
        if (credential is null)
        {
            _logger.LogWarning(
                "No {Provider} API key configured for user {UserId}. Add one in Settings.",
                llmProvider, userId);
            return null;
        }

        try
        {
            var decryptedKey = _encryptionService!.Decrypt(credential.EncryptedToken);
            var secretsDir = Path.Combine(Path.GetTempPath(), $"stewie-secrets-{sessionId:N}");
            Directory.CreateDirectory(secretsDir);
            await File.WriteAllTextAsync(Path.Combine(secretsDir, "llm_api_key"), decryptedKey);

            _logger.LogInformation(
                "LLM API key written to secrets directory for session {SessionId}", sessionId);

            return secretsDir;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt/write LLM API key for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Maps an LLM provider name to the corresponding <see cref="CredentialType"/>.
    /// Returns null for unknown providers.
    /// </summary>
    private static CredentialType? MapProviderToCredentialType(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "anthropic" => CredentialType.AnthropicApiKey,
            "openai" => CredentialType.OpenAiApiKey,
            "google" => CredentialType.GoogleAiApiKey,
            _ => null
        };
    }
}
