/**
 * SystemDashboardPage — Admin system dashboard showing health status,
 * agent sessions, and recent activity.
 *
 * **Design decisions:**
 * - Guard clause exits early for loading/error states (GOV-003 §5.2).
 * - Health data fetched on mount with graceful error handling — the page
 *   displays an error card rather than crashing on API failure.
 * - All colors use ds-* design tokens for theme consistency.
 * - Uses `Card` and `Badge` from the ui/ component library.
 * - `data-testid` attributes on all interactive/data elements (GOV-003 §8.4).
 * - Semantic `<article>` and `<section>` elements for accessibility (GOV-003 §8.3).
 *
 * READING GUIDE FOR INCIDENT RESPONDERS:
 * 1. If health panel shows stale data     → check useEffect fetch in HealthPanel
 * 2. If agent sessions table is empty     → check fetchAgentSessions / API response
 * 3. If activity feed is broken           → check fetchEvents API call
 * 4. If page crashes on load              → check error boundaries in guard clauses
 *
 * Used by: App.tsx (route: /admin/system)
 * Related: api/client.ts, types/index.ts, GOV-003 §8
 *
 * REF: JOB-032 T-540, T-541, T-542
 *
 * @example
 * ```tsx
 * <Route path="system" element={<SystemDashboardPage />} />
 * ```
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchHealth, fetchProjects, fetchJobs, getAgentSessions } from "../../api/client";
import { Card, Badge, DataTable } from "../../components/ui";
import { skeleton } from "../../tw";
import type { HealthResponse, AgentSession, Project } from "../../types";
import type { Column } from "../../components/ui";

/** Auto-refresh interval for health data (30 seconds). */
const HEALTH_REFRESH_MS = 30_000;

/** Auto-refresh interval for agent sessions (30 seconds). */
const SESSIONS_REFRESH_MS = 30_000;

/**
 * Stat card style map — variant name → semi-transparent bg + text color.
 *
 * DECISION: Using inline styles with CSS custom properties rather than
 * Tailwind classes because the bg needs rgba transparency that isn't
 * available as a ds-* token.
 * TRADEOFF: Slightly less "pure Tailwind" but guarantees theme awareness.
 */
const STAT_STYLES = {
  green: { bg: "rgba(111, 172, 80, 0.15)", text: "var(--color-completed)" },
  blue: { bg: "rgba(59, 130, 246, 0.15)", text: "var(--color-running)" },
  red: { bg: "rgba(229, 72, 77, 0.15)", text: "var(--color-failed)" },
  gray: { bg: "rgba(139, 141, 147, 0.15)", text: "var(--color-pending)" },
} as const;

/** Props for the internal StatTile component. */
interface StatTileProps {
  /** Display icon (emoji or text). */
  icon: string;
  /** Formatted display value. */
  value: string | number;
  /** Label below the value. */
  label: string;
  /** Color variant controlling the icon background. */
  color: keyof typeof STAT_STYLES;
}

/**
 * Renders a single metric tile with icon, value, and label.
 *
 * @returns A styled `<article>` element.
 */
function StatTile({ icon, value, label, color }: StatTileProps) {
  const style = STAT_STYLES[color];
  return (
    <article
      className="bg-ds-surface border border-ds-border rounded-lg p-lg
                 transition-all duration-150
                 hover:border-ds-border-hover hover:-translate-y-0.5
                 hover:shadow-ds-md"
      aria-label={`${label}: ${value}`}
      data-testid={`stat-tile-${label.toLowerCase().replace(/\s+/g, "-")}`}
    >
      <div
        className="w-10 h-10 rounded-md flex items-center justify-center
                   mb-md text-[1.2rem]"
        style={{ background: style.bg, color: style.text }}
        aria-hidden="true"
      >
        {icon}
      </div>
      <div className="text-3xl font-bold text-ds-text mb-xs">{value}</div>
      <div className="text-s text-ds-text-muted">{label}</div>
    </article>
  );
}

/**
 * Formats an ISO timestamp into a human-readable relative or absolute time.
 *
 * @param isoTimestamp - ISO 8601 timestamp string.
 * @returns Human-readable timestamp string.
 */
function formatTimestamp(isoTimestamp: string): string {
  const date = new Date(isoTimestamp);
  return date.toLocaleString();
}

/**
 * SystemDashboardPage — Main admin dashboard with system health,
 * agent sessions, and recent activity panels.
 */
/**
 * Agent session row — enriched with project name for display in the table.
 *
 * DECISION: Extending AgentSession with projectName rather than doing a
 * lookup in the render function. Keeps the DataTable column definitions
 * simple and avoids repeated Map lookups per render cycle.
 */
