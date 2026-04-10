using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Tests.Mocks;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JobOrchestrationService"/>.
/// Covers the four primary execution paths: happy path, container failure,
/// missing result.json, and unhandled exception during execution.
///
/// NOTE: References JobOrchestrationService, IJobRepository, Job, JobStatus —
/// these classes will exist after Agent A's T-058/T-059 merge.
/// Until rebase, this file will not compile.
/// </summary>
public class JobOrchestrationServiceTests
{
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IContainerService _containerService;
    private readonly IGitPlatformService _gitPlatformService;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGovernanceReportRepository _governanceReportRepository;
    private readonly ITaskDependencyRepository _taskDependencyRepository;
    private readonly JobOrchestrationService _sut;

    public JobOrchestrationServiceTests()
    {
        _jobRepository = Substitute.For<IJobRepository>();
        _workTaskRepository = Substitute.For<IWorkTaskRepository>();
        _artifactRepository = Substitute.For<IArtifactRepository>();
        _eventRepository = Substitute.For<IEventRepository>();
        _workspaceRepository = Substitute.For<IWorkspaceRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _credentialRepository = Substitute.For<IUserCredentialRepository>();
        _workspaceService = Substitute.For<IWorkspaceService>();
        _containerService = Substitute.For<IContainerService>();
        _gitPlatformService = Substitute.For<IGitPlatformService>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _governanceReportRepository = Substitute.For<IGovernanceReportRepository>();
        _taskDependencyRepository = Substitute.For<ITaskDependencyRepository>();

        _sut = new JobOrchestrationService(
            _jobRepository,
            _workTaskRepository,
            _artifactRepository,
            _eventRepository,
            _workspaceRepository,
            _projectRepository,
            _credentialRepository,
            _workspaceService,
            _containerService,
            _gitPlatformService,
            _encryptionService,
            _unitOfWork,
            NullLogger<JobOrchestrationService>.Instance,
            _governanceReportRepository,
            _taskDependencyRepository,
            new NullRealTimeNotifier(),
            "stewie-script-worker");
    }

    [Fact]
    public async Task ExecuteTestJob_HappyPath_ReturnsCompletedWithArtifact()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
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
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Dummy worker executed successfully.", result.Summary);
        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.NotEqual(Guid.Empty, result.TaskId);
        Assert.NotNull(result.ArtifactId);
        Assert.NotEqual(Guid.Empty, result.ArtifactId.Value);

        // Verify artifact was persisted
        await _artifactRepository.Received(1).SaveAsync(Arg.Any<Artifact>());

        // Verify commit was called (once for Running status, once for final)
        await _unitOfWork.Received(2).CommitAsync();
    }

    [Fact]
    public async Task ExecuteTestJob_ContainerFails_ReturnsFailedWithExitCode()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(1);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

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
    public async Task ExecuteTestJob_ResultJsonMissing_ReturnsFailedWithError()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        _workspaceService.ReadResult(Arg.Any<WorkTask>())
            .Throws(new FileNotFoundException("result.json not found at /tmp/workspaces/test-task/output/result.json"));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("result.json not found", result.Summary);
        Assert.Null(result.ArtifactId);
    }

    [Fact]
    public async Task ExecuteTestJob_ExceptionDuringExecution_ReturnsFailedWithMessage()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/test-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Throws(new InvalidOperationException("Docker daemon not available"));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("Docker daemon not available", result.Summary);
        Assert.Null(result.ArtifactId);
    }
}
