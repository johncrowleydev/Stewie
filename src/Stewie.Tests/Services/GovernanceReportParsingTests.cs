/// <summary>
/// Unit tests for GovernanceReportPacket deserialization.
/// Tests the JSON parsing of governance-report.json files produced by the governance worker.
///
/// Covers:
/// 1. Valid governance-report.json → parses correctly
/// 2. Missing fields → handles gracefully
/// 3. Empty checks array → valid (0 checks)
/// 4. Mixed severity results → verdict computed correctly
///
/// REF: CON-001 §6, JOB-008 T-080
/// </summary>
using System.Text.Json;
using Stewie.Domain.Contracts;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Tests GovernanceReportPacket deserialization from JSON payloads
/// matching the governance worker's output format.
/// </summary>
public class GovernanceReportParsingTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // -------------------------------------------------------------------
    // Valid governance-report.json → parses correctly
    // -------------------------------------------------------------------

    /// <summary>
    /// A valid governance-report.json with all fields populated parses to
    /// a GovernanceReportPacket with correct values.
    /// </summary>
    [Fact]
    public void Deserialize_ValidReport_ParsesAllFields()
    {
        const string json = """
        {
            "taskId": "11111111-1111-1111-1111-111111111111",
            "status": "pass",
            "summary": "16/16 checks passed",
            "totalChecks": 16,
            "passedChecks": 16,
            "failedChecks": 0,
            "checks": [
                {
                    "ruleId": "GOV-002-001",
                    "ruleName": "Build Succeeds",
                    "category": "GOV-002",
                    "passed": true,
                    "details": null,
                    "severity": "error"
                },
                {
                    "ruleId": "GOV-003-001",
                    "ruleName": "No any Types",
                    "category": "GOV-003",
                    "passed": true,
                    "details": null,
                    "severity": "error"
                }
            ]
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), report.TaskId);
        Assert.Equal("pass", report.Status);
        Assert.Equal("16/16 checks passed", report.Summary);
        Assert.Equal(16, report.TotalChecks);
        Assert.Equal(16, report.PassedChecks);
        Assert.Equal(0, report.FailedChecks);
        Assert.Equal(2, report.Checks.Count);
    }

    /// <summary>
    /// Check results parse with all fields including severity and details.
    /// </summary>
    [Fact]
    public void Deserialize_ValidCheck_AllFieldsPopulated()
    {
        const string json = """
        {
            "taskId": "11111111-1111-1111-1111-111111111111",
            "status": "fail",
            "summary": "14/16 checks passed",
            "totalChecks": 16,
            "passedChecks": 14,
            "failedChecks": 2,
            "checks": [
                {
                    "ruleId": "GOV-003-001",
                    "ruleName": "No any Types",
                    "category": "GOV-003",
                    "passed": false,
                    "details": "src/utils.ts:42 — found `: any`",
                    "severity": "error"
                },
                {
                    "ruleId": "GOV-006-001",
                    "ruleName": "Services use ILogger",
                    "category": "GOV-006",
                    "passed": false,
                    "details": "MyService.cs does not inject ILogger",
                    "severity": "warning"
                }
            ]
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal("fail", report.Status);
        Assert.Equal(2, report.Checks.Count);

        var errorCheck = report.Checks.First(c => c.RuleId == "GOV-003-001");
        Assert.False(errorCheck.Passed);
        Assert.Equal("error", errorCheck.Severity);
        Assert.Contains("found `: any`", errorCheck.Details);

        var warningCheck = report.Checks.First(c => c.RuleId == "GOV-006-001");
        Assert.False(warningCheck.Passed);
        Assert.Equal("warning", warningCheck.Severity);
        Assert.Contains("ILogger", warningCheck.Details);
    }

    // -------------------------------------------------------------------
    // Missing fields → handles gracefully
    // -------------------------------------------------------------------

    /// <summary>
    /// JSON with only required fields (taskId, status) still parses.
    /// Missing numeric fields default to 0, empty collections default to [].
    /// </summary>
    [Fact]
    public void Deserialize_MinimalFields_DefaultsProperly()
    {
        const string json = """
        {
            "taskId": "22222222-2222-2222-2222-222222222222",
            "status": "pass"
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), report.TaskId);
        Assert.Equal("pass", report.Status);
        Assert.Equal(string.Empty, report.Summary);
        Assert.Equal(0, report.TotalChecks);
        Assert.Equal(0, report.PassedChecks);
        Assert.Equal(0, report.FailedChecks);
        Assert.Empty(report.Checks);
    }

    /// <summary>
    /// GovernanceCheckResult with missing details field defaults to null.
    /// </summary>
    [Fact]
    public void Deserialize_CheckWithoutDetails_DetailsIsNull()
    {
        const string json = """
        {
            "ruleId": "GOV-002-001",
            "ruleName": "Build Succeeds",
            "category": "GOV-002",
            "passed": true,
            "severity": "error"
        }
        """;

        var check = JsonSerializer.Deserialize<GovernanceCheckResult>(json, _jsonOptions);

        Assert.NotNull(check);
        Assert.Equal("GOV-002-001", check.RuleId);
        Assert.True(check.Passed);
        Assert.Null(check.Details);
    }

    // -------------------------------------------------------------------
    // Empty checks array → valid
    // -------------------------------------------------------------------

    /// <summary>
    /// Report with an empty checks array is valid.
    /// This can happen if the governance worker couldn't detect the stack.
    /// </summary>
    [Fact]
    public void Deserialize_EmptyChecksArray_IsValid()
    {
        const string json = """
        {
            "taskId": "33333333-3333-3333-3333-333333333333",
            "status": "pass",
            "summary": "No checks applicable",
            "totalChecks": 0,
            "passedChecks": 0,
            "failedChecks": 0,
            "checks": []
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal(0, report.TotalChecks);
        Assert.Empty(report.Checks);
    }

    // -------------------------------------------------------------------
    // Mixed severity results → verdict reflects errors only
    // -------------------------------------------------------------------

    /// <summary>
    /// A report with warnings that failed but no error failures should still be "pass".
    /// Warnings don't block acceptance (per JOB-008 design decision §2).
    /// </summary>
    [Fact]
    public void Deserialize_WarningFailures_StillPass()
    {
        const string json = """
        {
            "taskId": "44444444-4444-4444-4444-444444444444",
            "status": "pass",
            "summary": "14/16 checks passed (2 warnings)",
            "totalChecks": 16,
            "passedChecks": 14,
            "failedChecks": 2,
            "checks": [
                {
                    "ruleId": "GOV-002-001",
                    "ruleName": "Build Succeeds",
                    "category": "GOV-002",
                    "passed": true,
                    "details": null,
                    "severity": "error"
                },
                {
                    "ruleId": "GOV-003-002",
                    "ruleName": "No console.log",
                    "category": "GOV-003",
                    "passed": false,
                    "details": "Found console.log at src/debug.ts:10",
                    "severity": "warning"
                },
                {
                    "ruleId": "GOV-006-001",
                    "ruleName": "Services use ILogger",
                    "category": "GOV-006",
                    "passed": false,
                    "details": "TestHelper.cs missing ILogger injection",
                    "severity": "warning"
                }
            ]
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal("pass", report.Status); // Warnings don't make it "fail"
        Assert.Equal(2, report.FailedChecks);

        // Verify only errors count for verdict
        var errorFailures = report.Checks
            .Where(c => !c.Passed && c.Severity == "error")
            .ToList();
        Assert.Empty(errorFailures); // No error-severity failures

        var warningFailures = report.Checks
            .Where(c => !c.Passed && c.Severity == "warning")
            .ToList();
        Assert.Equal(2, warningFailures.Count);
    }

    /// <summary>
    /// A report with at least one error-severity failure should be "fail".
    /// </summary>
    [Fact]
    public void Deserialize_ErrorFailure_ResultInFail()
    {
        const string json = """
        {
            "taskId": "55555555-5555-5555-5555-555555555555",
            "status": "fail",
            "summary": "14/16 checks passed",
            "totalChecks": 16,
            "passedChecks": 14,
            "failedChecks": 2,
            "checks": [
                {
                    "ruleId": "SEC-001-001",
                    "ruleName": "No Secrets in Diff",
                    "category": "SEC-001",
                    "passed": false,
                    "details": "Found GitHub PAT: ghp_xxxx",
                    "severity": "error"
                },
                {
                    "ruleId": "GOV-003-002",
                    "ruleName": "No console.log",
                    "category": "GOV-003",
                    "passed": false,
                    "details": "Found console.log at src/debug.ts:10",
                    "severity": "warning"
                }
            ]
        }
        """;

        var report = JsonSerializer.Deserialize<GovernanceReportPacket>(json, _jsonOptions);

        Assert.NotNull(report);
        Assert.Equal("fail", report.Status);
        Assert.Equal(2, report.FailedChecks);

        // At least one error failure → should be fail
        var errorFailures = report.Checks
            .Where(c => !c.Passed && c.Severity == "error")
            .ToList();
        Assert.Single(errorFailures);
        Assert.Equal("SEC-001-001", errorFailures[0].RuleId);
    }

    // -------------------------------------------------------------------
    // GovernanceCheckResult round-trip serialization
    // -------------------------------------------------------------------

    /// <summary>
    /// GovernanceCheckResult serializes and deserializes correctly for
    /// storage in GovernanceReport.CheckResultsJson.
    /// </summary>
    [Fact]
    public void RoundTrip_CheckResultsList_PreservesAllFields()
    {
        var checks = new List<GovernanceCheckResult>
        {
            new()
            {
                RuleId = "GOV-002-001",
                RuleName = "Build Succeeds",
                Category = "GOV-002",
                Passed = true,
                Details = null,
                Severity = "error",
            },
            new()
            {
                RuleId = "SEC-001-001",
                RuleName = "No Secrets in Diff",
                Category = "SEC-001",
                Passed = false,
                Details = "Found API key in config.ts",
                Severity = "error",
            },
        };

        var json = JsonSerializer.Serialize(checks);
        var deserialized = JsonSerializer.Deserialize<List<GovernanceCheckResult>>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("GOV-002-001", deserialized[0].RuleId);
        Assert.True(deserialized[0].Passed);
        Assert.Null(deserialized[0].Details);
        Assert.Equal("SEC-001-001", deserialized[1].RuleId);
        Assert.False(deserialized[1].Passed);
        Assert.Equal("Found API key in config.ts", deserialized[1].Details);
    }
}
