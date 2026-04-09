using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

/// <summary>
/// Authentication controller — register (invite-only) and login.
/// Public endpoints — no [Authorize] required.
/// REF: CON-002 §4.0
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthController> _logger;
    private readonly string _jwtSecret;

    public AuthController(
        IUserRepository userRepository,
        IInviteCodeRepository inviteCodeRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _inviteCodeRepository = inviteCodeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _jwtSecret = configuration["Stewie:JwtSecret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");
    }

    /// <summary>Register with an invite code. Returns JWT on success.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Username and password are required.");

        if (string.IsNullOrWhiteSpace(request.InviteCode))
            throw new ArgumentException("Invite code is required.");

        // Validate invite code
        var invite = await _inviteCodeRepository.GetByCodeAsync(request.InviteCode);
        if (invite is null || invite.UsedByUserId.HasValue)
            throw new ArgumentException("Invalid or already used invite code.");

        if (invite.ExpiresAt.HasValue && invite.ExpiresAt.Value < DateTime.UtcNow)
            throw new ArgumentException("Invite code has expired.");

        // Check username uniqueness
        var existing = await _userRepository.GetByUsernameAsync(request.Username.Trim());
        if (existing is not null)
            throw new InvalidOperationException("Username already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _userRepository.SaveAsync(user);

        // Consume invite code
        invite.UsedByUserId = user.Id;
        invite.UsedAt = DateTime.UtcNow;
        await _inviteCodeRepository.SaveAsync(invite);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("User {Username} registered with invite code", user.Username);
        return Ok(GenerateAuthResponse(user));
    }

    /// <summary>Login with username/password. Returns JWT on success.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Username and password are required.");

        var user = await _userRepository.GetByUsernameAsync(request.Username.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Invalid credentials." } });
        }

        _logger.LogInformation("User {Username} logged in", user.Username);
        return Ok(GenerateAuthResponse(user));
    }

    private object GenerateAuthResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "stewie",
            audience: "stewie",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt = expiresAt.ToString("o"),
            user = new { id = user.Id, username = user.Username, role = user.Role.ToString().ToLowerInvariant() }
        };
    }
}

/// <summary>Registration request body.</summary>
public class RegisterRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? InviteCode { get; set; }
}

/// <summary>Login request body.</summary>
public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
