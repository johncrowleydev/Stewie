/// <summary>
/// Integration tests for extended Job creation API.
/// Tests the POST /api/jobs endpoint per CON-002 §4.2 (v1.5.0).
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
/// Verifies Job creation endpoint behavior per CON-002 contract.
/// </summary>
public class JobCreationTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public JobCreationTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    /// <summary>POST /api/jobs with valid projectId creates a job.</summary>
    [Fact]
    public async Task Create_WithProjectId_ReturnsCreatedJob()
    {
        // First create a project to reference
        var projectPayload = new { name = "Job Test Project", repoUrl = "https://github.com/test/job-test" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        // Create a job linked to the project
        var jobPayload = new { projectId, objective = "Integration test job" };
        var response = await _client.PostAsJsonAsync("/api/jobs", jobPayload);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 or 201, got {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("id", out _));
        Assert.Equal("Pending", doc.GetProperty("status").GetString());
    }

    /// <summary>POST /api/jobs without projectId creates a job (current behavior allows null).</summary>
    [Fact]
    public async Task Create_WithoutProjectId_CreatesJobOrReturnsError()
    {
        var payload = new { projectId = (Guid?)null };
        var response = await _client.PostAsJsonAsync("/api/jobs", payload);

        // Current behavior: 201 (Agent A's T-027 may change to 400)
        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 200, 201, or 400, got {(int)response.StatusCode}");
    }

    /// <summary>POST /api/jobs — response includes status field per CON-002 §5.2.</summary>
    [Fact]
    public async Task Create_ResponseHasRequiredFields()
    {
        var projectPayload = new { name = "Schema Test Project", repoUrl = "https://github.com/test/schema" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        var jobPayload = new { projectId, objective = "Schema field check" };
        var response = await _client.PostAsJsonAsync("/api/jobs", jobPayload);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        // CON-002 §5.2 required fields
        Assert.True(doc.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(doc.TryGetProperty("status", out _), "Missing 'status' field");
        Assert.True(doc.TryGetProperty("createdAt", out _), "Missing 'createdAt' field");
        Assert.True(doc.TryGetProperty("tasks", out _), "Missing 'tasks' field");
    }

    /// <summary>GET /api/jobs/{id} after creation returns the job with tasks array.</summary>
    [Fact]
    public async Task GetById_AfterCreate_ReturnsJobWithTasks()
    {
        var projectPayload = new { name = "Get Test Project", repoUrl = "https://github.com/test/get" };
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<JsonElement>(projectBody);
        var projectId = project.GetProperty("id").GetString();

        var jobPayload = new { projectId, objective = "GetById test" };
        var createResponse = await _client.PostAsJsonAsync("/api/jobs", jobPayload);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        var jobId = created.GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/jobs/{jobId}");

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
