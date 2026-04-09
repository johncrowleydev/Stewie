using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

/// <summary>
/// Represents an authenticated user in the Stewie system.
/// REF: CON-002 §4.0
/// </summary>
public class User
{
    /// <summary>Unique identifier.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Unique username for login.</summary>
    public virtual string Username { get; set; } = string.Empty;

    /// <summary>BCrypt-hashed password.</summary>
    public virtual string PasswordHash { get; set; } = string.Empty;

    /// <summary>User role (Admin or User).</summary>
    public virtual UserRole Role { get; set; }

    /// <summary>Account creation timestamp.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
