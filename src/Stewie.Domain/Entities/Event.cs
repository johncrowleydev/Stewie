/// <summary>
/// Event entity — immutable audit trail record for all state changes.
/// Used by: JobOrchestrationService (emits events), future event query endpoints.
/// REF: BLU-001 §3.2
/// </summary>
using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents an audit trail event recording a state change on an entity.
/// Events are append-only — once created they are never modified or deleted.
/// </summary>
public class Event
{
    /// <summary>Unique identifier for this event.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Type name of the entity that changed (e.g. "Run", "WorkTask").</summary>
    public virtual string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the entity that changed.</summary>
    public virtual Guid EntityId { get; set; }

    /// <summary>Classification of what happened.</summary>
    public virtual EventType EventType { get; set; }

    /// <summary>JSON-serialized payload with additional event context.</summary>
    public virtual string Payload { get; set; } = string.Empty;

    /// <summary>Timestamp when the event occurred.</summary>
    public virtual DateTime Timestamp { get; set; }
}
