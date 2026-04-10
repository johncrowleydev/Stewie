/// <summary>
/// Unit tests for RabbitMQ service layer — AgentMessage serialization,
/// RabbitMqSettings defaults, consumer message type mapping, and NullRabbitMqService mock.
/// REF: JOB-016 T-161
/// </summary>
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Stewie.Infrastructure.Services;
using Stewie.Tests.Mocks;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for the RabbitMQ service layer components.
/// These are pure unit tests — no RabbitMQ server required.
/// </summary>
public class RabbitMqServiceTests
{
    // ── AgentMessage serialization ─────────────────────────────────────

    [Fact]
    public void AgentMessage_RoundTrip_JsonSerialization()
    {
        // Arrange
        var original = new AgentMessage
        {
            Type = "agent.started",
            AgentId = "agent-abc123",
            RoutingKey = "agent.abc123.started",
            Payload = "{\"taskId\":\"550e8400-e29b-41d4-a716-446655440000\"}",
            Timestamp = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc),
            CorrelationId = "corr-001"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AgentMessage>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.AgentId, deserialized.AgentId);
        Assert.Equal(original.RoutingKey, deserialized.RoutingKey);
        Assert.Equal(original.Payload, deserialized.Payload);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
        Assert.Equal(original.CorrelationId, deserialized.CorrelationId);
    }

    [Fact]
    public void AgentMessage_NullCorrelationId_SerializesCorrectly()
    {
        // Arrange
        var message = new AgentMessage
        {
            Type = "agent.progress",
            AgentId = "agent-xyz",
            RoutingKey = "agent.xyz.progress",
            Payload = "{}",
            CorrelationId = null
        };

        // Act
        var json = JsonSerializer.Serialize(message);
        var deserialized = JsonSerializer.Deserialize<AgentMessage>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.CorrelationId);
    }

    [Fact]
    public void AgentMessage_Defaults_AreCorrect()
    {
        // Act
        var message = new AgentMessage();

        // Assert
        Assert.Equal(string.Empty, message.Type);
        Assert.Equal(string.Empty, message.AgentId);
        Assert.Equal(string.Empty, message.RoutingKey);
        Assert.Equal(string.Empty, message.Payload);
        Assert.Null(message.CorrelationId);
        // Timestamp should be close to now (within 5 seconds)
        Assert.True((DateTime.UtcNow - message.Timestamp).TotalSeconds < 5);
    }

    [Fact]
    public void AgentMessage_SourceGenerated_JsonSerializationRoundTrip()
    {
        // Arrange — test the source-generated JSON context used by RabbitMqService
        var original = new AgentMessage
        {
            Type = "task.assign",
            AgentId = "dev-agent-001",
            RoutingKey = "dev-agent-001",
            Payload = "{\"spec\":\"Build the login page\"}",
            Timestamp = DateTime.UtcNow,
            CorrelationId = "job-42"
        };

        // Act
        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, AgentMessageJsonContext.Default.AgentMessage);
        var deserialized = JsonSerializer.Deserialize(bytes, AgentMessageJsonContext.Default.AgentMessage);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.AgentId, deserialized.AgentId);
        Assert.Equal(original.Payload, deserialized.Payload);
    }

    // ── RabbitMqSettings ───────────────────────────────────────────────

    [Fact]
    public void RabbitMqSettings_Defaults_MatchDockerCompose()
    {
        // Act
        var settings = new RabbitMqSettings();

        // Assert
        Assert.Equal("localhost", settings.HostName);
        Assert.Equal(5672, settings.Port);
        Assert.Equal("stewie", settings.UserName);
        Assert.Equal("stewie_dev", settings.Password);
        Assert.Equal("/", settings.VirtualHost);
        Assert.Equal(3, settings.MaxRetryAttempts);
        Assert.Equal(1000, settings.RetryBaseDelayMs);
    }

    [Fact]
    public void RabbitMqSettings_CustomValues_Override()
    {
        // Arrange & Act
        var settings = new RabbitMqSettings
        {
            HostName = "rabbitmq.prod.internal",
            Port = 5673,
            UserName = "prod_user",
            Password = "prod_secret",
            VirtualHost = "/stewie",
            MaxRetryAttempts = 5,
            RetryBaseDelayMs = 2000
        };

        // Assert
        Assert.Equal("rabbitmq.prod.internal", settings.HostName);
        Assert.Equal(5673, settings.Port);
        Assert.Equal("prod_user", settings.UserName);
        Assert.Equal("prod_secret", settings.Password);
        Assert.Equal("/stewie", settings.VirtualHost);
        Assert.Equal(5, settings.MaxRetryAttempts);
        Assert.Equal(2000, settings.RetryBaseDelayMs);
    }

    // ── Exchange constants ─────────────────────────────────────────────

    [Fact]
    public void RabbitMqService_ExchangeNames_MatchTopology()
    {
        // These constants must match the CON-004 topology
        Assert.Equal("stewie.commands", RabbitMqService.CommandsExchange);
        Assert.Equal("stewie.events", RabbitMqService.EventsExchange);
        Assert.Equal("stewie.chat", RabbitMqService.ChatExchange);
    }

    // ── Consumer message type mapping ──────────────────────────────────

    [Theory]
    [InlineData("agent.started", EventType.TaskStarted)]
    [InlineData("agent.progress", EventType.TaskStarted)]
    [InlineData("agent.completed", EventType.TaskCompleted)]
    [InlineData("agent.failed", EventType.TaskFailed)]
    [InlineData("agent.blocker", EventType.TaskStarted)]
    [InlineData("unknown.type", EventType.TaskCreated)]
    [InlineData("", EventType.TaskCreated)]
    public void MapMessageTypeToEventType_ReturnsCorrectMapping(string messageType, EventType expected)
    {
        // Act
        var result = RabbitMqConsumerHostedService.MapMessageTypeToEventType(messageType);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Consumer queue name ────────────────────────────────────────────

    [Fact]
    public void ConsumerHostedService_QueueName_IsCorrect()
    {
        Assert.Equal("stewie.api.events", RabbitMqConsumerHostedService.EventsQueueName);
    }

    // ── NullRabbitMqService mock ───────────────────────────────────────

    [Fact]
    public async Task NullRabbitMqService_PublishCommand_CapturesMessage()
    {
        // Arrange
        var mock = new NullRabbitMqService();
        var message = new AgentMessage { Type = "task.assign", AgentId = "dev-1" };

        // Act
        await mock.PublishCommandAsync("dev-1", message);

        // Assert
        Assert.Single(mock.PublishedCommands);
        Assert.Equal("dev-1", mock.PublishedCommands[0].RoutingKey);
        Assert.Equal("task.assign", mock.PublishedCommands[0].Message.Type);
    }

    [Fact]
    public async Task NullRabbitMqService_PublishChat_CapturesMessage()
    {
        // Arrange
        var mock = new NullRabbitMqService();
        var message = new AgentMessage { Type = "chat.message", AgentId = "architect-1" };

        // Act
        await mock.PublishChatAsync("architect.proj-42", message);

        // Assert
        Assert.Single(mock.PublishedChats);
        Assert.Equal("architect.proj-42", mock.PublishedChats[0].RoutingKey);
    }

    [Fact]
    public async Task NullRabbitMqService_IsConnected_DefaultsToTrue()
    {
        // Arrange
        var mock = new NullRabbitMqService();

        // Act & Assert
        Assert.True(await mock.IsConnectedAsync());
    }

    [Fact]
    public async Task NullRabbitMqService_IsConnected_RespectsSimulatedState()
    {
        // Arrange
        var mock = new NullRabbitMqService { SimulatedConnectionState = false };

        // Act & Assert
        Assert.False(await mock.IsConnectedAsync());
    }

    [Fact]
    public async Task NullRabbitMqService_MultiplePublishes_TrackedSeparately()
    {
        // Arrange
        var mock = new NullRabbitMqService();

        // Act
        await mock.PublishCommandAsync("agent-1", new AgentMessage { Type = "task.assign" });
        await mock.PublishCommandAsync("agent-2", new AgentMessage { Type = "task.assign" });
        await mock.PublishChatAsync("architect", new AgentMessage { Type = "chat.message" });

        // Assert
        Assert.Equal(2, mock.PublishedCommands.Count);
        Assert.Single(mock.PublishedChats);
    }

    // ── RabbitMqService null-guard tests ───────────────────────────────

    [Fact]
    public void RabbitMqService_Constructor_ThrowsOnNullSettings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqService(null!, NullLogger<RabbitMqService>.Instance));
    }

    [Fact]
    public void RabbitMqService_Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqService(new RabbitMqSettings(), null!));
    }

    [Fact]
    public async Task RabbitMqService_IsConnected_ReturnsFalseWhenNoConnection()
    {
        // Arrange — fresh service with no connection established
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act
        var connected = await service.IsConnectedAsync();

        // Assert
        Assert.False(connected);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_PublishCommand_ThrowsOnNullRoutingKey()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act & Assert — null triggers ArgumentNullException (subclass of ArgumentException)
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.PublishCommandAsync(null!, new AgentMessage()));

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_PublishCommand_ThrowsOnEmptyRoutingKey()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PublishCommandAsync("", new AgentMessage()));

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_PublishCommand_ThrowsOnNullMessage()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.PublishCommandAsync("test-key", null!));

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_PublishChat_ThrowsOnNullMessage()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.PublishChatAsync("test-key", null!));

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);

        // Act — should not throw
        await service.DisposeAsync();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqService_PublishAfterDispose_ThrowsObjectDisposed()
    {
        // Arrange
        var service = new RabbitMqService(
            new RabbitMqSettings(),
            NullLogger<RabbitMqService>.Instance);
        await service.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            service.PublishCommandAsync("key", new AgentMessage()));
    }
}
