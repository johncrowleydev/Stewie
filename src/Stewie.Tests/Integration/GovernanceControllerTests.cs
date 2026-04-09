/// <summary>
/// Integration tests for Governance API endpoints.
/// Tests the GET /api/jobs/{id}/governance and GET /api/tasks/{id}/governance
/// endpoints per CON-002 §4.6 (v1.6.0).
///
/// NOTE: These tests depend on Agent A's T-067 (GovernanceReport entity)
/// and T-069 (orchestration chaining) being merged. Until then, some tests
/// are skipped because the governance worker pipeline doesn't exist yet.
///
/// REF: CON-002 §4.6, GOV-002, JOB-007 T-072
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies Governance controller behavior against CON-002 §4.6 contract.
/// </summary>
public class GovernanceControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public GovernanceControllerTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    // -------------------------------------------------------------------
    // GET /api/jobs/{id}/governance — Job-level governance report
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/jobs/{nonexistent}/governance returns 404 with NOT_FOUND error.
    /// </summary>
    [Fact]
    public async Task GetByJobId_MissingJob_Returns404()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/jobs/{missingId}/governance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
    }

    /// <summary>
    /// GET /api/jobs/{id}/governance for a job without governance returns 404.
    /// A job created via POST /api/jobs will not have a governance report until
    /// the orchestration pipeline runs the governance worker (T-069).
    /// </summary>
    [Fact]
    public async Task GetByJobId_JobWithoutGovernance_Returns404()
    {
        // Create a project and job
        var projectPayload = new { name = "Gov Test Project", repoUrl = "https://github.com/test/gov-test" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projDoc = JsonSerializer.Deserialize<JsonElement>(await projResponse.Content.ReadAsStringAsync());
        var projectId = projDoc.GetProperty("id").GetString();

        var jobPayload = new { projectId, objective = "Test governance endpoint" };
        var jobResponse = await _client.PostAsJsonAsync("/api/jobs", jobPayload);
        var jobDoc = JsonSerializer.Deserialize<JsonElement>(await jobResponse.Content.ReadAsStringAsync());
        var jobId = jobDoc.GetProperty("id").GetString();

        // Query governance — should be 404 since no governance worker has run
        var response = await _client.GetAsync($"/api/jobs/{jobId}/governance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// GET /api/jobs/{id}/governance returns 200 with correctly shaped report.
    /// This test will only pass after Agent A's T-069 (orchestration chaining)
    /// is merged and a governance worker has produced a report.
    /// </summary>
    [Fact(Skip = "Requires Agent A T-069: governance worker pipeline not yet implemented")]
    public async Task GetByJobId_WithGovernanceReport_Returns200WithSchema()
    {
        // This test will be implemented during rebase when the governance
        // pipeline exists and can produce reports via the test job endpoint.
        // Expected schema per CON-002 §4.6:
        // {
        //   "id": "uuid",
        //   "taskId": "uuid",
        //   "passed": bool,
        //   "totalChecks": int,
        //   "passedChecks": int,
        //   "failedChecks": int,
        //   "checks": [{ "ruleId", "ruleName", "category", "passed", "details", "severity" }],
        //   "createdAt": "ISO 8601"
        // }
        await Task.CompletedTask;
    }

    // -------------------------------------------------------------------
    // GET /api/tasks/{id}/governance — Task-level governance report
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/tasks/{nonexistent}/governance returns 404 with NOT_FOUND error.
    /// </summary>
    [Fact]
    public async Task GetByTaskId_MissingTask_Returns404()
    {
        var missingId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tasks/{missingId}/governance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(doc.TryGetProperty("error", out var errorObj));
        Assert.Equal("NOT_FOUND", errorObj.GetProperty("code").GetString());
    }

    /// <summary>
    /// GET /api/tasks/{id}/governance for a developer task (non-tester) returns 404.
    /// Only tester-role tasks produce governance reports.
    /// </summary>
    [Fact]
    public async Task GetByTaskId_DeveloperTask_Returns404()
    {
        // Create a project and job (which creates a developer task)
        var projectPayload = new { name = "Gov Task Test", repoUrl = "https://github.com/test/gov-task" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projDoc = JsonSerializer.Deserialize<JsonElement>(await projResponse.Content.ReadAsStringAsync());
        var projectId = projDoc.GetProperty("id").GetString();

        var jobPayload = new { projectId, objective = "Test task governance" };
        var jobResponse = await _client.PostAsJsonAsync("/api/jobs", jobPayload);
        var jobDoc = JsonSerializer.Deserialize<JsonElement>(await jobResponse.Content.ReadAsStringAsync());

        // Get the first task (should be developer-role)
        var tasks = jobDoc.GetProperty("tasks");
        Assert.True(tasks.GetArrayLength() > 0, "Job should have at least one task");
        var taskId = tasks[0].GetProperty("id").GetString();

        // Query governance — developer tasks don't have governance reports
        var response = await _client.GetAsync($"/api/tasks/{taskId}/governance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // Task chain field tests — parentTaskId + attemptNumber
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/jobs/{id} response includes parentTaskId and attemptNumber in task objects.
    /// For a newly created job, the first task should have parentTaskId=null, attemptNumber=1.
    /// </summary>
    [Fact(Skip = "Requires Agent A T-066: ParentTaskId and AttemptNumber fields not yet on WorkTask")]
    public async Task GetJobById_TasksIncludeChainFields()
    {
        // Create a project and job
        var projectPayload = new { name = "Chain Fields Test", repoUrl = "https://github.com/test/chain" };
        var projResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projDoc = JsonSerializer.Deserialize<JsonElement>(await projResponse.Content.ReadAsStringAsync());
        var projectId = projDoc.GetProperty("id").GetString();

        var jobPayload = new { projectId, objective = "Test chain fields" };
        var jobResponse = await _client.PostAsJsonAsync("/api/jobs", jobPayload);
        var jobDoc = JsonSerializer.Deserialize<JsonElement>(await jobResponse.Content.ReadAsStringAsync());
        var jobId = jobDoc.GetProperty("id").GetString();

        // Fetch full job
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);
        var tasks = doc.GetProperty("tasks");
        Assert.True(tasks.GetArrayLength() > 0);

        var firstTask = tasks[0];
        Assert.True(firstTask.TryGetProperty("parentTaskId", out _), "Task must include parentTaskId");
        Assert.True(firstTask.TryGetProperty("attemptNumber", out var attempt), "Task must include attemptNumber");
        Assert.Equal(1, attempt.GetInt32());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
