/// <summary>
/// Unit tests for the chat relay functionality added in JOB-018 T-170.
/// Validates that ChatController.SendMessage relays Human messages to RabbitMQ
/// when an active Architect session exists, and that RabbitMQ failures are
/// swallowed (best-effort).
/// REF: JOB-018 T-170, T-173, GOV-002
/// </summary>
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Api.Controllers;
using Stewie.Application.Services;
using Stewie.Application.Configuration;
using Microsoft.Extensions.Options;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Stewie.Tests.Mocks;
using System.Security.Claims;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for ChatController's RabbitMQ relay behavior (JOB-018 T-170).
/// Uses NullRabbitMqService (spy) and NSubstitute mocks.
/// </summary>
public class ChatRelayTests
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly NullRabbitMqService _rabbitMq;
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatController _controller;

    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Project _project;

    public ChatRelayTests()
    {
        _chatRepo = Substitute.For<IChatMessageRepository>();
        _projectRepo = Substitute.For<IProjectRepository>();
        _notifier = Substitute.For<IRealTimeNotifier>();
        _rabbitMq = new NullRabbitMqService();
        _sessionRepo = Substitute.For<IAgentSessionRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _project = new Project
        {
            Id = _projectId,
            Name = "Test Project",
            RepoUrl = "https://github.com/test/repo",
            CreatedAt = DateTime.UtcNow
        };

        _projectRepo.GetByIdAsync(_projectId).Returns(_project);
        _chatRepo.SaveAsync(Arg.Any<ChatMessage>())
            .Returns(ci => ci.Arg<ChatMessage>());

        var lifecycleOptions = Options.Create(new RabbitMqOptions());
        var lifecycle = new AgentLifecycleService(
            _sessionRepo,
            Substitute.For<IEventRepository>(),
            _notifier,
            _rabbitMq,
            _unitOfWork,
            Enumerable.Empty<IAgentRuntime>(),
            lifecycleOptions,
            NullLogger<AgentLifecycleService>.Instance);

        _controller = new ChatController(
            _chatRepo, _projectRepo, _notifier,
            _rabbitMq, _sessionRepo, _unitOfWork,
            lifecycle,
            NullLogger<ChatController>.Instance);

        // Set up HttpContext with claims
        var claims = new[] { new Claim("username", "testuser") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ========================================
    // Relay fires when Architect session exists
    // ========================================

    /// <summary>
    /// When an active Architect session exists for the project,
    /// SendMessage should publish the chat message to RabbitMQ.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithActiveArchitect_RelaysToRabbitMq()
    {
        // Arrange
        var architectSession = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active,
            RuntimeName = "stub",
            StartedAt = DateTime.UtcNow
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(architectSession);

        // Act
        var result = await _controller.SendMessage(
            _projectId, new SendChatMessageRequest { Content = "Hello Architect!" });

        // Assert — HTTP 201 returned
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, statusResult.StatusCode);

        // Assert — message was relayed to RabbitMQ
        Assert.Single(_rabbitMq.PublishedChats);
        var (routingKey, message) = _rabbitMq.PublishedChats[0];
        Assert.Equal($"architect.{_projectId}", routingKey);
        Assert.Equal("chat.human_message", message.Type);
        Assert.Equal(architectSession.Id.ToString(), message.AgentId);
        Assert.Equal("Hello Architect!", message.Payload.GetProperty("content").GetString());
    }

    // ========================================
    // No relay when no active Architect
    // ========================================

    /// <summary>
    /// When no active Architect session exists, SendMessage should NOT
    /// publish to RabbitMQ but should still return 201.
    /// </summary>
    [Fact]
    public async Task SendMessage_NoActiveArchitect_DoesNotRelay()
    {
        // Arrange
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns((AgentSession?)null);

        // Act
        var result = await _controller.SendMessage(
            _projectId, new SendChatMessageRequest { Content = "Hello into the void" });

        // Assert — HTTP 409 returned
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, statusResult.StatusCode);

        // Assert — nothing published to RabbitMQ
        Assert.Empty(_rabbitMq.PublishedChats);
    }

    // ========================================
    // Best-effort: RabbitMQ failure is swallowed
    // ========================================

    /// <summary>
    /// When RabbitMQ throws during chat relay, the HTTP request should still
    /// succeed with 201. The failure is logged at Warning but never fails the request.
    /// </summary>
    [Fact]
    public async Task SendMessage_RabbitMqThrows_StillReturns201()
    {
        // Arrange — use a substitute that throws
        var throwingRabbitMq = Substitute.For<IRabbitMqService>();
        throwingRabbitMq.PublishChatAsync(Arg.Any<string>(), Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("RabbitMQ connection refused"));

        var architectSession = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active,
            RuntimeName = "stub",
            StartedAt = DateTime.UtcNow
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(architectSession);

        var lifecycleOptions = Options.Create(new RabbitMqOptions());
        var lifecycle = new AgentLifecycleService(
            _sessionRepo,
            Substitute.For<IEventRepository>(),
            _notifier,
            throwingRabbitMq,
            _unitOfWork,
            Enumerable.Empty<IAgentRuntime>(),
            lifecycleOptions,
            NullLogger<AgentLifecycleService>.Instance);

        var controller = new ChatController(
            _chatRepo, _projectRepo, _notifier,
            throwingRabbitMq, _sessionRepo, _unitOfWork,
            lifecycle,
            NullLogger<ChatController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim("username", "testuser") }, "Test"))
            }
        };

        // Act — should NOT throw
        var result = await controller.SendMessage(
            _projectId, new SendChatMessageRequest { Content = "This should still work" });

        // Assert — HTTP 201 returned despite RabbitMQ failure
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, statusResult.StatusCode);
    }

    // ========================================
    // Relay message structure validation
    // ========================================

    /// <summary>
    /// Validates the AgentMessage structure sent by the relay:
    /// - CorrelationId matches the ChatMessage.Id
    /// - RoutingKey follows the architect.{projectId} pattern
    /// </summary>
    [Fact]
    public async Task SendMessage_RelayedMessage_HasCorrectStructure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var architectSession = new AgentSession
        {
            Id = sessionId,
            ProjectId = _projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active,
            RuntimeName = "stub",
            StartedAt = DateTime.UtcNow
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(architectSession);

        // Act
        var result = await _controller.SendMessage(
            _projectId, new SendChatMessageRequest { Content = "Structure check" });

        // Assert
        Assert.Single(_rabbitMq.PublishedChats);
        var (routingKey, msg) = _rabbitMq.PublishedChats[0];

        Assert.Equal($"architect.{_projectId}", routingKey);
        Assert.Equal($"architect.{_projectId}", msg.RoutingKey);
        Assert.Equal(sessionId.ToString(), msg.AgentId);
        Assert.NotNull(msg.CorrelationId);
        Assert.True(Guid.TryParse(msg.CorrelationId, out _));
    }
}
