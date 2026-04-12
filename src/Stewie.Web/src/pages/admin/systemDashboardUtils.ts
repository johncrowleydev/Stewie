/**
 * SystemDashboard utilities — shared constants, types, and helper functions
 * for the Admin System Dashboard page.
 *
 * Extracted to keep `SystemDashboardPage.tsx` under the 500-line GOV-003 §4.1
 * limit. Contains pure functions and constant maps — no React or side effects.
 *
 * READING GUIDE FOR INCIDENT RESPONDERS:
 * 1. If event colors are wrong   → check EVENT_TYPE_VARIANT map
 * 2. If descriptions are wrong   → check describeEvent function
 * 3. If timing is wrong          → check relativeTime / formatDuration
 *
 * Used by: SystemDashboardPage.tsx
 * Related: types/index.ts, GOV-003 §4.1
 *
 * REF: JOB-032 T-540, T-541, T-542
 */

import type { Event, AgentSession } from "../../types";
import type { BadgeVariant } from "../../components/ui/Badge";
import type { Column } from "../../components/ui";

// ── Constants ──

/** Auto-refresh interval for health data (30 seconds). */
export const HEALTH_REFRESH_MS = 30_000;

/** Auto-refresh interval for agent sessions (30 seconds). */
export const SESSIONS_REFRESH_MS = 30_000;

/** Number of recent events to display. */
export const ACTIVITY_FEED_LIMIT = 20;

// ── Style Maps ──

/**
 * Stat card style map — variant name → semi-transparent bg + text color.
 *
 * DECISION: Using inline styles with CSS custom properties rather than
 * Tailwind classes because the bg needs rgba transparency that isn't
 * available as a ds-* token.
 * TRADEOFF: Slightly less "pure Tailwind" but guarantees theme awareness.
 */
export const STAT_STYLES = {
  green: { bg: "rgba(111, 172, 80, 0.15)", text: "var(--color-completed)" },
  blue: { bg: "rgba(59, 130, 246, 0.15)", text: "var(--color-running)" },
  red: { bg: "rgba(229, 72, 77, 0.15)", text: "var(--color-failed)" },
  gray: { bg: "rgba(139, 141, 147, 0.15)", text: "var(--color-pending)" },
} as const;

/**
 * Identifier for which SVG icon component to render for an event type.
 * The actual React component is resolved in SystemDashboardPage.tsx
 * to keep this file free of JSX/React imports.
 */
export type EventIconId = "plus" | "play" | "check" | "x" | "gear" | "refresh" | "stop" | "question";

/** Configuration for an event type's visual representation. */
export interface EventTypeConfig {
  /** Badge variant to use. */
  variant: BadgeVariant;
  /** Icon identifier to render in the timeline dot. Resolved to SVG in SystemDashboardPage. */
  icon: EventIconId;
}

/**
 * Event type → Badge variant mapping.
 *
 * Uses a plain object rather than Record<EventType, ...> because the API
 * may return event types not yet in the frontend TypeScript union
 * (e.g. AgentStarted, AgentTerminated). Partial + fallback prevents
 * runtime crashes on unknown event types.
 */
export const EVENT_TYPE_VARIANT: Record<string, EventTypeConfig> = {
  JobCreated: { variant: "info", icon: "plus" },
  JobStarted: { variant: "running", icon: "play" },
  JobCompleted: { variant: "completed", icon: "check" },
  JobFailed: { variant: "failed", icon: "x" },
  TaskCreated: { variant: "info", icon: "plus" },
  TaskStarted: { variant: "running", icon: "play" },
  TaskCompleted: { variant: "completed", icon: "check" },
  TaskFailed: { variant: "failed", icon: "x" },
  GovernanceStarted: { variant: "warning", icon: "gear" },
  GovernancePassed: { variant: "completed", icon: "check" },
  GovernanceFailed: { variant: "failed", icon: "x" },
  GovernanceRetry: { variant: "warning", icon: "refresh" },
  AgentStarted: { variant: "running", icon: "play" },
  AgentTerminated: { variant: "pending", icon: "stop" },
} as const;

