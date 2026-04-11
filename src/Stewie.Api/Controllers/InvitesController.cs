using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Api.Controllers;

/// <summary>
/// Invite code management — admin only.
/// REF: CON-002 §4.0.2
/// </summary>
[ApiController]
[Route("api/invites")]
[Authorize(Roles = "Admin")]
public class InvitesController : ControllerBase
{
    private readonly IInviteCodeRepository _inviteCodeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InvitesController> _logger;

    public InvitesController(
        IInviteCodeRepository inviteCodeRepository,
        IUnitOfWork unitOfWork,
        ILogger<InvitesController> logger)
    {
        _inviteCodeRepository = inviteCodeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>Generate a new invite code. Admin only.</summary>
    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var userId = Guid.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException());

        var inviteCode = new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _inviteCodeRepository.SaveAsync(inviteCode);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Invite code {Code} created by user {UserId}", inviteCode.Code, userId);

        return Ok(new
        {
            id = inviteCode.Id,
            code = inviteCode.Code,
            createdAt = inviteCode.CreatedAt.ToString("o")
        });
    }

    /// <summary>List all invite codes. Admin only.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var codes = await _inviteCodeRepository.GetAllAsync();
        return Ok(codes.Select(c => new
        {
            id = c.Id,
            code = c.Code,
            usedByUserId = c.UsedByUserId,
            usedAt = c.UsedAt?.ToString("o"),
            expiresAt = c.ExpiresAt?.ToString("o"),
            createdAt = c.CreatedAt.ToString("o")
        }));
    }

    /// <summary>
    /// Revoke (delete) an unused invite code. Admin only.
    /// Returns 404 if code does not exist. Returns 409 if code was already used.
    /// REF: CON-002 §4.0.2, JOB-026 T-313
    /// </summary>
    /// <param name="id">The invite code ID to revoke.</param>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var invite = await _inviteCodeRepository.GetByIdAsync(id);
        if (invite is null)
        {
            return NotFound(new
            {
                error = new { code = "NOT_FOUND", message = "Invite code not found." }
            });
        }

        if (invite.UsedByUserId.HasValue)
        {
            return Conflict(new
            {
                error = new { code = "ALREADY_USED", message = "Cannot revoke an invite code that has already been used." }
            });
        }

        _unitOfWork.BeginTransaction();
        await _inviteCodeRepository.DeleteAsync(invite);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Invite code {Code} (ID: {Id}) revoked by admin", invite.Code, invite.Id);
        return NoContent();
    }
}
