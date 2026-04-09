/**
 * DashboardPage — Overview page with summary stats and auto-refresh.
 * Polls GET /api/jobs every 5s for live updates.
 * Shows "New Job" button accessible from the dashboard.
 */
import { useCallback } from "react";
import { Link } from "react-router-dom";
import { fetchJobs } from "../api/client";
import type { Job } from "../types";
import { usePolling } from "../hooks/usePolling";

/** Polling interval for dashboard data */
const DASHBOARD_POLL_MS = 5000;

/** Dashboard overview with summary statistics cards and auto-refresh */
export function DashboardPage() {
  const fetchJobsFn = useCallback(() => fetchJobs(), []);
  const { data: jobs, loading, polling, error } = usePolling<Job[]>(
    fetchJobsFn,
    DASHBOARD_POLL_MS
  );

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
        <h1>Dashboard</h1>
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          {polling && (
            <span className="live-indicator" id="dashboard-live">
              <span className="live-dot" />
              Live
            </span>
          )}
          <Link to="/jobs/new" className="btn btn-primary" id="dashboard-new-job">
            + New Job
          </Link>
        </div>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon blue">⚡</div>
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
          <div className="empty-icon">🐢</div>
          <h3>No jobs yet</h3>
          <p>Create your first job to see orchestration data here.</p>
        </div>
      )}
    </div>
  );
}
