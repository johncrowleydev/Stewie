/// <summary>
/// Governance Analytics API controller — violation trending, pass rates, GOV update suggestions.
/// REF: JOB-011 T-105, CON-002 v1.8.0
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Services;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes governance analytics endpoints for the dashboard.
/// </summary>
[ApiController]
[Authorize]
public class GovernanceAnalyticsController : ControllerBase
{
    private readonly GovernanceAnalyticsService _analyticsService;
    private readonly ILogger<GovernanceAnalyticsController> _logger;

    /// <summary>Initializes the controller.</summary>
    public GovernanceAnalyticsController(
        GovernanceAnalyticsService analyticsService,
        ILogger<GovernanceAnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Returns governance analytics — violation trending, pass rate, top failing rules,
    /// and GOV document update suggestions.
    /// </summary>
    /// <param name="days">Time window in days (default 30, max 365).</param>
    /// <param name="projectId">Optional project filter.</param>
    /// <returns>200 OK with analytics payload.</returns>
    [HttpGet("api/governance/analytics")]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] int? days = 30,
        [FromQuery] Guid? projectId = null)
    {
        var effectiveDays = Math.Clamp(days ?? 30, 1, 365);

        _logger.LogInformation(
            "Governance analytics requested: days={Days}, projectId={ProjectId}",
            effectiveDays, projectId);

        var result = await _analyticsService.GetAnalyticsAsync(effectiveDays, projectId);

        return Ok(new
        {
            totalJobs = result.TotalJobs,
            totalGovernanceRuns = result.TotalGovernanceRuns,
            passRate = result.PassRate,
            topFailingRules = result.TopFailingRules.Select(r => new
            {
                ruleId = r.RuleId,
                ruleName = r.RuleName,
                failCount = r.FailCount,
                trend = r.Trend
            }),
            suggestedGovUpdates = result.SuggestedGovUpdates.Select(s => new
            {
                govDoc = s.GovDoc,
                reason = s.Reason
            })
        });
    }
}
