using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

public interface IWorkspaceService
{
    string PrepareWorkspace(WorkTask task, Run run);
    ResultPacket ReadResult(WorkTask task);
}
