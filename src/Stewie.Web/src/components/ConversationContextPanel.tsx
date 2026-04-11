/**
 * ConversationContextPanel — Displays the Architect's context window state.
 *
 * Shows token usage progress bar, project stats (chat count, jobs, tasks,
 * governance reports), and last-updated timestamp. Gracefully handles
 * the case where no Architect is running or the context API is unavailable.
 *
 * REF: JOB-023 T-204
 */
import { useState, useEffect, useCallback, useRef } from "react";
import { fetchArchitectContext } from "../api/client";
import type { ArchitectContext } from "../types";

/** Polling interval when architect is active (ms) */
const CONTEXT_POLL_MS = 15_000;

interface ConversationContextPanelProps {
  /** The project to query context for */
  projectId: string;
  /** Whether the Architect agent is currently active */
  architectActive: boolean;
}

/**
 * ConversationContextPanel — Architect context visibility.
 *
 * Renders a card showing:
 * - Token usage progress bar with percentage
 * - Summary counts (chat messages, jobs, tasks, governance reports)
 * - Last-updated relative time
 * - Empty state when no Architect is running
 */
export function ConversationContextPanel({
  projectId,
  architectActive,
}: ConversationContextPanelProps) {
  const [context, setContext] = useState<ArchitectContext | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  /** Fetch context data — silently handles errors */
  const loadContext = useCallback(async () => {
    try {
      const data = await fetchArchitectContext(projectId);
      setContext(data);
      setError(false);
    } catch {
      // Endpoint may not exist yet (Dev B) or no active architect — show empty state
      setContext(null);
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  // Fetch on mount and when architect status changes
  useEffect(() => {
    if (architectActive) {
      void loadContext();
    } else {
      setLoading(false);
      setContext(null);
    }
  }, [architectActive, loadContext]);

  // Poll when architect is active
  useEffect(() => {
    if (architectActive) {
      intervalRef.current = setInterval(() => void loadContext(), CONTEXT_POLL_MS);
    }
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [architectActive, loadContext]);

  /** Format a relative time string from ISO timestamp */
  function formatRelativeTime(iso: string): string {
    const diffMs = Date.now() - new Date(iso).getTime();
    const seconds = Math.floor(diffMs / 1000);
    if (seconds < 10) return "just now";
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    return `${hours}h ${minutes % 60}m ago`;
  }

  // Loading skeleton
  if (loading) {
    return (
      <div className="context-panel" id="context-panel">
        <div className="skeleton skeleton-card" style={{ height: 100 }} />
      </div>
    );
  }

  // Empty state — no architect running or endpoint unavailable
  if (!architectActive || !context || error) {
    return (
      <div className="context-panel context-panel--empty" id="context-panel">
        <div className="context-panel-header">
          <span className="context-panel-icon">🧠</span>
          <span className="context-panel-title">Architect Context</span>
        </div>
        <div className="context-empty-state">
          <span className="context-empty-icon">💤</span>
          <span className="context-empty-text">
            Start an Architect to see context
          </span>
        </div>
      </div>
    );
  }

  const usagePercent = context.maxTokens > 0
    ? Math.min(100, Math.round((context.tokenEstimate / context.maxTokens) * 100))
    : 0;

  const usageLevel =
    usagePercent >= 90 ? "critical" :
    usagePercent >= 70 ? "warning" :
    "normal";

  return (
    <div className="context-panel" id="context-panel">
      {/* Header */}
      <div className="context-panel-header">
        <div className="context-panel-title-row">
          <span className="context-panel-icon">🧠</span>
          <span className="context-panel-title">Architect Context</span>
        </div>
        <span className="context-panel-updated">
          {formatRelativeTime(context.lastUpdated)}
        </span>
      </div>

      {/* Token usage bar */}
      <div className="context-usage" id="context-usage">
        <div className="context-usage-header">
          <span className="context-usage-label">Context usage</span>
          <span className="context-usage-percent">{usagePercent}%</span>
        </div>
        <div className="context-progress-bar">
          <div
            className={`context-progress-fill context-progress-fill--${usageLevel}`}
            style={{ width: `${usagePercent}%` }}
          />
        </div>
        <div className="context-usage-detail">
          ~{context.tokenEstimate.toLocaleString()} / {context.maxTokens.toLocaleString()} tokens
        </div>
      </div>

      {/* Summary stats */}
      <div className="context-stats" id="context-stats">
        <div className="context-stat-item">
          <span className="context-stat-icon">💬</span>
          <span className="context-stat-value">{context.chatMessageCount}</span>
          <span className="context-stat-label">Messages</span>
        </div>
        <div className="context-stat-item">
          <span className="context-stat-icon">📦</span>
          <span className="context-stat-value">{context.activeJobCount}</span>
          <span className="context-stat-label">Active Jobs</span>
        </div>
        <div className="context-stat-item">
          <span className="context-stat-icon">✓</span>
          <span className="context-stat-value">
            {context.completedTaskCount}/{context.totalTaskCount}
          </span>
          <span className="context-stat-label">Tasks</span>
        </div>
        <div className="context-stat-item">
          <span className="context-stat-icon">📊</span>
          <span className="context-stat-value">{context.governanceReportCount}</span>
          <span className="context-stat-label">Reports</span>
        </div>
      </div>
    </div>
  );
}
