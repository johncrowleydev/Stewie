using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Repositories;

public class ArtifactRepository : IArtifactRepository
{
    private readonly IUnitOfWork _unitOfWork;

    public ArtifactRepository(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task SaveAsync(Artifact artifact)
    {
        await _unitOfWork.Session.SaveOrUpdateAsync(artifact);
    }
}
