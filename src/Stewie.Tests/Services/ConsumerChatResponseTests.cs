/// <summary>
/// Unit tests for RabbitMqConsumerHostedService chat.response handling (JOB-018 T-171).
/// Validates that chat.response agent events are persisted as ChatMessage entities
/// and pushed via SignalR.
/// REF: JOB-018 T-171, T-173, GOV-002
/// </summary>
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Stewie.Infrastructure.Services;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for chat.response message handling in RabbitMqConsumerHostedService.
/// </summary>
public class ConsumerChatResponseTests
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;
    private readonly IEventRepository _eventRepo;
    private readonly RabbitMqConsumerHostedService _consumer;

    public ConsumerChatResponseTests()
    {
        _chatRepo = Substitute.For<IChatMessageRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _notifier = Substitute.For<IRealTimeNotifier>();
        _eventRepo = Substitute.For<IEventRepository>();

        _chatRepo.SaveAsync(Arg.Any<ChatMessage>())
            .Returns(ci => ci.Arg<ChatMessage>());

        // Build a real IServiceScopeFactory with our test services
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IChatMessageRepository>(_ => _chatRepo);
        serviceCollection.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        serviceCollection.AddScoped<IRealTimeNotifier>(_ => _notifier);
        serviceCollection.AddScoped<IEventRepository>(_ => _eventRepo);

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // RabbitMqService needs a real instance (constructor validates non-null).
        // The HandleChatResponseAsync path doesn't touch the connection.
        var dummySettings = new RabbitMqSettings();
        var dummyRabbitMq = new RabbitMqService(dummySettings, NullLogger<RabbitMqService>.Instance);

        _consumer = new RabbitMqConsumerHostedService(
            dummyRabbitMq,
            scopeFactory,
            NullLogger<RabbitMqConsumerHostedService>.Instance);
    }

    // ========================================
    // chat.response → ChatMessage persistence
    // ========================================

    /// <summary>
    /// A chat.response message should be persisted as a ChatMessage
    /// with SenderRole="Architect".
    /// </summary>
    [Fact]
    public async Task HandleChatResponse_PersistsChatMessage()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var message = new AgentMessage
        {
            Type = "chat.architect_response",
            AgentId = Guid.NewGuid().ToString(),
            RoutingKey = $"architect.{projectId}",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement("Hello Human, I'm the Architect!"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _consumer.HandleChatResponseAsync(message);

        // Assert — ChatMessage was saved
        await _chatRepo.Received(1).SaveAsync(Arg.Is<ChatMessage>(m =>
            m.SenderRole == "Architect" &&
            m.SenderName == "Architect" &&
            m.Content == "Hello Human, I'm the Architect!" &&
            m.ProjectId == projectId));
    }

    /// <summary>
    /// A chat.response should push a SignalR notification after persistence.
    /// </summary>
    [Fact]
    public async Task HandleChatResponse_PushesSignalRNotification()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var message = new AgentMessage
        {
            Type = "chat.architect_response",
            AgentId = Guid.NewGuid().ToString(),
            RoutingKey = $"architect.{projectId}",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement("SignalR test response"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _consumer.HandleChatResponseAsync(message);

        // Assert — NotifyChatMessageAsync was called
        await _notifier.Received(1).NotifyChatMessageAsync(
            projectId,
            Arg.Any<Guid>(),
            "Architect",
            "Architect",
            "SignalR test response",
            Arg.Any<DateTime>());
    }

    /// <summary>
    /// A chat.response should commit the UnitOfWork transaction.
    /// </summary>
    [Fact]
    public async Task HandleChatResponse_CommitsTransaction()
    {
        // Arrange
        var message = new AgentMessage
        {
            Type = "chat.architect_response",
            AgentId = Guid.NewGuid().ToString(),
            RoutingKey = $"architect.{Guid.NewGuid()}",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement("Transaction test"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _consumer.HandleChatResponseAsync(message);

        // Assert
        _unitOfWork.Received(1).BeginTransaction();
        await _unitOfWork.Received(1).CommitAsync();
    }

    /// <summary>
    /// The projectId should be extracted from the routing key pattern "architect.{guid}".
    /// </summary>
    [Fact]
    public async Task HandleChatResponse_ExtractsProjectIdFromRoutingKey()
    {
        // Arrange
        var expectedProjectId = Guid.NewGuid();
        var message = new AgentMessage
        {
            Type = "chat.architect_response",
            AgentId = Guid.NewGuid().ToString(),
            RoutingKey = $"architect.{expectedProjectId}",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement("Routing key test"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _consumer.HandleChatResponseAsync(message);

        // Assert
        await _chatRepo.Received(1).SaveAsync(Arg.Is<ChatMessage>(m =>
            m.ProjectId == expectedProjectId));
    }

    /// <summary>
    /// When the routing key does not match the expected pattern,
    /// projectId should default to Guid.Empty.
    /// </summary>
    [Fact]
    public async Task HandleChatResponse_UnknownRoutingKey_UsesEmptyGuid()
    {
        // Arrange
        var message = new AgentMessage
        {
            Type = "chat.architect_response",
            AgentId = Guid.NewGuid().ToString(),
            RoutingKey = "unknown.routing.key",
            Payload = System.Text.Json.JsonSerializer.SerializeToElement("Fallback test"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _consumer.HandleChatResponseAsync(message);

        // Assert
        await _chatRepo.Received(1).SaveAsync(Arg.Is<ChatMessage>(m =>
            m.ProjectId == Guid.Empty));
    }

    // ========================================
    // MapMessageTypeToEventType
    // ========================================

    /// <summary>
    /// chat.response should map to EventType.AgentChatResponse.
    /// </summary>
    [Fact]
    public void MapMessageType_ChatResponse_ReturnsAgentChatResponse()
    {
        var result = RabbitMqConsumerHostedService.MapMessageTypeToEventType("chat.architect_response");
        Assert.Equal(EventType.AgentChatResponse, result);
    }

    /// <summary>
    /// Verify all known message types still map correctly (regression guard).
    /// </summary>
    [Theory]
    [InlineData("agent.started", EventType.TaskStarted)]
    [InlineData("agent.progress", EventType.TaskStarted)]
    [InlineData("agent.completed", EventType.TaskCompleted)]
    [InlineData("agent.failed", EventType.TaskFailed)]
    [InlineData("agent.blocker", EventType.TaskStarted)]
    [InlineData("chat.architect_response", EventType.AgentChatResponse)]
    [InlineData("unknown", EventType.TaskCreated)]
    public void MapMessageType_KnownTypes_MapCorrectly(string messageType, EventType expected)
    {
        Assert.Equal(expected, RabbitMqConsumerHostedService.MapMessageTypeToEventType(messageType));
    }
}
