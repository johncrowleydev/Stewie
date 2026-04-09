/// <summary>
/// Interface for workspace filesystem operations.
/// REF: BLU-001 §3.2, CON-001 §4.1
/// </summary>
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines workspace operations: preparation, result reading, and git repository management.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>Prepares a workspace directory structure and writes task.json.</summary>
    /// <param name="task">The task entity.</param>
    /// <param name="run">The parent run entity.</param>
    /// <returns>The absolute path to the workspace directory.</returns>
    string PrepareWorkspace(WorkTask task, Run run);

    /// <summary>Reads and deserializes result.json from the task's workspace output directory.</summary>
    /// <param name="task">The task entity with a valid WorkspacePath.</param>
    /// <returns>The deserialized result packet.</returns>
    ResultPacket ReadResult(WorkTask task);

    /// <summary>
    /// Clones a git repository into the workspace's repo/ directory.
    /// Phase 2 plumbing — not yet wired into ExecuteTestRunAsync.
    /// </summary>
    /// <param name="repoUrl">The HTTPS or SSH URL of the repository to clone.</param>
    /// <param name="workspacePath">The workspace root directory (repo/ subdirectory will be used).</param>
    /// <returns>A task that completes when the clone finishes.</returns>
    /// <exception cref="InvalidOperationException">Thrown if git clone fails.</exception>
    Task CloneRepositoryAsync(string repoUrl, string workspacePath);

    /// <summary>
    /// Creates a new git branch in the workspace's repo/ directory.
    /// Phase 2 plumbing — not yet wired into ExecuteTestRunAsync.
    /// </summary>
    /// <param name="workspacePath">The workspace root directory (repo/ subdirectory will be used).</param>
    /// <param name="branchName">The name of the branch to create.</param>
    /// <returns>A task that completes when the branch is created.</returns>
    /// <exception cref="InvalidOperationException">Thrown if git checkout fails.</exception>
    Task CreateBranchAsync(string workspacePath, string branchName);
}
