/// <summary>
/// Integration tests for Jobs and Tasks API endpoints.
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
/// Verifies Jobs and Tasks controller behavior against CON-002 contracts.
/// </summary>
public class JobsControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public JobsControllerTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    /// <summary>GET /api/jobs returns 200 with array.</summary>
    [Fact]
    public async Task GetAll_Returns200WithArray()
    {
        var response = await _client.GetAsync("/api/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<JsonElement[]>(body);
        Assert.NotNull(jobs);
    }

    /// <summary>POST /api/jobs with valid body creates a job and returns 201.</summary>
    [Fact]
    public async Task Create_ValidJob_Returns201()
    {
        // First create a project
        var projectPayload = new { name = "Test Project", repoUrl = "https://github.com/test/repo.git" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projBody = await projResponse.Content.ReadAsStringAsync();
        var projDoc = JsonSerializer.Deserialize<JsonElement>(projBody);
        var projectId = projDoc.GetProperty("id").GetString();

        var payload = new { projectId, objective = "Test objective", scope = "test scope" };
        var response = await _client.PostAsJsonAsync("/api/jobs", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.True(doc.TryGetProperty("id", out _));
        Assert.Equal("Pending", doc.GetProperty("status").GetString());
        Assert.True(doc.TryGetProperty("tasks", out var tasks));
        Assert.Equal(JsonValueKind.Array, tasks.ValueKind);
    }

    /// <summary>GET /api/jobs/{id} returns 200 with tasks for existing job.</summary>
    [Fact]
    public async Task GetById_ExistingJob_Returns200WithTasks()
    {
        // Arrange: create a project and job
        var projectPayload = new { name = "Get By Id Project", repoUrl = "https://github.com/test/repo2.git" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projDoc = JsonSerializer.Deserialize<JsonElement>(await projResponse.Content.ReadAsStringAsync());
        var projectId = projDoc.GetProperty("id").GetString();

        var payload = new { projectId, objective = "Test for getById" };
        var createResponse = await _client.PostAsJsonAsync("/api/jobs", payload);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        var id = created.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/api/jobs/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("tasks", out _));
    }

    /// <summary>GET /api/jobs/{id} returns 404 with structured error for missing job.</summary>
    [Fact]
    public async Task GetById_MissingJob_Returns404()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/jobs/{missingId}");

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
