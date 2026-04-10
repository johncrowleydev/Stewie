/// <summary>
/// Interface for RabbitMQ message publishing operations.
/// REF: JOB-016 T-159, CON-004 §4
/// </summary>
using Stewie.Domain.Messaging;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Abstracts RabbitMQ publish operations. The Application layer uses this interface
/// to send commands and chat messages to agent containers without depending on the
/// RabbitMQ.Client NuGet package directly.
/// </summary>
public interface IRabbitMqService : IAsyncDisposable
{
    /// <summary>
    /// Publishes a command message to the <c>stewie.commands</c> direct exchange.
    /// Commands flow from the API to agent containers (e.g. task assignments).
    /// </summary>
    /// <param name="routingKey">Routing key targeting a specific agent queue.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishCommandAsync(string routingKey, AgentMessage message, CancellationToken ct = default);

    /// <summary>
    /// Publishes a chat message to the <c>stewie.chat</c> direct exchange.
    /// Chat messages flow from the Human (via SignalR) to the Architect Agent.
    /// </summary>
    /// <param name="routingKey">Routing key targeting a specific Architect queue.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishChatAsync(string routingKey, AgentMessage message, CancellationToken ct = default);

    /// <summary>
    /// Returns whether the RabbitMQ connection is currently open and healthy.
    /// Used by health checks and readiness probes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if connected and the connection is open.</returns>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
}
