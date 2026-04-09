/// <summary>
/// Integration tests for Runs and Tasks API endpoints.
/// Tests run against a real ASP.NET pipeline with SQLite in-memory database.
///
/// REF: CON-002 §4.2, §4.3, §5.2, §5.3, §6
/// REF: GOV-002 (testing protocol)
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies Runs and Tasks controller behavior against CON-002 contracts.
/// </summary>
public class RunsControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public RunsControllerTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>GET /api/runs returns 200 with array.</summary>
    [Fact]
    public async Task GetAll_Returns200WithArray()
    {
        var response = await _client.GetAsync("/api/runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var runs = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(runs);
    }

    /// <summary>POST /api/runs with valid body creates a run and returns 201.</summary>
    [Fact]
    public async Task Create_ValidRun_Returns201()
    {
        // First create a project
        var projectPayload = new { name = "Test Project", repoUrl = "https://github.com/test/repo.git" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projBody = await projResponse.Content.ReadAsStringAsync();
        var projDoc = JsonSerializer.Deserialize<JsonElement>(projBody);
        var projectId = projDoc.GetProperty("id").GetString();

        var payload = new { projectId, objective = "Test objective", scope = "test scope" };
        var response = await _client.PostAsJsonAsync("/api/runs", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(doc.TryGetProperty("id", out _));
        Assert.Equal("Pending", doc.GetProperty("status").GetString());
        Assert.True(doc.TryGetProperty("tasks", out var tasks));
        Assert.Equal(JsonValueKind.Array, tasks.ValueKind);
    }

    /// <summary>GET /api/runs/{id} returns 200 with tasks for existing run.</summary>
    [Fact]
    public async Task GetById_ExistingRun_Returns200WithTasks()
    {
        // Arrange: create a project and run
        var projectPayload = new { name = "Get By Id Project", repoUrl = "https://github.com/test/repo2.git" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projDoc = JsonSerializer.Deserialize<JsonElement>(await projResponse.Content.ReadAsStringAsync());
        var projectId = projDoc.GetProperty("id").GetString();

        var payload = new { projectId, objective = "Test for getById" };
        var createResponse = await _client.PostAsJsonAsync("/api/runs", payload);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        var id = created.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/api/runs/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("tasks", out _));
    }

    /// <summary>GET /api/runs/{id} returns 404 with structured error for missing run.</summary>
    [Fact]
    public async Task GetById_MissingRun_Returns404()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/runs/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
    }

    /// <summary>GET /api/tasks/{nonexistent} returns 404.</summary>
    [Fact]
    public async Task GetTaskById_MissingTask_Returns404()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tasks/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
