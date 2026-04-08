using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

public class WorkTask
{
    public virtual Guid Id { get; set; }
    public virtual Guid RunId { get; set; }
    public virtual Run Run { get; set; } = null!;
    public virtual string Role { get; set; } = string.Empty;
    public virtual WorkTaskStatus Status { get; set; }
    public virtual string WorkspacePath { get; set; } = string.Empty;
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime? StartedAt { get; set; }
    public virtual DateTime? CompletedAt { get; set; }
}
