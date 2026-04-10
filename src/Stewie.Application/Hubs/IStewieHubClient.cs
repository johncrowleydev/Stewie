/// <summary>
/// Typed SignalR hub client — defines push methods the server can call on connected clients.
/// REF: JOB-012 T-120
/// </summary>
namespace Stewie.Application.Hubs;

/// <summary>
/// Defines the methods that the server can invoke on connected SignalR clients.
/// All parameters are primitives/strings to avoid serialization issues across WebSocket.
/// </summary>
public interface IStewieHubClient
{
    /// <summary>Notifies clients that a job's status has changed.</summary>
    Task JobUpdated(Guid jobId, string status);

    /// <summary>Notifies clients that a task's status has changed within a job.</summary>
    Task TaskUpdated(Guid jobId, Guid taskId, string status);

    /// <summary>Notifies clients that a new chat message was posted in a project. (JOB-013)</summary>
    Task ChatMessageReceived(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, string createdAt);

    /// <summary>Streams a container output line to connected clients. (JOB-014)</summary>
    Task ContainerOutput(Guid taskId, string line);
}
