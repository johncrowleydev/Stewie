/// <summary>
/// Deserialization target for governance-report.json produced by the governance worker.
/// Mirrors CON-001 §6 schema exactly.
/// REF: JOB-007 T-069
/// </summary>
using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

/// <summary>
/// The governance-report.json schema written by the governance worker container.
/// </summary>
public class GovernanceReportPacket
{
    /// <summary>Must match the tester task's taskId.</summary>
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    /// <summary>Overall verdict: "pass" or "fail".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Human-readable summary (e.g., "14/16 checks passed").</summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Total number of checks executed.</summary>
    [JsonPropertyName("totalChecks")]
    public int TotalChecks { get; set; }

    /// <summary>Number of checks that passed.</summary>
    [JsonPropertyName("passedChecks")]
    public int PassedChecks { get; set; }

    /// <summary>Number of checks that failed.</summary>
    [JsonPropertyName("failedChecks")]
    public int FailedChecks { get; set; }

    /// <summary>Array of individual check results.</summary>
    [JsonPropertyName("checks")]
    public List<GovernanceCheckResult> Checks { get; set; } = [];
}
