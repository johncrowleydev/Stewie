/**
 * RunsPage — Lists all runs with status badges and auto-refresh.
 * Polls GET /api/runs every 5s (CON-002 §4.2).
 * Click a row to navigate to run detail page.
 */
import { useCallback } from "react";
import { useNavigate, Link } from "react-router-dom";
import { fetchRuns } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Run } from "../types";
import { usePolling } from "../hooks/usePolling";

/** Polling interval for runs list */
const RUNS_POLL_MS = 5000;

/** Runs list page with auto-refresh and navigation */
export function RunsPage() {
  const navigate = useNavigate();
  const fetchRunsFn = useCallback(() => fetchRuns(), []);
  const { data: runs, loading, polling, error } = usePolling<Run[]>(
    fetchRunsFn,
    RUNS_POLL_MS
  );

  const runList = runs ?? [];

  /** Format an ISO date string to a human-readable local string */
  function formatDate(dateStr: string | null): string {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString();
  }

  if (loading) {
    return (
      <div>
        <div className="page-title-row">
          <h1>Runs</h1>
        </div>
        <div className="card">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="skeleton skeleton-row" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !runs) {
    return (
      <div>
        <div className="page-title-row">
          <h1>Runs</h1>
        </div>
        <div className="error-state">
          <h3>Failed to load runs</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="runs-page">
      <div className="page-title-row">
        <h1>Runs</h1>
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {polling && (
            <span className="live-indicator" id="runs-live">
              <span className="live-dot" />
              Live
            </span>
          )}
          <span className="card-label">{runList.length} total</span>
          <Link to="/runs/new" className="btn btn-primary" id="runs-new-run">
            + New Run
          </Link>
        </div>
      </div>

      {runList.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📋</div>
          <h3>No runs found</h3>
          <p>Runs will appear here once orchestration begins.</p>
        </div>
      ) : (
        <div className="card">
          <table className="data-table" id="runs-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Run ID</th>
                <th>Project</th>
                <th>Created</th>
                <th>Completed</th>
                <th>Tasks</th>
              </tr>
            </thead>
            <tbody>
              {runList.map((run) => (
                <tr
                  key={run.id}
                  className="clickable"
                  onClick={() => { void navigate(`/runs/${run.id}`); }}
                  id={`run-row-${run.id}`}
                >
                  <td><StatusBadge status={run.status} /></td>
                  <td className="mono">{run.id.slice(0, 8)}…</td>
                  <td className="mono">{run.projectId ? run.projectId.slice(0, 8) + "…" : "—"}</td>
                  <td>{formatDate(run.createdAt)}</td>
                  <td>{formatDate(run.completedAt)}</td>
                  <td>{run.tasks?.length ?? 0}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
