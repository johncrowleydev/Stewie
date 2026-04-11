---
id: JOB-026
title: "Job 026 — Admin User Management + Invite UI"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-7, admin, users, invites, frontend, backend]
related: [JOB-024, PRJ-001, CON-002]
created: 2026-04-11
updated: 2026-04-11
version: 1.1.0
---

> **BLUF:** Build admin-only UI panels for invite code generation/revocation and user list/deletion. Extends SettingsPage with admin-only panels. New backend endpoints for invite management (DELETE /api/invites/{id}) and user management (GET /api/users, DELETE /api/users/{id}).

# JOB-026 — Admin User Management + Invite UI

## Objective

Give admins a UI to generate invite codes, view/revoke codes, list users, and delete users. Backend endpoints for invite generation and listing already exist — this job fills in the gaps.

## Branch

`feature/JOB-026-admin-ui` — single developer agent owns all frontend + backend work.

## Tasks

### T-310: Invite Management Panel
- Add "User Management" section to SettingsPage (visible only when user role is `admin`)
- "Generate Invite Code" button → calls `POST /api/invites`
- Shows generated code inline with a **copy-to-clipboard** button
- List all invite codes with created date → calls `GET /api/invites`
- "Revoke" button on unused codes → calls `DELETE /api/invites/{id}`
- Visual feedback on generate/revoke actions (success/error messages)
- Use existing card/form design patterns — no new CSS framework

### T-311: User Management Panel
- List all users in a table: username, role, created date → calls `GET /api/users`
- "Delete" button on non-admin users → calls `DELETE /api/users/{id}`
- Confirmation step before delete (inline confirm, not a modal)
- Cannot delete own account — disable button with tooltip
- Admin-only visibility (hide section for non-admin users)

### T-312: API Client Functions + Types
- Add to `api/client.ts`:
  - `generateInviteCode(): Promise<InviteCode>`
  - `fetchInviteCodes(): Promise<InviteCode[]>`
  - `revokeInviteCode(id: string): Promise<void>`
  - `fetchUsers(): Promise<UserInfo[]>`
  - `deleteUser(id: string): Promise<void>`
- Add to `types/index.ts`:
  - `InviteCode { id, code, createdAt }`
  - `UserInfo { id, username, role, createdAt }`

### T-313: Invite Revocation Endpoint
- Add `DELETE /api/invites/{id}` to `InvitesController.cs`
- Admin-only authorization
- Return 404 if code doesn't exist
- Return 409 if code was already used
- Full XML doc

### T-314: User Management Endpoints
- Add `GET /api/users` to `UsersController.cs` — list all users (admin-only)
- Returns `{ id, username, role, createdAt }[]`
- Add `DELETE /api/users/{id}` to `UsersController.cs` — delete user (admin-only)
- Cannot delete self (return 400)
- Cannot delete other admins (return 403)
- Full XML doc on all methods

### T-315: CON-002 Update
- Document new endpoints:
  - `DELETE /api/invites/{id}` (request, response, error codes)
  - `GET /api/users` (response schema)
  - `DELETE /api/users/{id}` (request, response, error codes)
- Bump contract version

### T-316: Integration Tests
- Test invite generation (existing endpoint, verify response)
- Test invite revocation (happy path, not found, already used)
- Test user list (returns all users, requires admin)
- Test user deletion (happy path, self-delete blocked, admin-delete blocked)
- Test non-admin access denied for all admin endpoints

## Acceptance Criteria

- [ ] Admin can generate invite codes from Settings page
- [ ] Generated code shown with copy-to-clipboard
- [ ] Admin can see list of all codes
- [ ] Admin can revoke unused codes
- [ ] Admin can see list of all users
- [ ] Admin can delete non-admin users
- [ ] Non-admin users cannot see the management section
- [ ] `npm run build` — zero errors
- [ ] `dotnet build` — zero errors
- [ ] All existing + new tests pass

## Dependencies

- JOB-024 must be merged first (emoji cleanup on SettingsPage) ✅
- Existing `InvitesController.cs` (POST + GET already implemented)
- Existing `UsersController.cs` (needs new endpoints)
- Existing `SettingsPage.tsx` (extend with new sections)

## Notes

- The `InviteCode` entity and repository already exist — just needs the DELETE endpoint
- `IUserRepository` already has `GetByIdAsync`, `GetByUsernameAsync` — may need `GetAllAsync`
- Check if `InviteCode` has a `UsedBy` field to determine if it's been used
