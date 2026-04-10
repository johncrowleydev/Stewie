/// <summary>
/// Unit tests for ProjectConfigService — stewie.json parser.
/// Tests parsing, defaults, missing files, and error handling.
///
/// REF: JOB-011 T-111, CON-003
/// </summary>
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Application.Services;
using Stewie.Domain.Contracts;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Verifies ProjectConfigService handles all stewie.json scenarios:
/// full config, minimal config, missing file, invalid JSON, and unknown stacks.
/// </summary>
public class ProjectConfigServiceTests : IDisposable
{
    private readonly ProjectConfigService _service;
    private readonly string _tempDir;

    public ProjectConfigServiceTests()
    {
        _service = new ProjectConfigService(NullLogger<ProjectConfigService>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"stewie-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Writes a stewie.json file to the temp directory.
    /// </summary>
    private void WriteConfig(string json)
    {
        File.WriteAllText(Path.Combine(_tempDir, "stewie.json"), json);
    }

    // -------------------------------------------------------------------
    // Valid config — all fields populated
    // -------------------------------------------------------------------

    /// <summary>
    /// A full stewie.json with all fields populated parses correctly.
    /// </summary>
    [Fact]
    public void ValidConfig_ParsesAllFields()
    {
        var json = """
        {
          "version": "1.0",
          "stack": "dotnet",
          "language": "csharp",
          "buildCommand": "dotnet build",
          "testCommand": "dotnet test",
          "governance": {
            "rules": "all",
            "warningsBlockAcceptance": true,
            "maxRetries": 3
          },
          "paths": {
            "source": ["src/"],
            "tests": ["tests/"],
            "forbidden": ["bin/", "obj/"]
          }
        }
        """;
        WriteConfig(json);

        var config = _service.LoadFromRepo(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("1.0", config.Version);
        Assert.Equal("dotnet", config.Stack);
        Assert.Equal("csharp", config.Language);
        Assert.Equal("dotnet build", config.BuildCommand);
        Assert.Equal("dotnet test", config.TestCommand);

        // Governance config
        Assert.NotNull(config.Governance);
        Assert.Equal("all", config.Governance.Rules);
        Assert.True(config.Governance.WarningsBlockAcceptance);
        Assert.Equal(3, config.Governance.MaxRetries);

        // Path config
        Assert.NotNull(config.Paths);
        Assert.Single(config.Paths.Source);
        Assert.Contains("src/", config.Paths.Source);
        Assert.Single(config.Paths.Tests);
        Assert.Contains("tests/", config.Paths.Tests);
        Assert.Equal(2, config.Paths.Forbidden.Count);
    }

    // -------------------------------------------------------------------
    // Minimal config — only stack, defaults fill in
    // -------------------------------------------------------------------

    /// <summary>
    /// A minimal stewie.json with only "stack" parses correctly,
    /// with all other fields taking their default values.
    /// </summary>
    [Fact]
    public void MinimalConfig_DefaultsFilled()
    {
        WriteConfig("""{ "stack": "node" }""");

        var config = _service.LoadFromRepo(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("node", config.Stack);
        Assert.Equal("1.0", config.Version); // default
        Assert.Equal(string.Empty, config.Language); // default
        Assert.Null(config.BuildCommand); // default null
        Assert.Null(config.TestCommand); // default null
        Assert.Null(config.Governance); // not provided
        Assert.Null(config.Paths); // not provided
    }

    // -------------------------------------------------------------------
    // Missing file — returns null, no exception
    // -------------------------------------------------------------------

    /// <summary>
    /// When no stewie.json exists in the repo, LoadFromRepo returns null
    /// without throwing any exception.
    /// </summary>
    [Fact]
    public void MissingFile_ReturnsNull()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), $"stewie-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);

        try
        {
            var config = _service.LoadFromRepo(emptyDir);
            Assert.Null(config);
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    // -------------------------------------------------------------------
    // Invalid JSON — throws descriptive error
    // -------------------------------------------------------------------

    /// <summary>
    /// Malformed JSON in stewie.json throws a JsonException with
    /// descriptive error information.
    /// </summary>
    [Fact]
    public void InvalidJson_ThrowsDescriptive()
    {
        WriteConfig("{ this is not valid json }}}");

        var ex = Assert.Throws<JsonException>(() => _service.LoadFromRepo(_tempDir));
        Assert.Contains("stewie.json", ex.Message);
    }

    // -------------------------------------------------------------------
    // Unknown stack value — accepted as-is
    // -------------------------------------------------------------------

    /// <summary>
    /// A custom/unknown stack value is parsed as-is without validation.
    /// The service does not restrict stack to a known set.
    /// </summary>
    [Fact]
    public void UnknownStack_AcceptsAnyString()
    {
        WriteConfig("""{ "stack": "rust-embedded", "language": "rust" }""");

        var config = _service.LoadFromRepo(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("rust-embedded", config.Stack);
        Assert.Equal("rust", config.Language);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
