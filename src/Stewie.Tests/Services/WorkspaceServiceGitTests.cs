/// <summary>
/// Unit tests for git operations in WorkspaceService.
/// Tests real git operations using temporary repositories.
///
/// These tests verify the git plumbing layer that will be used by
/// CaptureDiffAsync and CommitChangesAsync (Agent A's T-030/T-031).
/// They exercise CloneRepositoryAsync, CreateBranchAsync, and validate that
/// git operations produce expected filesystem outcomes.
///
/// REF: GOV-002, SPR-003 T-036
/// </summary>
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stewie.Infrastructure.Services;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests git-related operations in WorkspaceService using real temp repos.
/// </summary>
public class WorkspaceServiceGitTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _bareRepoDir;
    private readonly WorkspaceService _service;

    public WorkspaceServiceGitTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"stewie-git-test-{Guid.NewGuid():N}");
        _bareRepoDir = Path.Combine(_tempDir, "bare-repo.git");
        Directory.CreateDirectory(_tempDir);

        var logger = Substitute.For<ILogger<WorkspaceService>>();
        _service = new WorkspaceService(_tempDir, logger);

        // Create a bare repo to use as a clone source
        CreateBareRepo();
    }

    /// <summary>Creates a bare git repo with one commit for clone tests.</summary>
    private void CreateBareRepo()
    {
        // First create a normal repo with a commit
        var initDir = Path.Combine(_tempDir, "init-repo");
        Directory.CreateDirectory(initDir);

        RunGit("init", initDir);
        RunGit("config user.email \"test@stewie.dev\"", initDir);
        RunGit("config user.name \"Test\"", initDir);

        File.WriteAllText(Path.Combine(initDir, "README.md"), "# Test Repo");
        RunGit("add -A", initDir);
        RunGit("commit -m \"initial commit\"", initDir);

        // Clone as bare
        RunGit($"clone --bare \"{initDir}\" \"{_bareRepoDir}\"", _tempDir);
    }

    /// <summary>CloneRepositoryAsync successfully clones a repo into workspace/repo/.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_ClonesRepo()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-clone");
        Directory.CreateDirectory(Path.Combine(workspaceDir, "repo"));

        await _service.CloneRepositoryAsync(_bareRepoDir, workspaceDir);

        Assert.True(Directory.Exists(Path.Combine(workspaceDir, "repo", ".git")),
            "Cloned repo should have .git directory");
        Assert.True(File.Exists(Path.Combine(workspaceDir, "repo", "README.md")),
            "Cloned repo should have README.md");
    }

    /// <summary>CloneRepositoryAsync throws when given an empty URL.</summary>
    [Fact]
    public async Task CloneRepositoryAsync_EmptyUrl_Throws()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-empty-url");
        Directory.CreateDirectory(workspaceDir);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CloneRepositoryAsync("", workspaceDir));
    }

    /// <summary>CreateBranchAsync creates a new branch in the cloned repo.</summary>
    [Fact]
    public async Task CreateBranchAsync_CreatesBranch()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-branch");
        Directory.CreateDirectory(Path.Combine(workspaceDir, "repo"));
        await _service.CloneRepositoryAsync(_bareRepoDir, workspaceDir);

        await _service.CreateBranchAsync(workspaceDir, "stewie/test-branch");

        // Verify branch exists by checking git output
        var branchOutput = RunGitWithOutput("branch", Path.Combine(workspaceDir, "repo"));
        Assert.Contains("stewie/test-branch", branchOutput);
    }

    /// <summary>CreateBranchAsync throws when no repo exists.</summary>
    [Fact]
    public async Task CreateBranchAsync_NoRepo_Throws()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-no-repo");
        Directory.CreateDirectory(workspaceDir);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateBranchAsync(workspaceDir, "branch"));
    }

    /// <summary>
    /// Verifies that diff capture works: modify a file in a cloned repo,
    /// then check that git diff shows changes. This validates the prerequisite
    /// for CaptureDiffAsync (T-030).
    /// </summary>
    [Fact]
    public async Task GitDiff_WithModifiedFiles_ReturnsNonEmptyDiff()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-diff");
        Directory.CreateDirectory(Path.Combine(workspaceDir, "repo"));
        await _service.CloneRepositoryAsync(_bareRepoDir, workspaceDir);
        await _service.CreateBranchAsync(workspaceDir, "stewie/diff-test");

        var repoDir = Path.Combine(workspaceDir, "repo");

        // Modify a file
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "# Modified by Stewie");

        // Check diff output
        var diffStat = RunGitWithOutput("diff --stat", repoDir);
        var diffPatch = RunGitWithOutput("diff", repoDir);

        Assert.False(string.IsNullOrWhiteSpace(diffStat), "diff --stat should have output");
        Assert.Contains("README.md", diffStat);
        Assert.False(string.IsNullOrWhiteSpace(diffPatch), "diff should have output");
        Assert.Contains("Modified by Stewie", diffPatch);
    }

    /// <summary>
    /// Verifies that committing changes works: modify, add, commit,
    /// then verify the commit exists. This validates the prerequisite
    /// for CommitChangesAsync (T-031).
    /// </summary>
    [Fact]
    public async Task GitCommit_WithChanges_CreatesCommit()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-commit");
        Directory.CreateDirectory(Path.Combine(workspaceDir, "repo"));
        await _service.CloneRepositoryAsync(_bareRepoDir, workspaceDir);
        await _service.CreateBranchAsync(workspaceDir, "stewie/commit-test");

        var repoDir = Path.Combine(workspaceDir, "repo");

        // Modify, add, commit
        File.WriteAllText(Path.Combine(repoDir, "README.md"), "# Committed by Stewie");
        RunGit("add -A", repoDir);
        RunGit("config user.email \"stewie@test.dev\"", repoDir);
        RunGit("config user.name \"Stewie Worker\"", repoDir);
        RunGit("commit -m \"feat(stewie): test objective [Job abc123]\"", repoDir);

        // Verify commit SHA exists
        var sha = RunGitWithOutput("rev-parse HEAD", repoDir).Trim();
        Assert.NotEmpty(sha);
        Assert.Equal(40, sha.Length); // Full SHA is 40 hex chars
    }

    /// <summary>Git diff with no changes returns empty output.</summary>
    [Fact]
    public async Task GitDiff_NoChanges_ReturnsEmpty()
    {
        var workspaceDir = Path.Combine(_tempDir, "workspace-no-diff");
        Directory.CreateDirectory(Path.Combine(workspaceDir, "repo"));
        await _service.CloneRepositoryAsync(_bareRepoDir, workspaceDir);

        var repoDir = Path.Combine(workspaceDir, "repo");
        var diffStat = RunGitWithOutput("diff --stat", repoDir);

        Assert.True(string.IsNullOrWhiteSpace(diffStat), "diff --stat should be empty with no changes");
    }

    /// <summary>Runs a git command synchronously (test utility).</summary>
    private static void RunGit(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = Process.Start(psi)!;
        proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit();
    }

    /// <summary>Runs a git command and returns stdout (test utility).</summary>
    private static string RunGitWithOutput(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return output;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup is best-effort */ }
    }
}
