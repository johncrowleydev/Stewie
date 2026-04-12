/**
 * JobDetailPage — Displays a single job with task chain timeline, governance reports,
 * git info, diff viewer, and events mini-timeline.
 * REF: CON-002 §5.2, §5.6, §4.6, JOB-012 T-128, JOB-027 T-404, JOB-030 T-525
 */
import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchJob, fetchEventsByEntity, fetchTaskGovernance } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import { GovernanceReportPanel } from "../components/GovernanceReportPanel";
import { ContainerOutputPanel } from "../components/ContainerOutputPanel";
import { backButton, card, skeleton, btnGhost, sectionHeading } from "../tw";
import type { Job, Event, EventType, Artifact, DiffContent, GovernanceReport, WorkTask } from "../types";
import { useSignalR } from "../hooks/useSignalR";
import { useProject } from "../contexts/ProjectContext";

const EVENT_COLORS: Record<EventType, string> = {
  JobCreated: "var(--color-running)", JobStarted: "var(--color-warning)",
  JobCompleted: "var(--color-completed)", JobFailed: "var(--color-failed)",
  TaskCreated: "var(--color-running)", TaskStarted: "var(--color-warning)",
  TaskCompleted: "var(--color-completed)", TaskFailed: "var(--color-failed)",
  GovernanceStarted: "var(--color-warning)", GovernancePassed: "var(--color-completed)",
  GovernanceFailed: "var(--color-failed)", GovernanceRetry: "var(--color-warning)",
};

const EVENT_SHORT_LABELS: Record<EventType, string> = {
  JobCreated: "Created", JobStarted: "Started", JobCompleted: "Completed", JobFailed: "Failed",
  TaskCreated: "Task Created", TaskStarted: "Task Started", TaskCompleted: "Task Done", TaskFailed: "Task Failed",
  GovernanceStarted: "Gov Started", GovernancePassed: "Gov Passed", GovernanceFailed: "Gov Failed", GovernanceRetry: "Gov Retry",
};

const ROLE_ICONS: Record<string, string> = { developer: "D", tester: "T", researcher: "🔬" };

/* Status-specific dot styles */
const dotStatusStyles: Record<string, string> = {
  completed: "border-ds-completed shadow-[0_0_0_3px_rgba(111,172,80,0.15)]",
  failed: "border-ds-failed shadow-[0_0_0_3px_rgba(229,72,77,0.15)]",
  running: "border-ds-warning shadow-[0_0_0_3px_rgba(245,166,35,0.15)] animate-[pulse-ring_2s_ease-in-out_infinite]",
  pending: "border-ds-border opacity-60",
};

function DiffViewer({ patch }: { patch: string }) {
  const lines = patch.split("\n");
  return (
    <pre className="overflow-x-auto p-md bg-ds-bg rounded-md font-mono text-xs leading-relaxed mt-sm" id="diff-viewer">
      {lines.map((line, i) => {
        let cls = "";
        if (line.startsWith("+") && !line.startsWith("+++")) cls = "text-ds-completed bg-[rgba(111,172,80,0.08)]";
        else if (line.startsWith("-") && !line.startsWith("---")) cls = "text-ds-failed bg-[rgba(229,72,77,0.08)]";
        else if (line.startsWith("@@")) cls = "text-ds-running font-semibold";
        return <div key={i} className={cls}>{line}</div>;
      })}
    </pre>
  );
}

