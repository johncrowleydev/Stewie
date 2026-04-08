using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

public interface IWorkTaskRepository
{
    Task<WorkTask> GetByIdAsync(Guid id);
    Task SaveAsync(WorkTask workTask);
}
