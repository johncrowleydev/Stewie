/// <summary>
/// TaskPacket — the JSON schema for task.json input to worker containers.
/// REF: CON-001 §4.1, §4.3
/// </summary>
using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

/// <summary>
/// Represents the task.json input file written by the orchestrator
/// and read by worker containers. Conforms to CON-001 v1.2.0 §4.
/// </summary>
public class TaskPacket
{
    /// <summary>Unique identifier for this task.</summary>
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    /// <summary>Parent Job identifier.</summary>
    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    /// <summary>Agent role executing this task.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>What the worker should accomplish.</summary>
    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    /// <summary>Boundaries of the work.</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>File paths the worker may read/modify.</summary>
    [JsonPropertyName("allowedPaths")]
    public List<string> AllowedPaths { get; set; } = [];

    /// <summary>File paths the worker must NOT touch.</summary>
    [JsonPropertyName("forbiddenPaths")]
    public List<string> ForbiddenPaths { get; set; } = [];

    /// <summary>Conditions that must be met for success.</summary>
    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];

    /// <summary>Git repository URL to clone into workspace. Optional.</summary>
    [JsonPropertyName("repoUrl")]
    public string? RepoUrl { get; set; }

    /// <summary>Branch name to create after clone. Optional.</summary>
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    /// <summary>Bash commands to execute sequentially in /workspace/repo/. Optional.</summary>
    [JsonPropertyName("script")]
    public List<string>? Script { get; set; }

    /// <summary>FK to parent task — tester task points to its dev task. Null for root tasks.</summary>
    [JsonPropertyName("parentTaskId")]
    public Guid? ParentTaskId { get; set; }

    /// <summary>Governance violations from prior tester, injected for retry feedback. Null if first attempt.</summary>
    [JsonPropertyName("governanceViolations")]
    public List<GovernanceViolation>? GovernanceViolations { get; set; }

    /// <summary>Which retry iteration this task belongs to. Starts at 1.</summary>
    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; } = 1;
}
