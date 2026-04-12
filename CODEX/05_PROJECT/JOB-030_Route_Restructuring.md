---
id: JOB-030
title: "Route Restructuring + ProjectContext"
type: planning
status: CLOSED
owner: architect
agents: [developer]
tags: [frontend, routing, architecture, phase-8]
related: [PRJ-001, GOV-003, JOB-028]
created: 2026-04-12
updated: 2026-04-12
version: 1.0.0
---

> **BLUF:** Restructure the flat React Router config into a hierarchical layout: project-scoped routes (`/p/:projectId/*`), admin routes (`/admin/*`), and global routes (`/projects`, `/settings`). Add a `ProjectContext` provider to track the active project. Add an `AdminRoute` guard. No visual changes — this is pure plumbing.

# JOB-030: Route Restructuring + ProjectContext

## Context

Currently all routes are flat siblings under a single `<Layout>`:
```
/           → DashboardPage
/jobs       → JobsPage
/jobs/:id   → JobDetailPage
/projects   → ProjectsPage
/projects/:id → ProjectDetailPage
/events     → EventsPage
/settings   → SettingsPage
```

This doesn't support:
- **Project scoping** — jobs, events, and chat belong to a specific project, but there's no way to indicate which project is "active" in the URL
- **Admin separation** — invite codes and user management are crammed into SettingsPage behind `isAdmin` conditionals instead of living in a proper admin section
- **Future admin dashboard** (JOB-032) has no route to live under

### Target Route Structure

```
/login                          → LoginPage      (public)
/register                       → RegisterPage   (public)

/projects                       → ProjectsPage   (global — project picker)
/settings                       → SettingsPage   (global — personal prefs only)

/p/:projectId/                  → DashboardPage  (project-scoped)
/p/:projectId/jobs              → JobsPage       (project-scoped)
/p/:projectId/jobs/:id          → JobDetailPage   (project-scoped)
/p/:projectId/events            → EventsPage     (project-scoped)

/admin/users                    → AdminUsersPage (admin only — JOB-033)
/admin/invites                  → AdminInvitesPage (admin only — JOB-033)
/admin/system                   → AdminDashboard (admin only — JOB-032)
```

**This job implements the route structure and ProjectContext. It does NOT build the admin pages** — JOB-032 and JOB-033 will fill those in. This job creates placeholder routes that render "Coming soon" text.

## Design Decisions

1. **`/p/:projectId` prefix** — short, doesn't collide with `/projects`. Follows GitHub convention (`/orgs/:org/repos`).
2. **ProjectContext** — stores the active `projectId` from the URL param. Components that need it call `useProject()`. Persists last-used project to `localStorage` so `/projects` can redirect to the last one.
3. **AdminRoute guard** — wraps admin routes. Redirects non-admin users to `/projects`. Implemented as a component like `ProtectedRoute`.
4. **Default redirect** — `/` redirects to `/projects` (project picker). If only 1 project exists and `localStorage` has a saved ID, redirect to `/p/:projectId/` instead.
5. **Layout stays mostly unchanged** — sidebar links are updated but the shell (header, sidebar, content area) stays the same. Data-driven sidebar config is JOB-031.
6. **No API changes** — existing API endpoints already filter by projectId in query params or URL paths. The frontend just needs to pass the `projectId` from context.

## Branch

`feature/JOB-030-route-restructuring`

---

## Tasks

### T-520: Create ProjectContext

**File:** `src/contexts/ProjectContext.tsx`

```tsx
interface ProjectContextValue {
  /** The currently active project ID from the URL param */
  projectId: string;
  /** The full project object (loaded from API on mount) */
  project: Project | null;
  /** Whether the project is still loading */
  loading: boolean;
  /** Error message if project load failed */
  error: string | null;
}
```

Implementation:
1. Create `ProjectProvider` component that wraps project-scoped routes
2. Read `projectId` from `useParams()` — this provider sits inside a `/p/:projectId/*` route
3. Fetch the project details via `getProject(projectId)` on mount
4. Store last-used `projectId` in `localStorage` key `stewie:lastProjectId`
5. Export `useProject()` hook with null check (like `useAuth()`)

**Acceptance criteria:**
- `useProject()` returns `{ projectId, project, loading, error }`
- `localStorage` is updated whenever `projectId` changes
- If the project fetch fails (404), set `error` but don't crash
- TypeScript: explicit types, no `any`
- JSDoc on provider and hook

**Commit:** `feat(JOB-030): add ProjectContext provider (T-520)`

---

### T-521: Create AdminRoute Guard

**File:** `src/components/AdminRoute.tsx`

```tsx
interface AdminRouteProps {
  children: React.ReactNode;
}
```

Implementation:
1. Uses `useAuth()` to check `user.role === "admin"`
2. If not admin, redirect to `/projects` with `<Navigate>`
3. If auth is still `loading`, render nothing (or a loading state)
4. Pattern matches existing `ProtectedRoute.tsx`

**Acceptance criteria:**
- Admin user can access children
- Non-admin user is redirected to `/projects`
- Loading state doesn't flash incorrect content
- TypeScript: explicit types, no `any`
- JSDoc on component

**Commit:** `feat(JOB-030): add AdminRoute guard (T-521)`

---

### T-522: Restructure App.tsx Routes

**File:** `src/App.tsx`

Replace the current flat route config with the hierarchical structure:

