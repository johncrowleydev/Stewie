/**
 * EventsPage — Vertical timeline of all system events.
 * Fetches from GET /api/events (CON-002 §4.5).
 * Color-coded by event type with entity type filtering.
 *
 * Soft dependency on Agent A's T-020 (Events endpoint).
 * Renders gracefully if the endpoint is not yet available.
 *
 * REF: JOB-027 T-406
 */
import { useEffect, useState } from "react";
import { fetchEvents } from "../api/client";
import type { Event, EventType } from "../types";

/** Maps event types to display colors */
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

/** Human-readable labels for event types */
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

/** Entity type filter options */
const FILTER_OPTIONS = [
  { value: "", label: "All" },
  { value: "Job", label: "Jobs" },
  { value: "Task", label: "Tasks" },
];

/** Formats an ISO datetime to a human-readable relative or absolute string */
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

/** Events timeline page with vertical timeline and entity filter */
export function EventsPage() {
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [entityFilter, setEntityFilter] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadEvents() {
      try {
        const data = await fetchEvents(200);
        if (!cancelled) {
          setEvents(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Failed to load events"
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadEvents();
    return () => { cancelled = true; };
  }, []);

  const filteredEvents = entityFilter
    ? events.filter((e) => e.entityType === entityFilter)
    : events;

  if (loading) {
    return (
      <div>
        <div className="page-title-row">
          
        </div>
        {[1, 2, 3, 4, 5].map((i) => (
          <div key={i} className="skeleton skeleton-row" />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div>
        <div className="page-title-row">
          
        </div>
        <div className="error-state">
          <h3>Unable to load events</h3>
          <p>{error}</p>
          <p style={{ marginTop: 8, fontSize: "var(--font-size-sm)" }}>
            The events endpoint may not be deployed yet (soft dependency on Agent A).
          </p>
        </div>
      </div>
    );
  }

  return (
    <div id="events-page">
      <div className="page-title-row">
        
        <div className="flex gap-[var(--space-xs)]">
          {FILTER_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              className={`btn ${entityFilter === opt.value ? "btn-primary" : "btn-ghost"}`}
              onClick={() => setEntityFilter(opt.value)}
              id={`filter-${opt.value || "all"}`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {filteredEvents.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">--</div>
          <h3>No events{entityFilter ? ` for ${entityFilter}s` : ""}</h3>
          <p>Events will appear here as orchestration actions occur.</p>
        </div>
      ) : (
        <div className="relative pl-7" id="events-timeline">
          {filteredEvents.map((event, idx) => (
            <div
              className={`relative ${idx < filteredEvents.length - 1 ? "pb-[var(--space-lg)]" : "pb-0"}`}
              key={event.id}
              id={`event-${event.id}`}
            >
              {/* Timeline dot */}
              <div
                className="absolute -left-7 top-1 w-3 h-3 rounded-full border-2 border-[var(--color-bg)] z-[2]"
                style={{ background: EVENT_COLORS[event.eventType] }}
              />
              {/* Timeline connector */}
              {idx < filteredEvents.length - 1 && (
                <div className="absolute -left-[23px] top-4 bottom-0 w-0.5 bg-[var(--color-border)]" />
              )}
              {/* Content card */}
              <div className="bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-md)] p-[var(--space-md)] transition-[border-color] duration-150 hover:border-[var(--color-border-hover)]">
                <div className="flex items-center justify-between mb-[var(--space-xs)]">
                  <span
                    className="text-[var(--font-size-md)] font-semibold"
                    style={{ color: EVENT_COLORS[event.eventType] }}
                  >
                    {EVENT_LABELS[event.eventType]}
                  </span>
                  <span className="text-[var(--font-size-xs)] text-[var(--color-text-muted)]">
                    {formatTimestamp(event.timestamp)}
                  </span>
                </div>
                <div className="flex items-center gap-[var(--space-sm)] mt-[var(--space-xs)]">
                  <span className="inline-block px-2 py-px rounded-full text-[var(--font-size-xs)] font-medium bg-[var(--color-primary-muted)] text-[var(--color-primary)]">
                    {event.entityType}
                  </span>
                  <span className="mono" style={{ fontSize: "var(--font-size-xs)" }}>
                    {event.entityId.slice(0, 8)}…
                  </span>
                </div>
                {event.payload && event.payload !== "{}" && (
                  <div className="mt-[var(--space-sm)] p-[var(--space-sm)] bg-[var(--color-bg)] rounded-[var(--radius-sm)] font-mono text-[var(--font-size-xs)] text-[var(--color-text-muted)] break-all max-h-[100px] overflow-y-auto">
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
