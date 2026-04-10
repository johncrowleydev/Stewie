/**
 * JobProgressPanel — Multi-task job progress display component.
 * Shows total/completed/failed/running task counts, a segmented progress bar,
 * and a per-task status list with role badges.
 *
 * For single-task jobs, collapses to a simple inline badge.
 *
 * REF: JOB-011 T-102, CON-002 v1.7.0
 */
import { StatusBadge } from "./StatusBadge";
import type { WorkTask } from "../types";

/** Role icons consistent with JobDetailPage */
const ROLE_ICONS: Record<string, string> = {
  developer: "🔧",
  tester: "🔍",
  researcher: "🔬",
};

interface JobProgressPanelProps {
  /** All tasks belonging to the job */
  tasks: WorkTask[];
  /** Optional click handler for individual task rows */
  onTaskClick?: (taskId: string) => void;
}

/**
 * Counts tasks by status category for progress display.
 */
function countByStatus(tasks: WorkTask[]) {
  let completed = 0;
  let failed = 0;
  let running = 0;
  let pending = 0;
  let blocked = 0;
  let cancelled = 0;

  for (const t of tasks) {
    switch (t.status) {
      case "Completed": completed++; break;
      case "Failed": failed++; break;
      case "Running": running++; break;
      case "Pending": pending++; break;
      case "Blocked": blocked++; break;
      case "Cancelled": cancelled++; break;
    }
  }

  return { completed, failed, running, pending, blocked, cancelled };
}

/**
 * Renders multi-task job progress with a segmented progress bar
 * and per-task status list. Collapses to a simple badge for single-task jobs.
 */
export function JobProgressPanel({ tasks, onTaskClick }: JobProgressPanelProps) {
  if (!tasks || tasks.length === 0) {
    return null;
  }

  // Single-task job — show compact badge
  if (tasks.length === 1) {
    const task = tasks[0];
    return (
      <div className="job-progress-panel" id="job-progress-single">
        <div className="progress-task-item">
          <span className="dag-node-role">{ROLE_ICONS[task.role] || "📦"}</span>
          <span className="progress-task-role">
            {task.role.charAt(0).toUpperCase() + task.role.slice(1)}
          </span>
          <StatusBadge status={task.status} />
          <span className="progress-task-objective">{task.objective}</span>
        </div>
      </div>
    );
  }

  // Multi-task job — full progress panel
  const counts = countByStatus(tasks);
  const total = tasks.length;
  const terminalCount = counts.completed + counts.failed + counts.cancelled;
  const percent = (n: number) => `${((n / total) * 100).toFixed(1)}%`;

  return (
    <div className="job-progress-panel" id="job-progress-panel">
      {/* Header with counts */}
      <div className="progress-header">
        <h3>Progress ({terminalCount} / {total})</h3>
        <div className="progress-counts">
          {counts.completed > 0 && (
            <span className="progress-count">
              <span className="count-dot green" /> {counts.completed} completed
            </span>
          )}
          {counts.failed > 0 && (
            <span className="progress-count">
              <span className="count-dot red" /> {counts.failed} failed
            </span>
          )}
          {counts.running > 0 && (
            <span className="progress-count">
              <span className="count-dot blue" /> {counts.running} running
            </span>
          )}
          {counts.pending > 0 && (
            <span className="progress-count">
              <span className="count-dot gray" /> {counts.pending} pending
            </span>
          )}
          {counts.blocked > 0 && (
            <span className="progress-count">
              <span className="count-dot gray" /> {counts.blocked} blocked
            </span>
          )}
          {counts.cancelled > 0 && (
            <span className="progress-count">
              <span className="count-dot amber" /> {counts.cancelled} cancelled
            </span>
          )}
        </div>
      </div>

      {/* Segmented progress bar */}
      <div className="progress-bar-container" id="progress-bar">
        {counts.completed > 0 && (
          <div
            className="progress-bar-segment completed"
            style={{ width: percent(counts.completed) }}
          />
        )}
        {counts.failed > 0 && (
          <div
            className="progress-bar-segment failed"
            style={{ width: percent(counts.failed) }}
          />
        )}
        {counts.running > 0 && (
          <div
            className="progress-bar-segment running"
            style={{ width: percent(counts.running) }}
          />
        )}
        {counts.cancelled > 0 && (
          <div
            className="progress-bar-segment cancelled"
            style={{ width: percent(counts.cancelled) }}
          />
        )}
      </div>

      {/* Per-task status list */}
      <div className="progress-task-list" id="progress-task-list">
        {tasks.map((task) => (
          <div
            key={task.id}
            className="progress-task-item"
            id={`progress-task-${task.id}`}
            onClick={() => onTaskClick?.(task.id)}
            style={{ cursor: onTaskClick ? "pointer" : undefined }}
          >
            <span className="dag-node-role">{ROLE_ICONS[task.role] || "📦"}</span>
            <span className="progress-task-role">
              {task.role.charAt(0).toUpperCase() + task.role.slice(1)}
            </span>
            <StatusBadge status={task.status} />
            <span className="progress-task-objective">
              {task.objective || `Task ${task.id.slice(0, 8)}…`}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
