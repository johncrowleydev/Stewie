/// <summary>
/// Interface for real-time push notifications to connected clients.
/// REF: JOB-012 T-122
/// </summary>
namespace Stewie.Application.Interfaces;

/// <summary>
/// Abstracts real-time push notifications. Implementation routes to SignalR groups.
/// All methods are fire-and-forget safe — failures must never break orchestration.
/// </summary>
public interface IRealTimeNotifier
{
    /// <summary>Pushes a job status update to dashboard and project groups.</summary>
    Task NotifyJobUpdatedAsync(Guid? projectId, Guid jobId, string status);

    /// <summary>Pushes a task status update to the parent job's group.</summary>
    Task NotifyTaskUpdatedAsync(Guid jobId, Guid taskId, string status);

    /// <summary>Pushes a chat message to the project group. (JOB-013)</summary>
    Task NotifyChatMessageAsync(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, DateTime createdAt);

    /// <summary>Pushes a container output line to the job group. (JOB-014)</summary>
    Task NotifyContainerOutputAsync(Guid jobId, Guid taskId, string line);

    /// <summary>Pushes an agent session status change to the project group. (JOB-017)</summary>
    Task NotifyAgentStatusChangedAsync(Guid projectId, Guid sessionId, string status);
}
