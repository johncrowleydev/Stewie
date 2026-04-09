/**
 * RunDetailPage — Displays a single run with its tasks and events mini-timeline.
 * Fetches from GET /api/runs/{id} (CON-002 §4.2) and
 * GET /api/events?entityType=Run&entityId={id} (CON-002 §4.5).
 * Shows run metadata, tasks table, and lifecycle event timeline.
 */
import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchRun, fetchEventsByEntity } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Run, Event, EventType } from "../types";

/** Maps event types to display colors */
const EVENT_COLORS: Record<EventType, string> = {
  RunCreated: "var(--color-running)",
  RunStarted: "var(--color-warning)",
  RunCompleted: "var(--color-completed)",
  RunFailed: "var(--color-failed)",
  TaskCreated: "var(--color-running)",
  TaskStarted: "var(--color-warning)",
  TaskCompleted: "var(--color-completed)",
  TaskFailed: "var(--color-failed)",
};

/** Short labels for mini-timeline */
const EVENT_SHORT_LABELS: Record<EventType, string> = {
  RunCreated: "Created",
  RunStarted: "Started",
  RunCompleted: "Completed",
  RunFailed: "Failed",
  TaskCreated: "Task Created",
  TaskStarted: "Task Started",
  TaskCompleted: "Task Done",
  TaskFailed: "Task Failed",
};

/** Run detail page with metadata cards, tasks table, and events mini-timeline */
export function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [run, setRun] = useState<Run | null>(null);
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadData() {
      if (!id) return;
      try {
        const runData = await fetchRun(id);
        if (!cancelled) {
          setRun(runData);
          setError(null);
        }

        // Fetch events — soft dependency, don't fail the page if unavailable
        try {
          const eventData = await fetchEventsByEntity("Run", id);
          if (!cancelled) setEvents(eventData);
        } catch {
          // Events endpoint may not exist yet — ignore
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load run");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadData();
    return () => { cancelled = true; };
  }, [id]);

  /** Format an ISO date string to a human-readable local string */
  function formatDate(dateStr: string | null): string {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString();
  }

  /** Format timestamp to short time string */
  function formatShortTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
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

      {/* Events Mini-Timeline (T-026) */}
      {events.length > 0 && (
        <>
          <h2 className="section-heading">Lifecycle Events</h2>
          <div className="card" style={{ marginBottom: "var(--space-xl)" }}>
            <div className="mini-timeline" id="run-events-timeline">
              {events.map((event, idx) => (
                <div className="mini-timeline-item" key={event.id}>
                  <div
                    className="mini-timeline-dot"
                    style={{ background: EVENT_COLORS[event.eventType] }}
                  />
                  {idx < events.length - 1 && <div className="mini-timeline-line" />}
                  <div
                    className="mini-timeline-label"
                    style={{ color: EVENT_COLORS[event.eventType] }}
                  >
                    {EVENT_SHORT_LABELS[event.eventType]}
                  </div>
                  <div className="mini-timeline-time">
                    {formatShortTime(event.timestamp)}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}

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