/** Fallback config for unknown event types — prevents crash on API mismatch. */
export const DEFAULT_EVENT_CONFIG: EventTypeConfig = { variant: "pending", icon: "question" };

// ── Types ──

/**
 * Agent session row — enriched with project name for display in the table.
 *
 * DECISION: Extending AgentSession with projectName rather than doing a
 * lookup in the render function. Keeps the DataTable column definitions
 * simple and avoids repeated Map lookups per render cycle.
 */
export interface SessionRow extends AgentSession {
  /** Project name resolved from the project ID. */
  projectName: string;
  /** Index signature required by DataTable<T extends Record<string, unknown>>. */
  [key: string]: unknown;
}

// ── Helper Functions ──

/**
 * Formats an ISO timestamp into a human-readable absolute time.
 *
 * @param isoTimestamp - ISO 8601 timestamp string.
 * @returns Human-readable timestamp string.
 */
export function formatTimestamp(isoTimestamp: string): string {
  const date = new Date(isoTimestamp);
  return date.toLocaleString();
}

/**
 * Computes a human-readable runtime duration from a start time.
 *
 * @param startedAt - ISO 8601 timestamp of when the session started.
 * @returns Human-readable duration string (e.g., "5m 23s").
 */
export function formatDuration(startedAt: string): string {
  const startMs = new Date(startedAt).getTime();
  const nowMs = Date.now();
  const diffSeconds = Math.max(0, Math.floor((nowMs - startMs) / 1000));
  const minutes = Math.floor(diffSeconds / 60);
  const seconds = diffSeconds % 60;
  if (minutes === 0) return `${String(seconds)}s`;
  return `${String(minutes)}m ${String(seconds)}s`;
}

/**
 * Generates a human-readable description for an event.
 *
 * @param event - The event to describe.
 * @returns A short string describing the event.
 */
export function describeEvent(event: Event): string {
  const entityLabel = `${event.entityType} ${event.entityId.slice(0, 8)}…`;
  const descriptions: Record<string, string> = {
    JobCreated: `${entityLabel} was created`,
    JobStarted: `${entityLabel} started executing`,
    JobCompleted: `${entityLabel} completed successfully`,
    JobFailed: `${entityLabel} failed`,
    TaskCreated: `${entityLabel} was created`,
    TaskStarted: `${entityLabel} started executing`,
    TaskCompleted: `${entityLabel} completed successfully`,
    TaskFailed: `${entityLabel} failed`,
    GovernanceStarted: `Governance check started for ${entityLabel}`,
    GovernancePassed: `Governance check passed for ${entityLabel}`,
    GovernanceFailed: `Governance check failed for ${entityLabel}`,
    GovernanceRetry: `Governance retry for ${entityLabel}`,
    AgentStarted: `Agent started for ${entityLabel}`,
    AgentTerminated: `Agent terminated for ${entityLabel}`,
  };
  return descriptions[event.eventType] ?? `${event.eventType} — ${entityLabel}`;
}

/**
 * Computes a human-readable relative time (e.g., "5m ago").
 *
 * @param isoTimestamp - ISO 8601 timestamp string.
 * @returns Relative time string.
 */
export function relativeTime(isoTimestamp: string): string {
  const diffMs = Date.now() - new Date(isoTimestamp).getTime();
  const seconds = Math.floor(diffMs / 1000);
  if (seconds < 60) return `${String(seconds)}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${String(minutes)}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${String(hours)}h ago`;
  const days = Math.floor(hours / 24);
  return `${String(days)}d ago`;
}

/**
 * Maps a variant name to a STAT_STYLES key for color resolution.
 *
 * @param variant - The badge variant name.
 * @returns The corresponding STAT_STYLES key.
 */
export function variantToStatStyle(variant: BadgeVariant): keyof typeof STAT_STYLES {
  if (variant === "completed") return "green";
  if (variant === "failed") return "red";
  if (variant === "running" || variant === "info") return "blue";
  return "gray";
}

// ── DataTable column definitions ──

/** These are imported as a function to avoid circular dep issues with JSX in .ts files. */
export type SessionColumnDefs = Column<SessionRow>[];
