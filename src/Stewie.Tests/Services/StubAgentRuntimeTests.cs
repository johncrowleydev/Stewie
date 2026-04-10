/// <summary>
/// Unit tests for StubAgentRuntime — pure unit tests, no Docker required.
/// REF: JOB-017 T-169
/// </summary>
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Domain.Messaging;
using Stewie.Infrastructure.AgentRuntimes;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for <see cref="StubAgentRuntime"/> that don't require Docker.
/// Covers constructor validation, naming conventions, and argument guards.
/// </summary>
public class StubAgentRuntimeTests
{
    // ── RuntimeName ────────────────────────────────────────────────────

    [Fact]
    public void RuntimeName_ReturnsStub()
    {
        // Arrange
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        // Act & Assert
        Assert.Equal("stub", runtime.RuntimeName);
    }

    // ── DefaultImageName ───────────────────────────────────────────────

    [Fact]
    public void DefaultImageName_IsStewie_stub_agent()
    {
        Assert.Equal("stewie-stub-agent", StubAgentRuntime.DefaultImageName);
    }

    // ── Constructor ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new StubAgentRuntime(null!));
    }

    [Fact]
    public void Constructor_AcceptsCustomImageName()
    {
        // Should not throw
        var runtime = new StubAgentRuntime(
            NullLogger<StubAgentRuntime>.Instance,
            "my-custom-agent");

        Assert.Equal("stub", runtime.RuntimeName);
    }

    // ── FormatContainerName ────────────────────────────────────────────

    [Fact]
    public void FormatContainerName_ProducesValidDockerName()
    {
        // Arrange
        var sessionId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        // Act
        var name = StubAgentRuntime.FormatContainerName(sessionId);

        // Assert — no hyphens in the GUID portion, valid Docker name
        Assert.Equal("stewie-agent-550e8400e29b41d4a716446655440000", name);
        Assert.DoesNotContain(" ", name);
        Assert.StartsWith("stewie-agent-", name);
    }

    [Fact]
    public void FormatContainerName_DifferentGuids_ProduceDifferentNames()
    {
        var name1 = StubAgentRuntime.FormatContainerName(Guid.NewGuid());
        var name2 = StubAgentRuntime.FormatContainerName(Guid.NewGuid());

        Assert.NotEqual(name1, name2);
    }

    // ── LaunchAsync argument guards ────────────────────────────────────

    [Fact]
    public async Task LaunchAgentAsync_ThrowsOnNullRequest()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.LaunchAgentAsync(null!));
    }

    // ── TerminateAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task TerminateAgentAsync_ThrowsOnNullContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.TerminateAgentAsync(null!));
    }

    [Fact]
    public async Task TerminateAgentAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.TerminateAgentAsync(""));
    }

    // ── IsRunningAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ThrowsOnNullContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.GetStatusAsync(null!));
    }

    [Fact]
    public async Task GetStatusAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.GetStatusAsync(""));
    }

    // ── AgentLaunchRequest model tests ─────────────────────────────────

    [Fact]
    public void AgentLaunchRequest_Defaults_AreCorrect()
    {
        var request = new AgentLaunchRequest();

        Assert.Equal(Guid.Empty, request.SessionId);
        Assert.Equal(Guid.Empty, request.ProjectId);
        Assert.Null(request.TaskId);
        Assert.Equal(string.Empty, request.AgentRole);
        Assert.Equal("localhost", request.RabbitMqHost);
        Assert.Equal(5672, request.RabbitMqPort);
        Assert.Equal(string.Empty, request.WorkspacePath);
    }

    [Fact]
    public void AgentLaunchRequest_FullConfiguration()
    {
        var sessionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var request = new AgentLaunchRequest
        {
            SessionId = sessionId,
            ProjectId = projectId,
            TaskId = taskId,
            AgentRole = "developer",
            RabbitMqHost = "rabbitmq.internal",
            RabbitMqPort = 5673,
            RabbitMqUser = "dev_user",
            RabbitMqPassword = "dev_pass",
            RabbitMqVHost = "/stewie",
            CommandQueueName = $"agent.{sessionId}",
            WorkspacePath = "/workspaces/test",
        };

        Assert.Equal(sessionId, request.SessionId);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal(taskId, request.TaskId);
        Assert.Equal("developer", request.AgentRole);
        Assert.Equal("rabbitmq.internal", request.RabbitMqHost);
        Assert.Equal(5673, request.RabbitMqPort);
    }
}
