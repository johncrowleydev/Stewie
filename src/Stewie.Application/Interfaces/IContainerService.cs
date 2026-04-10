/// <summary>
/// Interface for launching worker containers.
/// REF: BLU-001 §3.3, CON-001 §7
/// </summary>
using Stewie.Domain.Entities;

namespace Stewie.Application.Interfaces;

/// <summary>
/// Defines container launch operations for worker execution.
/// All overloads support cooperative cancellation for timeout enforcement.
/// </summary>
public interface IContainerService
{
    /// <summary>Launches a worker container using the default (dummy) image.</summary>
    /// <param name="task">The task entity with workspace path.</param>
    /// <param name="cancellationToken">Cancellation token for timeout enforcement.</param>
    /// <returns>The container exit code (0 = success, 124 = timeout).</returns>
    Task<int> LaunchWorkerAsync(WorkTask task, CancellationToken cancellationToken = default);

    /// <summary>Launches a worker container using the specified image, with writable repo mount.</summary>
    /// <param name="task">The task entity with workspace path.</param>
    /// <param name="imageName">Docker image name to use.</param>
    /// <param name="cancellationToken">Cancellation token for timeout enforcement.</param>
    /// <returns>The container exit code (0 = success, 124 = timeout).</returns>
    Task<int> LaunchWorkerAsync(WorkTask task, string imageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches a worker container using the default image, streaming stdout/stderr
    /// line-by-line via the provided callback. Returns exit code on completion.
    /// REF: JOB-014 T-140
    /// </summary>
    /// <param name="task">The task entity with workspace path.</param>
    /// <param name="onOutputLine">Callback invoked for each stdout/stderr line. Stderr lines are prefixed with [stderr].</param>
    /// <param name="cancellationToken">Cancellation token for timeout enforcement.</param>
    /// <returns>The container exit code (0 = success, 124 = timeout).</returns>
    Task<int> LaunchWorkerAsync(WorkTask task, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches a worker container using the specified image, streaming stdout/stderr
    /// line-by-line via the provided callback. Returns exit code on completion.
    /// REF: JOB-014 T-140
    /// </summary>
    /// <param name="task">The task entity with workspace path.</param>
    /// <param name="imageName">Docker image name to use.</param>
    /// <param name="onOutputLine">Callback invoked for each stdout/stderr line. Stderr lines are prefixed with [stderr].</param>
    /// <param name="cancellationToken">Cancellation token for timeout enforcement.</param>
    /// <returns>The container exit code (0 = success, 124 = timeout).</returns>
    Task<int> LaunchWorkerAsync(WorkTask task, string imageName, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default);
}
