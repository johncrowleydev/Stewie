/**
 * RunsPage — Lists all runs with status badges.
 * Fetches from GET /api/runs (CON-002 §4.2).
 * Click a row to navigate to run detail page.
 */
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { fetchRuns } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Run } from "../types";

/** Runs list page with sortable table and navigation */
export function RunsPage() {
  const [runs, setRuns] = useState<Run[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;

    async function loadRuns() {
      try {
        const data = await fetchRuns();
        if (!cancelled) {
          setRuns(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load runs");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadRuns();
    return () => { cancelled = true; };
  }, []);

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

  if (error) {
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
        <span className="card-label">{runs.length} total</span>
      </div>

      {runs.length === 0 ? (
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
              {runs.map((run) => (
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
