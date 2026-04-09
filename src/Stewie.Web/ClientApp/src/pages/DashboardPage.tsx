/**
 * DashboardPage — Overview page with summary stats.
 * Fetches run data to compute stats. Handles loading, error, and empty states.
 */
import { useEffect, useState } from "react";
import { fetchRuns } from "../api/client";
import type { Run } from "../types";

/** Dashboard overview with summary statistics cards */
export function DashboardPage() {
  const [runs, setRuns] = useState<Run[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadData() {
      try {
        const data = await fetchRuns();
        if (!cancelled) {
          setRuns(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load dashboard data");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadData();
    return () => { cancelled = true; };
  }, []);

  const totalRuns = runs.length;
  const completedRuns = runs.filter((r) => r.status === "Completed").length;
  const failedRuns = runs.filter((r) => r.status === "Failed").length;
  const runningRuns = runs.filter((r) => r.status === "Running").length;
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

  if (error) {
    return (
      <div className="error-state">
        <h3>Unable to load dashboard</h3>
        <p>{error}</p>
      </div>
    );
  }

  return (
    <div id="dashboard-page">
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

      {runs.length > 0 && (
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
              {runs.slice(0, 5).map((run) => (
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

      {runs.length === 0 && (
        <div className="empty-state">
          <div className="empty-icon">🐢</div>
          <h3>No runs yet</h3>
          <p>Create your first run to see orchestration data here.</p>
        </div>
      )}
    </div>
  );
}
