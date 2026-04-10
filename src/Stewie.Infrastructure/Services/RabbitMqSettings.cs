/// <summary>
/// Configuration POCO for RabbitMQ connection settings.
/// Bound from the "RabbitMQ" section of appsettings.json.
/// REF: JOB-016 T-160
/// </summary>
namespace Stewie.Infrastructure.Services;

/// <summary>
/// Settings for connecting to RabbitMQ. Defaults match the docker-compose dev environment.
/// In production, override via environment variables or appsettings.Production.json.
/// </summary>
public class RabbitMqSettings
{
    /// <summary>RabbitMQ server hostname.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>AMQP port (default 5672).</summary>
    public int Port { get; set; } = 5672;

    /// <summary>RabbitMQ username.</summary>
    public string UserName { get; set; } = "stewie";

    /// <summary>RabbitMQ password.</summary>
    public string Password { get; set; } = "stewie_dev";

    /// <summary>RabbitMQ virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Maximum number of connection retry attempts on startup.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in milliseconds for exponential backoff between retries.</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;
}