interface SessionRow extends AgentSession {
  /** Project name resolved from the project ID. */
  projectName: string;
  /** Index signature required by DataTable<T extends Record<string, unknown>>. */
  [key: string]: unknown;
}

/**
 * Computes a human-readable runtime duration from a start time.
 *
 * @param startedAt - ISO 8601 timestamp of when the session started.
 * @returns Human-readable duration string (e.g., "5m 23s").
 */
function formatDuration(startedAt: string): string {
  const startMs = new Date(startedAt).getTime();
  const nowMs = Date.now();
  const diffSeconds = Math.max(0, Math.floor((nowMs - startMs) / 1000));
  const minutes = Math.floor(diffSeconds / 60);
  const seconds = diffSeconds % 60;
  if (minutes === 0) return `${String(seconds)}s`;
  return `${String(minutes)}m ${String(seconds)}s`;
}

/** Column definitions for the agent sessions DataTable. */
const SESSION_COLUMNS: Column<SessionRow>[] = [
  { key: "projectName", header: "Project" },
  {
    key: "role",
    header: "Role",
    render: (row) => (
      <Badge
        variant={row.role === "architect" ? "info" : "pending"}
        size="sm"
        dot={false}
      >
        {row.role}
      </Badge>
    ),
  },
  {
    key: "runtimeName",
    header: "Runtime",
    render: (row) => (
      <span className="font-mono text-xs text-ds-text-muted">{row.runtimeName}</span>
    ),
  },
  {
    key: "status",
    header: "Status",
    render: (row) => {
      const variant = row.status === "Running" ? "running"
        : row.status === "Completed" ? "completed"
        : row.status === "Failed" ? "failed"
        : "pending";
      return <Badge variant={variant} size="sm">{row.status}</Badge>;
    },
  },
  {
    key: "startedAt",
    header: "Running For",
    render: (row) => (
      <span className="text-s text-ds-text-muted">
        {formatDuration(row.startedAt)}
      </span>
    ),
  },
];

