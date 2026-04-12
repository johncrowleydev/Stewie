/**
 * ContainerOutputPanel — Terminal-style component for live container output streaming.
 * REF: JOB-014 T-147, JOB-027 T-407
 */
import { useEffect, useState, useRef, useCallback } from "react";
import { fetchContainerOutput } from "../api/client";
import { useSignalR } from "../hooks/useSignalR";

export interface ContainerOutputPanelProps {
  taskId: string;
  jobId: string;
  isActive?: boolean;
}

interface OutputLine {
  text: string;
  isStderr: boolean;
  lineNumber: number;
}

function parseLine(raw: string, lineNumber: number): OutputLine {
  const isStderr = raw.startsWith("[stderr] ");
  return { text: isStderr ? raw.slice(9) : raw, isStderr, lineNumber };
}

export function ContainerOutputPanel({ taskId, jobId, isActive = false }: ContainerOutputPanelProps) {
  const [lines, setLines] = useState<OutputLine[]>([]);
  const [showLineNumbers, setShowLineNumbers] = useState(false);
  const [scrollLocked, setScrollLocked] = useState(false);
  const [backlogLoaded, setBacklogLoaded] = useState(false);
  const bodyRef = useRef<HTMLDivElement>(null);
  const lineCountRef = useRef(0);
  const { state: signalRState, on } = useSignalR();
  const isLive = signalRState === "connected";

  const scrollToBottom = useCallback(() => {
    const el = bodyRef.current;
    if (!el || scrollLocked) return;
    el.scrollTop = el.scrollHeight;
  }, [scrollLocked]);

  const handleScroll = useCallback(() => {
    const el = bodyRef.current;
    if (!el) return;
    setScrollLocked(el.scrollHeight - el.scrollTop - el.clientHeight >= 40);
  }, []);

  const appendLines = useCallback((rawLines: string[]) => {
    setLines(prev => {
      let counter = lineCountRef.current;
      const newParsed = rawLines.map(raw => { counter++; return parseLine(raw, counter); });
      lineCountRef.current = counter;
      return [...prev, ...newParsed];
    });
  }, []);

  useEffect(() => {
    let cancelled = false;
    async function loadBacklog() {
      try {
        const response = await fetchContainerOutput(taskId);
        if (cancelled) return;
        if (response.lines.length > 0) {
          lineCountRef.current = 0;
          const parsed = response.lines.map((raw: string, idx: number) => parseLine(raw, idx + 1));
          lineCountRef.current = parsed.length;
          setLines(parsed);
        }
      } catch { /* noop */ }
      finally { if (!cancelled) setBacklogLoaded(true); }
    }
    void loadBacklog();
    return () => { cancelled = true; };
  }, [taskId]);

  useEffect(() => { if (backlogLoaded) requestAnimationFrame(() => scrollToBottom()); }, [backlogLoaded, scrollToBottom]);

  useEffect(() => {
    if (!isLive || !isActive) return;
    return on("ContainerOutput", (...args: unknown[]) => {
      if (args[0] !== jobId || args[1] !== taskId) return;
      appendLines([args[2] as string]);
    });
  }, [isLive, isActive, on, jobId, taskId, appendLines]);

  useEffect(() => { if (lines.length > 0) requestAnimationFrame(() => scrollToBottom()); }, [lines.length, scrollToBottom]);

  const lineCount = lines.length;
  const hasOutput = lineCount > 0;

  return (
    <div className="bg-[#0d1117] border border-ds-border rounded-lg overflow-hidden mt-sm font-mono text-xs" id={`terminal-panel-${taskId}`}>
      {/* Header */}
      <div className="flex items-center justify-between px-md py-sm bg-[#161b22] border-b border-[rgba(255,255,255,0.06)]">
        <div className="flex items-center gap-sm">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-[#ff5f57]" />
            <div className="w-3 h-3 rounded-full bg-[#febc2e]" />
            <div className="w-3 h-3 rounded-full bg-[#28c840]" />
          </div>
          <span className="text-[#8b949e] text-xs ml-sm">container output — {taskId.slice(0, 8)}…</span>
        </div>
        <button
          className={`px-1.5 py-px rounded-sm text-[10px] font-mono cursor-pointer border transition-all duration-150 ${showLineNumbers ? "bg-[rgba(111,172,80,0.15)] text-ds-primary border-ds-primary" : "bg-transparent text-[#8b949e] border-transparent hover:text-[#c9d1d9]"}`}
          onClick={() => setShowLineNumbers(v => !v)}
          title="Toggle line numbers"
          id={`terminal-toggle-lines-${taskId}`}
        >
          #
        </button>
      </div>

      {/* Body */}
      <div className="max-h-[400px] overflow-y-auto p-md leading-relaxed" ref={bodyRef} onScroll={handleScroll} id={`terminal-body-${taskId}`}>
        {!hasOutput && (
          <div className="text-center text-[#8b949e] py-xl italic">
            {isActive ? (
              <>Waiting for output…<span className="inline-block w-1.5 h-4 bg-ds-primary ml-1 animate-[terminal-blink_1s_step-end_infinite]" /></>
            ) : "No output recorded"}
          </div>
        )}
        {lines.map((line) => (
          <div key={line.lineNumber} className={`py-px px-0 animate-[terminal-line-appear_0.15s_ease-out] ${line.isStderr ? "text-[#f97583] bg-[rgba(249,115,131,0.06)] border-l-2 border-[#f97583] pl-sm" : "text-[#c9d1d9]"}`}>
            {showLineNumbers && (
              <span className="inline-block w-10 text-right mr-md text-[#484f58] select-none">{line.lineNumber}</span>
            )}
            {line.isStderr && (
              <span className="inline-block text-[10px] font-semibold uppercase text-[#f97583] bg-[rgba(249,115,131,0.12)] px-1 py-px rounded-sm mr-sm">stderr</span>
            )}
            <span>{line.text}</span>
          </div>
        ))}
      </div>

      {/* Status bar */}
      <div className="flex items-center justify-between px-md py-xs bg-[#161b22] border-t border-[rgba(255,255,255,0.06)] text-[10px] text-[#8b949e]">
        <div className="flex items-center gap-1.5">
          {isActive ? (
            <>
              <div className="w-1.5 h-1.5 rounded-full bg-ds-completed animate-[terminal-pulse_1.5s_ease-in-out_infinite]" />
              <span className="text-ds-completed font-medium">Streaming…</span>
            </>
          ) : (
            <span className="text-[#8b949e]">{hasOutput ? "Completed" : "Idle"}</span>
          )}
        </div>
        <div className="flex items-center gap-md">
          {scrollLocked && (
            <span className="inline-flex items-center gap-xs px-1.5 py-px rounded-full bg-[rgba(245,166,35,0.15)] text-ds-warning font-medium animate-[scroll-lock-appear_0.2s_ease]">
              ⏸ Scroll locked
            </span>
          )}
          <span>{lineCount} lines</span>
        </div>
      </div>
    </div>
  );
}
