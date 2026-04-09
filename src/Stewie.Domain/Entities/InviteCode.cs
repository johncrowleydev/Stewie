namespace Stewie.Domain.Entities;

/// <summary>
/// Invite code for registration. System is invite-only — no open signup.
/// REF: CON-002 §4.0.2
/// </summary>
public class InviteCode
{
    /// <summary>Unique identifier.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>The invite code string (unique).</summary>
    public virtual string Code { get; set; } = string.Empty;

    /// <summary>User who created this invite code.</summary>
    public virtual Guid CreatedByUserId { get; set; }

    /// <summary>User who used this invite code. Null if unused.</summary>
    public virtual Guid? UsedByUserId { get; set; }

    /// <summary>When the code was used. Null if unused.</summary>
    public virtual DateTime? UsedAt { get; set; }

    /// <summary>Optional expiration. Null = never expires.</summary>
    public virtual DateTime? ExpiresAt { get; set; }

    /// <summary>Creation timestamp.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
