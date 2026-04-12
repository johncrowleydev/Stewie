/**
 * DashboardPage — Overview page with summary stats and real-time updates.
 *
 * Primary: SignalR WebSocket push via "dashboard" group.
 * Fallback: Polls GET /api/jobs every 5s when WebSocket disconnects.
 *
 * Live indicator shows connection mode:
 * - Green "Live" = WebSocket connected
 * - Blue "Polling" = HTTP polling fallback
 *
 * REF: JOB-012 T-126, JOB-027 T-404
 */
import { useCallback, useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import { fetchJobs } from "../api/client";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";
import { useSignalR } from "../hooks/useSignalR";

/** Polling interval — only used as fallback when SignalR is disconnected */
const FALLBACK_POLL_MS = 5000;

/** Dashboard overview with summary statistics cards and real-time updates */
export function DashboardPage() {
  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  // Use polling only as fallback when SignalR is NOT connected
  const fetchJobsFn = useCallback(() => fetchJobs(), []);
  const { data: jobs, loading, error, refresh } = usePolling<Job[]>(
    fetchJobsFn,
    FALLBACK_POLL_MS,
    !isLive // Disable polling when WebSocket is connected
  );

  // Join/leave dashboard group when SignalR connects/disconnects
  const joinedRef = useRef(false);
  useEffect(() => {
    if (isLive && !joinedRef.current) {
      void joinGroup("dashboard");
      joinedRef.current = true;
    }

    return () => {
      if (joinedRef.current) {
        void leaveGroup("dashboard");
        joinedRef.current = false;
      }
    };
  }, [isLive, joinGroup, leaveGroup]);

  // Listen for JobUpdated events and re-fetch when received
  useEffect(() => {
    if (!isLive) return;

    const cleanup = on("JobUpdated", () => {
      refresh();
    });

    return cleanup;
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
        <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-[var(--space-lg)] mb-[var(--space-xl)]">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="skeleton skeleton-card" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !jobs) {
    return (
      <div className="error-state">
        <h3>Unable to load dashboard</h3>
        <p>{error}</p>
      </div>
    );
  }

  return (
    <div id="dashboard-page">
      <div className="flex items-center justify-between mb-[var(--space-xl)]">
        
        <div className="title-actions">
          <Link to="/jobs/new" className="btn btn-primary" id="dashboard-new-job">
            + New Job
          </Link>
        </div>
      </div>

      <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-[var(--space-lg)] mb-[var(--space-xl)]">
        <div
          className="bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-lg)] p-[var(--space-lg)] transition-all duration-150 hover:border-[var(--color-border-hover)] hover:-translate-y-0.5 hover:shadow-[var(--shadow-md)]"
        >
          <div
            className="w-10 h-10 rounded-[var(--radius-md)] flex items-center justify-center mb-[var(--space-md)] text-[1.2rem]"
            style={{ background: "rgba(59, 130, 246, 0.15)", color: "var(--color-running)" }}
          >
            B
          </div>
          <div className="text-[var(--font-size-3xl)] font-bold text-[var(--color-text)] mb-[var(--space-xs)]">{totalJobs}</div>
          <div className="text-[var(--font-size-sm)] text-[var(--color-text-muted)]">Total Jobs</div>
        </div>

        <div
          className="bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-lg)] p-[var(--space-lg)] transition-all duration-150 hover:border-[var(--color-border-hover)] hover:-translate-y-0.5 hover:shadow-[var(--shadow-md)]"
        >
          <div
            className="w-10 h-10 rounded-[var(--radius-md)] flex items-center justify-center mb-[var(--space-md)] text-[1.2rem]"
            style={{ background: "rgba(111, 172, 80, 0.15)", color: "var(--color-completed)" }}
          >
            ✓
          </div>
          <div className="text-[var(--font-size-3xl)] font-bold text-[var(--color-text)] mb-[var(--space-xs)]">{passRate}%</div>
          <div className="text-[var(--font-size-sm)] text-[var(--color-text-muted)]">Pass Rate</div>
        </div>

        <div
          className="bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-lg)] p-[var(--space-lg)] transition-all duration-150 hover:border-[var(--color-border-hover)] hover:-translate-y-0.5 hover:shadow-[var(--shadow-md)]"
        >
          <div
            className="w-10 h-10 rounded-[var(--radius-md)] flex items-center justify-center mb-[var(--space-md)] text-[1.2rem]"
            style={{ background: "rgba(229, 72, 77, 0.15)", color: "var(--color-failed)" }}
          >
            ✕
          </div>
          <div className="text-[var(--font-size-3xl)] font-bold text-[var(--color-text)] mb-[var(--space-xs)]">{failedJobs}</div>
          <div className="text-[var(--font-size-sm)] text-[var(--color-text-muted)]">Failed</div>
        </div>

        <div
          className="bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-lg)] p-[var(--space-lg)] transition-all duration-150 hover:border-[var(--color-border-hover)] hover:-translate-y-0.5 hover:shadow-[var(--shadow-md)]"
        >
          <div
            className="w-10 h-10 rounded-[var(--radius-md)] flex items-center justify-center mb-[var(--space-md)] text-[1.2rem]"
            style={{ background: "rgba(139, 141, 147, 0.15)", color: "var(--color-pending)" }}
          >
            ◉
          </div>
          <div className="text-[var(--font-size-3xl)] font-bold text-[var(--color-text)] mb-[var(--space-xs)]">{runningJobs}</div>
          <div className="text-[var(--font-size-sm)] text-[var(--color-text-muted)]">In Progress</div>
        </div>
      </div>

      {jobList.length > 0 && (
        <div className="card">
          <div className="card-header">
            <span className="card-title">Recent Jobs</span>
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Job ID</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {jobList.slice(0, 5).map((job) => (
                <tr key={job.id}>
                  <td>
                    <span className={`status-badge ${job.status.toLowerCase()}`}>
                      <span className="status-dot" />
                      {job.status}
                    </span>
                  </td>
                  <td className="mono">{job.id.slice(0, 8)}…</td>
                  <td>{new Date(job.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {jobList.length === 0 && (
        <div className="empty-state">
          <div className="empty-icon">~</div>
          <h3>No jobs yet</h3>
          <p>Create your first job to see orchestration data here.</p>
        </div>
      )}
    </div>
  );
}
