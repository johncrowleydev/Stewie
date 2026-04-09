using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Octokit;
using Stewie.Application.Interfaces;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// GitHub API service — Octokit for PRs/repos, git CLI for push.
/// REF: CON-002 §5.2, boot doc §9
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(ILogger<GitHubService> logger) => _logger = logger;

    /// <inheritdoc/>
    public async Task PushBranchAsync(string workspacePath, string remoteUrl, string branchName, string patToken)
    {
        var repoDir = Path.Combine(workspacePath, "repo");

        // Set remote with PAT-embedded URL: https://{pat}@github.com/{owner}/{repo}.git
        var uri = new Uri(remoteUrl);
        var authedUrl = $"{uri.Scheme}://{patToken}@{uri.Host}{uri.AbsolutePath}";

        // Set or update origin
        await RunGitAsync($"remote set-url origin \"{authedUrl}\"", repoDir);

        // Push
        var exitCode = await RunGitAsync($"push -u origin \"{branchName}\"", repoDir);
        if (exitCode != 0)
            throw new InvalidOperationException($"git push failed with exit code {exitCode}");

        _logger.LogInformation("Pushed branch {Branch} to remote", branchName);
    }

    /// <inheritdoc/>
    public async Task<string> CreatePullRequestAsync(string owner, string repo, string branchName,
        string title, string body, string patToken)
    {
        var client = CreateClient(patToken);
        var pr = await client.PullRequest.Create(owner, repo, new NewPullRequest(title, branchName, "main")
        {
            Body = body
        });

        _logger.LogInformation("Created PR #{PrNumber} in {Owner}/{Repo}", pr.Number, owner, repo);
        return pr.HtmlUrl;
    }

    /// <inheritdoc/>
    public async Task<string> CreateRepositoryAsync(string name, string description, bool isPrivate, string patToken)
    {
        var client = CreateClient(patToken);
        var newRepo = new NewRepository(name) { Description = description, Private = isPrivate, AutoInit = true };
        var repo = await client.Repository.Create(newRepo);

        _logger.LogInformation("Created repo {RepoUrl}", repo.HtmlUrl);
        return repo.CloneUrl;
    }

    /// <inheritdoc/>
    public async Task<GitHubUserInfo> ValidateTokenAsync(string patToken)
    {
        var client = CreateClient(patToken);
        var user = await client.User.Current();
        return new GitHubUserInfo { Login = user.Login, Name = user.Name };
    }

    private static GitHubClient CreateClient(string patToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("Stewie"));
        client.Credentials = new Credentials(patToken);
        return client;
    }

    private async Task<int> RunGitAsync(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git", Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            _logger.LogWarning("git {Args} failed (exit {Code}): {Err}", arguments, process.ExitCode, stderr);

        return process.ExitCode;
    }
}
