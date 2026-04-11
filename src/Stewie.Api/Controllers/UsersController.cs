using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

/// <summary>
/// User endpoints — profile, GitHub token management, and admin user management.
/// All endpoints require authentication. Admin endpoints require Admin role.
/// REF: CON-002 §4.0.1, JOB-026 T-314
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

    // ── Admin-only endpoints ──────────────────────────────────────────

    /// <summary>
    /// List all users. Admin only.
    /// Returns id, username, role, createdAt for each user.
    /// REF: CON-002 §4.0.1, JOB-026 T-314
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            role = u.Role.ToString().ToLowerInvariant(),
            createdAt = u.CreatedAt.ToString("o")
        }));
    }

    /// <summary>
    /// Delete a user by ID. Admin only.
    /// Cannot delete self (returns 400). Cannot delete other admins (returns 403).
    /// REF: CON-002 §4.0.1, JOB-026 T-314
    /// </summary>
    /// <param name="id">The user ID to delete.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUserId = GetUserId();

        if (id == currentUserId)
        {
            return BadRequest(new
            {
                error = new { code = "VALIDATION_ERROR", message = "Cannot delete your own account." }
            });
        }

        var target = await _userRepository.GetByIdAsync(id);
        if (target is null)
        {
            return NotFound(new
            {
                error = new { code = "NOT_FOUND", message = "User not found." }
            });
        }

        if (target.Role == UserRole.Admin)
        {
            return StatusCode(403, new
            {
                error = new { code = "FORBIDDEN", message = "Cannot delete another admin user." }
            });
        }

        _unitOfWork.BeginTransaction();
        await _userRepository.DeleteAsync(target);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("User {Username} (ID: {UserId}) deleted by admin {AdminId}",
            target.Username, target.Id, currentUserId);
        return NoContent();
    }

    // ── Self-service endpoints ────────────────────────────────────────

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