function formatDuration(start: string | null, end: string | null): string {
  if (!start) return "—";
  const startMs = new Date(start).getTime();
  const endMs = end ? new Date(end).getTime() : Date.now();
  const diffSec = Math.floor((endMs - startMs) / 1000);
  if (diffSec < 60) return `${diffSec}s`;
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ${diffSec % 60}s`;
  return `${Math.floor(diffSec / 3600)}h ${Math.floor((diffSec % 3600) / 60)}m`;
}

function TaskChainTimeline({ tasks, jobId }: { tasks: WorkTask[]; jobId: string }) {
  const [expandedTask, setExpandedTask] = useState<string | null>(null);
  const [reports, setReports] = useState<Record<string, GovernanceReport | null>>({});
  const [reportLoading, setReportLoading] = useState<Record<string, boolean>>({});
  const [reportErrors, setReportErrors] = useState<Record<string, string | null>>({});
  const [expandedOutput, setExpandedOutput] = useState<Record<string, boolean>>({});

  const toggleGovernance = useCallback(async (task: WorkTask) => {
    if (task.role !== "tester") return;
    const taskId = task.id;
    if (expandedTask === taskId) { setExpandedTask(null); return; }
    setExpandedTask(taskId);
    if (reports[taskId] !== undefined) return;
    setReportLoading((prev) => ({ ...prev, [taskId]: true }));
    try {
      const report = await fetchTaskGovernance(taskId);
      setReports((prev) => ({ ...prev, [taskId]: report }));
      setReportErrors((prev) => ({ ...prev, [taskId]: null }));
    } catch (err) {
      setReports((prev) => ({ ...prev, [taskId]: null }));
      setReportErrors((prev) => ({ ...prev, [taskId]: err instanceof Error ? err.message : "Failed to load governance report" }));
    } finally {
      setReportLoading((prev) => ({ ...prev, [taskId]: false }));
    }
  }, [expandedTask, reports]);

  return (
    <div className="relative py-md mb-xl" id="task-chain-timeline">
      {tasks.map((task, idx) => {
        const isLast = idx === tasks.length - 1;
        const isTester = task.role === "tester";
        const isExpanded = expandedTask === task.id;
        const statusClass = task.status.toLowerCase();

        return (
          <div className="relative flex gap-md pb-lg last:pb-0" key={task.id} id={`chain-node-${task.id}`}>
            {!isLast && <div className="absolute left-[19px] top-10 bottom-0 w-0.5 bg-ds-border opacity-50" />}
            <div className={`shrink-0 w-10 h-10 rounded-full flex items-center justify-center border-2 bg-ds-surface z-[1] transition-all duration-200 ${dotStatusStyles[statusClass] ?? dotStatusStyles.pending}`}>
              <span className="text-[18px] leading-none">{ROLE_ICONS[task.role] || "?"}</span>
            </div>
            <div
              className={`flex-1 bg-ds-surface border border-ds-border rounded-md p-md transition-all duration-200 ${isTester ? "cursor-pointer hover:border-ds-primary hover:shadow-[0_2px_8px_rgba(111,172,80,0.1)]" : ""}`}
              onClick={() => { if (isTester) void toggleGovernance(task); }}
              role={isTester ? "button" : undefined}
              tabIndex={isTester ? 0 : undefined}
              aria-expanded={isTester ? isExpanded : undefined}
            >
              <div className="flex items-center gap-sm flex-wrap">
                <span className="font-semibold text-md">{task.role.charAt(0).toUpperCase() + task.role.slice(1)}</span>
                <StatusBadge status={task.status} />
                {(task.attemptNumber ?? 1) > 1 && (
                  <span className="text-xs py-px px-2 rounded-full bg-[rgba(245,166,35,0.15)] text-ds-warning font-semibold">Attempt {task.attemptNumber}</span>
                )}
                {isTester && (
                  <span className="text-xs text-ds-text-secondary ml-auto">{isExpanded ? "▼ Hide Report" : "▶ View Report"}</span>
                )}
              </div>
              <div className="flex items-center gap-md mt-xs text-s text-ds-text-secondary flex-wrap">
                <span className="font-mono text-xs">{task.id.slice(0, 8)}…</span>
                <span className="text-xs py-px px-1.5 rounded-sm bg-ds-surface-elevated">{formatDuration(task.startedAt, task.completedAt)}</span>
                {task.objective && <span className="max-w-[300px] truncate">{task.objective}</span>}
              </div>
            </div>

            {isTester && isExpanded && (
              <div className="ml-[56px] mt-sm animate-[slideDown_0.2s_ease-out]">
                <GovernanceReportPanel report={reports[task.id] ?? null} loading={reportLoading[task.id]} error={reportErrors[task.id]} />
              </div>
            )}

            {task.status === "Running" ? (
              <ContainerOutputPanel taskId={task.id} jobId={jobId} isActive={true} />
            ) : (task.status === "Completed" || task.status === "Failed") ? (
              <>
                <button
                  className="flex items-center gap-xs text-s text-ds-text-muted mt-sm ml-[56px] bg-transparent border-none cursor-pointer font-sans transition-colors duration-150 hover:text-ds-text"
                  onClick={() => setExpandedOutput(prev => ({ ...prev, [task.id]: !prev[task.id] }))}
                  id={`toggle-output-${task.id}`}
                >
                  <span className={`transition-transform duration-150 ${expandedOutput[task.id] ? "rotate-90" : ""}`}>▶</span>
                  {expandedOutput[task.id] ? "Hide output" : "Show output"}
                </button>
                {expandedOutput[task.id] && <ContainerOutputPanel taskId={task.id} jobId={jobId} isActive={false} />}
              </>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}

export function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { projectId } = useProject();
  const [job, setJob] = useState<Job | null>(null);
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [diffExpanded, setDiffExpanded] = useState(false);

  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  const loadData = useCallback(async () => {
    if (!id) return;
    try {
      const jobData = await fetchJob(id);
      setJob(jobData); setError(null);
      try { setEvents(await fetchEventsByEntity("Job", id)); } catch { /* noop */ }
    } catch (err) { setError(err instanceof Error ? err.message : "Failed to load job"); }
    finally { setLoading(false); }
  }, [id]);

  useEffect(() => { void loadData(); }, [loadData]);

  const joinedRef = useRef(false);
  useEffect(() => {
    if (isLive && id && !joinedRef.current) { void joinGroup("job", id); joinedRef.current = true; }
    return () => { if (joinedRef.current && id) { void leaveGroup("job", id); joinedRef.current = false; } };
  }, [isLive, id, joinGroup, leaveGroup]);

  useEffect(() => {
    if (!isLive) return;
    const c1 = on("TaskUpdated", () => { void loadData(); });
    const c2 = on("JobUpdated", () => { void loadData(); });
    return () => { c1(); c2(); };
  }, [isLive, on, loadData]);

  function formatDate(dateStr: string | null): string { return dateStr ? new Date(dateStr).toLocaleString() : "—"; }
  function formatShortTime(iso: string): string { return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit" }); }

  function getDiffContent(): DiffContent | null {
    if (!job?.tasks) return null;
    for (const task of job.tasks) {
      const artifacts = (task as unknown as { artifacts?: Artifact[] }).artifacts;
      if (!artifacts) continue;
      const diffArtifact = artifacts.find((a) => a.type === "diff");
      if (diffArtifact?.contentJson) { try { return JSON.parse(diffArtifact.contentJson) as DiffContent; } catch { /* noop */ } }
    }
    return null;
  }

  if (loading) {
    return (
      <div>
        <div className={`${skeleton} w-[120px] h-[38px] mb-lg`} />
        <div className={`${skeleton} h-[120px] mb-lg`} />
        <div className={`${skeleton} h-[200px]`} />
      </div>
    );
  }

  if (error || !job) {
    return (
      <div>
        <Link to={`/p/${projectId}/jobs`} className={backButton} id="back-to-jobs">← Back to Jobs</Link>
        <div className="text-center p-2xl text-ds-text-muted bg-[rgba(229,72,77,0.08)] border border-[rgba(229,72,77,0.2)] rounded-lg">
          <h3 className="text-base font-semibold text-ds-failed mb-sm">{error?.includes("404") ? "Job not found" : "Error loading job"}</h3>
          <p className="text-md">{error ?? "The requested job could not be found."}</p>
        </div>
      </div>
    );
  }

  const diffContent = getDiffContent();
  const hasTasks = job.tasks && job.tasks.length > 0;
  const hasMultipleTasks = job.tasks && job.tasks.length > 1;

  return (
    <div id="job-detail-page">
      <Link to="/jobs" className={backButton} id="back-to-jobs">← Back to Jobs</Link>

      {/* Header row */}
      <div className="flex items-center gap-lg mb-xl">
        <StatusBadge status={job.status} />
        {job.branch && (
          <span className="inline-flex items-center gap-1.5 py-px px-2.5 rounded-full text-xs font-medium bg-ds-surface-elevated text-ds-text-muted border border-ds-border" id="job-branch-badge">
            🌿 {job.branch}
          </span>
        )}
        {job.pullRequestUrl && (
          <a href={job.pullRequestUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1.5 py-1 px-3 rounded-full text-xs font-medium bg-[rgba(111,172,80,0.12)] text-ds-primary no-underline transition-colors duration-150 hover:bg-[rgba(111,172,80,0.24)] [&_svg]:w-3.5 [&_svg]:h-3.5" id="job-pr-badge">
            <svg viewBox="0 0 16 16" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" /></svg>
            View PR
          </a>
        )}
      </div>

      {/* Metadata grid */}
      <div className="grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-md mb-xl">
        {[
          { label: "Job ID", value: job.id, mono: true },
          { label: "Status", value: job.status },
          { label: "Created", value: formatDate(job.createdAt) },
          { label: "Completed", value: formatDate(job.completedAt) },
          ...(job.projectId ? [{ label: "Project ID", value: job.projectId, mono: true }] : []),
          ...(job.commitSha ? [{ label: "Commit", value: job.commitSha.slice(0, 12), mono: true }] : []),
        ].map((m) => (
          <div key={m.label} className="bg-ds-surface border border-ds-border rounded-md p-md">
            <div className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs">{m.label}</div>
            <div className={`text-md text-ds-text break-all ${m.mono ? "font-mono text-s" : ""}`}>{m.value}</div>
          </div>
        ))}
      </div>

      {/* Task Chain */}
      <h2 className={`${sectionHeading} pb-sm border-b border-ds-border`}>Task Chain ({job.tasks?.length ?? 0})</h2>

      {!hasTasks ? (
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">--</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No tasks</h3>
          <p className="text-md">This job has no associated tasks.</p>
        </div>
      ) : hasMultipleTasks ? (
        <TaskChainTimeline tasks={job.tasks} jobId={job.id} />
      ) : (
        <div className={`${card} mb-xl`}>
          <div className="flex items-center gap-md p-md">
            <span className="text-[18px] leading-none">{ROLE_ICONS[job.tasks[0].role] || "?"}</span>
            <StatusBadge status={job.tasks[0].status} />
            <span className="font-semibold text-md">{job.tasks[0].role.charAt(0).toUpperCase() + job.tasks[0].role.slice(1)}</span>
            <span className="font-mono text-xs">{job.tasks[0].id.slice(0, 8)}…</span>
            <span className="text-xs py-px px-1.5 rounded-sm bg-ds-surface-elevated">{formatDuration(job.tasks[0].startedAt, job.tasks[0].completedAt)}</span>
          </div>
          {(job.tasks[0].status === "Running" || job.tasks[0].status === "Completed" || job.tasks[0].status === "Failed") && (
            <ContainerOutputPanel taskId={job.tasks[0].id} jobId={job.id} isActive={job.tasks[0].status === "Running"} />
          )}
        </div>
      )}

      {/* Diff */}
      {(job.diffSummary || diffContent) && (
        <>
          <h2 className={`${sectionHeading} pb-sm border-b border-ds-border`}>Changes</h2>
          <div className={`${card} mb-xl`}>
            {job.diffSummary && (
              <pre className="overflow-x-auto p-md bg-ds-bg rounded-md font-mono text-xs leading-relaxed" id="diff-summary">{job.diffSummary}</pre>
            )}
            {diffContent?.diffPatch && (
              <div>
                <button className={`${btnGhost} mt-sm`} onClick={() => setDiffExpanded(!diffExpanded)} id="toggle-diff-btn">
                  {diffExpanded ? "▼ Hide full diff" : "▶ Show full diff"}
                </button>
                {diffExpanded && <DiffViewer patch={diffContent.diffPatch} />}
              </div>
            )}
          </div>
        </>
      )}

      {/* Events */}
      {events.length > 0 && (
        <>
          <h2 className={`${sectionHeading} pb-sm border-b border-ds-border`}>Lifecycle Events</h2>
          <div className={`${card} mb-xl`}>
            <div className="flex flex-wrap gap-md" id="job-events-timeline">
              {events.map((event, idx) => (
                <div className="flex items-center gap-sm" key={event.id}>
                  <div className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: EVENT_COLORS[event.eventType] }} />
                  <div className="text-s font-medium" style={{ color: EVENT_COLORS[event.eventType] }}>{EVENT_SHORT_LABELS[event.eventType]}</div>
                  <div className="text-xs text-ds-text-muted">{formatShortTime(event.timestamp)}</div>
                  {idx < events.length - 1 && <div className="w-6 h-px bg-ds-border" />}
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
