/// <summary>
/// Integration tests for the governance retry flow:
/// dev → tester (FAIL) → dev (retry, attempt 2) → tester (PASS).
///
/// These tests verify the API-level behavior of the retry cycle.
/// Since full container-based orchestration isn't available in the test harness,
/// tests validate:
/// 1. Event endpoints emit governance lifecycle events
/// 2. Task chain fields propagate correctly in retry scenarios
/// 3. GovernanceReport response shape per CON-002 §4.6
///
/// REF: CON-002 §4.6, JOB-008 T-079
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies retry loop behavior at the API level.
/// Directly inserts entities to simulate the orchestration pipeline's output.
/// </summary>
public class GovernanceRetryFlowTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly StewieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GovernanceRetryFlowTests(StewieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    /// <summary>
    /// Simulates the orchestration pipeline output for a retry cycle:
    /// Job → DevTask1 (completed) → TesterTask1 (failed) → GovernanceReport(FAIL)
    ///     → DevTask2 (completed, attempt 2) → TesterTask2 (completed) → GovernanceReport(PASS)
    /// Returns the job ID.
    /// </summary>
    private async Task<Guid> SeedRetryScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
        var govRepo = scope.ServiceProvider.GetRequiredService<IGovernanceReportRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var jobId = Guid.NewGuid();
        var devTask1Id = Guid.NewGuid();
        var testerTask1Id = Guid.NewGuid();
        var devTask2Id = Guid.NewGuid();
        var testerTask2Id = Guid.NewGuid();

        // Create job
        var job = new Job
        {
            Id = jobId,
            Status = JobStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow,
        };

        // Dev task 1 (attempt 1)
        var devTask1 = new WorkTask
        {
            Id = devTask1Id,
            JobId = jobId,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Completed,
            Objective = "Implement feature",
            AttemptNumber = 1,
            WorkspacePath = "/workspace/1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            StartedAt = DateTime.UtcNow.AddMinutes(-9),
            CompletedAt = DateTime.UtcNow.AddMinutes(-7),
        };

        // Tester task 1 (failed governance)
        var testerTask1 = new WorkTask
        {
            Id = testerTask1Id,
            JobId = jobId,
            Job = job,
            Role = "tester",
            Status = WorkTaskStatus.Failed,
            ParentTaskId = devTask1Id,
            AttemptNumber = 1,
            FailureReason = TaskFailureReason.GovernanceFailed.ToString(),
            WorkspacePath = "/workspace/1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-7),
            StartedAt = DateTime.UtcNow.AddMinutes(-7),
            CompletedAt = DateTime.UtcNow.AddMinutes(-6),
        };

        // Governance report 1 (FAIL)
        var govReport1 = new GovernanceReport
        {
            Id = Guid.NewGuid(),
            TaskId = testerTask1Id,
            Passed = false,
            TotalChecks = 16,
            PassedChecks = 14,
            FailedChecks = 2,
            CheckResultsJson = JsonSerializer.Serialize(new[]
            {
                new { ruleId = "GOV-002-001", ruleName = "Build Succeeds", category = "GOV-002", passed = true, details = (string?)null, severity = "error" },
                new { ruleId = "GOV-002-002", ruleName = "Tests Pass", category = "GOV-002", passed = true, details = (string?)null, severity = "error" },
                new { ruleId = "GOV-003-001", ruleName = "No any Types", category = "GOV-003", passed = false, details = "src/utils.ts:42 — found `: any`", severity = "error" },
                new { ruleId = "SEC-001-001", ruleName = "No Secrets in Diff", category = "SEC-001", passed = false, details = "Detected API key pattern in src/config.ts", severity = "error" },
            }),
            CreatedAt = DateTime.UtcNow.AddMinutes(-6),
        };

        // Dev task 2 (retry, attempt 2)
        var devTask2 = new WorkTask
        {
            Id = devTask2Id,
            JobId = jobId,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Completed,
            Objective = "Implement feature",
            ParentTaskId = devTask1Id,
            AttemptNumber = 2,
            GovernanceViolationsJson = JsonSerializer.Serialize(new[]
            {
                new { ruleId = "GOV-003-001", ruleName = "No any Types", details = "src/utils.ts:42 — found `: any`" },
                new { ruleId = "SEC-001-001", ruleName = "No Secrets in Diff", details = "Detected API key pattern in src/config.ts" },
            }),
            WorkspacePath = "/workspace/1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-3),
        };

        // Tester task 2 (passed governance)
        var testerTask2 = new WorkTask
        {
            Id = testerTask2Id,
            JobId = jobId,
            Job = job,
            Role = "tester",
            Status = WorkTaskStatus.Completed,
            ParentTaskId = devTask1Id,
            AttemptNumber = 2,
            WorkspacePath = "/workspace/1",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            StartedAt = DateTime.UtcNow.AddMinutes(-3),
            CompletedAt = DateTime.UtcNow.AddMinutes(-2),
        };

        // Governance report 2 (PASS)
        var govReport2 = new GovernanceReport
        {
            Id = Guid.NewGuid(),
            TaskId = testerTask2Id,
            Passed = true,
            TotalChecks = 16,
            PassedChecks = 16,
            FailedChecks = 0,
            CheckResultsJson = JsonSerializer.Serialize(new[]
            {
                new { ruleId = "GOV-002-001", ruleName = "Build Succeeds", category = "GOV-002", passed = true, details = (string?)null, severity = "error" },
                new { ruleId = "GOV-003-001", ruleName = "No any Types", category = "GOV-003", passed = true, details = (string?)null, severity = "error" },
                new { ruleId = "SEC-001-001", ruleName = "No Secrets in Diff", category = "SEC-001", passed = true, details = (string?)null, severity = "error" },
            }),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
        };

        // Governance events
        var events = new[]
        {
            new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = jobId, EventType = EventType.GovernanceStarted, Payload = "{}", Timestamp = DateTime.UtcNow.AddMinutes(-7) },
            new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = jobId, EventType = EventType.GovernanceFailed, Payload = "{\"failedChecks\": 2}", Timestamp = DateTime.UtcNow.AddMinutes(-6) },
            new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = jobId, EventType = EventType.GovernanceRetry, Payload = "{\"attemptNumber\": 2}", Timestamp = DateTime.UtcNow.AddMinutes(-5) },
            new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = jobId, EventType = EventType.GovernanceStarted, Payload = "{}", Timestamp = DateTime.UtcNow.AddMinutes(-3) },
            new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = jobId, EventType = EventType.GovernancePassed, Payload = "{\"passedChecks\": 16}", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
        };

        uow.BeginTransaction();
        await jobRepo.SaveAsync(job);
        await taskRepo.SaveAsync(devTask1);
        await taskRepo.SaveAsync(testerTask1);
        await govRepo.SaveAsync(govReport1);
        await taskRepo.SaveAsync(devTask2);
        await taskRepo.SaveAsync(testerTask2);
        await govRepo.SaveAsync(govReport2);
        foreach (var evt in events)
            await eventRepo.SaveAsync(evt);
        await uow.CommitAsync();

        return jobId;
    }

    // -------------------------------------------------------------------
    // Retry cycle: task chain has 4 tasks in correct order
    // -------------------------------------------------------------------

    /// <summary>
    /// After a retry cycle, GET /api/jobs/{id} returns 4 tasks:
    /// dev1 (attempt 1) → tester1 (attempt 1, failed) → dev2 (attempt 2) → tester2 (attempt 2, pass).
    /// Tasks are ordered by CreatedAt.
    /// </summary>
    [Fact]
    public async Task RetryFlow_JobHasFourTasks_InCorrectOrder()
    {
        var jobId = await SeedRetryScenarioAsync();
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(4, tasks.GetArrayLength());

        // Task 1: developer, attempt 1
        Assert.Equal("developer", tasks[0].GetProperty("role").GetString());
        Assert.Equal(1, tasks[0].GetProperty("attemptNumber").GetInt32());
        Assert.Equal(JsonValueKind.Null, tasks[0].GetProperty("parentTaskId").ValueKind);

        // Task 2: tester, attempt 1 (parent = dev1)
        Assert.Equal("tester", tasks[1].GetProperty("role").GetString());
        Assert.Equal(1, tasks[1].GetProperty("attemptNumber").GetInt32());
        Assert.Equal("Failed", tasks[1].GetProperty("status").GetString());

        // Task 3: developer, attempt 2 (parent = dev1)
        Assert.Equal("developer", tasks[2].GetProperty("role").GetString());
        Assert.Equal(2, tasks[2].GetProperty("attemptNumber").GetInt32());

        // Task 4: tester, attempt 2
        Assert.Equal("tester", tasks[3].GetProperty("role").GetString());
        Assert.Equal(2, tasks[3].GetProperty("attemptNumber").GetInt32());
        Assert.Equal("Completed", tasks[3].GetProperty("status").GetString());
    }

    // -------------------------------------------------------------------
    // Latest governance report is the passing one
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/jobs/{id}/governance returns the LATEST governance report (the passing one).
    /// </summary>
    [Fact]
    public async Task RetryFlow_LatestGovernanceReport_IsPassing()
    {
        var jobId = await SeedRetryScenarioAsync();
        var response = await _client.GetAsync($"/api/jobs/{jobId}/governance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(doc.GetProperty("passed").GetBoolean(), "Latest report should be PASS");
        Assert.Equal(16, doc.GetProperty("totalChecks").GetInt32());
        Assert.Equal(16, doc.GetProperty("passedChecks").GetInt32());
        Assert.Equal(0, doc.GetProperty("failedChecks").GetInt32());
        Assert.True(doc.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() > 0, "Checks array should be populated");
    }

    // -------------------------------------------------------------------
    // Failed tester task has governance report with violations
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/tasks/{testerTask1Id}/governance returns the FAILED governance report
    /// with check results including failure details.
    /// </summary>
    [Fact]
    public async Task RetryFlow_FailedTesterReport_HasViolations()
    {
        var jobId = await SeedRetryScenarioAsync();

        // Get the tasks to find the first tester (failed one)
        var jobResponse = await _client.GetAsync($"/api/jobs/{jobId}");
        var jobDoc = JsonSerializer.Deserialize<JsonElement>(await jobResponse.Content.ReadAsStringAsync());
        var tasks = jobDoc.GetProperty("tasks");

        // Find the failed tester task
        string? failedTesterTaskId = null;
        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            if (tasks[i].GetProperty("role").GetString() == "tester" &&
                tasks[i].GetProperty("status").GetString() == "Failed")
            {
                failedTesterTaskId = tasks[i].GetProperty("id").GetString();
                break;
            }
        }
        Assert.NotNull(failedTesterTaskId);

        var response = await _client.GetAsync($"/api/tasks/{failedTesterTaskId}/governance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.False(doc.GetProperty("passed").GetBoolean(), "Failed tester report should be FAIL");
        Assert.Equal(2, doc.GetProperty("failedChecks").GetInt32());

        // Verify checks include failure details
        var checks = doc.GetProperty("checks");
        var failedChecks = new List<string>();
        for (int i = 0; i < checks.GetArrayLength(); i++)
        {
            if (!checks[i].GetProperty("passed").GetBoolean())
            {
                failedChecks.Add(checks[i].GetProperty("ruleId").GetString()!);
                // Failed checks should have details
                Assert.NotNull(checks[i].GetProperty("details").GetString());
            }
        }
        Assert.Contains("GOV-003-001", failedChecks);
        Assert.Contains("SEC-001-001", failedChecks);
    }

    // -------------------------------------------------------------------
    // Governance events are emitted in correct order
    // -------------------------------------------------------------------

    /// <summary>
    /// GET /api/events?entityType=Job&entityId={id} returns governance lifecycle events
    /// in the correct sequence: Started → Failed → Retry → Started → Passed.
    /// </summary>
    [Fact]
    public async Task RetryFlow_GovernanceEvents_EmittedInOrder()
    {
        var jobId = await SeedRetryScenarioAsync();
        var response = await _client.GetAsync($"/api/events?entityType=Job&entityId={jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(events.GetArrayLength() >= 5, "Should have at least 5 governance events");

        // Collect governance event types in order
        // GetByEntityAsync returns OrderBy(Timestamp) — chronological
        var govEvents = new List<string>();
        for (int i = 0; i < events.GetArrayLength(); i++)
        {
            var eventType = events[i].GetProperty("eventType").GetString()!;
            if (eventType.StartsWith("Governance"))
            {
                govEvents.Add(eventType);
            }
        }

        Assert.Equal(5, govEvents.Count);
        Assert.Equal("GovernanceStarted", govEvents[0]);
        Assert.Equal("GovernanceFailed", govEvents[1]);
        Assert.Equal("GovernanceRetry", govEvents[2]);
        Assert.Equal("GovernanceStarted", govEvents[3]);
        Assert.Equal("GovernancePassed", govEvents[4]);
    }

    // -------------------------------------------------------------------
    // Governance report response shape matches CON-002 §4.6
    // -------------------------------------------------------------------

    /// <summary>
    /// Governance report response includes all required fields per CON-002 §4.6:
    /// id, taskId, passed, totalChecks, passedChecks, failedChecks, checks[], createdAt.
    /// Each check includes: ruleId, ruleName, category, passed, details, severity.
    /// </summary>
    [Fact]
    public async Task GovernanceReport_HasCorrectResponseShape()
    {
        var jobId = await SeedRetryScenarioAsync();
        var response = await _client.GetAsync($"/api/jobs/{jobId}/governance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Top-level fields
        Assert.True(doc.TryGetProperty("id", out _), "Response must include 'id'");
        Assert.True(doc.TryGetProperty("taskId", out _), "Response must include 'taskId'");
        Assert.True(doc.TryGetProperty("passed", out _), "Response must include 'passed'");
        Assert.True(doc.TryGetProperty("totalChecks", out _), "Response must include 'totalChecks'");
        Assert.True(doc.TryGetProperty("passedChecks", out _), "Response must include 'passedChecks'");
        Assert.True(doc.TryGetProperty("failedChecks", out _), "Response must include 'failedChecks'");
        Assert.True(doc.TryGetProperty("checks", out var checks), "Response must include 'checks'");
        Assert.True(doc.TryGetProperty("createdAt", out _), "Response must include 'createdAt'");

        // Check result fields
        Assert.True(checks.GetArrayLength() > 0, "Checks should not be empty");
        var firstCheck = checks[0];
        Assert.True(firstCheck.TryGetProperty("ruleId", out _), "Check must include 'ruleId'");
        Assert.True(firstCheck.TryGetProperty("ruleName", out _), "Check must include 'ruleName'");
        Assert.True(firstCheck.TryGetProperty("category", out _), "Check must include 'category'");
        Assert.True(firstCheck.TryGetProperty("passed", out _), "Check must include 'passed'");
        Assert.True(firstCheck.TryGetProperty("details", out _), "Check must include 'details'");
        Assert.True(firstCheck.TryGetProperty("severity", out _), "Check must include 'severity'");
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
