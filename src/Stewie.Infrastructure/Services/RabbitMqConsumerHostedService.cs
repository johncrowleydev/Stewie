/// <summary>
/// Background service that consumes agent events from the stewie.events topic exchange.
/// Deserializes AgentMessage DTOs, persists Event entities, and pushes real-time notifications.
/// Also handles chat.response events from Architect agents — persisting them as ChatMessage
/// entities and broadcasting via SignalR.
/// REF: JOB-016 T-160, JOB-018 T-171, CON-004 §5
/// </summary>
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Long-running hosted service that listens on the <c>stewie.events</c> topic exchange
/// for agent lifecycle events. Each received message is:
/// 1. Deserialized to <see cref="AgentMessage"/>
/// 2. Persisted as an <see cref="Event"/> entity
/// 3. Pushed to connected dashboard clients via <see cref="IRealTimeNotifier"/>
///
/// Reconnection is automatic — if the connection drops, the service waits 5 seconds
/// and retries until stopped.
/// </summary>
public class RabbitMqConsumerHostedService : BackgroundService
{
    /// <summary>Name of the queue bound to the events exchange.</summary>
    internal const string EventsQueueName = "stewie.api.events";

    /// <summary>Wildcard binding to receive all agent events.</summary>
    private const string BindingPattern = "#";

    /// <summary>Delay before reconnection attempt after a connection drop.</summary>
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly RabbitMqService _rabbitMqService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerHostedService> _logger;

    private IChannel? _channel;

    /// <summary>Initializes the consumer with the RabbitMQ service and DI scope factory.</summary>
    public RabbitMqConsumerHostedService(
        RabbitMqService rabbitMqService,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerHostedService> logger)
    {
        _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Main loop: connect, consume, reconnect on failure.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ consumer starting — waiting for agent events");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — expected
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ consumer failed — reconnecting in {Delay}s",
                    ReconnectDelay.TotalSeconds);

                await CleanupChannelAsync();

                try
                {
                    await Task.Delay(ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await CleanupChannelAsync();
        _logger.LogInformation("RabbitMQ consumer stopped");
    }

    /// <summary>
    /// Connects to RabbitMQ, declares the events queue, binds to the topic exchange,
    /// and starts consuming messages. Blocks until the channel closes or cancellation.
    /// </summary>
    private async Task ConsumeEventsAsync(CancellationToken stoppingToken)
    {
        var connection = await _rabbitMqService.GetOrCreateConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare the queue (idempotent — safe if Dev A already declared it)
        await _channel.QueueDeclareAsync(
            queue: EventsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Declare the exchange (idempotent)
        await _channel.ExchangeDeclareAsync(
            exchange: RabbitMqService.EventsExchange,
            type: "topic",
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Bind queue to events exchange (idempotent)
        await _channel.QueueBindAsync(
            queue: EventsQueueName,
            exchange: RabbitMqService.EventsExchange,
            routingKey: BindingPattern,
            cancellationToken: stoppingToken);

        // Prefetch 10 messages at a time for back-pressure control
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            await HandleMessageAsync(ea, stoppingToken);
        };

        await _channel.BasicConsumeAsync(
            queue: EventsQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "RabbitMQ consumer bound to {Exchange} via queue {Queue}",
            RabbitMqService.EventsExchange, EventsQueueName);

        // Keep alive until cancellation — the consumer runs via callbacks
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Handles an incoming agent event message: deserialize, persist, notify, ack.
    /// Nacks on failure to allow redelivery.
    /// </summary>
    internal async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            var body = ea.Body.ToArray();
            var message = JsonSerializer.Deserialize(body, AgentMessageJsonContext.Default.AgentMessage);

            if (message is null)
            {
                _logger.LogWarning("Received null/unparseable message — acking to discard");
                if (_channel is not null)
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            // Fix properties missing from Python stub-agent wire format
            message.RoutingKey = ea.RoutingKey;
            
            if (string.IsNullOrWhiteSpace(message.AgentId))
            {
                if (message.Payload.ValueKind == JsonValueKind.Object && message.Payload.TryGetProperty("agentId", out var agentIdEl))
                {
                    message.AgentId = agentIdEl.GetString() ?? "";
                }
                else if (ea.RoutingKey.StartsWith("agent."))
                {
                    var parts = ea.RoutingKey.Split('.');
                    if (parts.Length >= 2)
                        message.AgentId = parts[1];
                }
            }

            _logger.LogDebug(
                "Processing agent event: type={Type}, agentId={AgentId}, routingKey={RoutingKey}",
                message.Type, message.AgentId, ea.RoutingKey);

            // Route chat.response messages to dedicated handler (T-171)
            if (message.Type == "chat.architect_response")
            {
                await HandleChatResponseAsync(message);

                if (_channel is not null)
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            // Route chat.plan_proposed messages to plan proposal handler (T-194)
            if (message.Type == "chat.plan_proposed")
            {
                await HandlePlanProposalAsync(message);

                if (_channel is not null)
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
                return;
            }

            // Persist as audit trail Event using a scoped DI container
            await using var scope = _scopeFactory.CreateAsyncScope();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notifier = scope.ServiceProvider.GetRequiredService<IRealTimeNotifier>();

            var agentEvent = new Event
            {
                Id = Guid.NewGuid(),
                EntityType = "Agent",
                EntityId = Guid.TryParse(message.AgentId, out var agentGuid) ? agentGuid : Guid.Empty,
                EventType = MapMessageTypeToEventType(message.Type),
                Payload = JsonSerializer.Serialize(message, AgentMessageJsonContext.Default.AgentMessage),
                Timestamp = message.Timestamp
            };

            unitOfWork.BeginTransaction();
            await eventRepository.SaveAsync(agentEvent);
            await unitOfWork.CommitAsync();

            // Push real-time notification to connected dashboard clients
            await notifier.NotifyJobUpdatedAsync(
                projectId: null,
                jobId: agentEvent.EntityId,
                status: message.Type);

            if (_channel is not null)
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);

            _logger.LogDebug("Agent event persisted and acked: {EventId}", agentEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process agent event — nacking for redelivery");

            try
            {
                if (_channel is not null)
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, ct);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "Failed to nack message after processing error");
            }
        }
    }

    /// <summary>
    /// Maps an agent message type string to the closest <see cref="EventType"/> enum value.
    /// Unknown types default to <see cref="EventType.TaskStarted"/>.
    /// </summary>
    internal static EventType MapMessageTypeToEventType(string messageType)
    {
        return messageType switch
        {
            "agent.started" => EventType.TaskStarted,
            "agent.progress" => EventType.TaskStarted,
            "agent.completed" => EventType.TaskCompleted,
            "agent.failed" => EventType.TaskFailed,
            "agent.blocker" => EventType.TaskStarted,
            "chat.architect_response" => EventType.AgentChatResponse,
            "chat.plan_proposed" => EventType.AgentChatResponse,
            _ => EventType.TaskCreated
        };
    }

    /// <summary>
    /// Handles a chat.architect_response event from an Architect agent.
    /// Creates a ChatMessage entity with SenderRole="Architect", persists it,
    /// and pushes the message to connected clients via SignalR.
    /// REF: JOB-018 T-171
    /// </summary>
    internal async Task HandleChatResponseAsync(AgentMessage message)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var chatRepo = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealTimeNotifier>();

        // Extract projectId from payload since the routing key is agent.{id}.completed
        Guid projectId = Guid.Empty;
        if (message.Payload.ValueKind == JsonValueKind.Object && message.Payload.TryGetProperty("projectId", out var projEl))
        {
            Guid.TryParse(projEl.GetString(), out projectId);
        }

        if (projectId == Guid.Empty && message.RoutingKey.StartsWith("architect."))
        {
            var projectIdStr = message.RoutingKey["architect.".Length..];
            Guid.TryParse(projectIdStr, out projectId);
        }

        var content = "";
        if (message.Payload.ValueKind == JsonValueKind.Object && message.Payload.TryGetProperty("content", out var tempElement))
        {
            content = tempElement.GetString() ?? "";
        }
        else
        {
            // Fallback for backwards compatibility or unexpected format
            content = message.Payload.ToString() ?? "";
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SenderRole = "Architect",
            SenderName = "Architect",
            Content = content,
            CreatedAt = message.Timestamp
        };

        unitOfWork.BeginTransaction();
        await chatRepo.SaveAsync(chatMessage);
        await unitOfWork.CommitAsync();

        await notifier.NotifyChatMessageAsync(
            projectId, chatMessage.Id, chatMessage.SenderRole,
            chatMessage.SenderName, chatMessage.Content, chatMessage.CreatedAt);

        _logger.LogInformation(
            "Architect chat response persisted as ChatMessage {MessageId} for project {ProjectId}",
            chatMessage.Id, projectId);
    }

