/**
 * ArchitectControls — Start/stop panel for the Architect Agent.
 *
 * Displays Architect status (online/offline), session metadata,
 * runtime/model selectors, and start/stop controls.
 * Polls the status endpoint every 10s.
 *
 * REF: JOB-018 T-174, JOB-023 T-200
 */
import { useState, useEffect, useCallback, useRef } from "react";
import { startArchitect, stopArchitect, getArchitectStatus } from "../api/client";
import type { ArchitectStatus } from "../types";

/** Polling interval (ms) — status refresh cycle */
const POLL_INTERVAL_MS = 10_000;

/** Available agent runtimes (static list per JOB-023 spec) */
const RUNTIMES = ["stub", "opencode"] as const;

/** Models available per runtime */
const MODELS_BY_RUNTIME: Record<string, string[]> = {
  stub: [],
  opencode: [
    "google/gemini-2.0-flash",
    "google/gemini-2.5-pro",
    "anthropic/claude-3-haiku",
    "openai/gpt-4o-mini",
  ],
};

interface ArchitectControlsProps {
  /** The project this architect belongs to */
  projectId: string;
  /** Callback to notify parent of status changes */
  onStatusChange?: (active: boolean) => void;
}

/**
 * ArchitectControls — Architect Agent lifecycle panel.
 *
 * Renders a card with:
 * - Online/offline status badge
 * - Runtime and model selector dropdowns
 * - Start or stop button
 * - Runtime, session ID, and uptime metadata when active
 */
