/// <summary>
/// SignalR implementation of IRealTimeNotifier — routes push notifications to hub groups.
/// REF: JOB-012 T-122
/// </summary>
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Stewie.Application.Hubs;
using Stewie.Application.Interfaces;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Routes real-time notifications to connected SignalR clients via group-scoped pushes.
/// All methods swallow exceptions — SignalR push failures must never break orchestration.
/// </summary>
public class SignalRNotifier : IRealTimeNotifier
{
    private readonly IHubContext<StewieHub, IStewieHubClient> _hubContext;
    private readonly ILogger<SignalRNotifier> _logger;

    /// <summary>Initializes the notifier with the SignalR hub context.</summary>
    public SignalRNotifier(
        IHubContext<StewieHub, IStewieHubClient> hubContext,
        ILogger<SignalRNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyJobUpdatedAsync(Guid? projectId, Guid jobId, string status)
    {
        try
        {
            // Always push to dashboard group
            await _hubContext.Clients.Group("dashboard").JobUpdated(jobId, status);

            // Also push to project group if project is known
            if (projectId.HasValue)
            {
                await _hubContext.Clients.Group($"project:{projectId.Value}")
                    .JobUpdated(jobId, status);
            }

            _logger.LogDebug("Pushed JobUpdated: jobId={JobId}, status={Status}", jobId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push JobUpdated for {JobId}", jobId);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyTaskUpdatedAsync(Guid jobId, Guid taskId, string status)
    {
        try
        {
            await _hubContext.Clients.Group($"job:{jobId}")
                .TaskUpdated(jobId, taskId, status);

            _logger.LogDebug("Pushed TaskUpdated: jobId={JobId}, taskId={TaskId}, status={Status}",
                jobId, taskId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push TaskUpdated for task {TaskId} in job {JobId}",
                taskId, jobId);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyChatMessageAsync(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, DateTime createdAt)
    {
        try
        {
            await _hubContext.Clients.Group($"project:{projectId}")
                .ChatMessageReceived(projectId, messageId, senderRole, senderName, content,
                    createdAt.ToString("O"));

            _logger.LogDebug("Pushed ChatMessageReceived: projectId={ProjectId}, messageId={MessageId}",
                projectId, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push ChatMessageReceived for project {ProjectId}", projectId);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyContainerOutputAsync(Guid jobId, Guid taskId, string line)
    {
        try
        {
            await _hubContext.Clients.Group($"job:{jobId}")
                .ContainerOutput(taskId, line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push ContainerOutput for task {TaskId}", taskId);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyAgentStatusChangedAsync(Guid projectId, Guid sessionId, string status)
    {
        try
        {
            await _hubContext.Clients.Group($"project:{projectId}")
                .AgentStatusChanged(projectId, sessionId, status);

            _logger.LogDebug("Pushed AgentStatusChanged: projectId={ProjectId}, sessionId={SessionId}, status={Status}",
                projectId, sessionId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push AgentStatusChanged for session {SessionId}", sessionId);
        }
    }
}
