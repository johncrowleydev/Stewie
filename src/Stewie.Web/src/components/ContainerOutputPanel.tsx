/**
 * ContainerOutputPanel — Terminal-style component for live container output streaming.
 *
 * Features:
 * - Dark terminal aesthetic with macOS-style header dots
 * - Auto-scrolls to bottom as new lines arrive
 * - Scroll lock: stops auto-scrolling when user scrolls up; resumes at bottom
 * - Toggleable line numbers
 * - Stderr lines highlighted in red/amber with [stderr] tag
 * - Status indicator: pulsing "Streaming..." when active, "Completed" when finished
 * - Fetches backlog on mount via fetchContainerOutput
 * - Subscribes to SignalR ContainerOutput events for real-time lines
 *
 * REF: JOB-014 T-147
 */
import { useEffect, useState, useRef, useCallback } from "react";
import { fetchContainerOutput } from "../api/client";
import { useSignalR } from "../hooks/useSignalR";

/** Props for ContainerOutputPanel */
export interface ContainerOutputPanelProps {
  /** The task ID to stream output for */
  taskId: string;
  /** The job ID — used for SignalR group subscription */
  jobId: string;
  /** Whether the task is still running (controls streaming indicator) */
  isActive?: boolean;
}

/** Represents a single output line with metadata */
interface OutputLine {
  /** The raw text content */
  text: string;
  /** Whether this line came from stderr */
  isStderr: boolean;
  /** 1-indexed line number */
  lineNumber: number;
}

/**
 * Parses a raw line from the container output buffer.
 * Stderr lines are prefixed with "[stderr] " by the backend.
 */
function parseLine(raw: string, lineNumber: number): OutputLine {
  const isStderr = raw.startsWith("[stderr] ");
  const text = isStderr ? raw.slice(9) : raw;
  return { text, isStderr, lineNumber };
}

/**
 * ContainerOutputPanel — renders a terminal-like panel displaying container output.
 */
