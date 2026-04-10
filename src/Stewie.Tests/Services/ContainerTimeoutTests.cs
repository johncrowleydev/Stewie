/// <summary>
/// Unit tests for container timeout enforcement (SPR-005 T-051).
/// Tests verify that:
///   - Containers exceeding the timeout return exit code 124
///   - Containers completing within timeout return actual exit codes
///   - Timeout is configurable
///
/// NOTE: These tests validate the EXPECTED behavior per SPR-005 T-051.
/// Agent A will implement the timeout logic in DockerContainerService.
/// Until then, these tests mock IContainerService to validate the
/// JobOrchestrationService's handling of exit code 124 (timeout convention).
///
/// REF: CON-001 §7, GOV-002, SPR-005 T-051/T-055
/// </summary>
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stewie.Application.Interfaces;
using Stewie.Application.Services;
using System.Text.Json;
using Stewie.Domain.Contracts;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Stewie.Tests.Mocks;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Validates container timeout behavior by simulating exit code 124 (Unix timeout convention).
/// The JobOrchestrationService should treat exit code 124 as a timeout failure.
/// </summary>
public class ContainerTimeoutTests
{
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserCredentialRepository _credentialRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IArtifactWorkspaceStore _artifactStore;
    private readonly IContainerService _containerService;
    private readonly IGitPlatformService _gitHubService;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobOrchestrationService _sut;

    public ContainerTimeoutTests()
    {
        _jobRepository = Substitute.For<IJobRepository>();
        _workTaskRepository = Substitute.For<IWorkTaskRepository>();
        _artifactRepository = Substitute.For<IArtifactRepository>();
        _eventRepository = Substitute.For<IEventRepository>();
        _workspaceRepository = Substitute.For<IWorkspaceRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _credentialRepository = Substitute.For<IUserCredentialRepository>();
        _workspaceService = Substitute.For<IWorkspaceService>();
        _artifactStore = Substitute.For<IArtifactWorkspaceStore>();
        _containerService = Substitute.For<IContainerService>();
        _gitHubService = Substitute.For<IGitPlatformService>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        var governanceReportRepository = Substitute.For<IGovernanceReportRepository>();
        var taskDependencyRepository = Substitute.For<ITaskDependencyRepository>();

        _sut = new JobOrchestrationService(
            _jobRepository,
            _workTaskRepository,
            _artifactRepository,
            _eventRepository,
            _workspaceRepository,
            _projectRepository,
            _credentialRepository,
            _workspaceService,
            _artifactStore,
            _containerService,
            _gitHubService,
            _encryptionService,
            _unitOfWork,
            NullLogger<JobOrchestrationService>.Instance,
            governanceReportRepository,
            taskDependencyRepository,
            new NullRealTimeNotifier(),
            new ContainerOutputBuffer(),
            "stewie-script-worker");
    }

    /// <summary>
    /// When a container exits with code 124 (timeout), the orchestration service
    /// should mark the run as Failed and include "code 124" in the summary.
    /// </summary>
    [Fact]
    public async Task ExecuteTestRun_TimeoutExitCode124_ReturnsFailedWithTimeoutInfo()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/timeout-task");

        // Simulate container timeout with exit code 124
        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(124);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("124", result.Summary);

        // No result reading should happen on non-zero exit
        await _artifactStore.DidNotReceive().ReadTextArtifactAsync(Arg.Any<string>(), Arg.Any<string>());

        // No artifact should be created
        await _artifactRepository.DidNotReceive().SaveAsync(Arg.Any<Artifact>());
    }

    /// <summary>
    /// When a container exits with code 0 (success, within timeout), the orchestration
    /// service should process the result normally — not treat it as a timeout.
    /// </summary>
    [Fact]
    public async Task ExecuteTestRun_NormalExitCode0_ReturnsCompletedNormally()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/normal-task");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        var resultPacket = new ResultPacket
        {
            TaskId = Guid.NewGuid(),
            Status = "success",
            Summary = "Completed within timeout.",
            FilesChanged = [],
            TestsPassed = true,
            Errors = [],
            Notes = "All good",
            NextAction = "review"
        };

        _artifactStore.ReadTextArtifactAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(JsonSerializer.Serialize(resultPacket)));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Completed within timeout.", result.Summary);
        Assert.NotNull(result.ArtifactId);

        // Should have read the result
        await _artifactStore.Received(1).ReadTextArtifactAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// When a container exits with a non-zero, non-124 code (e.g., 1),
    /// the orchestration should report failure with the actual exit code.
    /// This distinguishes a normal crash from a timeout.
    /// </summary>
    [Fact]
    public async Task ExecuteTestRun_NonTimeoutFailure_ReportsActualExitCode()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/crash-task");

        // Non-timeout failure — exit code 1
        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(1);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("code 1", result.Summary);
        Assert.DoesNotContain("124", result.Summary);
    }

    /// <summary>
    /// When the Docker daemon throws an exception (container error),
    /// the service should mark the run as failed with the error message.
    /// </summary>
    [Fact]
    public async Task ExecuteTestRun_ContainerDaemonError_ReturnsFailedWithMessage()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/daemon-error");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Throws(new InvalidOperationException("Docker socket connection refused"));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Contains("Docker socket connection refused", result.Summary);
    }
}
