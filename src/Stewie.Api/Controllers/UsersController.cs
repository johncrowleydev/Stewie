using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Api.Controllers;

/// <summary>
/// User endpoints — profile, GitHub token management.
/// All endpoints require authentication.
/// REF: CON-002 §4.0.1
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        IUserCredentialRepository credentialRepository,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Get current user profile.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null) throw new KeyNotFoundException("User not found.");

        return Ok(new { id = user.Id, username = user.Username, role = user.Role.ToString().ToLowerInvariant() });
    }

    /// <summary>Store encrypted GitHub PAT.</summary>
    [HttpPut("me/github-token")]
    public async Task<IActionResult> SetGitHubToken([FromBody] SetTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Token is required.");

        var userId = GetUserId();
        var encrypted = _encryptionService.Encrypt(request.Token);

        _unitOfWork.BeginTransaction();

        var existing = await _credentialRepository.GetByUserAndProviderAsync(userId, "github");
        if (existing is not null)
        {
            existing.EncryptedToken = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.SaveAsync(existing);
        }
        else
        {
            var credential = new UserCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = "github",
                EncryptedToken = encrypted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _credentialRepository.SaveAsync(credential);
        }

        await _unitOfWork.CommitAsync();
        _logger.LogInformation("GitHub token stored for user {UserId}", userId);
        return Ok(new { status = "connected" });
    }

    /// <summary>Remove GitHub PAT.</summary>
    [HttpDelete("me/github-token")]
    public async Task<IActionResult> RemoveGitHubToken()
    {
        var userId = GetUserId();
        var credential = await _credentialRepository.GetByUserAndProviderAsync(userId, "github");
        if (credential is null) return Ok(new { status = "disconnected" });

        _unitOfWork.BeginTransaction();
        await _credentialRepository.DeleteAsync(credential);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("GitHub token removed for user {UserId}", userId);
        return Ok(new { status = "disconnected" });
    }

    /// <summary>Check GitHub connection status.</summary>
    [HttpGet("me/github-status")]
    public async Task<IActionResult> GetGitHubStatus()
    {
        var userId = GetUserId();
        var credential = await _credentialRepository.GetByUserAndProviderAsync(userId, "github");
        return Ok(new { connected = credential is not null });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}

/// <summary>Request body for setting a GitHub token.</summary>
public class SetTokenRequest
{
    public string? Token { get; set; }
}
