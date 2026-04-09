/// <summary>
/// Integration tests for the Project Creation flows (CON-002 §4.1 v1.4.0).
/// Tests both link-existing and create-new repository modes against
/// the real ASP.NET pipeline with SQLite in-memory database.
///
/// REF: CON-002 §4.1, §5.1, §6
/// REF: GOV-002 (testing protocol)
/// REF: JOB-005 T-054
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Stewie.Application.Interfaces;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies project creation in both link and create modes per CON-002 §4.1 v1.4.0.
/// Uses a custom factory that registers a mock IGitPlatformService for create-mode tests.
/// </summary>
public class ProjectCreationTests : IClassFixture<ProjectCreationTestFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly ProjectCreationTestFactory _factory;

    /// <summary>Creates an HTTP client bound to the test server with auth.</summary>
    public ProjectCreationTests(ProjectCreationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    // -------------------------------------------------------------------
    // Link Mode Tests (backward compatible)
    // -------------------------------------------------------------------

    /// <summary>POST /api/projects with name + repoUrl creates project (link mode, backward compatible) — 201.</summary>
    [Fact]
    public async Task Create_LinkMode_ValidPayload_Returns201()
    {
        var payload = new { name = "Link Project", repoUrl = "https://github.com/test/link-repo" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("Link Project", doc.GetProperty("name").GetString());
        Assert.Equal("https://github.com/test/link-repo", doc.GetProperty("repoUrl").GetString());
        Assert.True(doc.TryGetProperty("id", out var idProp));
        Assert.NotEqual(string.Empty, idProp.GetString());
        Assert.True(doc.TryGetProperty("createdAt", out _));
    }

    /// <summary>POST /api/projects with empty name returns 400 with VALIDATION_ERROR.</summary>
    [Fact]
    public async Task Create_LinkMode_MissingName_Returns400()
    {
        var payload = new { name = "", repoUrl = "https://github.com/test/repo" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("VALIDATION_ERROR", errorObj.GetProperty("code").GetString());
    }

    /// <summary>POST /api/projects with missing repoUrl and no createRepo returns 400.</summary>
    [Fact]
    public async Task Create_LinkMode_MissingRepoUrl_Returns400()
    {
        var payload = new { name = "No URL Project" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("VALIDATION_ERROR", errorObj.GetProperty("code").GetString());
    }

    // -------------------------------------------------------------------
    // Create Mode Tests
    // -------------------------------------------------------------------

    /// <summary>
    /// POST /api/projects with createRepo=true but no PAT configured returns 400
    /// with a message telling user to configure their PAT.
    /// NOTE: This test relies on the user not having a stored credential.
    /// The mock IGitPlatformService won't be reached — the controller should fail
    /// at the credential lookup stage.
    /// </summary>
    [Fact(Skip = "Requires Agent A T-050: createRepo flow not yet implemented in ProjectsController")]
    public async Task Create_CreateMode_NoPat_Returns400WithClearMessage()
    {
        var payload = new
        {
            name = "Create Without PAT",
            createRepo = true,
            repoName = "test-repo"
        };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        // Backend should return 400 because no PAT is configured
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // The error message should mention PAT configuration
        Assert.Contains("PAT", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>POST /api/projects with createRepo=true but missing repoName returns 400.</summary>
    [Fact]
    public async Task Create_CreateMode_MissingRepoName_Returns400()
    {
        var payload = new
        {
            name = "Create Without RepoName",
            createRepo = true,
            repoName = ""
        };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("VALIDATION_ERROR", errorObj.GetProperty("code").GetString());
    }

    /// <summary>POST /api/projects with createRepo=true AND repoUrl is conflicting — returns 400.</summary>
    [Fact(Skip = "Requires Agent A T-050: createRepo validation not yet implemented in ProjectsController")]
    public async Task Create_CreateMode_ConflictingRepoUrl_Returns400()
    {
        var payload = new
        {
            name = "Conflicting Project",
            createRepo = true,
            repoName = "conflict-repo",
            repoUrl = "https://github.com/conflict/repo"
        };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("VALIDATION_ERROR", errorObj.GetProperty("code").GetString());
    }

    // -------------------------------------------------------------------
    // Response Schema Tests
    // -------------------------------------------------------------------

    /// <summary>GET /api/projects response includes repoProvider field per CON-002 §5.1 v1.4.0.</summary>
    [Fact(Skip = "Requires Agent A T-049: repoProvider field not yet in Project entity or API response")]
    public async Task GetAll_ResponseIncludesRepoProviderField()
    {
        // Create a project first so the list is non-empty
        var createPayload = new { name = "Provider Test", repoUrl = "https://github.com/test/provider" };
        await _client.PostAsJsonAsync("/api/projects", createPayload);

        var response = await _client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var projects = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(projects);
        Assert.NotEmpty(projects);

        // Verify each project has a repoProvider field (may be null)
        foreach (var project in projects)
        {
            Assert.True(
                project.TryGetProperty("repoProvider", out _),
                "Project response must include 'repoProvider' field per CON-002 §5.1 v1.4.0");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

/// <summary>
/// Custom WebApplicationFactory for project creation tests that
/// registers a mock IGitPlatformService.
/// This allows testing the create-repo flow without actual GitHub API calls.
/// </summary>
public class ProjectCreationTestFactory : StewieWebApplicationFactory
{
    /// <summary>The mock IGitPlatformService used by tests. Can be configured per-test via Record calls.</summary>
    public IGitPlatformService MockGitPlatformService { get; } = Substitute.For<IGitPlatformService>();

    /// <summary>Configures services to replace the real IGitPlatformService with a mock.</summary>
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace IGitPlatformService with mock
            var gitHubDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IGitPlatformService));
            if (gitHubDescriptor is not null) services.Remove(gitHubDescriptor);

            services.AddScoped<IGitPlatformService>(_ => MockGitPlatformService);
        });
    }
}
