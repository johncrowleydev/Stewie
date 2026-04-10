/// <summary>
/// ProjectConfigService — reads and parses stewie.json from a cloned repository.
/// REF: JOB-011 T-107, CON-003
/// </summary>
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stewie.Domain.Contracts;

namespace Stewie.Application.Services;

/// <summary>
/// Reads stewie.json from the repository root, returning a typed
/// <see cref="StewieProjectConfig"/> or null if the file doesn't exist.
/// Invalid JSON throws a descriptive <see cref="JsonException"/>.
/// </summary>
public class ProjectConfigService
{
    private const string ConfigFileName = "stewie.json";
    private readonly ILogger<ProjectConfigService> _logger;

    /// <summary>Initializes the config service.</summary>
    public ProjectConfigService(ILogger<ProjectConfigService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads and parses stewie.json from the given repository path.
    /// Returns null if the file doesn't exist (fallback to heuristic detection).
    /// </summary>
    /// <param name="repoPath">Absolute path to the cloned repository directory.</param>
    /// <returns>Parsed config or null if no stewie.json exists.</returns>
    /// <exception cref="JsonException">Thrown when the file exists but contains invalid JSON.</exception>
    public StewieProjectConfig? LoadFromRepo(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            _logger.LogDebug("No repo path provided — skipping stewie.json");
            return null;
        }

        var configPath = Path.Combine(repoPath, ConfigFileName);

        if (!File.Exists(configPath))
        {
            _logger.LogDebug("No stewie.json found at {ConfigPath} — using heuristic detection", configPath);
            return null;
        }

        _logger.LogInformation("Found stewie.json at {ConfigPath}", configPath);

        string json;
        try
        {
            json = File.ReadAllText(configPath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read stewie.json at {ConfigPath}", configPath);
            throw new JsonException($"Cannot read stewie.json at {configPath}: {ex.Message}", ex);
        }

        StewieProjectConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<StewieProjectConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Invalid JSON in stewie.json at {configPath}: {ex.Message}", ex);
        }

        if (config is null)
        {
            throw new JsonException($"stewie.json at {configPath} deserialized to null");
        }

        _logger.LogInformation(
            "Parsed stewie.json: stack={Stack}, language={Language}, buildCommand={Build}, testCommand={Test}",
            config.Stack, config.Language, config.BuildCommand ?? "(default)", config.TestCommand ?? "(default)");

        return config;
    }
}
