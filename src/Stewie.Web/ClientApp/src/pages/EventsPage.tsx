/**
 * EventsPage — Vertical timeline of all system events.
 * Fetches from GET /api/events (CON-002 §4.5).
 * Color-coded by event type with entity type filtering.
 *
 * Soft dependency on Agent A's T-020 (Events endpoint).
 * Renders gracefully if the endpoint is not yet available.
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
          <h1>Events</h1>
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
          <h1>Events</h1>
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
        <h1>Events</h1>
        <div className="event-filters">
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
          <div className="empty-icon">📡</div>
          <h3>No events{entityFilter ? ` for ${entityFilter}s` : ""}</h3>
          <p>Events will appear here as orchestration actions occur.</p>
        </div>
      ) : (
        <div className="timeline" id="events-timeline">
          {filteredEvents.map((event) => (
            <div
              className="timeline-item"
              key={event.id}
              id={`event-${event.id}`}
            >
              <div
                className="timeline-dot"
                style={{ background: EVENT_COLORS[event.eventType] }}
              />
              <div className="timeline-connector" />
              <div className="timeline-content">
                <div className="timeline-header">
                  <span
                    className="timeline-type"
                    style={{ color: EVENT_COLORS[event.eventType] }}
                  >
                    {EVENT_LABELS[event.eventType]}
                  </span>
                  <span className="timeline-time">
                    {formatTimestamp(event.timestamp)}
                  </span>
                </div>
                <div className="timeline-meta">
                  <span className="timeline-entity-badge">
                    {event.entityType}
                  </span>
                  <span className="mono" style={{ fontSize: "var(--font-size-xs)" }}>
                    {event.entityId.slice(0, 8)}…
                  </span>
                </div>
                {event.payload && event.payload !== "{}" && (
                  <div className="timeline-payload">
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
