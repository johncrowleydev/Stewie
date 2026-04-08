using Stewie.Domain.Enums;

namespace Stewie.Domain.Entities;

public class Run
{
    public virtual Guid Id { get; set; }
    public virtual RunStatus Status { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime? CompletedAt { get; set; }
}
