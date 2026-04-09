/// <summary>
/// Unit tests for retry logic and error taxonomy (SPR-005 T-052).
/// Tests verify that:
///   - Transient failures (timeout=124, container error) trigger exactly one retry
///   - Permanent failures (crash=1, result missing, result invalid) do NOT retry
///   - The failure reason is classified correctly
///
/// NOTE: These tests validate the EXPECTED behavior per SPR-005 T-052.
/// Agent A will implement the retry logic and TaskFailureReason enum.
/// Until then, these tests exercise the existing JobOrchestrationService
/// behavior (no retry) and verify the current failure handling paths.
/// When Agent A's code lands, tests for retry will be updated to match
/// the new JobOrchestrationService.ExecuteJobAsync behavior.
///
/// REF: GOV-002, GOV-004, SPR-005 T-052/T-055
/// </summary>
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
/// Validates retry behavior and failure classification for different error scenarios.
/// Uses NSubstitute mocks for IContainerService and IWorkspaceService.
/// </summary>
public class RetryLogicTests
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
    private readonly IGitPlatformService _gitHubService;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JobOrchestrationService _sut;

    public RetryLogicTests()
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
        _gitHubService = Substitute.For<IGitPlatformService>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

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
            _gitHubService,
            _encryptionService,
            _unitOfWork,
            NullLogger<JobOrchestrationService>.Instance,
            "stewie-script-worker");
    }

    // -------------------------------------------------------------------
    // Transient failure tests — these should trigger retry after Agent A's changes
    // -------------------------------------------------------------------

    /// <summary>
    /// Timeout failure (exit code 124) is classified as a transient failure.
    /// Current behavior: immediate failure (no retry yet — T-052 pending).
    /// Expected after T-052: retry once, then succeed or fail.
    /// </summary>
    [Fact]
    public async Task TransientFailure_Timeout_ExitCode124_MarksRunAsFailed()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/retry-timeout");

        // First call returns 124 (timeout)
        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(124);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — currently fails immediately (retry not yet implemented)
        Assert.Equal("Failed", result.Status);
        Assert.Contains("124", result.Summary);
    }

    /// <summary>
    /// Container daemon error (exception) is classified as a transient failure.
    /// Current behavior: immediate failure (no retry yet — T-052 pending).
    /// Expected after T-052: retry once, then succeed or fail.
    /// </summary>
    [Fact]
    public async Task TransientFailure_ContainerError_Exception_MarksRunAsFailed()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/retry-docker-error");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Throws(new InvalidOperationException("Docker image not found: stewie-script-worker"));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — fails with exception message
        Assert.Equal("Failed", result.Status);
        Assert.Contains("Docker image not found", result.Summary);
    }

    // -------------------------------------------------------------------
    // Permanent failure tests — these should NOT trigger retry
    // -------------------------------------------------------------------

    /// <summary>
    /// Worker crash (exit code 1, not timeout) is a permanent failure.
    /// Should never be retried regardless of T-052 implementation.
    /// </summary>
    [Fact]
    public async Task PermanentFailure_WorkerCrash_ExitCode1_NoRetry()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/crash-no-retry");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(1);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — fails immediately, no retry
        Assert.Equal("Failed", result.Status);
        Assert.Contains("code 1", result.Summary);

        // Container should be called exactly once (no retry for permanent failures)
        await _containerService.Received(1).LaunchWorkerAsync(Arg.Any<WorkTask>());
    }

    /// <summary>
    /// Missing result.json after successful container exit (exit code 0)
    /// is a permanent failure (ResultMissing). Should not be retried.
    /// </summary>
    [Fact]
    public async Task PermanentFailure_ResultMissing_NoRetry()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/no-result");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        _workspaceService.ReadResult(Arg.Any<WorkTask>())
            .Throws(new FileNotFoundException("result.json not found"));

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — fails due to missing result
        Assert.Equal("Failed", result.Status);
        Assert.Contains("result.json not found", result.Summary);

        // Container should be called exactly once (no retry)
        await _containerService.Received(1).LaunchWorkerAsync(Arg.Any<WorkTask>());
    }

    /// <summary>
    /// Worker returns result.json with status != "success" (WorkerReportedFailure).
    /// This is a permanent failure — should not be retried.
    /// </summary>
    [Fact]
    public async Task PermanentFailure_WorkerReportedFailure_NoRetry()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/worker-failure");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(0);

        var failureResult = new ResultPacket
        {
            TaskId = Guid.NewGuid(),
            Status = "failure",
            Summary = "Tests failed: 3 out of 10 assertions failed.",
            FilesChanged = [],
            TestsPassed = false,
            Errors = ["AssertionError: expected 42 got 0"],
            Notes = "Test suite incomplete",
            NextAction = "fix"
        };

        _workspaceService.ReadResult(Arg.Any<WorkTask>())
            .Returns(failureResult);

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — reports failure from worker
        Assert.Equal("Failed", result.Status);

        // Container should be called exactly once (no retry for worker-reported failures)
        await _containerService.Received(1).LaunchWorkerAsync(Arg.Any<WorkTask>());
    }

    // -------------------------------------------------------------------
    // Event emission tests
    // -------------------------------------------------------------------

    /// <summary>
    /// Verifies that failure events are emitted with appropriate payload
    /// when a container fails. Event payloads should include failure details
    /// for audit trail per GOV-004.
    /// </summary>
    [Fact]
    public async Task FailedRun_EmitsTaskFailedAndJobFailedEvents()
    {
        // Arrange
        _workspaceService.PrepareWorkspace(Arg.Any<WorkTask>(), Arg.Any<Job>())
            .Returns("/tmp/workspaces/event-test");

        _containerService.LaunchWorkerAsync(Arg.Any<WorkTask>())
            .Returns(137); // OOM kill

        // Act
        var result = await _sut.ExecuteTestJobAsync();

        // Assert — TaskFailed and JobFailed events should be emitted
        Assert.Equal("Failed", result.Status);

        // Verify events were saved (4 total: Created, Created, Started, Started happen before failure,
        // then TaskFailed + JobFailed in MarkFailedAsync)
        await _eventRepository.Received().SaveAsync(
            Arg.Is<Event>(e => e.EventType == EventType.TaskFailed));
        await _eventRepository.Received().SaveAsync(
            Arg.Is<Event>(e => e.EventType == EventType.JobFailed));
    }
}
