/// <summary>
/// GovernanceViolation DTO — passed to retry dev tasks via TaskPacket
/// so the worker knows what governance checks failed and needs fixing.
/// REF: JOB-007 T-068, CON-001 §4
/// </summary>
using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

/// <summary>
/// A single governance violation passed as feedback to a retry dev task.
/// </summary>
public class GovernanceViolation
{
    /// <summary>Rule identifier that failed (e.g., "GOV-003-001").</summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Human-readable rule name (e.g., "No TypeScript any Types").</summary>
    [JsonPropertyName("ruleName")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>Detailed error output (e.g., file:line reference).</summary>
    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}
