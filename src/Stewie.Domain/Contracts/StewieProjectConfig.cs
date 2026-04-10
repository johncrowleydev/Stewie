/// <summary>
/// Project configuration models — represents the stewie.json file format.
/// REF: JOB-011 T-107, CON-003
/// </summary>
using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

/// <summary>
/// Root configuration object parsed from stewie.json in the repository root.
/// </summary>
public class StewieProjectConfig
{
    /// <summary>Config file schema version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>Technology stack identifier: "dotnet", "node", "python", etc.</summary>
    [JsonPropertyName("stack")]
    public string Stack { get; set; } = string.Empty;

    /// <summary>Primary language: "csharp", "typescript", "python", etc.</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>Shell command to build the project. Optional.</summary>
    [JsonPropertyName("buildCommand")]
    public string? BuildCommand { get; set; }

    /// <summary>Shell command to run tests. Optional.</summary>
    [JsonPropertyName("testCommand")]
    public string? TestCommand { get; set; }

    /// <summary>Governance-specific configuration. Optional.</summary>
    [JsonPropertyName("governance")]
    public GovernanceConfig? Governance { get; set; }

    /// <summary>Path restrictions for workers. Optional.</summary>
    [JsonPropertyName("paths")]
    public PathConfig? Paths { get; set; }
}

/// <summary>
/// Governance configuration within stewie.json.
/// Controls how the governance worker evaluates task output.
/// </summary>
public class GovernanceConfig
{
    /// <summary>Which rules to enforce: "all" or a JSON array of rule IDs.</summary>
    [JsonPropertyName("rules")]
    public string Rules { get; set; } = "all";

    /// <summary>If true, warning-severity failures also block acceptance.</summary>
    [JsonPropertyName("warningsBlockAcceptance")]
    public bool WarningsBlockAcceptance { get; set; }

    /// <summary>Max governance retry attempts before permanent failure.</summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 2;
}

/// <summary>
/// Path configuration within stewie.json.
/// Defines source, test, and forbidden paths for worker sandboxing.
/// </summary>
public class PathConfig
{
    /// <summary>Source directories the worker should focus on.</summary>
    [JsonPropertyName("source")]
    public List<string> Source { get; set; } = [];

    /// <summary>Test directories for validation.</summary>
    [JsonPropertyName("tests")]
    public List<string> Tests { get; set; } = [];

    /// <summary>Directories the worker must not modify.</summary>
    [JsonPropertyName("forbidden")]
    public List<string> Forbidden { get; set; } = [];
}
