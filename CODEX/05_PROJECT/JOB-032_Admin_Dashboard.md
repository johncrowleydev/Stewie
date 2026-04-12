---
id: JOB-032
title: "Admin System Dashboard"
type: how-to
status: FAILED
owner: architect
agents: [developer]
tags: [frontend, admin, dashboard, phase-8]
related: [JOB-030, JOB-031, PRJ-001, GOV-003, JOB-028]
created: 2026-04-12
updated: 2026-04-12
version: 2.0.0
---

> **BLUF:** ~~Admin dashboard complete.~~ **FAILED.** SystemDashboardPage was
> built with health panel, agent sessions, and activity feed, but crashed on
> unknown event types (AgentStarted/AgentTerminated). UI uses emoji icons,
> has text overflow issues, and is missing critical features: no container
> monitoring, no log viewing. Crash hotfixed but UI quality unacceptable.
> Audit verdict (VER-032) retracted.

# JOB-032: Admin System Dashboard

## Context

JOB-030 created an admin placeholder at `/admin/system`. This job replaces it with a real dashboard showing system health and operations data. All API endpoints for this already exist:

- `GET /health` — system health
- `GET /api/projects/:projectId/agents` — agent sessions
- `GET /api/projects/:projectId/architect/status` — architect status
- `GET /api/events` — recent events
- `GET /api/governance/analytics` — governance stats

## Dependencies

- **JOB-030 must be CLOSED** (admin route structure exists)
- **JOB-031 should be CLOSED** (admin sidebar links visible)

## Branch

`feature/JOB-032-admin-dashboard`

---

## Tasks

### T-540: System Health Panel

**File:** `src/pages/admin/SystemDashboardPage.tsx`

Create the page with a top section showing system health:

1. Fetch `GET /health` on mount
2. Display: API status (up/down badge), version, uptime timestamp
3. Display: RabbitMQ connection status (from health endpoint if available, or "Unknown")
4. Display: Total projects count, total jobs count (fetch from existing APIs)
5. Use `Card` and `Badge` components from `ui/`

**Acceptance criteria:**
- Health data displays on page load
- Green badge for healthy, red for unhealthy
- Handles API error gracefully (shows error card, not crash)
- `npm run build` succeeds

**Commit:** `feat(JOB-032): add system health panel (T-540)`

---

### T-541: Active Agent Sessions Panel

**File:** `src/pages/admin/SystemDashboardPage.tsx` (add section)

Add a "Active Agent Sessions" panel below health:

1. Fetch active agent sessions across all projects
2. Display in a `DataTable` with columns: Project, Role, Runtime, Status, Started
3. Empty state: "No active agent sessions"
4. Auto-refresh every 30 seconds via `setInterval`

**Acceptance criteria:**
- DataTable shows active sessions
- Empty state renders when no sessions
- Auto-refresh works without memory leaks (cleanup on unmount)
- `npm run build` succeeds

**Commit:** `feat(JOB-032): add active agent sessions panel (T-541)`

---

### T-542: Recent Activity Feed

**File:** `src/pages/admin/SystemDashboardPage.tsx` (add section)

Add a "Recent Activity" panel:

1. Fetch latest 20 events from `GET /api/events`
2. Display as a vertical timeline with event type icons, timestamps, and descriptions
3. Color-code by event type (green=completed, red=failed, blue=running, etc.)
4. Link entity IDs to their detail pages where possible

**Acceptance criteria:**
- Activity feed shows latest events
- Events are color-coded by type
- Empty state if no events
- `npm run build` succeeds

**Commit:** `feat(JOB-032): add recent activity feed (T-542)`

---

### T-543: Wire Route and Remove Placeholder

**Files:** `src/App.tsx`, `src/pages/admin/SystemDashboardPage.tsx`

1. Replace `AdminPlaceholder title="System Dashboard"` route with `<SystemDashboardPage />`
2. Verify the page renders at `/admin/system`
3. Verify non-admin redirect still works

**Acceptance criteria:**
- `/admin/system` shows the full dashboard for admin users
- Non-admin users are redirected
- `npm run build` succeeds

**Commit:** `feat(JOB-032): wire system dashboard route (T-543)`

---

## Exit Criteria

- [ ] System health panel with API status and version
- [ ] Active agent sessions DataTable with auto-refresh
- [ ] Recent activity feed with color-coded timeline
- [ ] Route wired at `/admin/system`
- [ ] All panels use `ui/` components (Card, Badge, DataTable)
- [ ] Both themes render correctly
- [ ] `npm run build` succeeds with zero errors
- [ ] One commit per task (4 commits total)
