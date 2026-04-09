/// <summary>
/// Integration tests for the governance happy path: dev → tester → accept.
/// Tests the API-level behavior of governance reports and task chain fields.
///
/// REF: CON-002 §4.6, JOB-008 T-078
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies the happy path for governance: job creation produces a dev task,
/// and governance API endpoints behave correctly at each stage.
/// Full end-to-end (container-based) testing requires the orchestration pipeline;
/// these tests verify API contract compliance and data shape.
/// </summary>
public class GovernanceHappyPathTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public GovernanceHappyPathTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    /// <summary>
    /// Creates a project and returns its ID.
    /// </summary>
    private async Task<string> CreateProjectAsync(string name = "Gov Happy Path Project")
    {
        var payload = new { name, repoUrl = $"https://github.com/test/{Guid.NewGuid()}" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return doc.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Creates a job and returns (jobId, firstTaskId).
    /// </summary>
    private async Task<(string jobId, string taskId)> CreateJobAsync(string projectId)
    {
        var payload = new { projectId, objective = "Implement governance test feature" };
        var response = await _client.PostAsJsonAsync("/api/jobs", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var jobId = doc.GetProperty("id").GetString()!;
        var taskId = doc.GetProperty("tasks")[0].GetProperty("id").GetString()!;
        return (jobId, taskId);
    }

    // -------------------------------------------------------------------
    // Job creation produces developer task with correct chain fields
    // -------------------------------------------------------------------

    /// <summary>
    /// POST /api/jobs creates a job with a single developer task.
    /// First task has parentTaskId=null and attemptNumber=1.
    /// </summary>
    [Fact]
    public async Task CreateJob_ProducesDeveloperTask_WithChainFields()
    {
        var projectId = await CreateProjectAsync();
        var (jobId, taskId) = await CreateJobAsync(projectId);

        // Fetch the job to verify task chain fields
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(1, tasks.GetArrayLength());

        var task = tasks[0];
        Assert.Equal("developer", task.GetProperty("role").GetString());
        Assert.Equal("Pending", task.GetProperty("status").GetString());
        Assert.True(task.TryGetProperty("parentTaskId", out var parentId),
            "Task response must include parentTaskId per CON-002 v1.6.0");
        Assert.Equal(JsonValueKind.Null, parentId.ValueKind);
        Assert.True(task.TryGetProperty("attemptNumber", out var attempt),
            "Task response must include attemptNumber per CON-002 v1.6.0");
        Assert.Equal(1, attempt.GetInt32());
    }

    // -------------------------------------------------------------------
    // Governance report not available for new job
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/jobs/{id}/governance returns 404 for a freshly created job
    /// (no tester has run yet).
    /// </summary>
    [Fact]
    public async Task GetGovernance_NewJob_Returns404()
    {
        var projectId = await CreateProjectAsync("Gov 404 Test");
        var (jobId, _) = await CreateJobAsync(projectId);

        var response = await _client.GetAsync($"/api/jobs/{jobId}/governance");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
    }

    /// <summary>
    /// GET /api/tasks/{id}/governance returns 404 for a developer task
    /// (governance reports only exist for tester tasks).
    /// </summary>
    [Fact]
    public async Task GetTaskGovernance_DeveloperTask_Returns404()
    {
        var projectId = await CreateProjectAsync("Gov Dev Task Test");
        var (_, taskId) = await CreateJobAsync(projectId);

        var response = await _client.GetAsync($"/api/tasks/{taskId}/governance");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // Task list endpoint returns chain fields
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/jobs/{jobId}/tasks includes parentTaskId and attemptNumber
    /// in each task object.
    /// </summary>
    [Fact]
    public async Task GetTasksByJob_IncludesChainFields()
    {
        var projectId = await CreateProjectAsync("Task Chain Fields");
        var (jobId, _) = await CreateJobAsync(projectId);

        var response = await _client.GetAsync($"/api/jobs/{jobId}/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tasks = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(tasks.GetArrayLength() > 0);

        var task = tasks[0];
        Assert.True(task.TryGetProperty("parentTaskId", out _),
            "Task list response must include parentTaskId");
        Assert.True(task.TryGetProperty("attemptNumber", out _),
            "Task list response must include attemptNumber");
    }

    /// <summary>
    /// GET /api/tasks/{id} includes parentTaskId and attemptNumber.
    /// </summary>
    [Fact]
    public async Task GetTaskById_IncludesChainFields()
    {
        var projectId = await CreateProjectAsync("Single Task Chain");
        var (_, taskId) = await CreateJobAsync(projectId);

        var response = await _client.GetAsync($"/api/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var task = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(task.TryGetProperty("parentTaskId", out _),
            "GET /api/tasks/{id} must include parentTaskId");
        Assert.True(task.TryGetProperty("attemptNumber", out var attempt),
            "GET /api/tasks/{id} must include attemptNumber");
        Assert.Equal(1, attempt.GetInt32());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
