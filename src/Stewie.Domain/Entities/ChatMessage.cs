/// <summary>
/// ChatMessage entity — persistent project-scoped chat message.
/// REF: JOB-013 T-130
/// </summary>
namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a single chat message within a project conversation.
/// Messages are created by Humans (via REST API) or by the Architect Agent (via RabbitMQ in Phase 6).
/// </summary>
public class ChatMessage
{
    /// <summary>Unique identifier for this message.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>FK to the project this message belongs to.</summary>
    public virtual Guid ProjectId { get; set; }

    /// <summary>Who sent the message: "Human", "Architect", or "System"</summary>
    public virtual string SenderRole { get; set; } = string.Empty;

    /// <summary>Display name of the sender (username for Human, "Architect" for agent, etc.)</summary>
    public virtual string SenderName { get; set; } = string.Empty;

    /// <summary>Message text content. Supports markdown.</summary>
    public virtual string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was created.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
