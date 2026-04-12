/**
 * JobsPage — Lists all jobs with status badges and real-time updates.
 * REF: JOB-012 T-127, CON-002 §4.2, JOB-027 T-404
 */
import { useCallback, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { fetchJobs } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";
import { useSignalR } from "../hooks/useSignalR";


const FALLBACK_POLL_MS = 5000;

export function JobsPage() {
  const navigate = useNavigate();
  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  const fetchJobsFn = useCallback(() => fetchJobs(), []);
  const { data: jobs, loading, error, refresh } = usePolling<Job[]>(fetchJobsFn, FALLBACK_POLL_MS, !isLive);

  const joinedRef = useRef(false);
  useEffect(() => {
    if (isLive && !joinedRef.current) { void joinGroup("dashboard"); joinedRef.current = true; }
    return () => { if (joinedRef.current) { void leaveGroup("dashboard"); joinedRef.current = false; } };
  }, [isLive, joinGroup, leaveGroup]);

  useEffect(() => {
    if (!isLive) return;
    return on("JobUpdated", () => { refresh(); });
  }, [isLive, on, refresh]);

  const jobList = jobs ?? [];

  function formatDate(dateStr: string | null): string {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString();
  }

  if (loading) {
    return (
      <div>
        <div className="flex items-center justify-between mb-xl" />
        <div className="bg-ds-surface border border-ds-border rounded-lg p-lg">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="h-[44px] mb-sm bg-ds-surface rounded-md animate-[shimmer_1.5s_infinite] bg-[length:200%_100%] bg-[linear-gradient(90deg,var(--color-surface)_25%,var(--color-surface-hover)_50%,var(--color-surface)_75%)]" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !jobs) {
    return (
      <div>
        <div className="flex items-center justify-between mb-xl" />
        <div className="text-center p-2xl text-ds-text-muted">
          <h3 className="text-base font-semibold text-ds-text mb-sm">Failed to load jobs</h3>
          <p className="text-md max-w-[400px] mx-auto">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="jobs-page">
      <div className="flex items-center justify-between mb-xl">
        <div />
        <span className="text-s text-ds-text-muted">{jobList.length} total</span>
      </div>

      {jobList.length === 0 ? (
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">--</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No jobs found</h3>
          <p className="text-md max-w-[400px] mx-auto">Jobs will appear here once orchestration begins.</p>
        </div>
      ) : (
        <div className="bg-ds-surface border border-ds-border rounded-lg p-lg overflow-x-auto">
          <table className="w-full border-collapse" id="jobs-table">
            <thead>
              <tr>
                {["Status", "Job ID", "Project", "Created", "Completed", "Tasks"].map((h) => (
                  <th key={h} className="text-left py-sm px-md text-xs font-semibold uppercase tracking-wider text-ds-text-muted border-b border-ds-border">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {jobList.map((job) => (
                <tr
                  key={job.id}
                  className="border-b border-ds-border last:border-b-0 cursor-pointer hover:bg-ds-surface-hover transition-colors duration-150"
                  onClick={() => { void navigate(`/jobs/${job.id}`); }}
                  id={`job-row-${job.id}`}
                >
                  <td className="p-md"><StatusBadge status={job.status} /></td>
                  <td className="p-md font-mono text-s">{job.id.slice(0, 8)}…</td>
                  <td className="p-md font-mono text-s">{job.projectId ? job.projectId.slice(0, 8) + "…" : "—"}</td>
                  <td className="p-md text-s text-ds-text-muted">{formatDate(job.createdAt)}</td>
                  <td className="p-md text-s text-ds-text-muted">{formatDate(job.completedAt)}</td>
                  <td className="p-md text-s">{job.tasks?.length ?? 0}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
