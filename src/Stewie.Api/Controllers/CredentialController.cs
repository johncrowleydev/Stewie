/// <summary>
/// Credential CRUD controller — manage LLM provider API keys.
/// GET/POST/DELETE /api/settings/credentials
/// REF: JOB-023 T-202, CON-002 §4.8
/// </summary>
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

/// <summary>
/// REST endpoints for user credential (LLM API key) management.
/// Values are AES-256-CBC encrypted at rest via <see cref="IEncryptionService"/>.
/// GET returns masked values (last 4 chars only).
/// </summary>
[ApiController]
[Route("api/settings/credentials")]
[Authorize]
public class CredentialController : ControllerBase
{
    private readonly IUserCredentialRepository _credentialRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CredentialController> _logger;

    /// <summary>Initializes the credential controller with required dependencies.</summary>
    public CredentialController(
        IUserCredentialRepository credentialRepo,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        ILogger<CredentialController> logger)
    {
        _credentialRepo = credentialRepo;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>List all credentials for the current user (masked values).</summary>
    /// <returns>Array of credentials with masked values and metadata.</returns>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        var credentials = await _credentialRepo.GetByUserIdAsync(userId);

        var result = credentials.Select(c =>
        {
            var maskedValue = MaskValue(c.EncryptedToken);
            return new
            {
                id = c.Id,
                credentialType = c.CredentialType.ToString(),
                maskedValue,
                createdAt = c.CreatedAt.ToString("O")
            };
        });

        return Ok(result);
    }

    /// <summary>Add a new credential. Encrypts the value before storage.</summary>
    /// <param name="request">Credential type and plaintext value.</param>
    /// <returns>201 Created with the credential metadata, or 409 if duplicate type.</returns>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddCredentialRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { error = "Value is required." });

        if (!Enum.TryParse<CredentialType>(request.CredentialType, ignoreCase: true, out var credentialType))
            return BadRequest(new { error = $"Invalid credential type '{request.CredentialType}'. Valid types: {string.Join(", ", Enum.GetNames<CredentialType>())}." });

        var userId = GetUserId();

        // Check for duplicate type
        var existing = await _credentialRepo.GetByTypeAsync(userId, credentialType);
        if (existing is not null)
            return Conflict(new { error = $"A credential of type '{credentialType}' already exists. Delete the existing one first." });

        var encrypted = _encryptionService.Encrypt(request.Value);
        var provider = MapCredentialTypeToProvider(credentialType);

        var credential = new UserCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider,
            CredentialType = credentialType,
            EncryptedToken = encrypted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _credentialRepo.SaveAsync(credential);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Credential {CredentialType} stored for user {UserId}", credentialType, userId);

        return StatusCode(201, new
        {
            id = credential.Id,
            credentialType = credential.CredentialType.ToString(),
            maskedValue = MaskValue(credential.EncryptedToken),
            createdAt = credential.CreatedAt.ToString("O")
        });
    }

    /// <summary>Delete a credential by ID.</summary>
    /// <param name="id">Credential ID to delete.</param>
    /// <returns>204 No Content on success, 404 if not found.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var credential = await _credentialRepo.GetByIdAsync(id);

        if (credential is null)
            return NotFound(new { error = $"Credential '{id}' not found." });

        // IDOR guard: verify the credential belongs to the requesting user
        if (credential.UserId != userId)
            return NotFound(new { error = $"Credential '{id}' not found." });

        _unitOfWork.BeginTransaction();
        await _credentialRepo.DeleteAsync(credential);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Credential {CredentialId} ({Type}) deleted for user {UserId}",
            id, credential.CredentialType, userId);

        return NoContent();
    }

    /// <summary>
    /// Masks an encrypted token value: decrypts, then returns "••••" + last 4 chars.
    /// Returns "••••••••" if decryption fails or value is too short.
    /// </summary>
    private string MaskValue(string encryptedToken)
    {
        try
        {
            var decrypted = _encryptionService.Decrypt(encryptedToken);
            if (decrypted.Length <= 4)
                return "••••••••";
            return "••••••••" + decrypted[^4..];
        }
        catch
        {
            return "••••••••";
        }
    }

    /// <summary>Maps a CredentialType enum to the legacy provider string.</summary>
    private static string MapCredentialTypeToProvider(CredentialType type) => type switch
    {
        CredentialType.GitHubPat => "github",
        CredentialType.AnthropicApiKey => "anthropic",
        CredentialType.OpenAiApiKey => "openai",
        CredentialType.GoogleAiApiKey => "google",
        _ => type.ToString().ToLowerInvariant()
    };

    /// <summary>Extracts the user ID from the JWT sub claim.</summary>
    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}

/// <summary>Request body for adding a credential. REF: JOB-023 T-202.</summary>
public class AddCredentialRequest
{
    /// <summary>Credential type name (e.g. "GoogleAiApiKey"). Required.</summary>
    public string CredentialType { get; set; } = string.Empty;

    /// <summary>Plaintext credential value. Required. Will be encrypted before storage.</summary>
    public string Value { get; set; } = string.Empty;
}
