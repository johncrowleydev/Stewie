using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

public class RunRepository : IRunRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public RunRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Run> GetByIdAsync(Guid id)
    {
        return await _unitOfWork.Session.GetAsync<Run>(id);
    }

    public async Task SaveAsync(Run run)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(run);
    }
}
