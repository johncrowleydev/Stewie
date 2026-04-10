namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a user's membership or favorited status on a global project.
/// </summary>
public class ProjectMembership
{
    public virtual Guid UserId { get; set; }
    public virtual Guid ProjectId { get; set; }
    public virtual bool IsFavorite { get; set; }
    public virtual DateTime JoinedAt { get; set; }

    // Composite identity elements for NHibernate
    public override bool Equals(object? obj)
    {
        if (obj is not ProjectMembership other) return false;
        if (ReferenceEquals(this, other)) return true;
        return UserId == other.UserId && ProjectId == other.ProjectId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UserId, ProjectId);
    }
}
