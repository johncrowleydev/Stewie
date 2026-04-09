/**
 * RunDetailPage — Displays a single run with its tasks.
 * Fetches from GET /api/runs/{id} (CON-002 §4.2).
 * Shows run metadata and tasks table.
 */
import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchRun } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Run } from "../types";

/** Run detail page with metadata cards and tasks table */
export function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [run, setRun] = useState<Run | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadRun() {
      if (!id) return;
      try {
        const data = await fetchRun(id);
        if (!cancelled) {
          setRun(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load run");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadRun();
    return () => { cancelled = true; };
  }, [id]);

  /** Format an ISO date string to a human-readable local string */
  function formatDate(dateStr: string | null): string {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString();
  }

  if (loading) {
    return (
      <div>
        <div className="skeleton skeleton-row" style={{ width: 120, marginBottom: 24 }} />
        <div className="skeleton skeleton-card" style={{ marginBottom: 24 }} />
        <div className="skeleton skeleton-card" />
      </div>
    );
  }

  if (error || !run) {
    return (
      <div>
        <Link to="/runs" className="back-link">
          ← Back to Runs
        </Link>
        <div className="error-state">
          <h3>{error?.includes("404") ? "Run not found" : "Error loading run"}</h3>
          <p>{error ?? "The requested run could not be found."}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="run-detail-page">
      <Link to="/runs" className="back-link" id="back-to-runs">
        ← Back to Runs
      </Link>

      <div className="detail-header">
        <h1>Run</h1>
        <StatusBadge status={run.status} />
      </div>

      <div className="detail-meta">
        <div className="meta-item">
          <div className="meta-label">Run ID</div>
          <div className="meta-value mono">{run.id}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Status</div>
          <div className="meta-value">{run.status}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Created</div>
          <div className="meta-value">{formatDate(run.createdAt)}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Completed</div>
          <div className="meta-value">{formatDate(run.completedAt)}</div>
        </div>
        {run.projectId && (
          <div className="meta-item">
            <div className="meta-label">Project ID</div>
            <div className="meta-value mono">{run.projectId}</div>
          </div>
        )}
      </div>

      <h2 className="section-heading">
        Tasks ({run.tasks?.length ?? 0})
      </h2>

      {(!run.tasks || run.tasks.length === 0) ? (
        <div className="empty-state">
          <div className="empty-icon">📦</div>
          <h3>No tasks</h3>
          <p>This run has no associated tasks.</p>
        </div>
      ) : (
        <div className="card">
          <table className="data-table" id="tasks-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Task ID</th>
                <th>Role</th>
                <th>Workspace</th>
                <th>Started</th>
                <th>Completed</th>
              </tr>
            </thead>
            <tbody>
              {run.tasks.map((task) => (
                <tr key={task.id} id={`task-row-${task.id}`}>
                  <td><StatusBadge status={task.status} /></td>
                  <td className="mono">{task.id.slice(0, 8)}…</td>
                  <td>{task.role}</td>
                  <td className="mono">{task.workspacePath || "—"}</td>
                  <td>{formatDate(task.startedAt)}</td>
                  <td>{formatDate(task.completedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
