/**
 * JobsPage — Lists all jobs with status badges and auto-refresh.
 * Polls GET /api/jobs every 5s (CON-002 §4.2).
 * Click a row to navigate to job detail page.
 */
import { useCallback } from "react";
import { useNavigate, Link } from "react-router-dom";
import { fetchJobs } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";

/** Polling interval for jobs list */
const JOBS_POLL_MS = 5000;

/** Jobs list page with auto-refresh and navigation */
export function JobsPage() {
  const navigate = useNavigate();
  const fetchJobsFn = useCallback(() => fetchJobs(), []);
  const { data: jobs, loading, polling, error } = usePolling<Job[]>(
    fetchJobsFn,
    JOBS_POLL_MS
  );

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
          <h1>Jobs</h1>
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
          <h1>Jobs</h1>
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
        <h1>Jobs</h1>
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {polling && (
            <span className="live-indicator" id="jobs-live">
              <span className="live-dot" />
              Live
            </span>
          )}
          <span className="card-label">{jobList.length} total</span>
          <Link to="/jobs/new" className="btn btn-primary" id="jobs-new-job">
            + New Job
          </Link>
        </div>
      </div>

      {jobList.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📋</div>
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
