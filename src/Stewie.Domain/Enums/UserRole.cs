namespace Stewie.Domain.Enums;

/// <summary>User role for authorization. Admin can manage invite codes and users.</summary>
public enum UserRole
{
    /// <summary>Standard user — can create runs and manage their own credentials.</summary>
    User = 0,

    /// <summary>Administrator — can manage invite codes and all users.</summary>
    Admin = 1
}
