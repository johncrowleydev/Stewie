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
 * REF: JOB-012 T-126
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
  const { data: jobs, loading, polling, error, refresh } = usePolling<Job[]>(
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
        <div className="stats-grid">
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
      <div className="page-title-row">
        
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {/* Connection mode indicator */}
          {isLive ? (
            <span className="live-indicator live-indicator--ws" id="dashboard-live">
              <span className="live-dot" />
              Live
            </span>
          ) : polling ? (
            <span className="live-indicator live-indicator--poll" id="dashboard-polling">
              <span className="live-dot live-dot--poll" />
              Polling
            </span>
          ) : null}
          <Link to="/jobs/new" className="btn btn-primary" id="dashboard-new-job">
            + New Job
          </Link>
        </div>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon blue">B</div>
          <div className="card-value">{totalJobs}</div>
          <div className="card-label">Total Jobs</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon green">✓</div>
          <div className="card-value">{passRate}%</div>
          <div className="card-label">Pass Rate</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon red">✕</div>
          <div className="card-value">{failedJobs}</div>
          <div className="card-label">Failed</div>
        </div>

        <div className="stat-card">
          <div className="stat-icon gray">◉</div>
          <div className="card-value">{runningJobs}</div>
          <div className="card-label">In Progress</div>
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