export function ContainerOutputPanel({ taskId, jobId, isActive = false }: ContainerOutputPanelProps) {
  const [lines, setLines] = useState<OutputLine[]>([]);
  const [showLineNumbers, setShowLineNumbers] = useState(false);
  const [scrollLocked, setScrollLocked] = useState(false);
  const [backlogLoaded, setBacklogLoaded] = useState(false);

  const bodyRef = useRef<HTMLDivElement>(null);
  const lineCountRef = useRef(0);

  // SignalR connection
  const { state: signalRState, on } = useSignalR();
  const isLive = signalRState === "connected";

  /**
   * Auto-scroll to bottom unless the user has scrolled up (scroll lock).
   */
  const scrollToBottom = useCallback(() => {
    const el = bodyRef.current;
    if (!el || scrollLocked) return;
    el.scrollTop = el.scrollHeight;
  }, [scrollLocked]);

  /**
   * Detect user scroll position to toggle scroll lock.
   * Lock when user scrolls up; unlock when they reach the bottom.
   */
  const handleScroll = useCallback(() => {
    const el = bodyRef.current;
    if (!el) return;
    const threshold = 40; // px from bottom
    const isAtBottom = el.scrollHeight - el.scrollTop - el.clientHeight < threshold;
    setScrollLocked(!isAtBottom);
  }, []);

  /**
   * Append new lines to state and update line counter.
   */
  const appendLines = useCallback((rawLines: string[]) => {
    setLines(prev => {
      let counter = lineCountRef.current;
      const newParsed = rawLines.map(raw => {
        counter++;
        return parseLine(raw, counter);
      });
      lineCountRef.current = counter;
      return [...prev, ...newParsed];
    });
  }, []);

  // Fetch backlog on mount
  useEffect(() => {
    let cancelled = false;

    async function loadBacklog() {
      try {
        const response = await fetchContainerOutput(taskId);
        if (cancelled) return;
        if (response.lines.length > 0) {
          lineCountRef.current = 0;
          const parsed = response.lines.map((raw, idx) => parseLine(raw, idx + 1));
          lineCountRef.current = parsed.length;
          setLines(parsed);
        }
      } catch {
        // Endpoint may not exist yet or task has no output — silent fail
      } finally {
        if (!cancelled) setBacklogLoaded(true);
      }
    }

    void loadBacklog();
    return () => { cancelled = true; };
  }, [taskId]);

  // Auto-scroll after backlog load
  useEffect(() => {
    if (backlogLoaded) {
      // Small delay to let DOM render
      requestAnimationFrame(() => scrollToBottom());
    }
  }, [backlogLoaded, scrollToBottom]);

  // Subscribe to SignalR ContainerOutput events
  useEffect(() => {
    if (!isLive || !isActive) return;

    const cleanup = on("ContainerOutput", (...args: unknown[]) => {
      // Event payload: (string jobId, string taskId, string line)
      const eventJobId = args[0] as string;
      const eventTaskId = args[1] as string;
      const line = args[2] as string;

      // Only process lines for this specific task
      if (eventJobId !== jobId || eventTaskId !== taskId) return;

      appendLines([line]);
    });

    return cleanup;
  }, [isLive, isActive, on, jobId, taskId, appendLines]);

  // Auto-scroll when new lines arrive
  useEffect(() => {
    if (lines.length > 0) {
      requestAnimationFrame(() => scrollToBottom());
    }
  }, [lines.length, scrollToBottom]);

  const lineCount = lines.length;
  const hasOutput = lineCount > 0;

  return (
    <div className="terminal-panel" id={`terminal-panel-${taskId}`}>
      {/* Header bar with macOS dots and title */}
      <div className="terminal-header">
        <div className="terminal-header-left">
          <div className="terminal-header-dots">
            <div className="terminal-header-dot terminal-header-dot--red" />
            <div className="terminal-header-dot terminal-header-dot--yellow" />
            <div className="terminal-header-dot terminal-header-dot--green" />
          </div>
          <span className="terminal-header-title">
            container output — {taskId.slice(0, 8)}…
          </span>
        </div>
        <div className="terminal-header-right">
          <button
            className={`terminal-toggle-btn ${showLineNumbers ? "active" : ""}`}
            onClick={() => setShowLineNumbers(v => !v)}
            title="Toggle line numbers"
            id={`terminal-toggle-lines-${taskId}`}
          >
            #
          </button>
        </div>
      </div>

      {/* Scrollable output body */}
      <div
        className="terminal-body"
        ref={bodyRef}
        onScroll={handleScroll}
        id={`terminal-body-${taskId}`}
      >
        {!hasOutput && (
          <div className="terminal-empty">
            {isActive ? (
              <>
                Waiting for output…
                <span className="terminal-cursor" />
              </>
            ) : (
              "No output recorded"
            )}
          </div>
        )}

        {lines.map((line) => (
          <div
            key={line.lineNumber}
            className={`terminal-line ${line.isStderr ? "terminal-line--stderr" : ""}`}
          >
            {showLineNumbers && (
              <span className="terminal-line-number">{line.lineNumber}</span>
            )}
            {line.isStderr && (
              <span className="terminal-stderr-tag">stderr</span>
            )}
            <span className="terminal-line-text">{line.text}</span>
          </div>
        ))}
      </div>

      {/* Status bar */}
      <div className="terminal-status">
        <div className="terminal-status-left">
          {isActive ? (
            <>
              <div className="terminal-streaming-dot" />
              <span className="terminal-status-text--streaming">Streaming…</span>
            </>
          ) : (
            <span className="terminal-status-text--completed">
              {hasOutput ? "Completed" : "Idle"}
            </span>
          )}
        </div>
        <div className="terminal-status-right">
          {scrollLocked && (
            <span className="terminal-scroll-lock">
              <span className="terminal-scroll-lock-icon">⏸</span>
              Scroll locked
            </span>
          )}
          <span>{lineCount} lines</span>
        </div>
      </div>
    </div>
  );
}
