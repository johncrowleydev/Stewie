/**
 * EventsPage — Vertical timeline of all system events.
 * REF: CON-002 §4.5, JOB-027 T-406, JOB-030 T-525
 */
import { useEffect, useState } from "react";
import { fetchEvents } from "../api/client";
import type { Event, EventType } from "../types";
import { useProject } from "../contexts/ProjectContext";

const EVENT_COLORS: Record<EventType, string> = {
  JobCreated: "var(--color-running)",
  JobStarted: "var(--color-warning)",
  JobCompleted: "var(--color-completed)",
  JobFailed: "var(--color-failed)",
  TaskCreated: "var(--color-running)",
  TaskStarted: "var(--color-warning)",
  TaskCompleted: "var(--color-completed)",
  TaskFailed: "var(--color-failed)",
  GovernanceStarted: "var(--color-warning)",
  GovernancePassed: "var(--color-completed)",
  GovernanceFailed: "var(--color-failed)",
  GovernanceRetry: "var(--color-warning)",
};

const EVENT_LABELS: Record<EventType, string> = {
  JobCreated: "Job Created",
  JobStarted: "Job Started",
  JobCompleted: "Job Completed",
  JobFailed: "Job Failed",
  TaskCreated: "Task Created",
  TaskStarted: "Task Started",
  TaskCompleted: "Task Completed",
  TaskFailed: "Task Failed",
  GovernanceStarted: "Governance Started",
  GovernancePassed: "Governance Passed",
  GovernanceFailed: "Governance Failed",
  GovernanceRetry: "Governance Retry",
};

const FILTER_OPTIONS = [
  { value: "", label: "All" },
  { value: "Job", label: "Jobs" },
  { value: "Task", label: "Tasks" },
];

function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return "just now";
  if (diffMin < 60) return `${diffMin}m ago`;
  if (diffMin < 1440) return `${Math.floor(diffMin / 60)}h ago`;
  return date.toLocaleString();
}

export function EventsPage() {
  // projectId available for future project-scoped API filtering
  const { projectId: _projectId } = useProject();
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [entityFilter, setEntityFilter] = useState("");

  useEffect(() => {
    let cancelled = false;
    async function loadEvents() {
      try {
        const data = await fetchEvents(200);
        if (!cancelled) { setEvents(data); setError(null); }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load events");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void loadEvents();
    return () => { cancelled = true; };
  }, []);

  const filteredEvents = entityFilter ? events.filter((e) => e.entityType === entityFilter) : events;

  if (loading) {
    return (
      <div>
        <div className="flex items-center justify-between mb-xl" />
        {[1, 2, 3, 4, 5].map((i) => (
          <div key={i} className="h-[44px] mb-sm bg-ds-surface rounded-md animate-[shimmer_1.5s_infinite] bg-[length:200%_100%] bg-[linear-gradient(90deg,var(--color-surface)_25%,var(--color-surface-hover)_50%,var(--color-surface)_75%)]" />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div>
        <div className="flex items-center justify-between mb-xl" />
        <div className="text-center p-2xl text-ds-text-muted">
          <h3 className="text-base font-semibold text-ds-text mb-sm">Unable to load events</h3>
          <p className="text-md">{error}</p>
          <p className="text-s mt-sm">The events endpoint may not be deployed yet.</p>
        </div>
      </div>
    );
  }

  return (
    <div id="events-page">
      <div className="flex items-center justify-between mb-xl">
        <div />
        <div className="flex gap-xs">
          {FILTER_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              className={`inline-flex items-center gap-sm py-sm px-md rounded-md text-md font-medium font-sans cursor-pointer transition-all duration-150 ${
                entityFilter === opt.value
                  ? "border border-ds-primary bg-ds-primary text-white hover:bg-ds-primary-hover dark:bg-transparent dark:text-ds-primary dark:hover:bg-[rgba(111,172,80,0.15)]"
                  : "border border-ds-border bg-transparent text-ds-text-muted hover:bg-ds-surface-hover hover:text-ds-text"
              }`}
              onClick={() => setEntityFilter(opt.value)}
              id={`filter-${opt.value || "all"}`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {filteredEvents.length === 0 ? (
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">--</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No events{entityFilter ? ` for ${entityFilter}s` : ""}</h3>
          <p className="text-md max-w-[400px] mx-auto">Events will appear here as orchestration actions occur.</p>
        </div>
      ) : (
        <div className="relative pl-7" id="events-timeline">
          {filteredEvents.map((event, idx) => (
            <div className={`relative ${idx < filteredEvents.length - 1 ? "pb-lg" : "pb-0"}`} key={event.id} id={`event-${event.id}`}>
              <div className="absolute -left-7 top-1 w-3 h-3 rounded-full border-2 border-ds-bg z-[2]" style={{ background: EVENT_COLORS[event.eventType] }} />
              {idx < filteredEvents.length - 1 && (
                <div className="absolute -left-[23px] top-4 bottom-0 w-0.5 bg-ds-border" />
              )}
              <div className="bg-ds-surface border border-ds-border rounded-md p-md transition-[border-color] duration-150 hover:border-ds-border-hover">
                <div className="flex items-center justify-between mb-xs">
                  <span className="text-md font-semibold" style={{ color: EVENT_COLORS[event.eventType] }}>
                    {EVENT_LABELS[event.eventType]}
                  </span>
                  <span className="text-xs text-ds-text-muted">{formatTimestamp(event.timestamp)}</span>
                </div>
                <div className="flex items-center gap-sm mt-xs">
                  <span className="inline-block px-2 py-px rounded-full text-xs font-medium bg-ds-primary-muted text-ds-primary">
                    {event.entityType}
                  </span>
                  <span className="font-mono text-xs">{event.entityId.slice(0, 8)}…</span>
                </div>
                {event.payload && event.payload !== "{}" && (
                  <div className="mt-sm p-sm bg-ds-bg rounded-sm font-mono text-xs text-ds-text-muted break-all max-h-[100px] overflow-y-auto">
                    {event.payload}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
