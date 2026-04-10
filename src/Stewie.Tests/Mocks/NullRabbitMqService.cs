/// <summary>
/// No-op implementation of IRabbitMqService for unit tests.
/// Follows the same pattern as NullRealTimeNotifier.
/// REF: JOB-016 T-161
/// </summary>
using Stewie.Application.Interfaces;
using Stewie.Domain.Messaging;

namespace Stewie.Tests.Mocks;

/// <summary>
/// Silently swallows all RabbitMQ publish operations — used in unit tests
/// where a RabbitMQ server is not available.
/// </summary>
public class NullRabbitMqService : IRabbitMqService
{
    /// <summary>Tracks published command messages for test assertions.</summary>
    public List<(string RoutingKey, AgentMessage Message)> PublishedCommands { get; } = [];

    /// <summary>Tracks published chat messages for test assertions.</summary>
    public List<(string RoutingKey, AgentMessage Message)> PublishedChats { get; } = [];

    /// <summary>Controls what IsConnectedAsync returns. Defaults to true.</summary>
    public bool SimulatedConnectionState { get; set; } = true;

    /// <inheritdoc/>
    public Task PublishCommandAsync(string routingKey, AgentMessage message, CancellationToken ct = default)
    {
        PublishedCommands.Add((routingKey, message));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PublishChatAsync(string routingKey, AgentMessage message, CancellationToken ct = default)
    {
        PublishedChats.Add((routingKey, message));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(SimulatedConnectionState);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
