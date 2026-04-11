/// <summary>
/// AgentTokenService — issues short-lived JWT tokens scoped to agent sessions.
/// Agent tokens use a separate issuer ("stewie-agent") from user tokens ("stewie")
/// and expire in 24 hours.
/// REF: JOB-022 T-192
/// </summary>
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Stewie.Application.Services;

/// <summary>
/// Generates and validates JWT tokens for agent container sessions.
/// Agent tokens are scoped to a specific session + project and cannot access admin endpoints.
/// </summary>
public class AgentTokenService
{
    /// <summary>Issuer claim for agent tokens — distinct from user tokens ("stewie").</summary>
    public const string AgentIssuer = "stewie-agent";

    /// <summary>Audience claim for agent tokens.</summary>
    public const string AgentAudience = "stewie";

    /// <summary>Agent token lifetime.</summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<AgentTokenService> _logger;

    /// <summary>Initializes the agent token service with the JWT secret.</summary>
    /// <param name="configuration">App configuration containing Stewie:JwtSecret.</param>
    /// <param name="logger">Logger instance.</param>
    public AgentTokenService(IConfiguration configuration, ILogger<AgentTokenService> logger)
    {
        var jwtSecret = configuration["Stewie:JwtSecret"]
            ?? throw new InvalidOperationException("JWT secret is required for agent token generation.");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        _logger = logger;
    }

    /// <summary>
    /// Generate a JWT for an agent session. Expires in 24h.
    /// </summary>
    /// <param name="sessionId">Agent session ID (becomes the 'sub' claim).</param>
    /// <param name="projectId">Project ID the agent is authorized for.</param>
    /// <param name="agentRole">Agent role: "architect", "developer", "tester".</param>
    /// <returns>Signed JWT string.</returns>
    public string GenerateAgentToken(Guid sessionId, Guid projectId, string agentRole)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sessionId.ToString()),
            new Claim("project_id", projectId.ToString()),
            new Claim("role", "agent"),
            new Claim("agent_role", agentRole),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: AgentIssuer,
            audience: AgentAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation(
            "Generated agent token for session {SessionId} (project {ProjectId}, role {Role}), expires in 24h",
            sessionId, projectId, agentRole);

        return tokenString;
    }

    /// <summary>
    /// Validate an agent token and return the claims principal.
    /// Returns null if the token is invalid or expired.
    /// </summary>
    /// <param name="token">JWT string to validate.</param>
    /// <returns>ClaimsPrincipal on success, null on failure.</returns>
    public ClaimsPrincipal? ValidateAgentToken(string token)
    {
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AgentIssuer,
            ValidateAudience = true,
            ValidAudience = AgentAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            NameClaimType = "sub",
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Agent token validation failed: {Error}", ex.Message);
            return null;
        }
    }
}
