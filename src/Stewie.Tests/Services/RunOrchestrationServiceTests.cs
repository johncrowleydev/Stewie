using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RunOrchestrationService"/>.
/// Covers the four primary execution paths: happy path, container failure,
/// missing result.json, and unhandled exception during execution.
/// </summary>
public class RunOrchestrationServiceTests
{
    private readonly IRunRepository _runRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IContainerService _containerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RunOrchestrationService _sut;

    public RunOrchestrationServiceTests()
    {
        _runRepository = Substitute.For<IRunRepository>();
        _workTaskRepository = Substitute.For<IWorkTaskRepository>();
        _artifactRepository = Substitute.For<IArtifactRepository>();
        _workspaceService = Substitute.For<IWorkspaceService>();
        _containerService = Substitute.For<IContainerService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _sut = new RunOrchestrationService(
            _runRepository,
            _workTaskRepository,
            _artifactRepository,
            _workspaceService,
            _containerService,
            _unitOfWork,
            NullLogger<RunOrchestrationService>.Instance);
    }

    [Fact]
    public async Task ExecuteTestRun_HappyPath_ReturnsCompletedWithArtifact()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Run>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        var resultPacket = new ResultPacket
        {
            TaskId = Guid.NewGuid(),
            Status = "success",
            Summary = "Dummy worker executed successfully.",
            FilesChanged = [],
            TestsPassed = true,
            Errors = [],
            Notes = "Test note",
            NextAction = "review"
        };

        _workspaceService.ReadResult(Arg.Any<WorkTask>())
            .Returns(resultPacket);

        // Act
        var result = await _sut.ExecuteTestRunAsync();

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Dummy worker executed successfully.", result.Summary);
        Assert.NotEqual(Guid.Empty, result.RunId);
        Assert.NotEqual(Guid.Empty, result.TaskId);
        Assert.NotNull(result.ArtifactId);
        Assert.NotEqual(Guid.Empty, result.ArtifactId.Value);

        // Verify artifact was persisted
        await _artifactRepository.Received(1).SaveAsync(Arg.Any<Artifact>());

        // Verify commit was called (once for Running status, once for final)
        await _unitOfWork.Received(2).CommitAsync();
    }

    [Fact]
    public async Task ExecuteTestRun_ContainerFails_ReturnsFailedWithExitCode()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Run>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(1);

        // Act
        var result = await _sut.ExecuteTestRunAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("code 1", result.Summary);
        Assert.Null(result.ArtifactId);

        // Verify ReadResult was never called (short-circuited on exit code)
        _workspaceService.DidNotReceive().ReadResult(Arg.Any<WorkTask>());

        // Verify no artifact was created
        await _artifactRepository.DidNotReceive().SaveAsync(Arg.Any<Artifact>());
    }

    [Fact]
    public async Task ExecuteTestRun_ResultJsonMissing_ReturnsFailedWithError()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Run>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        _workspaceService.ReadResult(Arg.Any<WorkTask>())
            .Throws(new FileNotFoundException("result.json not found at /tmp/workspaces/test-task/output/result.json"));

        // Act
        var result = await _sut.ExecuteTestRunAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("result.json not found", result.Summary);
        Assert.Null(result.ArtifactId);
    }

    [Fact]
    public async Task ExecuteTestRun_ExceptionDuringExecution_ReturnsFailedWithMessage()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Run>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Throws(new InvalidOperationException("Docker daemon not available"));

        // Act
        var result = await _sut.ExecuteTestRunAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("Docker daemon not available", result.Summary);
        Assert.Null(result.ArtifactId);
    }
}
