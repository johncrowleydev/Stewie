/// <summary>
/// Unit tests for <see cref="ContainerOutputBuffer"/> — ring buffer for container output.
/// Covers: basic append/get, max line eviction, clear, concurrency safety, and unknown task handling.
/// REF: JOB-014 T-145, GOV-002
/// </summary>
using Stewie.Application.Services;
using Xunit;

namespace Stewie.Tests.Services;

/// <summary>
/// Validates the ContainerOutputBuffer ring buffer behavior:
/// thread safety, FIFO ordering, capacity enforcement, and memory cleanup.
/// </summary>
public class ContainerOutputBufferTests
{
    /// <summary>
    /// Append 3 lines, then get returns all 3 in insertion order.
    /// </summary>
    [Fact]
    public void AppendAndGet_ReturnsLines()
    {
        // Arrange
        var buffer = new ContainerOutputBuffer(maxLinesPerTask: 500);
        var taskId = Guid.NewGuid();

        // Act
        buffer.AppendLine(taskId, "line 1");
        buffer.AppendLine(taskId, "line 2");
        buffer.AppendLine(taskId, "line 3");

        var lines = buffer.GetLines(taskId);

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal("line 1", lines[0]);
        Assert.Equal("line 2", lines[1]);
        Assert.Equal("line 3", lines[2]);
    }

    /// <summary>
    /// When more lines are appended than the max capacity, the oldest lines are dropped.
    /// Append 600 lines with max 500 → only last 500 remain.
    /// </summary>
    [Fact]
    public void MaxLines_OldestDropped()
    {
        // Arrange
        const int maxLines = 500;
        const int totalLines = 600;
        var buffer = new ContainerOutputBuffer(maxLinesPerTask: maxLines);
        var taskId = Guid.NewGuid();

        // Act
        for (int i = 0; i < totalLines; i++)
        {
            buffer.AppendLine(taskId, $"line {i}");
        }

        var lines = buffer.GetLines(taskId);

        // Assert — only last 500 lines remain
        Assert.Equal(maxLines, lines.Count);

        // First line should be line 100 (lines 0-99 were evicted)
        Assert.Equal("line 100", lines[0]);

        // Last line should be line 599
        Assert.Equal("line 599", lines[maxLines - 1]);
    }

    /// <summary>
    /// After Clear(), GetLines returns an empty list and memory is freed.
    /// </summary>
    [Fact]
    public void Clear_RemovesBuffer()
    {
        // Arrange
        var buffer = new ContainerOutputBuffer(maxLinesPerTask: 500);
        var taskId = Guid.NewGuid();
        buffer.AppendLine(taskId, "line 1");
        buffer.AppendLine(taskId, "line 2");
        buffer.AppendLine(taskId, "line 3");

        // Act
        buffer.Clear(taskId);
        var lines = buffer.GetLines(taskId);

        // Assert
        Assert.Empty(lines);
    }

    /// <summary>
    /// Parallel writes from 10 threads should not throw exceptions.
    /// Validates thread safety of the ConcurrentDictionary + lock-based ring buffer.
    /// </summary>
    [Fact]
    public void ConcurrentAccess_NoExceptions()
    {
        // Arrange
        var buffer = new ContainerOutputBuffer(maxLinesPerTask: 500);
        var taskId = Guid.NewGuid();
        const int threadCount = 10;
        const int linesPerThread = 100;

        // Act — parallel writes from 10 threads
        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex =>
            Task.Run(() =>
            {
                for (int i = 0; i < linesPerThread; i++)
                {
                    buffer.AppendLine(taskId, $"thread-{threadIndex}-line-{i}");
                }
            })
        ).ToArray();

        // Assert — no exceptions
        var exception = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(exception);

        // Verify we can read the buffer without error
        var lines = buffer.GetLines(taskId);

        // Should have 500 lines (10 * 100 = 1000 appended, max 500 kept)
        Assert.Equal(500, lines.Count);
    }

    /// <summary>
    /// GetLines for an unknown task ID returns an empty list, not an exception.
    /// </summary>
    [Fact]
    public void GetLines_UnknownTask_ReturnsEmpty()
    {
        // Arrange
        var buffer = new ContainerOutputBuffer(maxLinesPerTask: 500);
        var unknownTaskId = Guid.NewGuid();

        // Act
        var lines = buffer.GetLines(unknownTaskId);

        // Assert
        Assert.Empty(lines);
    }
}
