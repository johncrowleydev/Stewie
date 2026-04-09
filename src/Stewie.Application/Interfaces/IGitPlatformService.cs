namespace Stewie.Application.Interfaces;

/// <summary>
/// Platform-agnostic git service interface for PR creation, repo management, and branch push.
/// Implementations are platform-specific (e.g., GitHubService for GitHub).
/// REF: CON-002 §5.2, SPR-005 T-048
/// </summary>
public interface IGitPlatformService
{
    /// <summary>The platform identifier (e.g., "github", "gitlab").</summary>
    string Provider { get; }

    /// <summary>Pushes a branch to remote via git CLI with PAT-embedded HTTPS URL.</summary>
    Task PushBranchAsync(string workspacePath, string remoteUrl, string branchName, string patToken);

    /// <summary>Creates a pull request via platform API. Returns the PR URL.</summary>
    Task<string> CreatePullRequestAsync(string owner, string repo, string branchName, string title, string body, string patToken);

    /// <summary>Creates a new repository via platform API. Returns the clone URL.</summary>
    Task<string> CreateRepositoryAsync(string name, string description, bool isPrivate, string patToken);

    /// <summary>Validates a PAT and returns basic user info from the platform.</summary>
    Task<PlatformUserInfo> ValidateTokenAsync(string patToken);
}

/// <summary>Platform user information returned by token validation.</summary>
public class PlatformUserInfo
{
    /// <summary>Platform login/username.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>User display name.</summary>
    public string? Name { get; set; }
}
