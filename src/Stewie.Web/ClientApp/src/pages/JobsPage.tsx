/**
 * JobsPage — Lists all jobs with status badges and real-time updates.
 *
 * Primary: SignalR WebSocket push via "dashboard" group (receives all job updates).
 * Fallback: Polls GET /api/jobs every 5s when WebSocket disconnects.
 *
 * REF: JOB-012 T-127, CON-002 §4.2
 */
import { useCallback, useEffect, useRef } from "react";
import { useNavigate, Link } from "react-router-dom";
import { fetchJobs } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";
import { useSignalR } from "../hooks/useSignalR";

/** Polling interval — only used as fallback */
const FALLBACK_POLL_MS = 5000;

/** Jobs list page with real-time updates and navigation */
export function JobsPage() {
  const navigate = useNavigate();
  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  const fetchJobsFn = useCallback(() => fetchJobs(), []);
  const { data: jobs, loading, polling, error, refresh } = usePolling<Job[]>(
    fetchJobsFn,
    FALLBACK_POLL_MS,
    !isLive // Disable polling when WebSocket is connected
  );

  // Join/leave dashboard group
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

  // Listen for JobUpdated events and re-fetch
  useEffect(() => {
    if (!isLive) return;
    const cleanup = on("JobUpdated", () => {
      refresh();
    });
    return cleanup;
  }, [isLive, on, refresh]);

  const jobList = jobs ?? [];

  /** Format an ISO date string to a human-readable local string */
  function formatDate(dateStr: string | null): string {
    if (!dateStr) return "—";
    return new Date(dateStr).toLocaleString();
  }

  if (loading) {
    return (
      <div>
        <div className="page-title-row">
          
        </div>
        <div className="card">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="skeleton skeleton-row" />
          ))}
        </div>
      </div>
    );
  }

  if (error && !jobs) {
    return (
      <div>
        <div className="page-title-row">
          
        </div>
        <div className="error-state">
          <h3>Failed to load jobs</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="jobs-page">
      <div className="page-title-row">
        
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {/* Connection mode indicator */}
          {isLive ? (
            <span className="live-indicator live-indicator--ws" id="jobs-live">
              <span className="live-dot" />
              Live
            </span>
          ) : polling ? (
            <span className="live-indicator live-indicator--poll" id="jobs-polling">
              <span className="live-dot live-dot--poll" />
              Polling
            </span>
          ) : null}
          <span className="card-label">{jobList.length} total</span>
          <Link to="/jobs/new" className="btn btn-primary" id="jobs-new-job">
            + New Job
          </Link>
        </div>
      </div>

      {jobList.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">--</div>
          <h3>No jobs found</h3>
          <p>Jobs will appear here once orchestration begins.</p>
        </div>
      ) : (
        <div className="card">
          <table className="data-table" id="jobs-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Job ID</th>
                <th>Project</th>
                <th>Created</th>
                <th>Completed</th>
                <th>Tasks</th>
              </tr>
            </thead>
            <tbody>
              {jobList.map((job) => (
                <tr
                  key={job.id}
                  className="clickable"
                  onClick={() => { void navigate(`/jobs/${job.id}`); }}
                  id={`job-row-${job.id}`}
                >
                  <td><StatusBadge status={job.status} /></td>
                  <td className="mono">{job.id.slice(0, 8)}…</td>
                  <td className="mono">{job.projectId ? job.projectId.slice(0, 8) + "…" : "—"}</td>
                  <td>{formatDate(job.createdAt)}</td>
                  <td>{formatDate(job.completedAt)}</td>
                  <td>{job.tasks?.length ?? 0}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
