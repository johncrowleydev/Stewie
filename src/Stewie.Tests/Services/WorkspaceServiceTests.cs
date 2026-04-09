using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Infrastructure.Services;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Unit tests for <see cref="WorkspaceService"/>.
/// Uses real temporary directories to verify filesystem operations.
/// Each test creates and cleans up its own temp directory.
/// </summary>
public class WorkspaceServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"stewie-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _sut = new WorkspaceService(
            _tempRoot,
            NullLogger<WorkspaceService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PrepareWorkspace_CreatesDirectoryStructure()
    {
        // Arrange
        var run = CreateTestRun();
        var task = CreateTestTask(run);

        // Act
        var workspacePath = _sut.PrepareWorkspace(task, run);

        // Assert
        Assert.True(Directory.Exists(workspacePath), "Workspace root should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "repo")), "repo/ should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "input")), "input/ should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "output")), "output/ should exist");
    }

    [Fact]
    public void PrepareWorkspace_WritesValidTaskJson()
    {
        // Arrange
        var run = CreateTestRun();
        var task = CreateTestTask(run);

        // Act
        var workspacePath = _sut.PrepareWorkspace(task, run);

        // Assert
        var taskJsonPath = Path.Combine(workspacePath, "input", "task.json");
        Assert.True(File.Exists(taskJsonPath), "task.json should exist in input/");

        var json = File.ReadAllText(taskJsonPath);
        var packet = JsonSerializer.Deserialize<TaskPacket>(json);

        Assert.NotNull(packet);
        Assert.Equal(task.Id, packet.TaskId);
        Assert.Equal(run.Id, packet.RunId);
        Assert.Equal("developer", packet.Role);
        Assert.NotEmpty(packet.Objective);
        Assert.NotEmpty(packet.AcceptanceCriteria);
    }

    [Fact]
    public void ReadResult_DeserializesCorrectly()
    {
        // Arrange
        var run = CreateTestRun();
        var task = CreateTestTask(run);
        var workspacePath = _sut.PrepareWorkspace(task, run);
        task.WorkspacePath = workspacePath;

        var expected = new ResultPacket
        {
            TaskId = task.Id,
            Status = "success",
            Summary = "All checks passed.",
            FilesChanged = ["src/main.cs"],
            TestsPassed = true,
            Errors = [],
            Notes = "Clean execution",
            NextAction = "review"
        };

        var outputDir = Path.Combine(workspacePath, "output");
        var resultJson = JsonSerializer.Serialize(expected, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputDir, "result.json"), resultJson);

        // Act
        var actual = _sut.ReadResult(task);

        // Assert
        Assert.Equal(expected.TaskId, actual.TaskId);
        Assert.Equal("success", actual.Status);
        Assert.Equal("All checks passed.", actual.Summary);
        Assert.Single(actual.FilesChanged);
        Assert.Equal("src/main.cs", actual.FilesChanged[0]);
        Assert.True(actual.TestsPassed);
        Assert.Empty(actual.Errors);
        Assert.Equal("Clean execution", actual.Notes);
        Assert.Equal("review", actual.NextAction);
    }

    [Fact]
    public void ReadResult_ThrowsWhenFileMissing()
    {
        // Arrange
        var run = CreateTestRun();
        var task = CreateTestTask(run);
        var workspacePath = _sut.PrepareWorkspace(task, run);
        task.WorkspacePath = workspacePath;

        // Do NOT write result.json — it should be missing

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() => _sut.ReadResult(task));
        Assert.Contains("result.json not found", ex.Message);
    }

    /// <summary>Creates a test Run entity with a new GUID and Pending status.</summary>
    private static Run CreateTestRun()
    {
        return new Run
        {
            Id = Guid.NewGuid(),
            Status = RunStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Creates a test WorkTask entity linked to the given Run.</summary>
    private static WorkTask CreateTestTask(Run run)
    {
        return new WorkTask
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Run = run,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }
}
