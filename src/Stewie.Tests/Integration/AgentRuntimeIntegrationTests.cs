/// <summary>
/// Integration tests for agent runtime — require Docker daemon.
/// Marked with Category=Integration trait; skip gracefully when Docker is unavailable.
/// REF: JOB-017 T-169
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Domain.Enums;
using Stewie.Domain.Messaging;
using Stewie.Infrastructure.AgentRuntimes;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="StubAgentRuntime"/> that require Docker.
/// These tests launch actual containers and verify lifecycle operations.
/// Skipped automatically if Docker is not available or image is not built.
/// </summary>
[Trait("Category", "Integration")]
public class AgentRuntimeIntegrationTests : IAsyncLifetime
{
    private readonly StubAgentRuntime _runtime;
    private bool _dockerAvailable;
    private bool _imageAvailable;

    public AgentRuntimeIntegrationTests()
    {
        _runtime = new StubAgentRuntime(NullLogger<StubAgentRuntime>.Instance);
    }

    public async ValueTask InitializeAsync()
    {
        _dockerAvailable = await IsDockerAvailableAsync();
        if (_dockerAvailable)
        {
            _imageAvailable = await IsImageAvailableAsync(StubAgentRuntime.DefaultImageName);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Full lifecycle test ────────────────────────────────────────────

    [Fact]
    public async Task StubAgent_LaunchAndTerminate_FullLifecycle()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker daemon not available");
        Assert.SkipUnless(_imageAvailable, $"Docker image '{StubAgentRuntime.DefaultImageName}' not built");

        var sessionId = Guid.NewGuid();
        var request = CreateTestRequest(sessionId);

        // Act — Launch
        var containerId = await _runtime.LaunchAgentAsync(request);

        try
        {
            // Assert — container is running
            Assert.False(string.IsNullOrWhiteSpace(containerId));
            var status = await _runtime.GetStatusAsync(containerId);
            Assert.Equal(AgentRuntimeStatus.Running, status);
        }
        finally
        {
            // Cleanup — always terminate
            await _runtime.TerminateAgentAsync(containerId);
        }

        // Assert — container is no longer running
        var stoppedStatus = await _runtime.GetStatusAsync(containerId);
        Assert.NotEqual(AgentRuntimeStatus.Running, stoppedStatus);
    }

    // ── Container name format test ─────────────────────────────────────

    [Fact]
    public async Task StubAgent_Launch_CreatesNamedContainer()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker daemon not available");
        Assert.SkipUnless(_imageAvailable, $"Docker image '{StubAgentRuntime.DefaultImageName}' not built");

        var sessionId = Guid.NewGuid();
        var request = CreateTestRequest(sessionId);
        var expectedName = StubAgentRuntime.FormatContainerName(sessionId);

        var containerId = await _runtime.LaunchAgentAsync(request);

        try
        {
            // Verify the container exists with the expected name
            var (exitCode, stdout, _) = await RunDockerAsync(
                $"inspect -f \"{{{{.Name}}}}\" \"{containerId}\"");

            Assert.Equal(0, exitCode);
            Assert.Contains(expectedName, stdout.Trim());
        }
        finally
        {
            await _runtime.TerminateAgentAsync(containerId);
        }
    }

    // ── IsRunning on nonexistent container ─────────────────────────────

    [Fact]
    public async Task StubAgent_IsRunning_ReturnsFalseForNonexistent()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker daemon not available");

        var status = await _runtime.GetStatusAsync("nonexistent_container_id_12345");
        Assert.Equal(AgentRuntimeStatus.Unknown, status);
    }

    // ── Launch with bad image ──────────────────────────────────────────

    [Fact]
    public async Task StubAgent_Launch_ThrowsOnBadImage()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker daemon not available");

        var badRuntime = new StubAgentRuntime(
            NullLogger<StubAgentRuntime>.Instance,
            "stewie-nonexistent-image:99.99.99");

        var request = CreateTestRequest(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            badRuntime.LaunchAgentAsync(request));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    /// <summary>Creates a test launch request with minimal RabbitMQ config.</summary>
    private static AgentLaunchRequest CreateTestRequest(Guid sessionId)
    {
        return new AgentLaunchRequest
        {
            SessionId = sessionId,
            ProjectId = Guid.NewGuid(),
            AgentRole = "developer",
            RabbitMqHost = "host.docker.internal",
            RabbitMqPort = 5672,
            RabbitMqUser = "stewie",
            RabbitMqPassword = "stewie_dev",
            RabbitMqVHost = "stewie",
            CommandQueueName = $"agent.{sessionId}",
        };
    }

    /// <summary>Checks if Docker daemon is running.</summary>
    private static async Task<bool> IsDockerAvailableAsync()
    {
        try
        {
            var (exitCode, _, _) = await RunDockerAsync("info --format \"{{.ServerVersion}}\"");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Checks if a Docker image exists locally.</summary>
    private static async Task<bool> IsImageAvailableAsync(string imageName)
    {
        var (exitCode, _, _) = await RunDockerAsync($"image inspect {imageName}");
        return exitCode == 0;
    }

    /// <summary>Runs a Docker CLI command and returns exit code, stdout, stderr.</summary>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDockerAsync(
        string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Docker process");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }
}
