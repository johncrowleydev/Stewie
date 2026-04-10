/// <summary>
/// AgentLaunchRequest — request DTO for launching an agent container.
/// Passed to IAgentRuntime.LaunchAgentAsync with all context needed to
/// spin up a container and connect it to the correct RabbitMQ queues.
/// REF: JOB-017 T-162, CON-004 §6.1
/// </summary>
namespace Stewie.Domain.Messaging;

/// <summary>
/// Immutable request object containing all parameters needed to launch an agent container.
/// </summary>
public class AgentLaunchRequest
{
    /// <summary>Unique session ID assigned by the API before launch.</summary>
    public Guid SessionId { get; init; }

    /// <summary>Project this agent is working on.</summary>
    public Guid ProjectId { get; init; }

    /// <summary>Optional task ID if this is a task-scoped agent (Dev/Tester). Null for Architect.</summary>
    public Guid? TaskId { get; init; }

    /// <summary>Agent role: "architect", "developer", "tester".</summary>
    public string AgentRole { get; init; } = string.Empty;

    /// <summary>Host-side workspace path to mount into the container.</summary>
    public string WorkspacePath { get; init; } = string.Empty;

    /// <summary>RabbitMQ hostname for the agent to connect to.</summary>
    public string RabbitMqHost { get; init; } = "localhost";

    /// <summary>RabbitMQ port.</summary>
    public int RabbitMqPort { get; init; } = 5672;

    /// <summary>RabbitMQ virtual host.</summary>
    public string RabbitMqVHost { get; init; } = "stewie";

    /// <summary>RabbitMQ username for agent containers.</summary>
    public string RabbitMqUser { get; init; } = string.Empty;

    /// <summary>RabbitMQ password for agent containers.</summary>
    public string RabbitMqPassword { get; init; } = string.Empty;

    /// <summary>
    /// The command queue this agent should consume from.
    /// Format: agent.{sessionId}.commands (per CON-004 §3).
    /// </summary>
    public string CommandQueueName { get; init; } = string.Empty;

    /// <summary>
    /// Additional key-value configuration passed as environment variables to the container.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}
