/**
 * RunDetailPage — Displays a single run with tasks, git info, diff viewer,
 * and events mini-timeline.
 *
 * Shows: branch badge, commit SHA, diff summary, expandable color-coded diff viewer.
 * Fetches from GET /api/runs/{id} (CON-002 §4.2) and
 * GET /api/events?entityType=Run&entityId={id} (CON-002 §4.5).
 *
 * REF: CON-002 §5.2, §5.6
 */
import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchRun, fetchEventsByEntity } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Run, Event, EventType, Artifact, DiffContent } from "../types";

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

/**
 * Renders a color-coded diff patch — green for added lines, red for removed.
 * Each line is styled individually for readability.
 */
function DiffViewer({ patch }: { patch: string }) {
  const lines = patch.split("\n");
  return (
    <pre className="diff-viewer" id="diff-viewer">
      {lines.map((line, i) => {
        let className = "diff-line";
        if (line.startsWith("+") && !line.startsWith("+++")) className += " diff-add";
        else if (line.startsWith("-") && !line.startsWith("---")) className += " diff-del";
        else if (line.startsWith("@@")) className += " diff-hunk";
        return (
          <div key={i} className={className}>
            {line}
          </div>
        );
      })}
    </pre>
  );
}

/** Run detail page with metadata cards, git info, diff viewer, tasks table, and events */
export function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [run, setRun] = useState<Run | null>(null);
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [diffExpanded, setDiffExpanded] = useState(false);

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

        // Fetch events — soft dependency
        try {
          const eventData = await fetchEventsByEntity("Run", id);
          if (!cancelled) setEvents(eventData);
        } catch {
          // Events endpoint may not exist yet
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

  /** Try to parse a diff artifact from the run's tasks */
  function getDiffContent(): DiffContent | null {
    if (!run?.tasks) return null;
    for (const task of run.tasks) {
      const artifacts = (task as unknown as { artifacts?: Artifact[] }).artifacts;
      if (!artifacts) continue;
      const diffArtifact = artifacts.find((a) => a.type === "diff");
      if (diffArtifact?.contentJson) {
        try {
          return JSON.parse(diffArtifact.contentJson) as DiffContent;
        } catch {
          // Invalid JSON — ignore
        }
      }
    }
    return null;
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

  const diffContent = getDiffContent();

  return (
    <div id="run-detail-page">
      <Link to="/runs" className="back-link" id="back-to-runs">
        ← Back to Runs
      </Link>

      <div className="detail-header">
        <h1>Run</h1>
        <StatusBadge status={run.status} />
        {run.branch && (
          <span className="branch-badge" id="run-branch-badge">
            🌿 {run.branch}
          </span>
        )}
        {run.pullRequestUrl && (
          <a
            href={run.pullRequestUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="pr-badge"
            id="run-pr-badge"
          >
            <svg viewBox="0 0 16 16" fill="currentColor">
              <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
            </svg>
            View PR
          </a>
        )}
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
        {run.commitSha && (
          <div className="meta-item">
            <div className="meta-label">Commit</div>
            <div className="meta-value mono">{run.commitSha.slice(0, 12)}</div>
          </div>
        )}
      </div>

      {/* Diff Summary + Viewer (T-033) */}
      {(run.diffSummary || diffContent) && (
        <>
          <h2 className="section-heading">Changes</h2>
          <div className="card" style={{ marginBottom: "var(--space-xl)" }}>
            {run.diffSummary && (
              <div className="diff-summary" id="diff-summary">
                <pre>{run.diffSummary}</pre>
              </div>
            )}
            {diffContent?.diffPatch && (
              <div>
                <button
                  className="btn btn-ghost"
                  onClick={() => setDiffExpanded(!diffExpanded)}
                  id="toggle-diff-btn"
                  style={{ marginTop: "var(--space-sm)" }}
                >
                  {diffExpanded ? "▼ Hide full diff" : "▶ Show full diff"}
                </button>
                {diffExpanded && <DiffViewer patch={diffContent.diffPatch} />}
              </div>
            )}
          </div>
        </>
      )}

      {/* Events Mini-Timeline */}
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
                <th>Objective</th>
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
                  <td>{task.objective || "—"}</td>
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
