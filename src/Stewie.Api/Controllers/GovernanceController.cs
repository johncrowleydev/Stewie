/// <summary>
/// Governance API controller — endpoints for querying governance reports.
/// REF: CON-002 §4.6, JOB-007 T-072
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If 404 on job governance     → check IGovernanceReportRepository.GetLatestByJobIdAsync
/// 2. If 404 on task governance    → check that task exists and has role=tester
/// 3. If checks array empty        → check GovernanceReport.CheckResultsJson deserialization
/// </summary>
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes read-only endpoints for governance reports.
/// Governance reports are created by the orchestration service after running
/// the governance worker against a completed developer task.
/// </summary>
[ApiController]
[Authorize]
public class GovernanceController : ControllerBase
{
    private readonly IGovernanceReportRepository _governanceReportRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly ILogger<GovernanceController> _logger;

    /// <summary>Initializes the GovernanceController with required dependencies.</summary>
    public GovernanceController(
        IGovernanceReportRepository governanceReportRepository,
        IJobRepository jobRepository,
        IWorkTaskRepository workTaskRepository,
        ILogger<GovernanceController> logger)
    {
        _governanceReportRepository = governanceReportRepository;
        _jobRepository = jobRepository;
        _workTaskRepository = workTaskRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the latest governance report for a job.
    /// Returns the most recent GovernanceReport across all tester tasks in the job.
    /// </summary>
    /// <param name="jobId">The job's GUID.</param>
    /// <returns>200 OK with governance report per CON-002 §4.6, or 404 if no report exists.</returns>
    [HttpGet("api/jobs/{jobId:guid}/governance")]
    public async Task<IActionResult> GetByJobId(Guid jobId)
    {
        _logger.LogInformation("Getting latest governance report for job {JobId}", jobId);

        // Validate job exists
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job is null)
        {
            throw new KeyNotFoundException($"Job with ID '{jobId}' was not found.");
        }

        // Find the latest governance report for any tester task in this job
        var report = await _governanceReportRepository.GetLatestByJobIdAsync(jobId);
        if (report is null)
        {
            throw new KeyNotFoundException(
                $"No governance report found for job '{jobId}'. " +
                "Governance reports are created after the tester task completes.");
        }

        return Ok(FormatReport(report));
    }

    /// <summary>
    /// Gets the governance report for a specific tester task.
    /// Each tester task produces exactly one governance report.
    /// </summary>
    /// <param name="taskId">The task's GUID (must be a tester-role task).</param>
    /// <returns>200 OK with governance report per CON-002 §4.6, or 404 if no report exists.</returns>
    [HttpGet("api/tasks/{taskId:guid}/governance")]
    public async Task<IActionResult> GetByTaskId(Guid taskId)
    {
        _logger.LogInformation("Getting governance report for task {TaskId}", taskId);

        // Validate task exists
        var task = await _workTaskRepository.GetByIdAsync(taskId);
        if (task is null)
        {
            throw new KeyNotFoundException($"Task with ID '{taskId}' was not found.");
        }

        var report = await _governanceReportRepository.GetByTaskIdAsync(taskId);
        if (report is null)
        {
            throw new KeyNotFoundException(
                $"No governance report found for task '{taskId}'. " +
                "Only tester-role tasks produce governance reports.");
        }

        return Ok(FormatReport(report));
    }

    /// <summary>
    /// Formats a GovernanceReport entity into the CON-002 §4.6 response shape.
    /// Deserializes the CheckResultsJson into a structured array.
    /// </summary>
    /// <param name="report">The governance report entity.</param>
    /// <returns>Anonymous object matching CON-002 §4.6 response schema.</returns>
    private static object FormatReport(Domain.Entities.GovernanceReport report)
    {
        // Deserialize the stored JSON array of check results
        var checks = new List<object>();
        if (!string.IsNullOrWhiteSpace(report.CheckResultsJson))
        {
            try
            {
                var checkResults = JsonSerializer.Deserialize<List<Domain.Contracts.GovernanceCheckResult>>(
                    report.CheckResultsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (checkResults is not null)
                {
                    checks.AddRange(checkResults.Select(c => new
                    {
                        ruleId = c.RuleId,
                        ruleName = c.RuleName,
                        category = c.Category,
                        passed = c.Passed,
                        details = c.Details,
                        severity = c.Severity
                    }));
                }
            }
            catch (JsonException)
            {
                // If JSON is malformed, return empty checks array rather than 500.
                // The report metadata (passed/totalChecks) is still useful.
            }
        }

        return new
        {
            id = report.Id,
            taskId = report.TaskId,
            passed = report.Passed,
            totalChecks = report.TotalChecks,
            passedChecks = report.PassedChecks,
            failedChecks = report.FailedChecks,
            checks,
            createdAt = report.CreatedAt.ToString("o")
        };
    }
}
