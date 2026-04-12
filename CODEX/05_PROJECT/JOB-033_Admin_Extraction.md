---
id: JOB-033
title: "Admin User Management Extraction"
type: planning
status: OPEN
owner: architect
agents: [developer]
tags: [frontend, admin, phase-8]
related: [JOB-030, JOB-031, PRJ-001, GOV-003, JOB-028]
created: 2026-04-12
updated: 2026-04-12
version: 1.0.0
---

> **BLUF:** Extract the invite code and user management UI from SettingsPage into dedicated admin pages at `/admin/invites` and `/admin/users`. Clean up SettingsPage to contain only personal preferences (GitHub token, LLM keys, theme). Replaces two JOB-030 placeholders.

# JOB-033: Admin User Management Extraction

## Context

SettingsPage currently has 3 responsibilities crammed into one page:
1. **Personal settings** — GitHub PAT, LLM provider keys (everyone)
2. **Invite codes** — generate/revoke invite codes (admin-only, gated by `isAdmin`)
3. **User management** — list/delete users (admin-only, gated by `isAdmin`)

After JOB-030, we have admin routes at `/admin/users` and `/admin/invites` with placeholder pages. This job extracts the admin-specific code from SettingsPage into those routes.

## Dependencies

- **JOB-030 must be CLOSED** (admin route structure and AdminRoute guard)
- Can run in parallel with JOB-031 and JOB-032

## Branch

`feature/JOB-033-admin-extraction`

---

## Tasks

### T-550: Extract Invite Codes to AdminInvitesPage

**File:** `src/pages/admin/AdminInvitesPage.tsx`

1. Move the invite codes section from SettingsPage (~lines 270-335) into a new page
2. Use `Card`, `Button`, and `DataTable` from `ui/` to rebuild the UI cleanly
3. Keep the existing API calls (`getInviteCodes`, `createInviteCode`, `revokeInviteCode`)
4. Add a page header with title and "Generate Code" button
5. DataTable columns: Code, Status (Active/Used/Expired), Used By, Created, Actions

**Acceptance criteria:**
- `/admin/invites` shows the invite management UI
- Generate, copy, and revoke all work
- Uses `ui/` components (Card, Button, DataTable)
- Both themes render correctly
- `npm run build` succeeds

**Commit:** `feat(JOB-033): extract invite codes to admin page (T-550)`

---

### T-551: Extract User Management to AdminUsersPage

**File:** `src/pages/admin/AdminUsersPage.tsx`

1. Move the user management section from SettingsPage (~lines 337-385) into a new page
2. Use `Card`, `Badge`, `Button`, and `DataTable` from `ui/`
3. Keep the existing API calls (`getUsers`, `deleteUser`)
4. Add a page header with title and user count
5. DataTable columns: Username, Role (Badge), Created, Actions
6. Delete confirmation — use `Modal` from `ui/` instead of inline confirm

**Acceptance criteria:**
- `/admin/users` shows the user management UI
- Delete user works with Modal confirmation
- Admin users cannot delete themselves or other admins (existing logic preserved)
- Uses `ui/` components (Card, Badge, Button, DataTable, Modal)
- Both themes render correctly
- `npm run build` succeeds

**Commit:** `feat(JOB-033): extract user management to admin page (T-551)`

---

### T-552: Clean Up SettingsPage

**File:** `src/pages/SettingsPage.tsx`

1. Remove ALL invite code and user management code (the `isAdmin` blocks)
2. Remove the `isAdmin` variable and role-checking logic
3. Remove invite/user-related state variables and API calls
4. Remove unused imports
5. What remains: GitHub Integration card and LLM Provider Keys card
6. Optionally add a "Theme" section with explicit theme toggle (currently only in user dropdown)

**Acceptance criteria:**
- SettingsPage contains ONLY personal settings (GitHub, LLM keys)
- No `isAdmin` checks remain in SettingsPage
- No invite or user management code remains
- Page renders correctly for both admin and non-admin users
- `npm run build` succeeds

**Commit:** `feat(JOB-033): clean up SettingsPage to personal preferences only (T-552)`

---

### T-553: Wire Routes and Remove Placeholders

**File:** `src/App.tsx`

1. Replace `AdminPlaceholder title="User Management"` with `<AdminUsersPage />`
2. Replace `AdminPlaceholder title="Invite Codes"` with `<AdminInvitesPage />`
3. Verify both routes render correctly
4. If all 3 admin placeholders are now replaced (including JOB-032's system dashboard), remove the `AdminPlaceholder` component entirely

**Acceptance criteria:**
- `/admin/users` renders AdminUsersPage
- `/admin/invites` renders AdminInvitesPage
- Non-admin users redirected on both
- `AdminPlaceholder` component removed if no longer used
- `npm run build` succeeds

**Commit:** `feat(JOB-033): wire admin pages and remove placeholders (T-553)`

---

## Exit Criteria

- [ ] Invite management at `/admin/invites` with full CRUD
- [ ] User management at `/admin/users` with Modal delete confirmation
- [ ] SettingsPage cleaned to personal preferences only
- [ ] Routes wired, placeholders removed
- [ ] All admin pages use `ui/` components
- [ ] Both themes render correctly
- [ ] `npm run build` succeeds with zero errors
- [ ] One commit per task (4 commits total)

## Phase 8 Completion

When JOB-030, JOB-031, JOB-032, and JOB-033 are all CLOSED, Phase 8 is complete. Update:
- PRJ-001 Roadmap: mark Phase 8 ✅ COMPLETE
- BCK-001 Backlog: mark Phase 8 items as done
- README.md: update phase status
