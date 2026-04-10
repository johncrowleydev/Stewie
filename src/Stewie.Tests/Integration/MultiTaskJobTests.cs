/// <summary>
/// Integration tests for multi-task parallel job execution (JOB-010 T-098, T-099, T-100).
///
/// Tests verify API-level behavior of multi-task jobs with DAG dependencies.
/// Since the test harness cannot run Docker containers, tests seed entities
/// directly via DI to simulate the orchestration pipeline output, then verify
/// the API endpoints return correct statuses, task states, and aggregated results.
///
/// NOTE: These tests are written against the extended API contract from JOB-010 T-095.
/// Dev A implements the multi-task creation endpoint and execution engine.
/// Until Dev A's code is merged, some tests may need adjustment during rebase.
///
/// REF: GOV-002, CON-002 v1.7.0, JOB-010 T-098, T-099, T-100
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
/// Validates multi-task job behavior at the API level.
/// Seeds entities directly to simulate orchestration pipeline output,
/// then verifies GET /api/jobs/{id} returns correct aggregated state.
/// </summary>
public class MultiTaskJobTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly StewieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MultiTaskJobTests(StewieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    // ===================================================================
    // Seeding helpers
    // ===================================================================

    /// <summary>
    /// Creates a project via API and returns its ID.
    /// </summary>
    private async Task<string> CreateProjectAsync(string name = "Multi-Task Test Project")
    {
        var payload = new { name, repoUrl = $"https://github.com/test/{Guid.NewGuid()}" };
        var response = await _client.PostAsJsonAsync("/api/projects", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return doc.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Seeds a multi-task job directly via DI repositories.
    /// Returns the job ID and a dictionary of task IDs keyed by label.
    /// </summary>
    private async Task<(Guid jobId, Dictionary<string, Guid> taskIds)> SeedMultiTaskJobAsync(
        List<(string label, WorkTaskStatus status, string role)> taskDefs,
        List<(string from, string to)>? depEdges = null,
        JobStatus jobStatus = JobStatus.Completed)
    {
        using var scope = _factory.Services.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
        var depRepo = scope.ServiceProvider.GetRequiredService<ITaskDependencyRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var jobId = Guid.NewGuid();
        var taskIds = new Dictionary<string, Guid>();

        // Create job
        var job = new Job
        {
            Id = jobId,
            Status = jobStatus,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = jobStatus is JobStatus.Completed or JobStatus.Failed or JobStatus.PartiallyCompleted
                ? DateTime.UtcNow : null
        };

        // Create tasks
        var tasks = new List<WorkTask>();
        var minuteOffset = -10;
        foreach (var (label, status, role) in taskDefs)
        {
            var taskId = Guid.NewGuid();
            taskIds[label] = taskId;

            var task = new WorkTask
            {
                Id = taskId,
                JobId = jobId,
                Job = job,
                Role = role,
                Status = status,
                Objective = $"Task {label}",
                WorkspacePath = $"/workspace/{jobId}/{taskId}",
                CreatedAt = DateTime.UtcNow.AddMinutes(minuteOffset),
                StartedAt = status != WorkTaskStatus.Pending && status != WorkTaskStatus.Blocked && status != WorkTaskStatus.Cancelled
                    ? DateTime.UtcNow.AddMinutes(minuteOffset + 1) : null,
                CompletedAt = status is WorkTaskStatus.Completed or WorkTaskStatus.Failed or WorkTaskStatus.Cancelled
                    ? DateTime.UtcNow.AddMinutes(minuteOffset + 2) : null,
            };
            tasks.Add(task);
            minuteOffset += 3;
        }

        // Create dependency edges
        var deps = new List<TaskDependency>();
        if (depEdges is not null)
        {
            foreach (var (from, to) in depEdges)
            {
                // "from" depends on "to" — from cannot execute until to completes
                deps.Add(new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskIds[from],
                    DependsOnTaskId = taskIds[to],
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                });
            }
        }

        // Persist
        uow.BeginTransaction();
        await jobRepo.SaveAsync(job);
        foreach (var t in tasks)
            await taskRepo.SaveAsync(t);
        foreach (var d in deps)
            await depRepo.SaveAsync(d);
        await uow.CommitAsync();

        return (jobId, taskIds);
    }

    // ===================================================================
    // T-098: Integration Tests — 2-Task Parallel Job
    // ===================================================================

    /// <summary>
    /// Two parallel tasks (no dependencies), both complete → Job = Completed.
    /// Verifies GET /api/jobs/{id} returns correct aggregate status and task count.
    /// </summary>
    [Fact]
    public async Task TwoParallelTasks_BothComplete()
    {
        // Arrange
        var (jobId, taskIds) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Completed, "developer")
            ],
            jobStatus: JobStatus.Completed);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("Completed", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(2, tasks.GetArrayLength());

        // Both tasks should be Completed
        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            Assert.Equal("Completed", tasks[i].GetProperty("status").GetString());
        }
    }

    /// <summary>
    /// Two parallel tasks (no dependencies), both fail → Job = Failed.
    /// </summary>
    [Fact]
    public async Task TwoParallelTasks_BothFail()
    {
        // Arrange
        var (jobId, _) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Failed, "developer"),
                ("B", WorkTaskStatus.Failed, "developer")
            ],
            jobStatus: JobStatus.Failed);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("Failed", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(2, tasks.GetArrayLength());

        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            Assert.Equal("Failed", tasks[i].GetProperty("status").GetString());
        }
    }

    /// <summary>
    /// Two parallel tasks (no dependencies), one fails, one succeeds → Job = PartiallyCompleted.
    /// </summary>
    [Fact]
    public async Task TwoParallelTasks_OneFailsOneSucceeds()
    {
        // Arrange
        var (jobId, _) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Failed, "developer")
            ],
            jobStatus: JobStatus.PartiallyCompleted);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("PartiallyCompleted", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(2, tasks.GetArrayLength());

        // Verify we have one of each status
        var statuses = new List<string>();
        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            statuses.Add(tasks[i].GetProperty("status").GetString()!);
        }
        Assert.Contains("Completed", statuses);
        Assert.Contains("Failed", statuses);
    }

    // ===================================================================
    // T-099: Integration Tests — 3-Task DAG (A→B, A→C)
    // ===================================================================

    /// <summary>
    /// Fan-out DAG: A first, then B and C in parallel. All complete → Job Completed.
    /// Verifies task ordering and dependency relationships in the response.
    /// </summary>
    [Fact]
    public async Task FanOutDag_ExecutesInOrder()
    {
        // Arrange — A (completed), B depends on A (completed), C depends on A (completed)
        var (jobId, taskIds) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Completed, "developer"),
                ("C", WorkTaskStatus.Completed, "developer")
            ],
            depEdges:
            [
                ("B", "A"), // B depends on A
                ("C", "A")  // C depends on A
            ],
            jobStatus: JobStatus.Completed);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert — all completed, 3 tasks present
        Assert.Equal("Completed", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(3, tasks.GetArrayLength());

        // First task (by CreatedAt) should be A — the root of the DAG
        Assert.Equal($"Task A", tasks[0].GetProperty("objective").GetString());
        Assert.Equal("Completed", tasks[0].GetProperty("status").GetString());
    }

    /// <summary>
    /// Fan-out DAG: A fails → B and C are Cancelled → Job Failed.
    /// When the root task fails, all dependent tasks should be cancelled.
    /// </summary>
    [Fact]
    public async Task FanOutDag_AFailsCascade()
    {
        // Arrange — A (failed), B (cancelled), C (cancelled)
        var (jobId, taskIds) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Failed, "developer"),
                ("B", WorkTaskStatus.Cancelled, "developer"),
                ("C", WorkTaskStatus.Cancelled, "developer")
            ],
            depEdges:
            [
                ("B", "A"),
                ("C", "A")
            ],
            jobStatus: JobStatus.Failed);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("Failed", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(3, tasks.GetArrayLength());

        // Root task A should be Failed
        Assert.Equal("Failed", tasks[0].GetProperty("status").GetString());

        // Downstream tasks B and C should be Cancelled
        var downstreamStatuses = new List<string>();
        for (int i = 1; i < tasks.GetArrayLength(); i++)
        {
            downstreamStatuses.Add(tasks[i].GetProperty("status").GetString()!);
        }
        Assert.All(downstreamStatuses, s => Assert.Equal("Cancelled", s));
    }

    /// <summary>
    /// Fan-out DAG: B and C are independent of each other after A.
    /// B fails, C succeeds → Job = PartiallyCompleted.
    /// </summary>
    [Fact]
    public async Task FanOutDag_BFailsCContinues()
    {
        // Arrange — A (completed), B (failed), C (completed)
        var (jobId, _) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Failed, "developer"),
                ("C", WorkTaskStatus.Completed, "developer")
            ],
            depEdges:
            [
                ("B", "A"),
                ("C", "A")
            ],
            jobStatus: JobStatus.PartiallyCompleted);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert — partial: 2 completed, 1 failed
        Assert.Equal("PartiallyCompleted", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(3, tasks.GetArrayLength());

        var statuses = new List<string>();
        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            statuses.Add(tasks[i].GetProperty("status").GetString()!);
        }
        Assert.Equal(2, statuses.Count(s => s == "Completed"));
        Assert.Equal(1, statuses.Count(s => s == "Failed"));
    }

    // ===================================================================
    // T-100: Integration Tests — Dependency Failure Cascade
    // ===================================================================

    /// <summary>
    /// Linear chain A→B→C: B fails → C is Cancelled → Job = PartiallyCompleted.
    /// A succeeded, B failed mid-chain, C was never started.
    /// </summary>
    [Fact]
    public async Task LinearChain_MiddleTaskFails()
    {
        // Arrange — A (completed), B (failed), C (cancelled)
        var (jobId, _) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Failed, "developer"),
                ("C", WorkTaskStatus.Cancelled, "developer")
            ],
            depEdges:
            [
                ("B", "A"), // B depends on A
                ("C", "B")  // C depends on B
            ],
            jobStatus: JobStatus.PartiallyCompleted);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal("PartiallyCompleted", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(3, tasks.GetArrayLength());

        // Verify chain: A completed, B failed, C cancelled
        Assert.Equal("Completed", tasks[0].GetProperty("status").GetString());
        Assert.Equal("Failed", tasks[1].GetProperty("status").GetString());
        Assert.Equal("Cancelled", tasks[2].GetProperty("status").GetString());

        // C should not have startedAt (it was cancelled before starting)
        Assert.Equal(JsonValueKind.Null, tasks[2].GetProperty("startedAt").ValueKind);
    }

    /// <summary>
    /// Diamond DAG: A→B, A→C, B+C→D. B fails → D cannot execute → PartiallyCompleted.
    /// A completed, C completed, B failed, D cancelled (blocked by B's failure).
    /// </summary>
    [Fact]
    public async Task Diamond_ConvergenceFailure()
    {
        // Arrange — A (completed), B (failed), C (completed), D (cancelled)
        var (jobId, _) = await SeedMultiTaskJobAsync(
            taskDefs:
            [
                ("A", WorkTaskStatus.Completed, "developer"),
                ("B", WorkTaskStatus.Failed, "developer"),
                ("C", WorkTaskStatus.Completed, "developer"),
                ("D", WorkTaskStatus.Cancelled, "developer")
            ],
            depEdges:
            [
                ("B", "A"), // B depends on A
                ("C", "A"), // C depends on A
                ("D", "B"), // D depends on B
                ("D", "C")  // D depends on C
            ],
            jobStatus: JobStatus.PartiallyCompleted);

        // Act
        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        // Assert — partial: A and C completed, B failed, D cancelled
        Assert.Equal("PartiallyCompleted", doc.GetProperty("status").GetString());
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(4, tasks.GetArrayLength());

        var statuses = new List<string>();
        for (int i = 0; i < tasks.GetArrayLength(); i++)
        {
            statuses.Add(tasks[i].GetProperty("status").GetString()!);
        }
        Assert.Equal(2, statuses.Count(s => s == "Completed"));
        Assert.Equal(1, statuses.Count(s => s == "Failed"));
        Assert.Equal(1, statuses.Count(s => s == "Cancelled"));
    }

    /// <summary>
    /// Backward compatibility: old single-task POST /api/jobs format with objective field
    /// still creates a job with one developer task. This validates JOB-010's requirement
    /// that the legacy request format continues to work.
    /// </summary>
    [Fact]
    public async Task BackwardCompat_SingleTaskJob()
    {
        // Arrange — create project first
        var projectId = await CreateProjectAsync("Backward Compat Project");

        // Act — use the OLD request format (no Tasks array, just objective)
        var payload = new { projectId, objective = "Single task backward compat test" };
        var response = await _client.PostAsJsonAsync("/api/jobs", payload);

        // Assert — should still work with 201 Created
        Assert.True(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 or 201 for legacy single-task format, got {(int)response.StatusCode}");

        var doc = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(doc.TryGetProperty("id", out _), "Response must include 'id'");
        Assert.Equal("Pending", doc.GetProperty("status").GetString());

        // Should have exactly 1 task
        var tasks = doc.GetProperty("tasks");
        Assert.Equal(1, tasks.GetArrayLength());
        Assert.Equal("developer", tasks[0].GetProperty("role").GetString());
        Assert.Equal("Single task backward compat test", tasks[0].GetProperty("objective").GetString());
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
