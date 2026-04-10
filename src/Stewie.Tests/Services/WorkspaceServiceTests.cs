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
        var job = CreateTestJob();
        var task = CreateTestTask(job);

        // Act
        var workspacePath = _sut.PrepareWorkspace(task, job);

        // Assert
        Assert.True(Directory.Exists(workspacePath), "Workspace root should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "repo")), "repo/ should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "input")), "input/ should exist");
        Assert.True(Directory.Exists(Path.Combine(workspacePath, "output")), "output/ should exist");
    }



    /// <summary>Creates a test Job entity with a new GUID and Pending status.</summary>
    private static Job CreateTestJob()
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Creates a test WorkTask entity linked to the given Job.</summary>
    private static WorkTask CreateTestTask(Job job)
    {
        return new WorkTask
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            Role = "developer",
            Status = WorkTaskStatus.Pending,
            WorkspacePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }
}
