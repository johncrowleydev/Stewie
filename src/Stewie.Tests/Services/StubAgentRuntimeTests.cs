/// <summary>
/// Unit tests for StubAgentRuntime — pure unit tests, no Docker required.
/// REF: JOB-017 T-169
/// </summary>
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Application.Models;
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
    public async Task LaunchAsync_ThrowsOnNullRequest()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.LaunchAsync(null!));
    }

    // ── TerminateAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task TerminateAsync_ThrowsOnNullContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.TerminateAsync(null!));
    }

    [Fact]
    public async Task TerminateAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.TerminateAsync(""));
    }

    // ── IsRunningAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task IsRunningAsync_ThrowsOnNullContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.IsRunningAsync(null!));
    }

    [Fact]
    public async Task IsRunningAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.IsRunningAsync(""));
    }

    // ── AgentLaunchRequest model tests ─────────────────────────────────

    [Fact]
    public void AgentLaunchRequest_Defaults_AreCorrect()
    {
        var request = new AgentLaunchRequest();

        Assert.Equal(Guid.Empty, request.SessionId);
        Assert.Equal(Guid.Empty, request.ProjectId);
        Assert.Null(request.TaskId);
        Assert.Equal(string.Empty, request.Role);
        Assert.NotNull(request.RabbitMq);
        Assert.Null(request.WorkspacePath);
        Assert.NotNull(request.Config);
        Assert.Empty(request.Config);
    }

    [Fact]
    public void RabbitMqConnectionInfo_Defaults_AreCorrect()
    {
        var info = new RabbitMqConnectionInfo();

        Assert.Equal("localhost", info.HostName);
        Assert.Equal(5672, info.Port);
        Assert.Equal(string.Empty, info.UserName);
        Assert.Equal(string.Empty, info.Password);
        Assert.Equal("/", info.VirtualHost);
        Assert.Equal(string.Empty, info.AgentQueueName);
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
            Role = "developer",
            RabbitMq = new RabbitMqConnectionInfo
            {
                HostName = "rabbitmq.internal",
                Port = 5673,
                UserName = "dev_user",
                Password = "dev_pass",
                VirtualHost = "/stewie",
                AgentQueueName = $"agent.{sessionId}",
            },
            WorkspacePath = "/workspaces/test",
            Config = new Dictionary<string, string>
            {
                ["timeout"] = "300",
                ["maxRetries"] = "2",
            },
        };

        Assert.Equal(sessionId, request.SessionId);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal(taskId, request.TaskId);
        Assert.Equal("developer", request.Role);
        Assert.Equal("rabbitmq.internal", request.RabbitMq.HostName);
        Assert.Equal(5673, request.RabbitMq.Port);
        Assert.Equal(2, request.Config.Count);
    }
}
