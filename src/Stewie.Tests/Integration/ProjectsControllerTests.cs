/// <summary>
/// Integration tests for the Projects API endpoints.
/// Tests run against a real ASP.NET pipeline with SQLite in-memory database.
///
/// REF: CON-002 §4.1, §5.1, §6
/// REF: GOV-002 (testing protocol)
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies Projects controller behavior against CON-002 §4.1 contract.
/// Uses shared factory fixture for database isolation per test class.
/// </summary>
public class ProjectsControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly StewieWebApplicationFactory _factory;

    /// <summary>Creates an HTTP client bound to the test server.</summary>
    public ProjectsControllerTests(StewieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>GET /api/projects returns 200 with empty array when no projects exist.</summary>
    [Fact]
    public async Task GetAll_EmptyDatabase_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var projects = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(projects);
    }

    /// <summary>POST /api/projects creates project and returns 201.</summary>
    [Fact]
    public async Task Create_ValidProject_Returns201WithProjectData()
    {
        var payload = new { name = "Test Project", repoUrl = "https://github.com/test/repo" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(doc.TryGetProperty("id", out var idProp));
        Assert.NotEqual(string.Empty, idProp.GetString());
        Assert.Equal("Test Project", doc.GetProperty("name").GetString());
        Assert.Equal("https://github.com/test/repo", doc.GetProperty("repoUrl").GetString());
        Assert.True(doc.TryGetProperty("createdAt", out _));
    }

    /// <summary>GET /api/projects/{id} returns 200 for an existing project.</summary>
    [Fact]
    public async Task GetById_ExistingProject_Returns200()
    {
        // Arrange: create a project first
        var payload = new { name = "Lookup Project", repoUrl = "https://github.com/test/lookup" };
        var createResponse = await _client.PostAsJsonAsync("/api/projects", payload);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        var id = created.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/api/projects/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("Lookup Project", doc.GetProperty("name").GetString());
    }

    /// <summary>GET /api/projects/{id} returns 404 for missing project per CON-002 §6.</summary>
    [Fact]
    public async Task GetById_MissingProject_Returns404WithErrorFormat()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/projects/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Verify CON-002 §6 error format
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
        Assert.True(errorObj.TryGetProperty("message", out _));
    }

    /// <summary>POST /api/projects with missing name returns 400 per CON-002 §6.</summary>
    [Fact]
    public async Task Create_MissingName_Returns400WithValidationError()
    {
        var payload = new { name = "", repoUrl = "https://github.com/test/repo" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("VALIDATION_ERROR", errorObj.GetProperty("code").GetString());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
