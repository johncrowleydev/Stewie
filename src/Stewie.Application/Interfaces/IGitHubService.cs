namespace Stewie.Application.Interfaces;

/// <summary>GitHub API service interface for PR creation, repo management and branch push. REF: CON-002 §5.2</summary>
public interface IGitHubService
{
    /// <summary>Pushes a branch to remote via git CLI with PAT-embedded HTTPS URL.</summary>
    Task PushBranchAsync(string workspacePath, string remoteUrl, string branchName, string patToken);

    /// <summary>Creates a pull request via Octokit API. Returns the PR URL.</summary>
    Task<string> CreatePullRequestAsync(string owner, string repo, string branchName, string title, string body, string patToken);

    /// <summary>Creates a new GitHub repository. Returns the repo URL.</summary>
    Task<string> CreateRepositoryAsync(string name, string description, bool isPrivate, string patToken);

    /// <summary>Validates a PAT and returns basic user info.</summary>
    Task<GitHubUserInfo> ValidateTokenAsync(string patToken);
}

/// <summary>Simplified GitHub user information.</summary>
public class GitHubUserInfo
{
    /// <summary>GitHub login/username.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>GitHub user display name.</summary>
    public string? Name { get; set; }
}
