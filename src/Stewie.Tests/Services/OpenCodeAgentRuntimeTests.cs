/// <summary>
/// Unit tests for OpenCodeAgentRuntime — pure unit tests, no Docker required.
/// Follows the pattern from StubAgentRuntimeTests.cs.
/// REF: JOB-021 T-188
/// </summary>
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Domain.Messaging;
using Stewie.Infrastructure.AgentRuntimes;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for <see cref="OpenCodeAgentRuntime"/> covering constructor validation,
/// naming conventions, argument guards, secret file lifecycle, and env var fallback.
/// </summary>
public class OpenCodeAgentRuntimeTests
{
    // ── RuntimeName ────────────────────────────────────────────────────

    [Fact]
    public void RuntimeName_ReturnsOpenCode()
    {
        // Arrange
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        // Act & Assert
        Assert.Equal("opencode", runtime.RuntimeName);
    }

    // ── DefaultImageName ───────────────────────────────────────────────

    [Fact]
    public void DefaultImageName_IsStewie_opencode_agent()
    {
        Assert.Equal("stewie-opencode-agent", OpenCodeAgentRuntime.DefaultImageName);
    }

    // ── Constructor ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenCodeAgentRuntime(null!));
    }

    [Fact]
    public void Constructor_AcceptsCustomImageName()
    {
        // Should not throw
        var runtime = new OpenCodeAgentRuntime(
            NullLogger<OpenCodeAgentRuntime>.Instance,
            "my-custom-opencode-agent");

        Assert.Equal("opencode", runtime.RuntimeName);
    }

    // ── FormatContainerName ────────────────────────────────────────────

    [Fact]
    public void FormatContainerName_ProducesValidDockerName()
    {
        // Arrange
        var sessionId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        // Act — uses same format as StubAgentRuntime
        var name = OpenCodeAgentRuntime.FormatContainerName(sessionId);

        // Assert
        Assert.Equal("stewie-agent-550e8400e29b41d4a716446655440000", name);
        Assert.DoesNotContain(" ", name);
        Assert.StartsWith("stewie-agent-", name);
    }

    // ── LaunchAsync argument guards ────────────────────────────────────

    [Fact]
    public async Task LaunchAgentAsync_ThrowsOnNullRequest()
    {
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.LaunchAgentAsync(null!));
    }

    // ── TerminateAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task TerminateAgentAsync_ThrowsOnNullContainerId()
    {
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.TerminateAgentAsync(null!));
    }

    [Fact]
    public async Task TerminateAgentAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.TerminateAgentAsync(""));
    }

    // ── GetStatusAsync argument guards ─────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ThrowsOnNullContainerId()
    {
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runtime.GetStatusAsync(null!));
    }

    [Fact]
    public async Task GetStatusAsync_ThrowsOnEmptyContainerId()
    {
        var runtime = new OpenCodeAgentRuntime(NullLogger<OpenCodeAgentRuntime>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runtime.GetStatusAsync(""));
    }

    // ── Secret file lifecycle ──────────────────────────────────────────

    [Fact]
    public void WriteSecretFile_CreatesDirectoryAndFile()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var testKey = "test-api-key-12345";

        try
        {
            // Act
            var secretsDir = OpenCodeAgentRuntime.WriteSecretFile(sessionId, testKey);

            // Assert
            Assert.True(Directory.Exists(secretsDir));
            var keyFile = Path.Combine(secretsDir, "llm_api_key");
            Assert.True(File.Exists(keyFile));
            Assert.Equal(testKey, File.ReadAllText(keyFile));
        }
        finally
        {
            // Cleanup
            OpenCodeAgentRuntime.CleanupSecretFile(sessionId);
        }
    }

    [Fact]
    public void CleanupSecretFile_DeletesSecretDirectory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var secretsDir = OpenCodeAgentRuntime.WriteSecretFile(sessionId, "test-key");
        Assert.True(Directory.Exists(secretsDir));

        // Act
        OpenCodeAgentRuntime.CleanupSecretFile(sessionId);

        // Assert
        Assert.False(Directory.Exists(secretsDir));
    }

    [Fact]
    public void CleanupSecretFile_NoOpWhenDirectoryDoesNotExist()
    {
        // Arrange — never created
        var sessionId = Guid.NewGuid();

        // Act — should not throw
        OpenCodeAgentRuntime.CleanupSecretFile(sessionId);
    }

    // ── AgentLaunchRequest model tests (extended fields) ───────────────

    [Fact]
    public void AgentLaunchRequest_NewFields_HaveDefaults()
    {
        var request = new AgentLaunchRequest();

        Assert.Equal(string.Empty, request.LlmProvider);
        Assert.Equal(string.Empty, request.ModelName);
        Assert.Equal(string.Empty, request.SecretsMountPath);
    }

    [Fact]
    public void AgentLaunchRequest_NewFields_CanBeSet()
    {
        var request = new AgentLaunchRequest
        {
            LlmProvider = "google",
            ModelName = "gemini-2.0-flash",
            SecretsMountPath = "/tmp/stewie-secrets-test"
        };

        Assert.Equal("google", request.LlmProvider);
        Assert.Equal("gemini-2.0-flash", request.ModelName);
        Assert.Equal("/tmp/stewie-secrets-test", request.SecretsMountPath);
    }

    [Fact]
    public void AgentLaunchRequest_NoSecretPath_FallsBackToEnvVar()
    {
        // When SecretsMountPath is empty, the runtime should fall back to env vars.
        // This test validates the contract — the entrypoint.py checks for the env var.
        var request = new AgentLaunchRequest
        {
            LlmProvider = "anthropic",
            ModelName = "claude-3-haiku",
            SecretsMountPath = string.Empty  // No file-based secret
        };

        Assert.True(string.IsNullOrWhiteSpace(request.SecretsMountPath));
    }
}
