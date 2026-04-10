/// <summary>
/// RabbitMQ implementation of IRabbitMqService — manages connection lifecycle
/// and publishes messages to command and chat exchanges.
/// REF: JOB-016 T-160, CON-004 §4
/// </summary>
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Stewie.Application.Interfaces;
using Stewie.Domain.Messaging;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Manages a long-lived RabbitMQ connection and publishes messages to the
/// <c>stewie.commands</c> and <c>stewie.chat</c> exchanges. Channels are created
/// per-publish and disposed immediately (channels are cheap, connections are expensive).
/// Implements <see cref="IAsyncDisposable"/> for graceful shutdown.
/// </summary>
public class RabbitMqService : IRabbitMqService
{
    /// <summary>Direct exchange for task assignments and configuration pushes (API → agents).</summary>
    public const string CommandsExchange = "stewie.commands";

    /// <summary>Topic exchange for agent events (agents → API).</summary>
    public const string EventsExchange = "stewie.events";

    /// <summary>Direct exchange for chat relay (Human → Architect Agent).</summary>
    public const string ChatExchange = "stewie.chat";

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    /// <summary>Initializes the service with connection settings.</summary>
    public RabbitMqService(RabbitMqSettings settings, ILogger<RabbitMqService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task PublishCommandAsync(string routingKey, AgentMessage message, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(message);

        await PublishToExchangeAsync(CommandsExchange, routingKey, message, ct);
    }

    /// <inheritdoc/>
    public async Task PublishChatAsync(string routingKey, AgentMessage message, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(message);

        await PublishToExchangeAsync(ChatExchange, routingKey, message, ct);
    }

    /// <inheritdoc/>
    public Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_connection?.IsOpen == true);
    }

    /// <summary>
    /// Returns the current connection, creating one lazily if needed.
    /// Connection creation uses exponential backoff with configurable retry count.
    /// </summary>
    internal async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken ct = default)
    {
        if (_connection?.IsOpen == true)
            return _connection;

        await _connectionLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_connection?.IsOpen == true)
                return _connection;

            _connection = await CreateConnectionWithRetryAsync(ct);
            _logger.LogInformation(
                "RabbitMQ connection established to {Host}:{Port}",
                _settings.HostName, _settings.Port);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _logger.LogInformation("RabbitMQ connection closed gracefully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing RabbitMQ connection during disposal");
            }
        }

        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Publishes a serialized AgentMessage to a specific exchange.</summary>
    private async Task PublishToExchangeAsync(
        string exchange, string routingKey, AgentMessage message, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connection = await GetOrCreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, AgentMessageJsonContext.Default.AgentMessage);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            CorrelationId = message.CorrelationId ?? string.Empty
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        _logger.LogDebug(
            "Published message to {Exchange}/{RoutingKey}: type={Type}, agentId={AgentId}",
            exchange, routingKey, message.Type, message.AgentId);
    }

    /// <summary>Creates a connection with exponential backoff retry.</summary>
    private async Task<IConnection> CreateConnectionWithRetryAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost,
            ClientProvidedName = "stewie-api"
        };

        Exception? lastException = null;

        for (var attempt = 1; attempt <= _settings.MaxRetryAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return await factory.CreateConnectionAsync(ct);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed",
                    attempt, _settings.MaxRetryAttempts);

                if (attempt < _settings.MaxRetryAttempts)
                {
                    var delay = _settings.RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delay, ct);
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to RabbitMQ at {_settings.HostName}:{_settings.Port} " +
            $"after {_settings.MaxRetryAttempts} attempts",
            lastException);
    }
}

/// <summary>
/// Source-generated JSON serialization context for <see cref="AgentMessage"/>.
/// Avoids runtime reflection overhead for message serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AgentMessage))]
internal partial class AgentMessageJsonContext : JsonSerializerContext
{
}
