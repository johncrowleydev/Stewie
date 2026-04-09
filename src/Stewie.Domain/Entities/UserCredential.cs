namespace Stewie.Domain.Entities;

/// <summary>
/// Stores an encrypted credential (e.g. GitHub PAT) for a user.
/// Token is AES-256-CBC encrypted at rest.
/// REF: CON-002 §4.0.1
/// </summary>
public class UserCredential
{
    /// <summary>Unique identifier.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Owner user ID.</summary>
    public virtual Guid UserId { get; set; }

    /// <summary>Provider name (e.g. "github").</summary>
    public virtual string Provider { get; set; } = string.Empty;

    /// <summary>AES-256-CBC encrypted token. IV prepended to ciphertext, base64 encoded.</summary>
    public virtual string EncryptedToken { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public virtual DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public virtual DateTime UpdatedAt { get; set; }
}
