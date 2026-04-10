/// <summary>
/// Configuration options for RabbitMQ connection and topology.
/// Bound from appsettings.json section "RabbitMq".
/// REF: CON-004, JOB-016 T-155
/// </summary>
namespace Stewie.Application.Configuration;

/// <summary>
/// Strongly-typed options for the RabbitMQ connection.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>RabbitMQ server hostname. Default: localhost.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>AMQP port. Default: 5672.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>RabbitMQ username.</summary>
    public string UserName { get; set; } = "stewie";

    /// <summary>RabbitMQ password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>RabbitMQ virtual host. Default: stewie.</summary>
    public string VirtualHost { get; set; } = "stewie";

    /// <summary>
    /// Connection retry count before giving up.
    /// Used by the connection factory for automatic recovery.
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Delay in seconds between retry attempts.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 3;

    /// <summary>
    /// Whether to enable automatic connection recovery.
    /// Default: true.
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>Exchange name for API → Agent commands (direct exchange).</summary>
    public string CommandsExchange { get; set; } = "stewie.commands";

    /// <summary>Exchange name for Agent → API events (topic exchange).</summary>
    public string EventsExchange { get; set; } = "stewie.events";

    /// <summary>Exchange name for Human → Architect chat relay (direct exchange).</summary>
    public string ChatExchange { get; set; } = "stewie.chat";
}
