/// <summary>
/// Integration tests for extended Run creation API.
/// Tests the POST /api/runs endpoint per CON-002 §4.2 (v1.2.0).
///
/// NOTE: Tests are written against the current backend behavior.
/// After Agent A's T-027 merges (extended validation), some tests may need
/// response code adjustments during rebase.
///
/// REF: CON-002 §4.2, §5.2, GOV-002
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies Run creation endpoint behavior per CON-002 contract.
/// </summary>
public class RunCreationTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public RunCreationTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>POST /api/runs with valid projectId creates a run.</summary>
    [Fact]
    public async Task Create_WithProjectId_ReturnsCreatedRun()
    {
        // First create a project to reference
        var projectPayload = new { name = "Run Test Project", repoUrl = "https://github.com/test/run-test" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        // Create a run linked to the project
        var runPayload = new { projectId, objective = "Integration test run" };
        var response = await _client.PostAsJsonAsync("/api/runs", runPayload);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 or 201, got {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("id", out _));
        Assert.Equal("Pending", doc.GetProperty("status").GetString());
    }

    /// <summary>POST /api/runs without projectId creates a run (current behavior allows null).</summary>
    [Fact]
    public async Task Create_WithoutProjectId_CreatesRunOrReturnsError()
    {
        var payload = new { projectId = (Guid?)null };
        var response = await _client.PostAsJsonAsync("/api/runs", payload);

        // Current behavior: 201 (Agent A's T-027 may change to 400)
        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 200, 201, or 400, got {(int)response.StatusCode}");
    }

    /// <summary>POST /api/runs — response includes status field per CON-002 §5.2.</summary>
    [Fact]
    public async Task Create_ResponseHasRequiredFields()
    {
        var projectPayload = new { name = "Schema Test Project", repoUrl = "https://github.com/test/schema" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        var runPayload = new { projectId, objective = "Schema field check" };
        var response = await _client.PostAsJsonAsync("/api/runs", runPayload);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        // CON-002 §5.2 required fields
        Assert.True(doc.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(doc.TryGetProperty("status", out _), "Missing 'status' field");
        Assert.True(doc.TryGetProperty("createdAt", out _), "Missing 'createdAt' field");
        Assert.True(doc.TryGetProperty("tasks", out _), "Missing 'tasks' field");
    }

    /// <summary>GET /api/runs/{id} after creation returns the run with tasks array.</summary>
    [Fact]
    public async Task GetById_AfterCreate_ReturnsRunWithTasks()
    {
        var projectPayload = new { name = "Get Test Project", repoUrl = "https://github.com/test/get" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        var runPayload = new { projectId, objective = "GetById test" };
        var createResponse = await _client.PostAsJsonAsync("/api/runs", runPayload);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        var runId = created.GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/runs/{runId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("tasks", out var tasks));
        Assert.Equal(JsonValueKind.Array, tasks.ValueKind);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
