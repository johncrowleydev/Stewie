namespace Stewie.Domain.Entities;

public class Artifact
{
    public virtual Guid Id { get; set; }
    public virtual Guid TaskId { get; set; }
    public virtual WorkTask WorkTask { get; set; } = null!;
    public virtual string Type { get; set; } = string.Empty;
    public virtual string ContentJson { get; set; } = string.Empty;
    public virtual DateTime CreatedAt { get; set; }
}
