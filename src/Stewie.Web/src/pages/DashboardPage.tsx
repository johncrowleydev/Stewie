/**
 * DashboardPage — Overview page with summary stats and real-time updates.
 * REF: JOB-012 T-126, JOB-027 T-404
 */
import { useCallback, useEffect, useRef } from "react";

import { fetchJobs } from "../api/client";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";
import { useSignalR } from "../hooks/useSignalR";
import { StatusBadge } from "../components/StatusBadge";


const FALLBACK_POLL_MS = 5000;

/** Stat icon color variants */
const statIconStyles: Record<string, { bg: string; text: string }> = {
  blue:  { bg: "rgba(59, 130, 246, 0.15)", text: "var(--color-running)" },
  green: { bg: "rgba(111, 172, 80, 0.15)", text: "var(--color-completed)" },
  red:   { bg: "rgba(229, 72, 77, 0.15)", text: "var(--color-failed)" },
  gray:  { bg: "rgba(139, 141, 147, 0.15)", text: "var(--color-pending)" },
};

function StatCard({ icon, value, label, color }: { icon: string; value: string | number; label: string; color: keyof typeof statIconStyles }) {
  const s = statIconStyles[color];
  return (
    <div className="bg-ds-surface border border-ds-border rounded-lg p-lg transition-all duration-150 hover:border-ds-border-hover hover:-translate-y-0.5 hover:shadow-ds-md">
      <div className="w-10 h-10 rounded-md flex items-center justify-center mb-md text-[1.2rem]" style={{ background: s.bg, color: s.text }}>
        {icon}
      </div>
      <div className="text-3xl font-bold text-ds-text mb-xs">{value}</div>
      <div className="text-s text-ds-text-muted">{label}</div>
    </div>
  );
}

export function DashboardPage() {
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
  const totalJobs = jobList.length;
  const completedJobs = jobList.filter((j) => j.status === "Completed").length;
  const failedJobs = jobList.filter((j) => j.status === "Failed").length;
  const runningJobs = jobList.filter((j) => j.status === "Running").length;
  const passRate = totalJobs > 0 ? Math.round((completedJobs / totalJobs) * 100) : 0;

  if (loading) {
    return (
      <div>
        <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-lg mb-xl">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-[140px] bg-ds-surface rounded-lg animate-[shimmer_1.5s_infinite] bg-[length:200%_100%] bg-[linear-gradient(90deg,var(--color-surface)_25%,var(--color-surface-hover)_50%,var(--color-surface)_75%)]" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !jobs) {
    return (
      <div className="text-center p-2xl text-ds-text-muted">
        <h3 className="text-base font-semibold text-ds-text mb-sm">Unable to load dashboard</h3>
        <p className="text-md max-w-[400px] mx-auto">{error}</p>
      </div>
    );
  }

  return (
    <div id="dashboard-page">


      <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-lg mb-xl">
        <StatCard icon="B" value={totalJobs} label="Total Jobs" color="blue" />
        <StatCard icon="✓" value={`${passRate}%`} label="Pass Rate" color="green" />
        <StatCard icon="✕" value={failedJobs} label="Failed" color="red" />
        <StatCard icon="◉" value={runningJobs} label="In Progress" color="gray" />
      </div>

      {jobList.length > 0 && (
        <div className="bg-ds-surface border border-ds-border rounded-lg p-lg transition-all duration-150 hover:border-ds-border-hover hover:shadow-ds-md overflow-x-auto">
          <div className="flex items-center justify-between mb-md">
            <span className="text-md font-semibold text-ds-text">Recent Jobs</span>
          </div>
          <table className="w-full border-collapse mt-md">
            <thead>
              <tr>
                <th className="text-left py-sm px-md text-xs font-semibold uppercase tracking-wider text-ds-text-muted border-b border-ds-border">Status</th>
                <th className="text-left py-sm px-md text-xs font-semibold uppercase tracking-wider text-ds-text-muted border-b border-ds-border">Job ID</th>
                <th className="text-left py-sm px-md text-xs font-semibold uppercase tracking-wider text-ds-text-muted border-b border-ds-border">Created</th>
              </tr>
            </thead>
            <tbody>
              {jobList.slice(0, 5).map((job) => (
                <tr key={job.id} className="border-b border-ds-border last:border-b-0 hover:bg-ds-surface-hover transition-colors duration-150">
                  <td className="p-md">
                    <StatusBadge status={job.status} />
                  </td>
                  <td className="p-md font-mono text-s">{job.id.slice(0, 8)}…</td>
                  <td className="p-md text-s text-ds-text-muted">{new Date(job.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {jobList.length === 0 && (
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">~</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No jobs yet</h3>
          <p className="text-md max-w-[400px] mx-auto">Create your first job to see orchestration data here.</p>
        </div>
      )}
    </div>
  );
}
