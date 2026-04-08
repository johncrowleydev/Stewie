using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

public interface IRunRepository
{
    Task<Run> GetByIdAsync(Guid id);
    Task SaveAsync(Run run);
}
