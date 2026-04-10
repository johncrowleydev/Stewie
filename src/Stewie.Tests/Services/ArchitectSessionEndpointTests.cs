/// <summary>
/// Unit tests for the Architect session management endpoints on AgentsController (JOB-018 T-172).
/// Validates StartArchitect, StopArchitect, and GetArchitectStatus endpoints.
/// Uses a real AgentLifecycleService with mocked repositories (NSubstitute can't mock
/// non-virtual methods on concrete classes).
/// REF: JOB-018 T-172, T-173, GOV-002
/// </summary>
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Api.Controllers;
using Stewie.Application.Configuration;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for Architect session management endpoints (T-172).
/// Constructs a real AgentLifecycleService backed by NSubstitute mocks.
/// </summary>
public class ArchitectSessionEndpointTests
{
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentRuntime _stubRuntime;
    private readonly AgentLifecycleService _lifecycle;
    private readonly AgentsController _controller;
    private readonly Guid _projectId = Guid.NewGuid();

    public ArchitectSessionEndpointTests()
    {
        _sessionRepo = Substitute.For<IAgentSessionRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _notifier = Substitute.For<IRealTimeNotifier>();
        _rabbitMq = Substitute.For<IRabbitMqService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _stubRuntime = Substitute.For<IAgentRuntime>();
        _stubRuntime.RuntimeName.Returns("stub");
        _stubRuntime.LaunchAgentAsync(Arg.Any<AgentLaunchRequest>(), Arg.Any<CancellationToken>())
            .Returns("container-arch-123");

        var options = Options.Create(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            VirtualHost = "stewie",
            UserName = "stewie",
            Password = "test-pass"
        });

        _lifecycle = new AgentLifecycleService(
            _sessionRepo, _eventRepo, _notifier, _rabbitMq, _unitOfWork,
            new[] { _stubRuntime }, options,
            NullLogger<AgentLifecycleService>.Instance);

        _controller = new AgentsController(
            _lifecycle,
            NullLogger<AgentsController>.Instance);
    }

    // ========================================
    // StartArchitect
    // ========================================

    /// <summary>
    /// StartArchitect with valid projectId returns 201 with session details.
    /// </summary>
    [Fact]
    public async Task StartArchitect_ValidProject_Returns201()
    {
        // Arrange — no existing active session
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act
        var result = await _controller.StartArchitect(
            _projectId, new StartArchitectRequest { RuntimeName = "stub" });

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);

        // Verify runtime was called
        await _stubRuntime.Received(1).LaunchAgentAsync(
            Arg.Is<AgentLaunchRequest>(req => req.AgentRole == "architect"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// StartArchitect with empty Guid returns 400.
    /// </summary>
    [Fact]
    public async Task StartArchitect_EmptyProjectId_Returns400()
    {
        // Act
        var result = await _controller.StartArchitect(Guid.Empty);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// StartArchitect when an active session already exists returns 409.
    /// </summary>
    [Fact]
    public async Task StartArchitect_DuplicateSession_Returns409()
    {
        // Arrange — existing active session
        var existing = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(existing);

        // Act
        var result = await _controller.StartArchitect(
            _projectId, new StartArchitectRequest { RuntimeName = "stub" });

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// StartArchitect with no request body uses default runtime "stub".
    /// </summary>
    [Fact]
    public async Task StartArchitect_NullRequest_UsesDefaultStub()
    {
        // Arrange
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act
        var result = await _controller.StartArchitect(_projectId, null);

        // Assert — should succeed with default stub runtime
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    // ========================================
    // StopArchitect
    // ========================================

    /// <summary>
    /// StopArchitect with active session returns 200.
    /// </summary>
    [Fact]
    public async Task StopArchitect_ActiveSession_Returns200()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = _projectId,
            AgentRole = "architect",
            ContainerId = "container-arch-456",
            RuntimeName = "stub",
            Status = AgentSessionStatus.Active,
            StartedAt = DateTime.UtcNow
        };

        // GetActiveArchitectAsync → returns active session
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(session);
        // GetByIdAsync → used by TerminateAgentAsync and GetStatusAsync
        _sessionRepo.GetByIdAsync(sessionId).Returns(session);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act
        var result = await _controller.StopArchitect(_projectId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(AgentSessionStatus.Terminated, session.Status);
    }

    /// <summary>
    /// StopArchitect with no active session returns 404.
    /// </summary>
    [Fact]
    public async Task StopArchitect_NoActiveSession_Returns404()
    {
        // Arrange
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns((AgentSession?)null);

        // Act
        var result = await _controller.StopArchitect(_projectId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ========================================
    // GetArchitectStatus
    // ========================================

    /// <summary>
    /// GetArchitectStatus with active session returns 200 with session details.
    /// </summary>
    [Fact]
    public async Task GetArchitectStatus_ActiveSession_Returns200()
    {
        // Arrange
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active,
            RuntimeName = "stub",
            StartedAt = DateTime.UtcNow
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns(session);

        // Act
        var result = await _controller.GetArchitectStatus(_projectId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// GetArchitectStatus with no active session returns 200 with active = false.
    /// </summary>
    [Fact]
    public async Task GetArchitectStatus_NoSession_Returns200WithActiveFalse()
    {
        // Arrange
        _sessionRepo.GetActiveByProjectAndRoleAsync(_projectId, "architect")
            .Returns((AgentSession?)null);

        // Act
        var result = await _controller.GetArchitectStatus(_projectId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var activeProp = valueType.GetProperty("active")?.GetValue(okResult.Value);
        Assert.Equal(false, activeProp);
    }
}