export function SystemDashboardPage() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [healthLoading, setHealthLoading] = useState(true);
  const [totalProjects, setTotalProjects] = useState(0);
  const [totalJobs, setTotalJobs] = useState(0);

  /* Agent sessions state */
  const [sessions, setSessions] = useState<SessionRow[]>([]);
  const [sessionsLoading, setSessionsLoading] = useState(true);
  const [projectsCache, setProjectsCache] = useState<Project[]>([]);

  /**
   * Fetch all health-related data in parallel.
   *
   * FAILURE MODE: If any fetch fails, the error is captured in healthError
   * and displayed as an error card. Other data that succeeded still renders.
   * BLAST RADIUS: Only the system health panel is affected.
   */
  const loadHealthData = useCallback(async () => {
    try {
      const [healthResult, projects, jobs] = await Promise.all([
        fetchHealth(),
        fetchProjects(),
        fetchJobs(),
      ]);
      setHealth(healthResult);
      setTotalProjects(projects.length);
      setTotalJobs(jobs.length);
      setProjectsCache(projects);
      setHealthError(null);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to fetch health data";
      setHealthError(message);
    } finally {
      setHealthLoading(false);
    }
  }, []);

  /**
   * Fetch active agent sessions across all projects.
   *
   * FAILURE MODE: If any project's session fetch fails, it's silently skipped.
   * Remaining projects still display correctly.
   * BLAST RADIUS: Only the sessions panel is affected.
   */
  const loadSessions = useCallback(async (projects: Project[]) => {
    try {
      const allSessions: SessionRow[] = [];
      const projectMap = new Map(projects.map((p) => [p.id, p.name]));

      /* Fetch sessions per project in parallel. */
      const results = await Promise.allSettled(
        projects.map((p) => getAgentSessions(p.id))
      );

      for (let i = 0; i < results.length; i++) {
        const result = results[i];
        if (result.status === "fulfilled") {
          const projectName = projectMap.get(projects[i].id) ?? "Unknown";
          for (const session of result.value) {
            allSessions.push({ ...session, projectName });
          }
        }
      }

      setSessions(allSessions);
    } catch {
      /* Swallowing top-level errors — individual failures handled above. */
    } finally {
      setSessionsLoading(false);
    }
  }, []);

  /* Initial fetch + auto-refresh timer with cleanup. */
  const healthIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const sessionsIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    void loadHealthData();
    healthIntervalRef.current = setInterval(() => {
      void loadHealthData();
    }, HEALTH_REFRESH_MS);

    return () => {
      if (healthIntervalRef.current !== null) {
        clearInterval(healthIntervalRef.current);
      }
    };
  }, [loadHealthData]);

  /**
   * Load sessions once projects are available, then auto-refresh.
   * Uses projectsCache to avoid re-fetching projects on each interval.
   */
  useEffect(() => {
    if (projectsCache.length === 0) return;

    void loadSessions(projectsCache);
    sessionsIntervalRef.current = setInterval(() => {
      void loadSessions(projectsCache);
    }, SESSIONS_REFRESH_MS);

    return () => {
      if (sessionsIntervalRef.current !== null) {
        clearInterval(sessionsIntervalRef.current);
      }
    };
  }, [projectsCache, loadSessions]);

  return (
    <div id="system-dashboard-page" data-testid="system-dashboard-page">
      {/* Page header */}
      <div className="flex items-center justify-between mb-xl">
        <div>
          <h1 className="text-2xl font-bold text-ds-text">System Dashboard</h1>
          <p className="text-s text-ds-text-muted mt-xs">
            Infrastructure health and operational overview
          </p>
        </div>
        {health && (
          <Badge variant={health.status === "healthy" ? "completed" : "failed"}>
            {health.status === "healthy" ? "All Systems Operational" : "System Degraded"}
          </Badge>
        )}
      </div>

      {/* ── System Health Panel ── */}
      {healthLoading && (
        <div
          className="grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-lg mb-xl"
          role="status"
          aria-label="Loading health data"
          data-testid="health-skeleton"
        >
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className={`${skeleton} h-[140px] rounded-lg`}
              aria-hidden="true"
            />
          ))}
        </div>
      )}

      {healthError && !health && (
        <Card className="mb-xl" data-testid="health-error-card">
          <Card.Header>
            <span className="flex items-center gap-sm">
              <span aria-hidden="true">⚠</span>
              Health Check Failed
            </span>
          </Card.Header>
          <p className="text-ds-text-muted text-md">{healthError}</p>
        </Card>
      )}

      {!healthLoading && health && (
        <>
          {/* KPI tiles */}
          <section
            className="grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-lg mb-xl"
            aria-label="System health metrics"
            data-testid="health-metrics"
          >
            <StatTile
              icon="♥"
              value={health.status === "healthy" ? "Healthy" : "Unhealthy"}
              label="API Status"
              color={health.status === "healthy" ? "green" : "red"}
            />
            <StatTile
              icon="⌘"
              value={health.version}
              label="API Version"
              color="blue"
            />
            <StatTile
              icon="📁"
              value={totalProjects}
              label="Total Projects"
              color="gray"
            />
            <StatTile
              icon="⚡"
              value={totalJobs}
              label="Total Jobs"
              color="blue"
            />
          </section>

          {/* Health details card */}
          <Card className="mb-xl">
            <Card.Header>
              <span className="flex items-center gap-sm">
                <span aria-hidden="true">🔧</span>
                System Details
              </span>
            </Card.Header>
            <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-md">
              <div data-testid="health-detail-status">
                <div className="text-xs text-ds-text-muted uppercase tracking-wider mb-xs">
                  API Status
                </div>
                <div className="flex items-center gap-sm">
                  <Badge
                    variant={health.status === "healthy" ? "completed" : "failed"}
                    size="sm"
                  >
                    {health.status}
                  </Badge>
                </div>
              </div>
              <div data-testid="health-detail-version">
                <div className="text-xs text-ds-text-muted uppercase tracking-wider mb-xs">
                  Version
                </div>
                <div className="text-md text-ds-text font-mono">{health.version}</div>
              </div>
              <div data-testid="health-detail-timestamp">
                <div className="text-xs text-ds-text-muted uppercase tracking-wider mb-xs">
                  Last Check
                </div>
                <div className="text-md text-ds-text">
                  {formatTimestamp(health.timestamp)}
                </div>
              </div>
              <div data-testid="health-detail-rabbitmq">
                <div className="text-xs text-ds-text-muted uppercase tracking-wider mb-xs">
                  RabbitMQ
                </div>
                <Badge variant="info" size="sm">Unknown</Badge>
              </div>
            </div>
          </Card>
        </>
      )}

      {/* ── Active Agent Sessions Panel ── */}
      <Card className="mb-xl">
        <Card.Header>
          <span className="flex items-center gap-sm">
            <span aria-hidden="true">🤖</span>
            Active Agent Sessions
          </span>
        </Card.Header>
        <DataTable<SessionRow>
          columns={SESSION_COLUMNS}
          data={sessions}
          loading={sessionsLoading}
          skeletonRows={3}
          emptyMessage="No active agent sessions"
        />
      </Card>
    </div>
  );
}
