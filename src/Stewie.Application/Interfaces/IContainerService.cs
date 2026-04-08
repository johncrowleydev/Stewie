using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

public interface IContainerService
{
    Task<int> LaunchWorkerAsync(WorkTask task);
}
