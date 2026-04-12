---
id: JOB-031
title: "Data-Driven Sidebar + Project Switcher"
type: how-to
status: FAILED
owner: architect
agents: [developer]
tags: [frontend, navigation, architecture, phase-8]
related: [JOB-030, PRJ-001, GOV-003, JOB-028]
created: 2026-04-12
updated: 2026-04-12
version: 2.0.0
---

> **BLUF:** ~~Sidebar rebuilt from config.~~ **FAILED.** Sidebar config and
> project switcher were implemented but both were broken because Layout sat
> above ProjectProvider (project context always null). Switcher did nothing,
> project links never appeared. Hotfixed by architect on 2026-04-12.
> Audit verdict (VER-031) retracted.

# JOB-031: Data-Driven Sidebar + Project Switcher

## Context

After JOB-030, we have:
- Routes split into `/p/:projectId/*`, `/admin/*`, and global (`/projects`, `/settings`)
- `ProjectContext` providing `projectId` and `project` to scoped pages
- `AdminRoute` guard for admin-only routes

But the sidebar is still a flat list of hardcoded `<NavLink>` elements. It doesn't:
- Show admin links (System, Users, Invites) for admin users
- Show a project switcher to change active project without going back to `/projects`
- Show a chat button on project-scoped pages
- Separate project-scoped vs. global nav items visually

## Design Decisions

1. **Nav config array**: A `NavItem[]` array where each item has `label`, `icon`, `path`, `scope` (project/global/admin), and `requiredRole`. The sidebar maps over this array and filters by current context.
2. **Project switcher**: Dropdown in the sidebar (below the logo, above nav items) showing the current project name. Uses the `Select` component from `ui/`. Fetches project list on mount. Changing selection navigates to `/p/:newProjectId/`.
3. **Chat FAB**: A floating "Chat" button (bottom-right corner) visible only on project-scoped pages. Clicking opens the existing `ChatSlideover`. This replaces any current chat entry point.
4. **Sidebar sections**: Visually group items with small section headers: "Project" (project-scoped items), "Admin" (admin items), and settings at the bottom.

## Branch

`feature/JOB-031-sidebar-switcher`

## Dependencies

- **JOB-030 must be CLOSED** before this job starts.

---

## Tasks

### T-530: Extract Sidebar Nav Config

**File:** `src/components/sidebar/navConfig.ts`

Create a typed nav config:

```tsx
interface NavItem {
  label: string;
  path: string;                  // may include :projectId placeholder
  icon: React.ComponentType;     // icon component reference
  scope: 'project' | 'global' | 'admin';
  requiredRole?: 'admin';        // if set, only shown to admins
  end?: boolean;                 // exact match for NavLink
}
```

Define the canonical nav items:
- **Project scope:** Dashboard (`/p/:projectId/`), Jobs (`/p/:projectId/jobs`), Events (`/p/:projectId/events`)
- **Global scope:** Projects (`/projects`), Settings (`/settings`)
- **Admin scope:** System (`/admin/system`), Users (`/admin/users`), Invites (`/admin/invites`)

Move the icon components from Layout.tsx into `src/components/sidebar/icons.tsx` (or import from `Icons.tsx` if they exist there).

**Acceptance criteria:**
- Nav config exported as typed array
- Icon components extracted to sidebar/ directory
- No layout changes yet (config is created but not consumed)
- `npm run build` succeeds

**Commit:** `feat(JOB-031): extract sidebar nav config and icons (T-530)`

---

### T-531: Rebuild Sidebar as Data-Driven Component

**File:** Modify `src/components/Layout.tsx`

Replace the hardcoded `<NavLink>` list with a loop over `navConfig`:

1. Import `navConfig` from `sidebar/navConfig`
2. Get user role from `useAuth()` and projectId from context (optional)
3. Filter items: hide `admin` scope items for non-admin users, hide `project` scope items when no project is active
4. Replace `:projectId` placeholder in paths with actual projectId
5. Group items into sections with small dividers/headers between scopes
6. Keep all existing styling (navLinkClass helper, active state, etc.)

**Acceptance criteria:**
- Sidebar renders identically to before for non-admin users on project pages
- Admin users see admin section in sidebar
- Section dividers appear between project/global/admin groups
- No visual regressions
- `npm run build` succeeds

**Commit:** `feat(JOB-031): rebuild sidebar from nav config (T-531)`

---

### T-532: Add Project Switcher Dropdown

**Files:** `src/components/sidebar/ProjectSwitcher.tsx`, modify `Layout.tsx`

Place a `Select` dropdown in the sidebar, between the logo and the nav:

1. Fetch all projects via API on mount
2. Show current project name as selected value (from `useProject()` if available)
3. On selection change, navigate to `/p/:newProjectId/` (same sub-page if possible, or just the dashboard)
4. When on a global page (no project context), show placeholder "Select project…"
5. Style: compact, fits sidebar width, uses `ds-*` tokens

**Acceptance criteria:**
- Dropdown shows project list
- Changing selection navigates to new project's dashboard
- Current project is pre-selected when on project-scoped pages
- Shows "Select project…" on global pages
- No crash if only 1 project exists
- `npm run build` succeeds

**Commit:** `feat(JOB-031): add project switcher to sidebar (T-532)`

---

### T-533: Add Chat FAB (Floating Action Button)

**Files:** `src/components/ChatFab.tsx`, modify `Layout.tsx`

1. Create a circular floating button, bottom-right corner, fixed position
2. Uses a chat/message-circle icon
3. Only renders when `ProjectContext` is available (project-scoped pages)
4. Clicking toggles the existing `ChatSlideover` component
5. Uses `ds-primary` background with white icon, `shadow-ds-lg`
6. Subtle hover scale animation
7. Badge with unread count if applicable (or just the button for now)

**Acceptance criteria:**
- FAB appears only on project-scoped pages (not on `/projects`, `/settings`, `/admin/*`)
- Clicking opens `ChatSlideover`
- Clicking again or closing slideover hides it
- Button doesn't overlap sidebar or main content
- Accessible: `aria-label="Open chat"`, keyboard focusable
- Both themes render correctly
- `npm run build` succeeds

**Commit:** `feat(JOB-031): add chat FAB on project-scoped pages (T-533)`

---

## Exit Criteria

- [ ] Nav config extracted to typed array with per-item scope and role filtering
- [ ] Sidebar renders from config, not hardcoded NavLinks
- [ ] Admin users see admin nav section
- [ ] Project switcher dropdown in sidebar
- [ ] Chat FAB visible on project-scoped pages only
- [ ] `npm run build` succeeds with zero errors
- [ ] One commit per task (4 commits total)
