/**
 * JobDetailPage — Displays a single job with task chain timeline, governance reports,
 * git info, diff viewer, and events mini-timeline.
 *
 * T-076: Task chain as vertical timeline (dev → tester → dev retry → tester)
 * T-077: Inline GovernanceReportPanel for tester tasks
 *
 * Fetches:
 * - GET /api/jobs/{id} (CON-002 §4.2)
 * - GET /api/events?entityType=Job&entityId={id} (CON-002 §4.5)
 * - GET /api/tasks/{taskId}/governance (CON-002 §4.6) — per tester task
 *
 * REF: CON-002 §5.2, §5.6, §4.6
 */
import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchJob, fetchEventsByEntity, fetchTaskGovernance } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import { GovernanceReportPanel } from "../components/GovernanceReportPanel";
import type { Job, Event, EventType, Artifact, DiffContent, GovernanceReport, WorkTask } from "../types";

/** Maps event types to display colors */
const EVENT_COLORS: Record<EventType, string> = {
  JobCreated: "var(--color-running)",
  JobStarted: "var(--color-warning)",
  JobCompleted: "var(--color-completed)",
  JobFailed: "var(--color-failed)",
  TaskCreated: "var(--color-running)",
  TaskStarted: "var(--color-warning)",
  TaskCompleted: "var(--color-completed)",
  TaskFailed: "var(--color-failed)",
  GovernanceStarted: "var(--color-warning)",
  GovernancePassed: "var(--color-completed)",
  GovernanceFailed: "var(--color-failed)",
  GovernanceRetry: "var(--color-warning)",
};

/** Short labels for mini-timeline */
const EVENT_SHORT_LABELS: Record<EventType, string> = {
  JobCreated: "Created",
  JobStarted: "Started",
  JobCompleted: "Completed",
  JobFailed: "Failed",
  TaskCreated: "Task Created",
  TaskStarted: "Task Started",
  TaskCompleted: "Task Done",
  TaskFailed: "Task Failed",
  GovernanceStarted: "Gov Started",
  GovernancePassed: "Gov Passed",
  GovernanceFailed: "Gov Failed",
  GovernanceRetry: "Gov Retry",
};

/** Role icons for the task chain timeline */
const ROLE_ICONS: Record<string, string> = {
  developer: "🔧",
  tester: "🔍",
  researcher: "🔬",
};

/**
 * Renders a color-coded diff patch — green for added lines, red for removed.
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

/**
 * Computes a human-readable duration between two ISO timestamps.
 */
