using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

public interface IArtifactRepository
{
    Task SaveAsync(Artifact artifact);
}
