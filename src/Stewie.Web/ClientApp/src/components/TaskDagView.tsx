/**
 * TaskDagView — Visual representation of task dependency graph.
 * Arranges tasks by topological "depth" layer, with dependency arrows.
 *
 * Layout modes:
 * - All parallel (no deps): horizontal row
 * - Linear chain: vertical column with connectors
 * - DAG: layered columns by dependency depth
 *
 * REF: JOB-011 T-103
 */
import { useMemo, useCallback } from "react";
import { StatusBadge } from "./StatusBadge";
import type { WorkTask } from "../types";

/** Role icons consistent with the design system */
const ROLE_ICONS: Record<string, string> = {
  developer: "D",
  tester: "T",
  researcher: "🔬",
};

interface TaskDagViewProps {
  /** All tasks in the job */
  tasks: WorkTask[];
  /** Dependency edges: [taskId, dependsOnTaskId] pairs */
  dependencies?: Array<{ taskId: string; dependsOnTaskId: string }>;
  /** Click handler for task nodes */
  onTaskClick?: (taskId: string) => void;
}

/** Compute topological depth for each task (0 = no deps) */
function computeDepths(
  tasks: WorkTask[],
  deps: Array<{ taskId: string; dependsOnTaskId: string }>
): Map<string, number> {
  const depths = new Map<string, number>();
  const depsOf = new Map<string, string[]>();

  for (const t of tasks) {
    depsOf.set(t.id, []);
    depths.set(t.id, 0);
  }

  for (const d of deps) {
    const existing = depsOf.get(d.taskId) || [];
    existing.push(d.dependsOnTaskId);
    depsOf.set(d.taskId, existing);
  }

  // BFS to compute depths
  let changed = true;
  let iterations = 0;
  while (changed && iterations < 100) {
    changed = false;
    iterations++;
    for (const t of tasks) {
      const myDeps = depsOf.get(t.id) || [];
      if (myDeps.length === 0) continue;
      const maxParentDepth = Math.max(...myDeps.map((d) => depths.get(d) ?? 0));
      const newDepth = maxParentDepth + 1;
      if ((depths.get(t.id) ?? 0) < newDepth) {
        depths.set(t.id, newDepth);
        changed = true;
      }
    }
  }

  return depths;
}

/** Group tasks by their depth layer */
function groupByLayer(
  tasks: WorkTask[],
  depths: Map<string, number>
): WorkTask[][] {
  const maxDepth = Math.max(...Array.from(depths.values()), 0);
  const layers: WorkTask[][] = Array.from({ length: maxDepth + 1 }, () => []);

  for (const t of tasks) {
    const depth = depths.get(t.id) ?? 0;
    layers[depth].push(t);
  }

  return layers;
}

/**
 * Renders a single DAG task node with role icon, status badge, and objective.
 */
function DagNode({
  task,
  onClick,
}: {
  task: WorkTask;
  onClick?: (id: string) => void;
}) {
  const statusClass = task.status.toLowerCase();

  return (
    <div
      className={`dag-node ${statusClass}`}
      id={`dag-node-${task.id}`}
      onClick={() => onClick?.(task.id)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") onClick?.(task.id);
      }}
    >
      <div className="dag-node-header">
        <span className="dag-node-role">{ROLE_ICONS[task.role] || "?"}</span>
        <StatusBadge status={task.status} />
      </div>
      <div className="dag-node-objective">
        {task.objective || `Task ${task.id.slice(0, 8)}…`}
      </div>
    </div>
  );
}

/**
 * TaskDagView — renders tasks in the appropriate layout based on dependency structure.
 */
export function TaskDagView({ tasks, dependencies = [], onTaskClick }: TaskDagViewProps) {
  const hasDeps = dependencies.length > 0;

  const depths = useMemo(
    () => computeDepths(tasks, dependencies),
    [tasks, dependencies]
  );

  const layers = useMemo(
    () => groupByLayer(tasks, depths),
    [tasks, depths]
  );

  const isLinearChain = useMemo(() => {
    if (!hasDeps) return false;
    // Linear chain: every layer has exactly 1 task
    return layers.every((layer) => layer.length === 1);
  }, [hasDeps, layers]);

  const handleClick = useCallback(
    (id: string) => onTaskClick?.(id),
    [onTaskClick]
  );

  if (!tasks || tasks.length === 0) {
    return (
      <div className="dag-view" id="dag-view-empty">
        <div className="empty-state">
          <div className="empty-icon">--</div>
          <h3>No tasks</h3>
          <p>This job has no associated tasks.</p>
        </div>
      </div>
    );
  }

  // No dependencies — all parallel, show in a row
  if (!hasDeps) {
    return (
      <div className="dag-view" id="dag-view-parallel">
        <div className="dag-parallel-row">
          {tasks.map((task) => (
            <DagNode key={task.id} task={task} onClick={handleClick} />
          ))}
        </div>
      </div>
    );
  }

  // Linear chain — vertical column with connectors
  if (isLinearChain) {
    return (
      <div className="dag-view" id="dag-view-linear">
        <div className="dag-linear-column">
          {layers.map((layer, idx) => (
            <div key={layer[0].id}>
              <DagNode task={layer[0]} onClick={handleClick} />
              {idx < layers.length - 1 && <div className="dag-linear-connector" />}
            </div>
          ))}
        </div>
      </div>
    );
  }

  // DAG — layered columns
  return (
    <div className="dag-view" id="dag-view-dag">
      <div className="dag-layers">
        {layers.map((layer, layerIdx) => (
          <div className="dag-layer" key={`layer-${layerIdx}`}>
            <div className="dag-layer-label">Stage {layerIdx + 1}</div>
            {layer.map((task) => (
              <DagNode key={task.id} task={task} onClick={handleClick} />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
