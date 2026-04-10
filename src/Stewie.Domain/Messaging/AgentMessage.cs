/// <summary>
/// AgentMessage DTO — canonical message format for all RabbitMQ communication.
/// Used by: IRabbitMqService (publish), RabbitMqConsumerHostedService (consume).
/// REF: JOB-016 T-158, CON-004 §3
/// </summary>
namespace Stewie.Domain.Messaging;

/// <summary>
/// Represents a message exchanged between the Stewie API control plane and agent containers
/// via RabbitMQ. All fields are serialized as JSON for wire transport.
/// </summary>
public class AgentMessage
{
    /// <summary>
    /// Message type classifier (e.g. "agent.started", "agent.progress", "task.assign").
    /// Used by consumers to dispatch to the correct handler.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the source or target agent. For commands, this is the target agent.
    /// For events, this is the agent that emitted the event.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// RabbitMQ routing key used when publishing this message.
    /// For topic exchanges, supports dot-separated patterns (e.g. "agent.abc123.progress").
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized message body containing type-specific data.
    /// The schema of this payload is determined by <see cref="Type"/>.
    /// </summary>
    public System.Text.Json.JsonElement Payload { get; set; }

    /// <summary>UTC timestamp when the message was created.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional correlation identifier for request/response patterns.
    /// When an agent responds to a command, it echoes the original CorrelationId.
    /// </summary>
    public string? CorrelationId { get; set; }
}