```tsx
<AuthProvider>
  <Routes>
    {/* Public */}
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register" element={<RegisterPage />} />

    {/* Protected global routes */}
    <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/projects" element={<ProjectsPage />} />
      <Route path="/settings" element={<SettingsPage />} />

      {/* Project-scoped routes */}
      <Route path="/p/:projectId" element={<ProjectProvider><Outlet /></ProjectProvider>}>
        <Route index element={<DashboardPage />} />
        <Route path="jobs" element={<JobsPage />} />
        <Route path="jobs/:id" element={<JobDetailPage />} />
        <Route path="events" element={<EventsPage />} />
      </Route>

      {/* Admin routes (placeholder pages for now) */}
      <Route path="/admin" element={<AdminRoute><Outlet /></AdminRoute>}>
        <Route path="users" element={<AdminPlaceholder title="User Management" />} />
        <Route path="invites" element={<AdminPlaceholder title="Invite Codes" />} />
        <Route path="system" element={<AdminPlaceholder title="System Dashboard" />} />
      </Route>
    </Route>
  </Routes>
</AuthProvider>
```

Also create a small `RootRedirect` component in the same file:
- Reads `localStorage` `stewie:lastProjectId`
- If exists, redirects to `/p/:lastProjectId/`
- If not, redirects to `/projects`

And a small `AdminPlaceholder` component:
- Renders a Card with the title and "Coming soon — this page is planned for a future sprint."
- Import `Card` from `components/ui`

**Acceptance criteria:**
- All existing pages still render (at new URLs)
- `/` redirects to `/projects` or last project
- `/login` and `/register` still work
- Admin placeholder routes render for admin users
- Non-admin users are redirected from `/admin/*`
- `npm run build` succeeds
- No unused imports

**Commit:** `feat(JOB-030): restructure routes with project scoping and admin section (T-522)`

---

### T-523: Update Layout Sidebar Links

**File:** `src/components/Layout.tsx`

Update the sidebar `<NavLink>` items:

1. **Project-scoped links** (Dashboard, Jobs, Events) must use `/p/:projectId/...` URLs. Get `projectId` from `useProject()` — BUT Layout renders for both global and project-scoped routes. So:
   - If a `ProjectContext` is available (project-scoped page), use project links
   - If not (global page like `/projects` or `/settings`), hide project-scoped links or show them greyed out

2. **Implementation approach:** Try `useProject()` inside a try-catch or use an optional context pattern. Use `useContext(ProjectContext)` directly (returns `null` if no provider) rather than the throwing `useProject()` hook.

3. **Update `getPageTitle()`** to handle new routes (`/p/:projectId/jobs`, etc.)

4. **Keep all existing sidebar styling and structure** — just update the `to=` props and conditional rendering.

**Acceptance criteria:**
- On `/p/:projectId/` pages, sidebar links point to correct project-scoped URLs
- On `/projects` and `/settings`, project-specific links are hidden or disabled
- Page titles show correctly for all new routes
- Mobile sidebar still works
- No visual changes to sidebar appearance

**Commit:** `feat(JOB-030): update Layout sidebar for project-scoped routes (T-523)`

---

### T-524: Update ProjectsPage Navigation

**File:** `src/pages/ProjectsPage.tsx`

Currently, clicking a project navigates to `/projects/:id` (ProjectDetailPage). Update to navigate to `/p/:id/` (the project's dashboard).

1. Change project card `onClick` / `Link` to navigate to `/p/${project.id}/`
2. Delete `ProjectDetailPage.tsx` — it's superseded by the project-scoped dashboard at `/p/:projectId/`
3. Remove the `/projects/:id` route reference (already done in T-522)

**Acceptance criteria:**
- Clicking a project card navigates to `/p/:projectId/`
- `ProjectDetailPage.tsx` deleted
- No references to `/projects/:id` remaining in codebase
- `npm run build` succeeds

**Commit:** `feat(JOB-030): update ProjectsPage to navigate to project-scoped routes (T-524)`

---

### T-525: Update Page Components to Use ProjectContext

**Files:** `DashboardPage.tsx`, `JobsPage.tsx`, `JobDetailPage.tsx`, `EventsPage.tsx`

These pages currently fetch data without project scoping (or get projectId from somewhere ad-hoc). Update them to use `useProject()`:

1. **DashboardPage:** Use `projectId` from context when fetching jobs (pass as query param if/when API supports it — if API doesn't filter by project yet, just add the context hook call for now)
2. **JobsPage:** Same — add `useProject()` hook, thread `projectId` through
3. **JobDetailPage:** Uses `:id` from URL. Verify it still works under `/p/:projectId/jobs/:id`
4. **EventsPage:** Add `useProject()` hook

For now, the goal is to **add the hook calls** so the plumbing is in place, even if the API calls don't yet filter by project. The API filtering is a separate backend task. Don't break existing functionality.

**Acceptance criteria:**
- All 4 pages call `useProject()` and have access to `projectId`
- All 4 pages still render and function correctly at their new URLs
- No API calls break (don't change API call signatures to require projectId yet)
- `npm run build` succeeds

**Commit:** `feat(JOB-030): wire project-scoped pages to ProjectContext (T-525)`

---

## Exit Criteria

- [ ] `ProjectContext` provider created with `useProject()` hook
- [ ] `AdminRoute` guard created and working
- [ ] Routes restructured: `/p/:projectId/*` for project pages, `/admin/*` for admin pages
- [ ] Layout sidebar updated for project-scoped navigation
- [ ] `ProjectDetailPage.tsx` deleted (superseded by project-scoped dashboard)
- [ ] All pages render at their new URLs
- [ ] Admin placeholder pages render for admin, redirect for non-admin
- [ ] `localStorage` persistence for last project
- [ ] `npm run build` succeeds with zero errors
- [ ] One commit per task (6 commits total)