    /// <summary>
    /// Handles a chat.plan_proposed event from an Architect agent.
    /// Creates a ChatMessage entity with SenderRole="Architect" and MessageType="plan_proposal",
    /// persists it, and pushes the message to connected clients via SignalR.
    /// REF: JOB-022 T-194
    /// </summary>
    internal async Task HandlePlanProposalAsync(AgentMessage message)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var chatRepo = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealTimeNotifier>();

        Guid projectId = Guid.Empty;
        if (message.Payload.ValueKind == System.Text.Json.JsonValueKind.Object && message.Payload.TryGetProperty("projectId", out var projEl))
        {
            Guid.TryParse(projEl.GetString(), out projectId);
        }

        var content = "";
        if (message.Payload.ValueKind == System.Text.Json.JsonValueKind.Object && message.Payload.TryGetProperty("summary", out var summaryEl))
        {
            content = summaryEl.GetString() ?? "";
        }

        // Include plan markdown if present
        if (message.Payload.ValueKind == System.Text.Json.JsonValueKind.Object && message.Payload.TryGetProperty("planMarkdown", out var mdEl))
        {
            var planMd = mdEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(planMd))
            {
                content = planMd;
            }
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SenderRole = "Architect",
            SenderName = "Architect",
            Content = content,
            MessageType = "plan_proposal",
            CreatedAt = message.Timestamp
        };

        unitOfWork.BeginTransaction();
        await chatRepo.SaveAsync(chatMessage);
        await unitOfWork.CommitAsync();

        await notifier.NotifyChatMessageAsync(
            projectId, chatMessage.Id, chatMessage.SenderRole,
            chatMessage.SenderName, chatMessage.Content, chatMessage.CreatedAt);

        _logger.LogInformation(
            "Architect plan proposal persisted as ChatMessage {MessageId} for project {ProjectId}",
            chatMessage.Id, projectId);
    }

    /// <summary>Safely disposes the consumer channel.</summary>
    private async Task CleanupChannelAsync()
    {
        if (_channel is null) return;

        try
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing consumer channel during cleanup");
        }
        finally
        {
            _channel = null;
        }
    }
}
