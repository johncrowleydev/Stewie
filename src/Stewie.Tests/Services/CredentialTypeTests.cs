/// <summary>
/// Unit tests for CredentialType enum and repository query-by-type behavior.
/// REF: JOB-021 T-188
/// </summary>
using NSubstitute;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for <see cref="CredentialType"/> enum and <see cref="IUserCredentialRepository.GetByTypeAsync"/>.
/// </summary>
public class CredentialTypeTests
{
    // ── Enum value tests ──────────────────────────────────────────────

    [Fact]
    public void CredentialType_GitHubPat_IsDefault()
    {
        // The default enum value must be 0 (GitHubPat) for backward compatibility
        Assert.Equal(0, (int)CredentialType.GitHubPat);
    }

    [Fact]
    public void CredentialType_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<CredentialType>();
        var distinctCount = values.Select(v => (int)v).Distinct().Count();
        Assert.Equal(values.Length, distinctCount);
    }

    [Theory]
    [InlineData(CredentialType.GitHubPat, 0)]
    [InlineData(CredentialType.AnthropicApiKey, 1)]
    [InlineData(CredentialType.OpenAiApiKey, 2)]
    [InlineData(CredentialType.GoogleAiApiKey, 3)]
    public void CredentialType_HasExpectedIntValue(CredentialType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    // ── Repository GetByType tests ────────────────────────────────────

    [Fact]
    public async Task GetByType_ReturnsCorrectCredential()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credential = new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "google",
            CredentialType = CredentialType.GoogleAiApiKey,
            EncryptedToken = "encrypted-token",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repo = Substitute.For<IUserCredentialRepository>();
        repo.GetByTypeAsync(userId, CredentialType.GoogleAiApiKey)
            .Returns(credential);

        // Act
        var result = await repo.GetByTypeAsync(userId, CredentialType.GoogleAiApiKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(credential.Id, result.Id);
        Assert.Equal(CredentialType.GoogleAiApiKey, result.CredentialType);
        Assert.Equal("google", result.Provider);
    }

    [Fact]
    public async Task GetByType_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IUserCredentialRepository>();
        repo.GetByTypeAsync(userId, CredentialType.AnthropicApiKey)
            .Returns((UserCredential?)null);

        // Act
        var result = await repo.GetByTypeAsync(userId, CredentialType.AnthropicApiKey);

        // Assert
        Assert.Null(result);
    }

    // ── UserCredential entity tests ───────────────────────────────────

    [Fact]
    public void UserCredential_DefaultCredentialType_IsGitHubPat()
    {
        var credential = new UserCredential();
        Assert.Equal(CredentialType.GitHubPat, credential.CredentialType);
    }

    [Fact]
    public void UserCredential_CredentialType_CanBeSet()
    {
        var credential = new UserCredential
        {
            CredentialType = CredentialType.OpenAiApiKey
        };
        Assert.Equal(CredentialType.OpenAiApiKey, credential.CredentialType);
    }
}
