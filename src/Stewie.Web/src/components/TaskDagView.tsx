/**
 * TaskDagView — Visual representation of task dependency graph.
 * REF: JOB-011 T-103, JOB-027 T-407
 */
import { useMemo, useCallback } from "react";
import { StatusBadge } from "./StatusBadge";
import { IconBeaker } from "./Icons";
import type { WorkTask } from "../types";

const ROLE_LABELS: Record<string, string> = { developer: "D", tester: "T", researcher: "R" };

const STATUS_BORDER: Record<string, string> = {
  completed: "border-ds-completed shadow-[0_0_0_2px_rgba(111,172,80,0.12)]",
  failed: "border-ds-failed shadow-[0_0_0_2px_rgba(229,72,77,0.12)]",
  running: "border-ds-warning shadow-[0_0_0_2px_rgba(245,166,35,0.12)] animate-[pulse-ring_2s_ease-in-out_infinite]",
  pending: "border-ds-border opacity-70",
  blocked: "border-ds-border opacity-50",
  cancelled: "border-ds-text-muted opacity-60",
};

interface TaskDagViewProps {
  tasks: WorkTask[];
  dependencies?: Array<{ taskId: string; dependsOnTaskId: string }>;
  onTaskClick?: (taskId: string) => void;
}

function computeDepths(tasks: WorkTask[], deps: Array<{ taskId: string; dependsOnTaskId: string }>): Map<string, number> {
  const depths = new Map<string, number>(); const depsOf = new Map<string, string[]>();
  for (const t of tasks) { depsOf.set(t.id, []); depths.set(t.id, 0); }
  for (const d of deps) { (depsOf.get(d.taskId) || []).push(d.dependsOnTaskId); depsOf.set(d.taskId, depsOf.get(d.taskId) || []); }
  let changed = true; let iterations = 0;
  while (changed && iterations < 100) {
    changed = false; iterations++;
    for (const t of tasks) {
      const myDeps = depsOf.get(t.id) || []; if (myDeps.length === 0) continue;
      const maxParentDepth = Math.max(...myDeps.map(d => depths.get(d) ?? 0));
      const newDepth = maxParentDepth + 1;
      if ((depths.get(t.id) ?? 0) < newDepth) { depths.set(t.id, newDepth); changed = true; }
    }
  }
  return depths;
}

function groupByLayer(tasks: WorkTask[], depths: Map<string, number>): WorkTask[][] {
  const maxD = Math.max(...Array.from(depths.values()), 0);
  const layers: WorkTask[][] = Array.from({ length: maxD + 1 }, () => []);
  for (const t of tasks) layers[depths.get(t.id) ?? 0].push(t);
  return layers;
}

function DagNode({ task, onClick }: { task: WorkTask; onClick?: (id: string) => void }) {
  const statusClass = task.status.toLowerCase();
  return (
    <div
      className={`bg-ds-surface border-2 rounded-md p-sm cursor-pointer transition-all duration-150 hover:border-ds-primary hover:shadow-ds-sm ${STATUS_BORDER[statusClass] ?? STATUS_BORDER.pending}`}
      id={`dag-node-${task.id}`} onClick={() => onClick?.(task.id)} role="button" tabIndex={0}
      onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") onClick?.(task.id); }}
    >
      <div className="flex items-center gap-sm mb-xs">
        <span className="text-[18px] leading-none">{task.role === "researcher" ? <IconBeaker size={18} /> : (ROLE_LABELS[task.role] || "?")}</span>
        <StatusBadge status={task.status} />
      </div>
      <div className="text-xs text-ds-text-muted truncate max-w-[200px]">{task.objective || `Task ${task.id.slice(0, 8)}…`}</div>
    </div>
  );
}

export function TaskDagView({ tasks, dependencies = [], onTaskClick }: TaskDagViewProps) {
  const hasDeps = dependencies.length > 0;
  const depths = useMemo(() => computeDepths(tasks, dependencies), [tasks, dependencies]);
  const layers = useMemo(() => groupByLayer(tasks, depths), [tasks, depths]);
  const isLinearChain = useMemo(() => hasDeps && layers.every(l => l.length === 1), [hasDeps, layers]);
  const handleClick = useCallback((id: string) => onTaskClick?.(id), [onTaskClick]);

  if (!tasks || tasks.length === 0) {
    return (
      <div id="dag-view-empty" className="text-center p-2xl text-ds-text-muted">
        <div className="text-[3rem] mb-md opacity-30">--</div>
        <h3 className="text-base font-semibold text-ds-text mb-sm">No tasks</h3>
        <p className="text-md">This job has no associated tasks.</p>
      </div>
    );
  }

  if (!hasDeps) {
    return (
      <div id="dag-view-parallel" className="py-md">
        <div className="flex flex-wrap gap-md">{tasks.map(t => <DagNode key={t.id} task={t} onClick={handleClick} />)}</div>
      </div>
    );
  }

  if (isLinearChain) {
    return (
      <div id="dag-view-linear" className="py-md">
        <div className="flex flex-col items-center gap-0">
          {layers.map((layer, idx) => (
            <div key={layer[0].id}>
              <DagNode task={layer[0]} onClick={handleClick} />
              {idx < layers.length - 1 && <div className="w-0.5 h-6 bg-ds-border mx-auto" />}
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div id="dag-view-dag" className="py-md overflow-x-auto">
      <div className="flex gap-xl">
        {layers.map((layer, layerIdx) => (
          <div className="flex flex-col items-center gap-md min-w-[180px]" key={`layer-${layerIdx}`}>
            <div className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted">Stage {layerIdx + 1}</div>
            {layer.map(t => <DagNode key={t.id} task={t} onClick={handleClick} />)}
          </div>
        ))}
      </div>
    </div>
  );
}
