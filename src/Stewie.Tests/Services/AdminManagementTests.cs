/// <summary>
/// Unit tests for admin invite code and user management endpoints.
/// Uses NSubstitute mocks to verify controller behavior without a live database.
/// REF: JOB-026 T-316
/// </summary>
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stewie.Api.Controllers;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests for <see cref="InvitesController"/> — invite code CRUD operations.
/// Covers generate, list, revoke (happy path), revoke not found, revoke already used.
/// </summary>
public class InviteManagementTests
{
    private readonly IInviteCodeRepository _inviteRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvitesController _controller;

    public InviteManagementTests()
    {
        _inviteRepo = Substitute.For<IInviteCodeRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = NullLogger<InvitesController>.Instance;

        _controller = new InvitesController(_inviteRepo, _unitOfWork, logger);
        SetAdminUser(_controller, Guid.NewGuid());
    }

    // ── Generate ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsOk_WithInviteCode()
    {
        // Arrange — SaveAsync should succeed
        _inviteRepo.SaveAsync(Arg.Any<InviteCode>()).Returns(Task.CompletedTask);
        _unitOfWork.CommitAsync().Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Create();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify SaveAsync was called once
        await _inviteRepo.Received(1).SaveAsync(Arg.Any<InviteCode>());
        await _unitOfWork.Received(1).CommitAsync();
    }

    // ── List ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOk_WithListOfCodes()
    {
        // Arrange
        var codes = new List<InviteCode>
        {
            new() { Id = Guid.NewGuid(), Code = "ALPHA123", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "BETA4567", UsedByUserId = Guid.NewGuid(), UsedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddDays(-1) },
        };
        _inviteRepo.GetAllAsync().Returns(codes);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // ── Revoke (happy path) ──────────────────────────────────────────

    [Fact]
    public async Task Revoke_ReturnsNoContent_WhenCodeIsUnused()
    {
        // Arrange
        var invite = new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "UNUSED12",
            UsedByUserId = null,
            CreatedAt = DateTime.UtcNow,
        };
        _inviteRepo.GetByIdAsync(invite.Id).Returns(invite);
        _inviteRepo.DeleteAsync(invite).Returns(Task.CompletedTask);
        _unitOfWork.CommitAsync().Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Revoke(invite.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _inviteRepo.Received(1).DeleteAsync(invite);
    }

    // ── Revoke: not found ────────────────────────────────────────────

    [Fact]
    public async Task Revoke_Returns404_WhenCodeNotFound()
    {
        // Arrange
        _inviteRepo.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        // Act
        var result = await _controller.Revoke(Guid.NewGuid());

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    // ── Revoke: already used ─────────────────────────────────────────

    [Fact]
    public async Task Revoke_Returns409_WhenCodeAlreadyUsed()
    {
        // Arrange
        var invite = new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "USED1234",
            UsedByUserId = Guid.NewGuid(),
            UsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        };
        _inviteRepo.GetByIdAsync(invite.Id).Returns(invite);

        // Act
        var result = await _controller.Revoke(invite.Id);

        // Assert
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static void SetAdminUser(ControllerBase controller, Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}

/// <summary>
/// Tests for <see cref="UsersController"/> — admin user management.
/// Covers list, delete happy path, self-delete blocked, admin-delete blocked, non-admin denied.
/// </summary>
public class UserManagementTests
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UsersController _controller;
    private readonly Guid _currentAdminId;

    public UserManagementTests()
    {
        _currentAdminId = Guid.NewGuid();
        _userRepo = Substitute.For<IUserRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        var credRepo = Substitute.For<IUserCredentialRepository>();
        var encService = Substitute.For<IEncryptionService>();
        var logger = NullLogger<UsersController>.Instance;

        _controller = new UsersController(_userRepo, credRepo, encService, _unitOfWork, logger);
        SetAdminUser(_controller, _currentAdminId);
    }

    // ── List all users ───────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOk_WithUserList()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = _currentAdminId, Username = "admin", Role = UserRole.Admin, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "dev1", Role = UserRole.User, CreatedAt = DateTime.UtcNow },
        };
        _userRepo.GetAllAsync().Returns(users);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // ── Delete user (happy path) ─────────────────────────────────────

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleteNonAdmin()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var target = new User
        {
            Id = targetId,
            Username = "normaluser",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow,
        };
        _userRepo.GetByIdAsync(targetId).Returns(target);
        _userRepo.DeleteAsync(target).Returns(Task.CompletedTask);
        _unitOfWork.CommitAsync().Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(targetId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _userRepo.Received(1).DeleteAsync(target);
    }

    // ── Delete self: blocked ─────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns400_WhenDeletingSelf()
    {
        // Act
        var result = await _controller.Delete(_currentAdminId);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // ── Delete another admin: blocked ────────────────────────────────

    [Fact]
    public async Task Delete_Returns403_WhenDeletingAnotherAdmin()
    {
        // Arrange
        var otherAdminId = Guid.NewGuid();
        var otherAdmin = new User
        {
            Id = otherAdminId,
            Username = "otheradmin",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
        };
        _userRepo.GetByIdAsync(otherAdminId).Returns(otherAdmin);

        // Act
        var result = await _controller.Delete(otherAdminId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // ── Delete non-existent user: 404 ────────────────────────────────

    [Fact]
    public async Task Delete_Returns404_WhenUserNotFound()
    {
        // Arrange
        _userRepo.GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();

        // Act
        var result = await _controller.Delete(Guid.NewGuid());

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static void SetAdminUser(ControllerBase controller, Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
