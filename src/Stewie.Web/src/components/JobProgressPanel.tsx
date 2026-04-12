/**
 * JobProgressPanel — Multi-task job progress display.
 * REF: JOB-011 T-102, CON-002 v1.7.0, JOB-027 T-407
 */
import { StatusBadge } from "./StatusBadge";
import { IconBeaker } from "./Icons";
import type { WorkTask } from "../types";

const ROLE_LABELS: Record<string, string> = { developer: "D", tester: "T", researcher: "R" };

const DOT_COLORS: Record<string, string> = {
  completed: "bg-ds-completed", failed: "bg-ds-failed", running: "bg-ds-running",
  pending: "bg-ds-text-muted", blocked: "bg-ds-text-muted", cancelled: "bg-ds-warning",
};

const BAR_COLORS: Record<string, string> = {
  completed: "bg-ds-completed", failed: "bg-ds-failed", running: "bg-ds-running", cancelled: "bg-ds-warning",
};

interface JobProgressPanelProps { tasks: WorkTask[]; onTaskClick?: (taskId: string) => void; }

function countByStatus(tasks: WorkTask[]) {
  let completed = 0, failed = 0, running = 0, pending = 0, blocked = 0, cancelled = 0;
  for (const t of tasks) {
    switch (t.status) { case "Completed": completed++; break; case "Failed": failed++; break; case "Running": running++; break; case "Pending": pending++; break; case "Blocked": blocked++; break; case "Cancelled": cancelled++; break; }
  }
  return { completed, failed, running, pending, blocked, cancelled };
}

export function JobProgressPanel({ tasks, onTaskClick }: JobProgressPanelProps) {
  if (!tasks || tasks.length === 0) return null;

  if (tasks.length === 1) {
    const task = tasks[0];
    return (
      <div className="bg-ds-surface border border-ds-border rounded-md p-md" id="job-progress-single">
        <div className="flex items-center gap-sm">
          <span className="text-[18px] leading-none">{task.role === "researcher" ? <IconBeaker size={18} /> : (ROLE_LABELS[task.role] || "?")}</span>
          <span className="font-semibold text-s">{task.role.charAt(0).toUpperCase() + task.role.slice(1)}</span>
          <StatusBadge status={task.status} />
          <span className="text-s text-ds-text-muted truncate">{task.objective}</span>
        </div>
      </div>
    );
  }

  const counts = countByStatus(tasks);
  const total = tasks.length;
  const terminalCount = counts.completed + counts.failed + counts.cancelled;
  const percent = (n: number) => `${((n / total) * 100).toFixed(1)}%`;

  const countEntries = [
    { key: "completed", count: counts.completed, label: "completed" },
    { key: "failed", count: counts.failed, label: "failed" },
    { key: "running", count: counts.running, label: "running" },
    { key: "pending", count: counts.pending, label: "pending" },
    { key: "blocked", count: counts.blocked, label: "blocked" },
    { key: "cancelled", count: counts.cancelled, label: "cancelled" },
  ].filter(e => e.count > 0);

  return (
    <div className="bg-ds-surface border border-ds-border rounded-md p-md" id="job-progress-panel">
      <div className="flex items-center justify-between mb-sm">
        <h3 className="text-s font-semibold m-0">Progress ({terminalCount} / {total})</h3>
        <div className="flex flex-wrap gap-sm text-xs">
          {countEntries.map(e => (
            <span key={e.key} className="flex items-center gap-1">
              <span className={`w-2 h-2 rounded-full ${DOT_COLORS[e.key] ?? "bg-ds-text-muted"}`} />
              {e.count} {e.label}
            </span>
          ))}
        </div>
      </div>

      {/* Segmented bar */}
      <div className="h-2 bg-ds-border rounded-full overflow-hidden flex mb-md" id="progress-bar">
        {(["completed", "failed", "running", "cancelled"] as const).map(key => {
          const n = counts[key];
          return n > 0 ? <div key={key} className={`h-full ${BAR_COLORS[key]} transition-all duration-300`} style={{ width: percent(n) }} /> : null;
        })}
      </div>

      {/* Task list */}
      <div className="flex flex-col gap-xs" id="progress-task-list">
        {tasks.map((task) => (
          <div
            key={task.id}
            className={`flex items-center gap-sm py-xs px-sm rounded-sm transition-colors duration-100 ${onTaskClick ? "cursor-pointer hover:bg-ds-surface-hover" : ""}`}
            id={`progress-task-${task.id}`}
            onClick={() => onTaskClick?.(task.id)}
          >
            <span className="text-[18px] leading-none">{task.role === "researcher" ? <IconBeaker size={18} /> : (ROLE_LABELS[task.role] || "?")}</span>
            <span className="font-semibold text-xs w-16">{task.role.charAt(0).toUpperCase() + task.role.slice(1)}</span>
            <StatusBadge status={task.status} />
            <span className="text-xs text-ds-text-muted truncate flex-1">{task.objective || `Task ${task.id.slice(0, 8)}…`}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
