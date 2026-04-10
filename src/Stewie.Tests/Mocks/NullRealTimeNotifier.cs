/// <summary>
/// No-op implementation of IRealTimeNotifier for unit tests.
/// REF: JOB-012 T-124
/// </summary>
using Stewie.Application.Interfaces;

namespace Stewie.Tests.Mocks;

/// <summary>
/// Silently swallows all real-time notifications — used in unit tests
/// where SignalR hub context is not available.
/// </summary>
public class NullRealTimeNotifier : IRealTimeNotifier
{
    /// <inheritdoc/>
    public Task NotifyJobUpdatedAsync(Guid? projectId, Guid jobId, string status) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task NotifyTaskUpdatedAsync(Guid jobId, Guid taskId, string status) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task NotifyChatMessageAsync(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, DateTime createdAt) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task NotifyContainerOutputAsync(Guid jobId, Guid taskId, string line) => Task.CompletedTask;
}
