/// <summary>
/// In-memory ring buffer for container output lines — holds the last N lines per task.
/// Late-joining dashboard clients fetch the backlog via REST, then switch to WebSocket streaming.
/// REF: JOB-014 T-142
/// </summary>
using System.Collections.Concurrent;

namespace Stewie.Application.Services;

/// <summary>
/// Thread-safe in-memory ring buffer that stores the most recent output lines per task.
/// Registered as singleton in DI. Oldest lines are dropped when capacity is exceeded.
/// </summary>
public class ContainerOutputBuffer
{
    private readonly int _maxLinesPerTask;
    private readonly ConcurrentDictionary<Guid, CircularLineBuffer> _buffers = new();

    /// <summary>Initializes the output buffer with a configurable max line count per task.</summary>
    /// <param name="maxLinesPerTask">Maximum lines to retain per task. Default: 500.</param>
    public ContainerOutputBuffer(int maxLinesPerTask = 500)
    {
        _maxLinesPerTask = maxLinesPerTask > 0 ? maxLinesPerTask : 500;
    }

    /// <summary>Appends a line to the output buffer for the given task.</summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="line">The output line to append.</param>
    public void AppendLine(Guid taskId, string line)
    {
        var buffer = _buffers.GetOrAdd(taskId, _ => new CircularLineBuffer(_maxLinesPerTask));
        buffer.Add(line);
    }

    /// <summary>
    /// Gets all buffered lines for a task, in order (oldest first).
    /// Returns an empty list for unknown or cleared tasks.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <returns>Read-only list of buffered output lines.</returns>
    public IReadOnlyList<string> GetLines(Guid taskId)
    {
        if (_buffers.TryGetValue(taskId, out var buffer))
        {
            return buffer.ToList();
        }

        return Array.Empty<string>();
    }

    /// <summary>Clears and removes the buffer for a completed/failed task to free memory.</summary>
    /// <param name="taskId">The task ID.</param>
    public void Clear(Guid taskId)
    {
        _buffers.TryRemove(taskId, out _);
    }

    /// <summary>
    /// Lock-free circular buffer backed by an array. Overwrites oldest entries when full.
    /// Thread-safe for concurrent Add calls via lock-based synchronization.
    /// </summary>
    private sealed class CircularLineBuffer
    {
        private readonly string[] _buffer;
        private readonly int _capacity;
        private int _writeIndex;
        private int _count;
        private readonly object _lock = new();

        /// <summary>Creates a circular buffer with the given capacity.</summary>
        public CircularLineBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new string[capacity];
            _writeIndex = 0;
            _count = 0;
        }

        /// <summary>Adds a line to the buffer. If full, overwrites the oldest line.</summary>
        public void Add(string line)
        {
            lock (_lock)
            {
                _buffer[_writeIndex] = line;
                _writeIndex = (_writeIndex + 1) % _capacity;
                if (_count < _capacity)
                {
                    _count++;
                }
            }
        }

        /// <summary>Returns all lines in chronological order (oldest first).</summary>
        public List<string> ToList()
        {
            lock (_lock)
            {
                var result = new List<string>(_count);

                if (_count < _capacity)
                {
                    // Buffer hasn't wrapped — read from 0 to _count
                    for (int i = 0; i < _count; i++)
                    {
                        result.Add(_buffer[i]);
                    }
                }
                else
                {
                    // Buffer has wrapped — read from _writeIndex (oldest) around
                    for (int i = 0; i < _capacity; i++)
                    {
                        result.Add(_buffer[(_writeIndex + i) % _capacity]);
                    }
                }

                return result;
            }
        }
    }
}
