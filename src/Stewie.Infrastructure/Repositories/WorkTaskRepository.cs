using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

public class WorkTaskRepository : IWorkTaskRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public WorkTaskRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkTask> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<WorkTask>(id);
    }

    public async Task SaveAsync(WorkTask workTask)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(workTask);
    }
}