function formatDuration(start: string | null, end: string | null): string {
  if (!start) return "—";
  const startMs = new Date(start).getTime();
  const endMs = end ? new Date(end).getTime() : Date.now();
  const diffSec = Math.floor((endMs - startMs) / 1000);
  if (diffSec < 60) return `${diffSec}s`;
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ${diffSec % 60}s`;
  return `${Math.floor(diffSec / 3600)}h ${Math.floor((diffSec % 3600) / 60)}m`;
}

/**
 * TaskChainTimeline — Renders the task chain as a vertical timeline.
 * Each node shows: role icon, status badge, attempt number, duration.
 * Clicking a tester node toggles the GovernanceReportPanel below it.
 */
function TaskChainTimeline({ tasks }: { tasks: WorkTask[] }) {
  const [expandedTask, setExpandedTask] = useState<string | null>(null);
  const [reports, setReports] = useState<Record<string, GovernanceReport | null>>({});
  const [reportLoading, setReportLoading] = useState<Record<string, boolean>>({});
  const [reportErrors, setReportErrors] = useState<Record<string, string | null>>({});

  /** Toggle governance report for a tester task */
  const toggleGovernance = useCallback(async (task: WorkTask) => {
    if (task.role !== "tester") return;

    const taskId = task.id;
    if (expandedTask === taskId) {
      setExpandedTask(null);
      return;
    }

    setExpandedTask(taskId);

    // Only fetch if not already cached
    if (reports[taskId] !== undefined) return;

    setReportLoading((prev) => ({ ...prev, [taskId]: true }));
    try {
      const report = await fetchTaskGovernance(taskId);
      setReports((prev) => ({ ...prev, [taskId]: report }));
      setReportErrors((prev) => ({ ...prev, [taskId]: null }));
    } catch (err) {
      setReports((prev) => ({ ...prev, [taskId]: null }));
      setReportErrors((prev) => ({
        ...prev,
        [taskId]: err instanceof Error ? err.message : "Failed to load governance report",
      }));
    } finally {
      setReportLoading((prev) => ({ ...prev, [taskId]: false }));
    }
  }, [expandedTask, reports]);

  return (
    <div className="task-chain" id="task-chain-timeline">
      {tasks.map((task, idx) => {
        const isLast = idx === tasks.length - 1;
        const isTester = task.role === "tester";
        const isExpanded = expandedTask === task.id;
        const statusClass = task.status.toLowerCase();

        return (
          <div className="task-chain-node" key={task.id} id={`chain-node-${task.id}`}>
            {/* Connector line */}
            {!isLast && <div className="task-chain-connector" />}

            {/* Node dot */}
            <div className={`task-chain-dot task-chain-dot--${statusClass}`}>
              <span className="task-chain-dot-icon">{ROLE_ICONS[task.role] || "📦"}</span>
            </div>

            {/* Node content */}
            <div
              className={`task-chain-content ${isTester ? "task-chain-content--clickable" : ""}`}
              onClick={() => { if (isTester) void toggleGovernance(task); }}
              role={isTester ? "button" : undefined}
              tabIndex={isTester ? 0 : undefined}
              aria-expanded={isTester ? isExpanded : undefined}
            >
              <div className="task-chain-header">
                <span className="task-chain-role">
                  {task.role.charAt(0).toUpperCase() + task.role.slice(1)}
                </span>
                <StatusBadge status={task.status} />
                {(task.attemptNumber ?? 1) > 1 && (
                  <span className="task-chain-attempt">
                    Attempt {task.attemptNumber}
                  </span>
                )}
                {isTester && (
                  <span className="task-chain-expand-hint">
                    {isExpanded ? "▼ Hide Report" : "▶ View Report"}
                  </span>
                )}
              </div>
              <div className="task-chain-meta">
                <span className="mono task-chain-id">{task.id.slice(0, 8)}…</span>
                <span className="task-chain-duration">
                  {formatDuration(task.startedAt, task.completedAt)}
                </span>
                {task.objective && (
                  <span className="task-chain-objective">{task.objective}</span>
                )}
              </div>
            </div>

            {/* Governance Report Panel (expands below tester nodes) */}
            {isTester && isExpanded && (
              <div className="task-chain-governance">
                <GovernanceReportPanel
                  report={reports[task.id] ?? null}
                  loading={reportLoading[task.id]}
                  error={reportErrors[task.id]}
                />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** Job detail page with metadata cards, task chain timeline, diff viewer, and events */
export function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [job, setJob] = useState<Job | null>(null);
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [diffExpanded, setDiffExpanded] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadData() {
      if (!id) return;
      try {
        const jobData = await fetchJob(id);
        if (!cancelled) {
          setJob(jobData);
          setError(null);
        }

        // Fetch events — soft dependency
        try {
          const eventData = await fetchEventsByEntity("Job", id);
          if (!cancelled) setEvents(eventData);
        } catch {
          // Events endpoint may not exist yet
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load job");
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

  /** Try to parse a diff artifact from the job's tasks */
  function getDiffContent(): DiffContent | null {
    if (!job?.tasks) return null;
    for (const task of job.tasks) {
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

  if (error || !job) {
    return (
      <div>
        <Link to="/jobs" className="back-link">
          ← Back to Jobs
        </Link>
        <div className="error-state">
          <h3>{error?.includes("404") ? "Job not found" : "Error loading job"}</h3>
          <p>{error ?? "The requested job could not be found."}</p>
        </div>
      </div>
    );
  }

  const diffContent = getDiffContent();
  const hasTasks = job.tasks && job.tasks.length > 0;
  const hasMultipleTasks = job.tasks && job.tasks.length > 1;

  return (
    <div id="job-detail-page">
      <Link to="/jobs" className="back-link" id="back-to-jobs">
        ← Back to Jobs
      </Link>

      <div className="detail-header">
        <h1>Job</h1>
        <StatusBadge status={job.status} />
        {job.branch && (
          <span className="branch-badge" id="job-branch-badge">
            🌿 {job.branch}
          </span>
        )}
        {job.pullRequestUrl && (
          <a
            href={job.pullRequestUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="pr-badge"
            id="job-pr-badge"
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
          <div className="meta-label">Job ID</div>
          <div className="meta-value mono">{job.id}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Status</div>
          <div className="meta-value">{job.status}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Created</div>
          <div className="meta-value">{formatDate(job.createdAt)}</div>
        </div>
        <div className="meta-item">
          <div className="meta-label">Completed</div>
          <div className="meta-value">{formatDate(job.completedAt)}</div>
        </div>
        {job.projectId && (
          <div className="meta-item">
            <div className="meta-label">Project ID</div>
            <div className="meta-value mono">{job.projectId}</div>
          </div>
        )}
        {job.commitSha && (
          <div className="meta-item">
            <div className="meta-label">Commit</div>
            <div className="meta-value mono">{job.commitSha.slice(0, 12)}</div>
          </div>
        )}
      </div>

      {/* Task Chain Timeline (T-076) */}
      <h2 className="section-heading">
        Task Chain ({job.tasks?.length ?? 0})
      </h2>

      {!hasTasks ? (
        <div className="empty-state">
          <div className="empty-icon">📦</div>
          <h3>No tasks</h3>
          <p>This job has no associated tasks.</p>
        </div>
      ) : hasMultipleTasks ? (
        /* Multi-task: render vertical timeline */
        <TaskChainTimeline tasks={job.tasks} />
      ) : (
        /* Single task: render compact card */
        <div className="card" style={{ marginBottom: "var(--space-xl)" }}>
          <div className="task-chain-single">
            <span className="task-chain-dot-icon">{ROLE_ICONS[job.tasks[0].role] || "📦"}</span>
            <StatusBadge status={job.tasks[0].status} />
            <span className="task-chain-role">
              {job.tasks[0].role.charAt(0).toUpperCase() + job.tasks[0].role.slice(1)}
            </span>
            <span className="mono" style={{ fontSize: "var(--font-size-xs)" }}>
              {job.tasks[0].id.slice(0, 8)}…
            </span>
            <span className="task-chain-duration">
              {formatDuration(job.tasks[0].startedAt, job.tasks[0].completedAt)}
            </span>
          </div>
        </div>
      )}

      {/* Diff Summary + Viewer */}
      {(job.diffSummary || diffContent) && (
        <>
          <h2 className="section-heading">Changes</h2>
          <div className="card" style={{ marginBottom: "var(--space-xl)" }}>
            {job.diffSummary && (
              <div className="diff-summary" id="diff-summary">
                <pre>{job.diffSummary}</pre>
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
            <div className="mini-timeline" id="job-events-timeline">
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
    </div>
  );
}
