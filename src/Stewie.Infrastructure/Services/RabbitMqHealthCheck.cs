using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Stewie.Application.Configuration;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// ASP.NET Core health check that verifies RabbitMQ connectivity.
/// Opens a transient connection to the configured broker and reports
/// Healthy/Unhealthy based on whether the connection succeeds.
/// REF: CON-004 §8, JOB-016 T-157
/// </summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqHealthCheck> _logger;

    /// <summary>Creates a new RabbitMQ health check instance.</summary>
    /// <param name="options">RabbitMQ connection options.</param>
    /// <param name="logger">Logger instance.</param>
    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options, ILogger<RabbitMqHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks RabbitMQ connectivity by opening and immediately closing
    /// a transient AMQP connection.
    /// </summary>
    /// <param name="context">Health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Healthy if the connection succeeds; Unhealthy otherwise.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            using var connection = await factory.CreateConnectionAsync(cancellationToken);

            _logger.LogDebug("RabbitMQ health check passed — connected to {Host}:{Port}/{VHost}",
                _options.HostName, _options.Port, _options.VirtualHost);

            return HealthCheckResult.Healthy($"RabbitMQ connection to {_options.HostName}:{_options.Port}/{_options.VirtualHost} is active.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ health check failed — cannot connect to {Host}:{Port}/{VHost}",
                _options.HostName, _options.Port, _options.VirtualHost);

            return HealthCheckResult.Unhealthy(
                $"RabbitMQ connection to {_options.HostName}:{_options.Port}/{_options.VirtualHost} failed.",
                exception: ex);
        }
    }
}
