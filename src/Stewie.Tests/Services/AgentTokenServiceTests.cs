/// <summary>
/// Unit tests for AgentTokenService — verifies token generation, validation,
/// claim structure, and expiry behavior.
/// REF: JOB-022 T-192
/// </summary>
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Application.Services;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for the AgentTokenService that issues scoped JWTs for agent sessions.
/// </summary>
public class AgentTokenServiceTests
{
    private readonly AgentTokenService _service;

    /// <summary>Initialize service with test configuration.</summary>
    public AgentTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stewie:JwtSecret"] = "test-jwt-secret-minimum-32-characters-long!!"
            })
            .Build();

        _service = new AgentTokenService(config, NullLogger<AgentTokenService>.Instance);
    }

    /// <summary>Verify a generated token can be validated back to a principal.</summary>
    [Fact]
    public void GenerateAndValidate_RoundTrips()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var role = "architect";

        // Act
        var token = _service.GenerateAgentToken(sessionId, projectId, role);
        var principal = _service.ValidateAgentToken(token);

        // Assert
        Assert.NotNull(principal);
        // JWT handler maps 'sub' to ClaimTypes.NameIdentifier by default
        var subClaim = principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst("sub")?.Value
                    ?? principal.Identity?.Name;
        Assert.Equal(sessionId.ToString(), subClaim);
    }

    /// <summary>Verify token has correct claims.</summary>
    [Fact]
    public void GenerateToken_HasCorrectClaims()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        // Act
        var token = _service.GenerateAgentToken(sessionId, projectId, "developer");
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        // Assert
        Assert.Equal(AgentTokenService.AgentIssuer, parsed.Issuer);
        Assert.Contains(parsed.Audiences, a => a == AgentTokenService.AgentAudience);
        Assert.Equal(sessionId.ToString(), parsed.Subject);
        Assert.Equal(projectId.ToString(), parsed.Claims.First(c => c.Type == "project_id").Value);
        Assert.Equal("agent", parsed.Claims.First(c => c.Type == "role").Value);
        Assert.Equal("developer", parsed.Claims.First(c => c.Type == "agent_role").Value);
    }

    /// <summary>Verify token expires in approximately 24 hours.</summary>
    [Fact]
    public void GenerateToken_Expires24Hours()
    {
        // Act
        var token = _service.GenerateAgentToken(Guid.NewGuid(), Guid.NewGuid(), "tester");
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        // Assert — check expires within 24h +/- 1 minute
        var expectedExpiry = DateTime.UtcNow.AddHours(24);
        Assert.InRange(
            parsed.ValidTo,
            expectedExpiry.AddMinutes(-1),
            expectedExpiry.AddMinutes(1));
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        // Act — malformed token should not throw, should return null
        var result = _service.ValidateAgentToken("not-a-valid-token");

        // Assert
        Assert.Null(result);
    }

    /// <summary>Verify tampered token returns null.</summary>
    [Fact]
    public void ValidateToken_TamperedToken_ReturnsNull()
    {
        // Arrange
        var token = _service.GenerateAgentToken(Guid.NewGuid(), Guid.NewGuid(), "architect");
        var tampered = token + "x";

        // Act
        var result = _service.ValidateAgentToken(tampered);

        // Assert
        Assert.Null(result);
    }

    /// <summary>Verify agent tokens use "stewie-agent" issuer, not "stewie".</summary>
    [Fact]
    public void AgentIssuer_IsDifferentFromUserIssuer()
    {
        Assert.Equal("stewie-agent", AgentTokenService.AgentIssuer);
        Assert.NotEqual("stewie", AgentTokenService.AgentIssuer);
    }
}