export function ArchitectControls({ projectId, onStatusChange }: ArchitectControlsProps) {
  const [status, setStatus] = useState<ArchitectStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionInFlight, setActionInFlight] = useState(false);
  const [confirmStop, setConfirmStop] = useState(false);

  const [selectedRuntime, setSelectedRuntime] = useState<string>("stub");
  const [selectedModel, setSelectedModel] = useState<string>("");

  /** Track the last notified active state to avoid redundant callbacks */
  const lastNotifiedRef = useRef<boolean | null>(null);

  /** Fetch architect status */
  const fetchStatus = useCallback(async () => {
    try {
      const result = await getArchitectStatus(projectId);
      setStatus(result);
      setError(null);

      // Sync selector state from an active session
      if (result.active && result.session) {
        setSelectedRuntime(result.session.runtimeName || "stub");
      }

      // Notify parent only on change
      if (onStatusChange && lastNotifiedRef.current !== result.active) {
        lastNotifiedRef.current = result.active;
        onStatusChange(result.active);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to fetch status");
    } finally {
      setLoading(false);
    }
  }, [projectId, onStatusChange]);

  // Initial fetch + polling
  useEffect(() => {
    void fetchStatus();
    const interval = setInterval(() => void fetchStatus(), POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  // Reset model when runtime changes
  useEffect(() => {
    const models = MODELS_BY_RUNTIME[selectedRuntime] ?? [];
    if (models.length > 0 && !models.includes(selectedModel)) {
      setSelectedModel(models[0]);
    } else if (models.length === 0) {
      setSelectedModel("");
    }
  }, [selectedRuntime]); // eslint-disable-line react-hooks/exhaustive-deps

  /** Start the Architect Agent */
  const handleStart = useCallback(async () => {
    setActionInFlight(true);
    setError(null);
    try {
      await startArchitect(
        projectId,
        selectedRuntime,
        selectedModel || undefined,
      );
      // Re-fetch to get the updated session info
      await fetchStatus();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start Architect");
    } finally {
      setActionInFlight(false);
    }
  }, [projectId, selectedRuntime, selectedModel, fetchStatus]);

  /** Stop the Architect Agent (requires confirmation) */
  const handleStop = useCallback(async () => {
    if (!confirmStop) {
      setConfirmStop(true);
      return;
    }

    setActionInFlight(true);
    setConfirmStop(false);
    setError(null);
    try {
      await stopArchitect(projectId);
      await fetchStatus();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to stop Architect");
    } finally {
      setActionInFlight(false);
    }
  }, [projectId, confirmStop, fetchStatus]);

  /** Cancel stop confirmation */
  const handleCancelStop = useCallback(() => {
    setConfirmStop(false);
  }, []);

  /** Format uptime from startedAt timestamp */
  function formatUptime(startedAt: string): string {
    const startMs = new Date(startedAt).getTime();
    const nowMs = Date.now();
    const diffMs = Math.max(0, nowMs - startMs);

    const totalSeconds = Math.floor(diffMs / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    }
    if (minutes > 0) {
      return `${minutes}m ${totalSeconds % 60}s`;
    }
    return `${totalSeconds}s`;
  }

  const isActive = status?.active ?? false;
  const session = status?.session ?? null;
  const availableModels = MODELS_BY_RUNTIME[selectedRuntime] ?? [];

  if (loading) {
    return (
      <div className="architect-controls" id="architect-controls">
        <div className="skeleton skeleton-card" style={{ height: 120 }} />
      </div>
    );
  }

  return (
    <div className="architect-controls" id="architect-controls">
      {/* Header row */}
      <div className="architect-header">
        <div className="architect-title">
          <span className="architect-icon">🤖</span>
          <span className="architect-label">Architect Agent</span>
        </div>
        <span
          className={`architect-status-badge ${isActive ? "architect-status-badge--online" : "architect-status-badge--offline"}`}
          id="architect-status-badge"
        >
          <span className="architect-status-dot" />
          {isActive ? "Online" : "Offline"}
        </span>
      </div>

      {/* Runtime / Model selectors — only when agent is offline */}
      {!isActive && (
        <div className="architect-selectors" id="architect-selectors">
          <div className="architect-selector-group">
            <label className="architect-selector-label" htmlFor="architect-runtime-select">
              Runtime
            </label>
            <select
              id="architect-runtime-select"
              className="form-input"
              value={selectedRuntime}
              onChange={(e) => setSelectedRuntime(e.target.value)}
              disabled={actionInFlight}
            >
              {RUNTIMES.map((rt) => (
                <option key={rt} value={rt}>{rt}</option>
              ))}
            </select>
          </div>

          {availableModels.length > 0 && (
            <div className="architect-selector-group">
              <label className="architect-selector-label" htmlFor="architect-model-select">
                Model
              </label>
              <select
                id="architect-model-select"
                className="form-input"
                value={selectedModel}
                onChange={(e) => setSelectedModel(e.target.value)}
                disabled={actionInFlight}
              >
                {availableModels.map((m) => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
          )}
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="architect-error">
          {error}
        </div>
      )}

      {/* Actions */}
      <div className="architect-actions">
        {!isActive ? (
          <button
            className="btn architect-start-btn"
            id="architect-start-btn"
            onClick={() => void handleStart()}
            disabled={actionInFlight}
          >
            {actionInFlight ? (
              <><span className="architect-spinner" /> Starting…</>
            ) : (
              "Start Architect"
            )}
          </button>
        ) : confirmStop ? (
          <div className="architect-confirm-stop">
            <span className="architect-confirm-text">Stop running agent?</span>
            <button
              className="btn architect-stop-btn"
              id="architect-stop-confirm-btn"
              onClick={() => void handleStop()}
              disabled={actionInFlight}
            >
              {actionInFlight ? "Stopping…" : "Confirm Stop"}
            </button>
            <button
              className="btn btn-ghost"
              onClick={handleCancelStop}
              disabled={actionInFlight}
            >
              Cancel
            </button>
          </div>
        ) : (
          <button
            className="btn architect-stop-btn"
            id="architect-stop-btn"
            onClick={() => void handleStop()}
            disabled={actionInFlight}
          >
            Stop Architect
          </button>
        )}
      </div>

      {/* Metadata */}
      <div className="architect-meta">
        <div className="architect-meta-item">
          <span className="architect-meta-label">Runtime</span>
          <span className="architect-meta-value">{session?.runtimeName ?? selectedRuntime}</span>
        </div>
        {isActive && session && (
          <>
            <div className="architect-meta-item">
              <span className="architect-meta-label">Session</span>
              <span className="architect-meta-value mono">{session.id.slice(0, 8)}…</span>
            </div>
            <div className="architect-meta-item">
              <span className="architect-meta-label">Uptime</span>
              <span className="architect-meta-value">{formatUptime(session.startedAt)}</span>
            </div>
          </>
        )}
        {!isActive && (
          <div className="architect-meta-item">
            <span className="architect-meta-label">Last active</span>
            <span className="architect-meta-value">
              {session?.stoppedAt
                ? new Date(session.stoppedAt).toLocaleString(undefined, {
                    month: "short",
                    day: "numeric",
                    hour: "2-digit",
                    minute: "2-digit",
                  })
                : "—"}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
