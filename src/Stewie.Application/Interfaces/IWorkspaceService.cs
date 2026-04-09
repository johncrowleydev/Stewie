/// <summary>
/// Interface for workspace filesystem operations.
/// REF: BLU-001 §3.2, CON-001 §4.1, CON-002 §5.6
/// </summary>
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines workspace operations: preparation, result reading, git repository management,
/// diff capture, and auto-commit.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>Prepares a workspace directory structure and writes task.json.</summary>
    /// <param name="task">The task entity.</param>
    /// <param name="job">The parent job entity.</param>
    /// <returns>The absolute path to the workspace directory.</returns>
    string PrepareWorkspace(WorkTask task, Job job);

    /// <summary>
    /// Prepares a workspace and writes task.json with full fields for real jobs.
    /// </summary>
    /// <param name="task">The task entity with Objective, Scope, etc.</param>
    /// <param name="job">The parent job entity.</param>
    /// <param name="repoUrl">Optional repo URL for cloning.</param>
    /// <param name="branch">Optional branch name.</param>
    /// <param name="script">Optional script commands.</param>
    /// <param name="acceptanceCriteria">Optional acceptance criteria.</param>
    /// <returns>The absolute path to the workspace directory.</returns>
    string PrepareWorkspaceForRun(WorkTask task, Job job, string? repoUrl,
        string? branch, List<string>? script, List<string>? acceptanceCriteria);

    /// <summary>Reads and deserializes result.json from the task's workspace output directory.</summary>
    ResultPacket ReadResult(WorkTask task);

    /// <summary>Clones a git repository into the workspace's repo/ directory.</summary>
    Task CloneRepositoryAsync(string repoUrl, string workspacePath);

    /// <summary>Creates a new git branch in the workspace's repo/ directory.</summary>
    Task CreateBranchAsync(string workspacePath, string branchName);

    /// <summary>
    /// Captures git diff output from the workspace's repo/ directory.
    /// </summary>
    /// <param name="workspacePath">The workspace root directory.</param>
    /// <returns>A <see cref="DiffResult"/> with stat and patch output, or null values if no changes.</returns>
    Task<DiffResult> CaptureDiffAsync(string workspacePath);

    /// <summary>
    /// Stages and commits all changes in the workspace's repo/ directory.
    /// LOCAL commit only — no push to remote.
    /// </summary>
    /// <param name="workspacePath">The workspace root directory.</param>
    /// <param name="message">The commit message.</param>
    /// <returns>The commit SHA string, or null if nothing to commit.</returns>
    Task<string?> CommitChangesAsync(string workspacePath, string message);

    /// <summary>Reads and deserializes governance-report.json from the task's workspace output directory.</summary>
    /// <param name="workspacePath">The workspace root directory.</param>
    /// <returns>The deserialized governance report packet.</returns>
    GovernanceReportPacket ReadGovernanceReport(string workspacePath);

    /// <summary>Writes a task.json to the workspace input directory for a governance worker.</summary>
    /// <param name="workspacePath">The workspace root directory.</param>
    /// <param name="taskPacket">The task packet to serialize.</param>
    void WriteTaskJson(string workspacePath, TaskPacket taskPacket);
}

/// <summary>Result of a git diff capture operation.</summary>
public class DiffResult
{
    /// <summary>Output of git diff --stat (file summary).</summary>
    public string DiffStat { get; set; } = string.Empty;

    /// <summary>Full output of git diff (patch).</summary>
    public string DiffPatch { get; set; } = string.Empty;
}
