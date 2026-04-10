/// <summary>
/// Unit tests for AgentLifecycleService.
/// Uses NSubstitute mocks to test launch, terminate, status, and error scenarios
/// without any infrastructure dependencies (no RabbitMQ, no DB, no Docker).
/// REF: JOB-017 T-166, GOV-002
/// </summary>
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Application.Configuration;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for AgentLifecycleService covering launch, terminate, status, and error flows.
/// </summary>
public class AgentLifecycleServiceTests
{
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentRuntime _stubRuntime;
    private readonly AgentLifecycleService _service;

    public AgentLifecycleServiceTests()
    {
        _sessionRepo = Substitute.For<IAgentSessionRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _notifier = Substitute.For<IRealTimeNotifier>();
        _rabbitMq = Substitute.For<IRabbitMqService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _stubRuntime = Substitute.For<IAgentRuntime>();
        _stubRuntime.RuntimeName.Returns("stub");
        _stubRuntime.LaunchAgentAsync(Arg.Any<AgentLaunchRequest>(), Arg.Any<CancellationToken>())
            .Returns("container-abc123");

        var options = Options.Create(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            VirtualHost = "stewie",
            UserName = "stewie",
            Password = "test-pass"
        });

        _service = new AgentLifecycleService(
            _sessionRepo,
            _eventRepo,
            _notifier,
            _rabbitMq,
            _unitOfWork,
            new[] { _stubRuntime },
            options,
            NullLogger<AgentLifecycleService>.Instance);
    }

    // ========================================
    // LaunchAgentAsync tests
    // ========================================

    [Fact]
    public async Task LaunchAgent_ValidRequest_CreatesSessionAndReturnsActive()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "developer")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act
        var result = await _service.LaunchAgentAsync(projectId, "developer", "stub");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("developer", result.AgentRole);
        Assert.Equal("stub", result.RuntimeName);
        Assert.Equal(AgentSessionStatus.Active, result.Status);
        Assert.Equal("container-abc123", result.ContainerId);
        Assert.NotEqual(Guid.Empty, result.Id);

        // Verify persistence calls
        await _sessionRepo.Received(2).SaveAsync(Arg.Any<AgentSession>());
        await _eventRepo.Received(1).SaveAsync(Arg.Any<Event>());
        _unitOfWork.Received(2).BeginTransaction();
        await _unitOfWork.Received(2).CommitAsync();

        // Verify SignalR notification
        await _notifier.Received(1).NotifyAgentStatusChangedAsync(
            projectId, result.Id, "Active");
    }

    [Fact]
    public async Task LaunchAgent_DuplicateActiveSession_ThrowsInvalidOperation()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var existing = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AgentRole = "architect",
            Status = AgentSessionStatus.Active
        };
        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect")
            .Returns(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LaunchAgentAsync(projectId, "architect", "stub"));

        Assert.Contains("already exists", ex.Message);
        await _stubRuntime.DidNotReceive().LaunchAgentAsync(
            Arg.Any<AgentLaunchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaunchAgent_UnknownRuntime_ThrowsInvalidOperation()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "developer")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LaunchAgentAsync(projectId, "developer", "nonexistent-runtime"));

        Assert.Contains("nonexistent-runtime", ex.Message);
    }

    [Fact]
    public async Task LaunchAgent_RuntimeThrows_MarksSessionFailed()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "developer")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());
        _stubRuntime.LaunchAgentAsync(Arg.Any<AgentLaunchRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Container launch failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => _service.LaunchAgentAsync(projectId, "developer", "stub"));

        // Verify session was saved with Failed status (once for Starting, once for Failed)
        await _sessionRepo.Received(2).SaveAsync(Arg.Is<AgentSession>(s =>
            s.Status == AgentSessionStatus.Starting ||
            s.Status == AgentSessionStatus.Failed));
    }

    [Fact]
    public async Task LaunchAgent_PassesRabbitMqConfig_InLaunchRequest()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "developer")
            .Returns((AgentSession?)null);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        AgentLaunchRequest? capturedRequest = null;
        _stubRuntime.LaunchAgentAsync(Arg.Any<AgentLaunchRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<AgentLaunchRequest>();
                return "container-xyz";
            });

        // Act
        await _service.LaunchAgentAsync(projectId, "developer", "stub", taskId, "/workspaces/test");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(projectId, capturedRequest.ProjectId);
        Assert.Equal(taskId, capturedRequest.TaskId);
        Assert.Equal("developer", capturedRequest.AgentRole);
        Assert.Equal("/workspaces/test", capturedRequest.WorkspacePath);
        Assert.Equal("localhost", capturedRequest.RabbitMqHost);
        Assert.Equal(5672, capturedRequest.RabbitMqPort);
        Assert.Equal("stewie", capturedRequest.RabbitMqVHost);
        Assert.StartsWith("agent.", capturedRequest.CommandQueueName);
        Assert.EndsWith(".commands", capturedRequest.CommandQueueName);
    }

    // ========================================
    // TerminateAgentAsync tests
    // ========================================

    [Fact]
    public async Task TerminateAgent_ActiveSession_TerminatesAndUpdatesStatus()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = Guid.NewGuid(),
            ContainerId = "container-123",
            RuntimeName = "stub",
            AgentRole = "developer",
            Status = AgentSessionStatus.Active
        };
        _sessionRepo.GetByIdAsync(sessionId).Returns(session);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());

        // Act
        await _service.TerminateAgentAsync(sessionId, "Test termination");

        // Assert
        Assert.Equal(AgentSessionStatus.Terminated, session.Status);
        Assert.Equal("Test termination", session.StopReason);
        Assert.NotNull(session.StoppedAt);

        await _stubRuntime.Received(1).TerminateAgentAsync("container-123", Arg.Any<CancellationToken>());
        await _eventRepo.Received(1).SaveAsync(Arg.Is<Event>(e => e.EventType == EventType.AgentTerminated));
        await _notifier.Received(1).NotifyAgentStatusChangedAsync(session.ProjectId, sessionId, "Terminated");
    }

    [Fact]
    public async Task TerminateAgent_NotFound_ThrowsKeyNotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepo.GetByIdAsync(sessionId).Returns((AgentSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.TerminateAgentAsync(sessionId));
    }

    [Fact]
    public async Task TerminateAgent_AlreadyTerminal_ThrowsInvalidOperation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new AgentSession
        {
            Id = sessionId,
            Status = AgentSessionStatus.Completed
        };
        _sessionRepo.GetByIdAsync(sessionId).Returns(session);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.TerminateAgentAsync(sessionId));
        Assert.Contains("terminal state", ex.Message);
    }

    [Fact]
    public async Task TerminateAgent_ContainerCleanupFails_StillMarksTerminated()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = Guid.NewGuid(),
            ContainerId = "container-456",
            RuntimeName = "stub",
            Status = AgentSessionStatus.Active
        };
        _sessionRepo.GetByIdAsync(sessionId).Returns(session);
        _sessionRepo.SaveAsync(Arg.Any<AgentSession>())
            .Returns(ci => ci.Arg<AgentSession>());
        _stubRuntime.TerminateAgentAsync("container-456", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Docker API error"));

        // Act — should NOT throw even though container cleanup failed
        await _service.TerminateAgentAsync(sessionId, "Cleanup test");

        // Assert — session is still marked terminated
        Assert.Equal(AgentSessionStatus.Terminated, session.Status);
    }

    // ========================================
    // GetStatusAsync tests
    // ========================================

    [Fact]
    public async Task GetStatus_ExistingSession_ReturnsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = Guid.NewGuid(),
            Status = AgentSessionStatus.Active,
            AgentRole = "architect"
        };
        _sessionRepo.GetByIdAsync(sessionId).Returns(session);

        // Act
        var result = await _service.GetStatusAsync(sessionId);

        // Assert
        Assert.Equal(sessionId, result.Id);
        Assert.Equal(AgentSessionStatus.Active, result.Status);
    }

    [Fact]
    public async Task GetStatus_NotFound_ThrowsKeyNotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _sessionRepo.GetByIdAsync(sessionId).Returns((AgentSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetStatusAsync(sessionId));
    }

    // ========================================
    // GetSessionsByProjectAsync tests
    // ========================================

    [Fact]
    public async Task GetSessionsByProject_ReturnsList()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sessions = new List<AgentSession>
        {
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Status = AgentSessionStatus.Active },
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Status = AgentSessionStatus.Terminated }
        };
        _sessionRepo.GetByProjectIdAsync(projectId).Returns(sessions);

        // Act
        var result = await _service.GetSessionsByProjectAsync(projectId);

        // Assert
        Assert.Equal(2, result.Count);
    }
}
