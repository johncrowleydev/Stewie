/// <summary>
/// Models for agent launch configuration.
/// REF: JOB-017 T-162
/// </summary>
namespace Stewie.Application.Models;

/// <summary>
/// Carries all configuration needed to launch an agent container.
/// Passed from AgentLifecycleService to IAgentRuntime.LaunchAsync.
/// </summary>
public class AgentLaunchRequest
{
    /// <summary>Unique session ID for this agent.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Project this agent belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Task this agent is assigned to (null for Architect).</summary>
    public Guid? TaskId { get; set; }

    /// <summary>Agent role: "architect", "developer", "tester".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>RabbitMQ connection details for the container.</summary>
    public RabbitMqConnectionInfo RabbitMq { get; set; } = new();

    /// <summary>Workspace path to mount (for dev/tester agents).</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>Additional runtime-specific configuration.</summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// RabbitMQ connection information passed to agent containers via environment variables.
/// </summary>
public class RabbitMqConnectionInfo
{
    /// <summary>RabbitMQ server hostname.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>AMQP port.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>RabbitMQ username.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>RabbitMQ password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>RabbitMQ virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Queue name this agent should consume from.</summary>
    public string AgentQueueName { get; set; } = string.Empty;
}
