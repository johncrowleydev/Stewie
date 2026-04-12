/**
 * ArchitectControls — Start/stop panel for the Architect Agent.
 * REF: JOB-018 T-174, JOB-023 T-200, JOB-027 T-407
 */
import { useState, useEffect, useCallback, useRef } from "react";
import { startArchitect, stopArchitect, getArchitectStatus } from "../api/client";
import { IconBot } from "./Icons";
import { card, formInput, btnPrimary, btnGhost, btnDanger, skeleton } from "../tw";
import type { ArchitectStatus } from "../types";

const POLL_INTERVAL_MS = 10_000;
const RUNTIMES = ["stub", "opencode"] as const;
const MODELS_BY_RUNTIME: Record<string, string[]> = {
  stub: [],
  opencode: ["google/gemini-2.0-flash", "google/gemini-2.5-pro", "anthropic/claude-3-haiku", "openai/gpt-4o-mini"],
};

interface ArchitectControlsProps {
  projectId: string;
  onStatusChange?: (active: boolean) => void;
}

export function ArchitectControls({ projectId, onStatusChange }: ArchitectControlsProps) {
  const [status, setStatus] = useState<ArchitectStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionInFlight, setActionInFlight] = useState(false);
  const [confirmStop, setConfirmStop] = useState(false);
  const [selectedRuntime, setSelectedRuntime] = useState<string>("stub");
  const [selectedModel, setSelectedModel] = useState<string>("");
  const lastNotifiedRef = useRef<boolean | null>(null);

  const fetchStatusCb = useCallback(async () => {
    try {
      const result = await getArchitectStatus(projectId);
      setStatus(result); setError(null);
      if (result.active && result.session) setSelectedRuntime(result.session.runtimeName || "stub");
      if (onStatusChange && lastNotifiedRef.current !== result.active) { lastNotifiedRef.current = result.active; onStatusChange(result.active); }
    } catch (err) { setError(err instanceof Error ? err.message : "Failed to fetch status"); }
    finally { setLoading(false); }
  }, [projectId, onStatusChange]);

  useEffect(() => { void fetchStatusCb(); const iv = setInterval(() => void fetchStatusCb(), POLL_INTERVAL_MS); return () => clearInterval(iv); }, [fetchStatusCb]);

  useEffect(() => {
    const models = MODELS_BY_RUNTIME[selectedRuntime] ?? [];
    if (models.length > 0 && !models.includes(selectedModel)) setSelectedModel(models[0]);
    else if (models.length === 0) setSelectedModel("");
  }, [selectedRuntime]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleStart = useCallback(async () => {
    setActionInFlight(true); setError(null);
    try { await startArchitect(projectId, selectedRuntime, selectedModel || undefined); await fetchStatusCb(); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to start Architect"); }
    finally { setActionInFlight(false); }
  }, [projectId, selectedRuntime, selectedModel, fetchStatusCb]);

  const handleStop = useCallback(async () => {
    if (!confirmStop) { setConfirmStop(true); return; }
    setActionInFlight(true); setConfirmStop(false); setError(null);
    try { await stopArchitect(projectId); await fetchStatusCb(); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to stop Architect"); }
    finally { setActionInFlight(false); }
  }, [projectId, confirmStop, fetchStatusCb]);

  function formatUptime(startedAt: string): string {
    const diff = Math.max(0, Date.now() - new Date(startedAt).getTime());
    const totalSec = Math.floor(diff / 1000); const min = Math.floor(totalSec / 60); const hrs = Math.floor(min / 60);
    if (hrs > 0) return `${hrs}h ${min % 60}m`;
    if (min > 0) return `${min}m ${totalSec % 60}s`;
    return `${totalSec}s`;
  }

  const isActive = status?.active ?? false;
  const session = status?.session ?? null;
  const availableModels = MODELS_BY_RUNTIME[selectedRuntime] ?? [];
  const selectInput = `${formInput} appearance-none bg-[url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%238b8d93' d='M2 4l4 4 4-4'/%3E%3C/svg%3E")] bg-no-repeat bg-[position:right_12px_center] pr-8 cursor-pointer`;

  if (loading) {
    return <div id="architect-controls"><div className={`${skeleton} h-[120px]`} /></div>;
  }

  return (
    <div className={`${card} mb-lg`} id="architect-controls">
      <div className="flex items-center justify-between mb-md">
        <div className="flex items-center gap-sm">
          <IconBot size={18} className="text-ds-primary" />
          <span className="font-semibold text-md">Architect Agent</span>
        </div>
        <span
          className={`inline-flex items-center gap-1.5 py-px px-2.5 rounded-full text-xs font-semibold ${isActive ? "bg-[rgba(111,172,80,0.15)] text-ds-completed" : "bg-[rgba(139,141,147,0.1)] text-ds-text-muted"}`}
          id="architect-status-badge"
        >
          <span className={`w-2 h-2 rounded-full ${isActive ? "bg-ds-completed animate-[pulse_1.5s_ease-in-out_infinite]" : "bg-ds-text-muted"}`} />
          {isActive ? "Online" : "Offline"}
        </span>
      </div>

      {!isActive && (
        <div className="grid grid-cols-[1fr_1fr] gap-md mb-md" id="architect-selectors">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs" htmlFor="architect-runtime-select">Runtime</label>
            <select id="architect-runtime-select" className={selectInput} value={selectedRuntime} onChange={(e) => setSelectedRuntime(e.target.value)} disabled={actionInFlight}>
              {RUNTIMES.map((rt) => <option key={rt} value={rt}>{rt}</option>)}
            </select>
          </div>
          {availableModels.length > 0 && (
            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs" htmlFor="architect-model-select">Model</label>
              <select id="architect-model-select" className={selectInput} value={selectedModel} onChange={(e) => setSelectedModel(e.target.value)} disabled={actionInFlight}>
                {availableModels.map((m) => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
          )}
        </div>
      )}

      {error && <div className="text-ds-failed text-s mb-md">{error}</div>}

      <div className="flex items-center gap-sm mb-md">
        {!isActive ? (
          <button className={btnPrimary} id="architect-start-btn" onClick={() => void handleStart()} disabled={actionInFlight}>
            {actionInFlight ? <><span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> Starting…</> : "Start Architect"}
          </button>
        ) : confirmStop ? (
          <div className="flex items-center gap-sm">
            <span className="text-s text-ds-warning font-medium">Stop running agent?</span>
            <button className={btnDanger} id="architect-stop-confirm-btn" onClick={() => void handleStop()} disabled={actionInFlight}>{actionInFlight ? "Stopping…" : "Confirm Stop"}</button>
            <button className={btnGhost} onClick={() => setConfirmStop(false)} disabled={actionInFlight}>Cancel</button>
          </div>
        ) : (
          <button className={btnDanger} id="architect-stop-btn" onClick={() => void handleStop()} disabled={actionInFlight}>Stop Architect</button>
        )}
      </div>

      <div className="flex gap-xl flex-wrap text-s">
        <div>
          <span className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted block mb-px">Runtime</span>
          <span className="text-ds-text">{session?.runtimeName ?? selectedRuntime}</span>
        </div>
        {isActive && session && (
          <>
            <div><span className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted block mb-px">Session</span><span className="font-mono text-ds-text">{session.id.slice(0, 8)}…</span></div>
            <div><span className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted block mb-px">Uptime</span><span className="text-ds-text">{formatUptime(session.startedAt)}</span></div>
          </>
        )}
        {!isActive && (
          <div><span className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted block mb-px">Last active</span><span className="text-ds-text">{session?.stoppedAt ? new Date(session.stoppedAt).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "—"}</span></div>
        )}
      </div>
    </div>
  );
}
