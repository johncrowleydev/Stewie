/// <summary>
/// AgentSession entity — tracks the lifecycle of a single agent container.
/// One session per agent launch. Sessions are never reused — a new launch
/// creates a new session.
/// REF: JOB-017 T-163
/// </summary>
using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Persistent record of an agent container's lifecycle, from launch request
/// through termination. Used for auditability, status querying, and
/// cleanup of orphaned containers.
/// </summary>
public class AgentSession
{
    /// <summary>Unique session identifier (also used as the agent's RabbitMQ identity).</summary>
    public virtual Guid Id { get; set; }

    /// <summary>FK to the project this agent is working on.</summary>
    public virtual Guid ProjectId { get; set; }

    /// <summary>Optional FK to the specific task (null for Architect agents).</summary>
    public virtual Guid? TaskId { get; set; }

    /// <summary>Docker container ID returned by the runtime after launch.</summary>
    public virtual string? ContainerId { get; set; }

    /// <summary>Name of the IAgentRuntime that launched this agent (e.g. "stub", "claude-code").</summary>
    public virtual string RuntimeName { get; set; } = string.Empty;

    /// <summary>Agent role: "architect", "developer", "tester".</summary>
    public virtual string AgentRole { get; set; } = string.Empty;

    /// <summary>Current lifecycle status of this session.</summary>
    public virtual AgentSessionStatus Status { get; set; } = AgentSessionStatus.Pending;

    /// <summary>UTC timestamp when the session was created (launch requested).</summary>
    public virtual DateTime StartedAt { get; set; }

    /// <summary>UTC timestamp when the session ended (completed, failed, or terminated). Null if still active.</summary>
    public virtual DateTime? StoppedAt { get; set; }

    /// <summary>Human-readable reason for termination, if applicable.</summary>
    public virtual string? StopReason { get; set; }
}
