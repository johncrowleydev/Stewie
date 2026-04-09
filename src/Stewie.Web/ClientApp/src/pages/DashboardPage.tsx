/**
 * DashboardPage — Overview page with summary stats and auto-refresh.
 * Polls GET /api/runs every 5s for live updates.
 * Shows "New Run" button accessible from the dashboard.
 */
import { useCallback } from "react";
import { Link } from "react-router-dom";
import { fetchRuns } from "../api/client";
import type { Run } from "../types";
import { usePolling } from "../hooks/usePolling";

/** Polling interval for dashboard data */
const DASHBOARD_POLL_MS = 5000;

/** Dashboard overview with summary statistics cards and auto-refresh */
export function DashboardPage() {
  const fetchRunsFn = useCallback(() => fetchRuns(), []);
  const { data: runs, loading, polling, error } = usePolling<Run[]>(
    fetchRunsFn,
    DASHBOARD_POLL_MS
  );

  const runList = runs ?? [];
  const totalRuns = runList.length;
  const completedRuns = runList.filter((r) => r.status === "Completed").length;
  const failedRuns = runList.filter((r) => r.status === "Failed").length;
  const runningRuns = runList.filter((r) => r.status === "Running").length;
  const passRate = totalRuns > 0 ? Math.round((completedRuns / totalRuns) * 100) : 0;

  if (loading) {
    return (
      <div>
        <div className="stats-grid">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="skeleton skeleton-card" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !runs) {
    return (
      <div className="error-state">
        <h3>Unable to load dashboard</h3>
        <p>{error}</p>
      </div>
    );
  }

  return (
    <div id="dashboard-page">
      <div className="page-title-row">
        <h1>Dashboard</h1>
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {polling && (
            <span className="live-indicator" id="dashboard-live">
              <span className="live-dot" />
              Live
            </span>
          )}
          <Link to="/runs/new" className="btn btn-primary" id="dashboard-new-run">
            + New Run
          </Link>
        </div>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon blue">⚡</div>
          <div className="card-value">{totalRuns}</div>
          <div className="card-label">Total Runs</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon green">✓</div>
          <div className="card-value">{passRate}%</div>
          <div className="card-label">Pass Rate</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon red">✕</div>
          <div className="card-value">{failedRuns}</div>
          <div className="card-label">Failed</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon gray">◉</div>
          <div className="card-value">{runningRuns}</div>
          <div className="card-label">In Progress</div>
        </div>
      </div>

      {runList.length > 0 && (
        <div className="card">
          <div className="card-header">
            <span className="card-title">Recent Runs</span>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Run ID</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {runList.slice(0, 5).map((run) => (
                <tr key={run.id}>
                  <td>
                    <span className={`status-badge ${run.status.toLowerCase()}`}>
                      <span className="status-dot" />
                      {run.status}
                    </span>
                  </td>
                  <td className="mono">{run.id.slice(0, 8)}…</td>
                  <td>{new Date(run.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {runList.length === 0 && (
        <div className="empty-state">
          <div className="empty-icon">🐢</div>
          <h3>No runs yet</h3>
          <p>Create your first run to see orchestration data here.</p>
        </div>
      )}
    </div>
  );
}
