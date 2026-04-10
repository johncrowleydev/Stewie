using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Stewie.Application.Configuration;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Services;

public class AgentLifecycleServiceHealthCheckTests
{
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentRuntime _stubRuntime;
    private readonly AgentLifecycleService _service;

    public AgentLifecycleServiceHealthCheckTests()
    {
        _sessionRepo = Substitute.For<IAgentSessionRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _notifier = Substitute.For<IRealTimeNotifier>();
        _rabbitMq = Substitute.For<IRabbitMqService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _stubRuntime = Substitute.For<IAgentRuntime>();
        _stubRuntime.RuntimeName.Returns("stub");

        var options = Options.Create(new RabbitMqOptions());

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

    [Fact]
    public async Task GetActiveArchitectAsync_ContainerDead_HealsSessionAndReturnsNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var containerId = "dead-container-123";

        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = projectId,
            AgentRole = "architect",
            RuntimeName = "stub",
            Status = AgentSessionStatus.Active,
            ContainerId = containerId
        };

        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect")
            .Returns(session);

        _stubRuntime.GetStatusAsync(containerId, Arg.Any<CancellationToken>())
            .Returns(AgentRuntimeStatus.Stopped);

        // Act
        var result = await _service.GetActiveArchitectAsync(projectId);

        // Assert
        Assert.Null(result); // Architect is offline
        Assert.Equal(AgentSessionStatus.Terminated, session.Status); // Session healed
        Assert.NotNull(session.StoppedAt);
        Assert.Contains("Auto-healed", session.StopReason);

        _unitOfWork.Received(1).BeginTransaction();
        await _sessionRepo.Received(1).SaveAsync(session);
        await _eventRepo.Received(1).SaveAsync(Arg.Is<Event>(e => e.EventType == EventType.AgentTerminated));
        await _unitOfWork.Received(1).CommitAsync();
        await _notifier.Received(1).NotifyAgentStatusChangedAsync(projectId, sessionId, "Terminated");
    }

    [Fact]
    public async Task GetActiveArchitectAsync_ContainerAlive_ReturnsSession()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var containerId = "alive-container-456";

        var session = new AgentSession
        {
            Id = sessionId,
            ProjectId = projectId,
            AgentRole = "architect",
            RuntimeName = "stub",
            Status = AgentSessionStatus.Active,
            ContainerId = containerId
        };

        _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect")
            .Returns(session);

        _stubRuntime.GetStatusAsync(containerId, Arg.Any<CancellationToken>())
            .Returns(AgentRuntimeStatus.Running);

        // Act
        var result = await _service.GetActiveArchitectAsync(projectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.Id);
        Assert.Equal(AgentSessionStatus.Active, session.Status); // Unchanged

        await _sessionRepo.DidNotReceive().SaveAsync(Arg.Any<AgentSession>());
        await _unitOfWork.DidNotReceive().CommitAsync();
    }
}
