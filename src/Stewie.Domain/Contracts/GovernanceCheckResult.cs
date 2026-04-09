/// <summary>
/// DTO representing a single governance check result.
/// Serialized as a JSON array in GovernanceReport.CheckResultsJson.
/// REF: JOB-007 T-067, CON-001 §6.1
/// </summary>
using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

/// <summary>
/// Individual governance check outcome — one entry per rule executed.
/// </summary>
public class GovernanceCheckResult
{
    /// <summary>Rule identifier (e.g., "GOV-002-001").</summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Human-readable rule name (e.g., "Build Succeeds").</summary>
    [JsonPropertyName("ruleName")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>GOV document category (e.g., "GOV-002").</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Whether this check passed.</summary>
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    /// <summary>Error details on failure. Null if passed.</summary>
    [JsonPropertyName("details")]
    public string? Details { get; set; }

    /// <summary>Severity: "error" blocks acceptance, "warning" is informational.</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "error";
}
