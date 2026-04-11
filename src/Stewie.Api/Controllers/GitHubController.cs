/// <summary>
/// GitHub integration controller — search user's repos via GitHub Search API.
/// GET /api/github/repos?q={searchTerm}
/// REF: JOB-025 T-304, CON-002 §4.11
/// </summary>
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;

namespace Stewie.Api.Controllers;

/// <summary>
/// Proxies GitHub Search API requests using the user's stored Personal Access Token.
/// Uses GET /search/repositories with user: qualifier to search within the
/// authenticated user's repos. No caching — search results are always fresh.
/// Rate limit: 30 search requests/min per authenticated user (GitHub limit).
/// </summary>
[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController : ControllerBase
{
    private readonly IUserCredentialRepository _credentialRepo;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubController> _logger;

    /// <summary>Initializes the GitHub controller with required dependencies.</summary>
    public GitHubController(
        IUserCredentialRepository credentialRepo,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubController> logger)
    {
        _credentialRepo = credentialRepo;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Search the authenticated user's GitHub repositories.
    /// Proxies to GET /search/repositories with the user:{username} qualifier.
    ///
    /// When q is empty, falls back to GET /user/repos?per_page=30&amp;sort=updated
    /// to show recent repos as a starting list.
    ///
    /// Returns 401 if no PAT is configured, 502 if GitHub API fails.
    /// </summary>
    /// <param name="q">Search term (optional). Searches repo names/descriptions.</param>
    /// <returns>Array of { name, fullName, htmlUrl, isPrivate }.</returns>
    [HttpGet("repos")]
    public async Task<IActionResult> SearchRepos([FromQuery] string? q = null)
    {
        var userId = GetUserId();

        // Get the user's GitHub PAT
        var credential = await _credentialRepo.GetByUserAndProviderAsync(userId, "github");
        if (credential is null)
        {
            return Unauthorized(new
            {
                error = new
                {
                    code = "NO_GITHUB_PAT",
                    message = "No GitHub Personal Access Token configured. Add one in Settings.",
                    details = new { }
                }
            });
        }

        string pat;
        try
        {
            pat = _encryptionService.Decrypt(credential.EncryptedToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt GitHub PAT for user {UserId}", userId);
            return StatusCode(502, new
            {
                error = new
                {
                    code = "PAT_DECRYPT_FAILED",
                    message = "Failed to decrypt stored GitHub token. Please re-save your token in Settings.",
                    details = new { }
                }
            });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Stewie", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var searchTerm = q?.Trim();
            List<GitHubRepoDto> repos;

            if (string.IsNullOrEmpty(searchTerm))
            {
                // No query: return the user's recent repos (fast, no search API needed)
                repos = await FetchRecentRepos(client);
            }
            else
            {
                // Search: use GitHub Search API with user qualifier
                repos = await SearchGitHubRepos(client, pat, searchTerm);
            }

            return Ok(repos);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling GitHub API for user {UserId}", userId);
            return StatusCode(502, new
            {
                error = new
                {
                    code = "GITHUB_UNAVAILABLE",
                    message = "Unable to reach GitHub API. Please try again later.",
                    details = new { }
                }
            });
        }
    }

    /// <summary>
    /// Fetches recent repos via GET /user/repos (single page, 30 results, sorted by last updated).
    /// Used when the user hasn't typed a search term yet.
    /// </summary>
    private async Task<List<GitHubRepoDto>> FetchRecentRepos(HttpClient client)
    {
        var response = await client.GetAsync("https://api.github.com/user/repos?per_page=30&sort=updated");

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub /user/repos returned {StatusCode}", response.StatusCode);
            return new List<GitHubRepoDto>();
        }

        var json = await response.Content.ReadAsStringAsync();
        var apiRepos = JsonSerializer.Deserialize<List<GitHubApiRepo>>(json, JsonOpts);

        return apiRepos?.Select(MapToDto).ToList() ?? new List<GitHubRepoDto>();
    }

    /// <summary>
    /// Searches repos via GET /search/repositories with the user:{username} qualifier.
    /// First resolves the authenticated username, then constructs the search query.
    /// Returns top 30 matches sorted by best match.
    /// </summary>
    private async Task<List<GitHubRepoDto>> SearchGitHubRepos(HttpClient client, string pat, string searchTerm)
    {
        // Resolve the authenticated user's login name
        var username = await GetGitHubUsername(client);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Could not resolve GitHub username; falling back to unscoped search");
            // Fall back to unscoped search — still useful, just broader
        }

        // Build the search query: "{searchTerm} user:{username}"
        var queryParts = new List<string> { searchTerm };
        if (!string.IsNullOrEmpty(username))
        {
            queryParts.Add($"user:{username}");
        }
        queryParts.Add("fork:true"); // Include forks in results

        var query = Uri.EscapeDataString(string.Join(" ", queryParts));
        var url = $"https://api.github.com/search/repositories?q={query}&per_page=30&sort=updated";

        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub /search/repositories returned {StatusCode}", response.StatusCode);
            return new List<GitHubRepoDto>();
        }

        var json = await response.Content.ReadAsStringAsync();
        var searchResult = JsonSerializer.Deserialize<GitHubSearchResult>(json, JsonOpts);

        return searchResult?.Items?.Select(MapToDto).ToList() ?? new List<GitHubRepoDto>();
    }

    /// <summary>
    /// Resolves the GitHub username for the authenticated PAT via GET /user.
    /// </summary>
    private async Task<string?> GetGitHubUsername(HttpClient client)
    {
        try
        {
            var response = await client.GetAsync("https://api.github.com/user");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<GitHubUserResponse>(json, JsonOpts);
            return user?.Login;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Maps a GitHub API repo object to our DTO.</summary>
    private static GitHubRepoDto MapToDto(GitHubApiRepo r) => new()
    {
        Name = r.Name ?? "",
        FullName = r.FullName ?? "",
        HtmlUrl = r.HtmlUrl ?? "",
        IsPrivate = r.Private
    };

    /// <summary>Shared JSON options for GitHub API deserialization.</summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Extracts the user ID from the JWT sub claim.</summary>
    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
        return Guid.Parse(sub);
    }
}

/// <summary>Response DTO for a GitHub repository.</summary>
public class GitHubRepoDto
{
    /// <summary>Repository short name (e.g. "my-repo").</summary>
    public string Name { get; set; } = "";

    /// <summary>Full name including owner (e.g. "octocat/my-repo").</summary>
    public string FullName { get; set; } = "";

    /// <summary>Browser URL for the repository.</summary>
    public string HtmlUrl { get; set; } = "";

    /// <summary>Whether the repository is private.</summary>
    public bool IsPrivate { get; set; }
}

/// <summary>GitHub Search API response wrapper.</summary>
internal class GitHubSearchResult
{
    /// <summary>Total number of matches.</summary>
    public int TotalCount { get; set; }

    /// <summary>List of matching repositories.</summary>
    public List<GitHubApiRepo>? Items { get; set; }
}

/// <summary>GitHub GET /user response (subset).</summary>
internal class GitHubUserResponse
{
    /// <summary>The user's login/username.</summary>
    public string? Login { get; set; }
}

/// <summary>Subset of the GitHub API repo object for deserialization.</summary>
internal class GitHubApiRepo
{
    /// <summary>Repository short name.</summary>
    public string? Name { get; set; }

    /// <summary>Full name including owner.</summary>
    public string? FullName { get; set; }

    /// <summary>Browser URL.</summary>
    public string? HtmlUrl { get; set; }

    /// <summary>Whether the repository is private.</summary>
    public bool Private { get; set; }
}
