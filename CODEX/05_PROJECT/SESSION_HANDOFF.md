---
id: SESSION_HANDOFF
title: "Phase 7 Developer Agent Handoff"
type: reference
status: ACTIVE
owner: architect
agents: [coder]
tags: [handoff, phase-7]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

# Phase 7 Developer Agent Handoff

Two parallel jobs starting from commit `684ebd5` on `main`. JOB-024 (emoji purge) is already merged.

---

## Dev Agent A — JOB-025: Chat Slideover + GitHub Repo Picker

**Branch:** `feature/JOB-025-chat-github`
**Sprint doc:** `CODEX/05_PROJECT/JOB-025_Chat_Slideover_GitHub.md`

### Context

You're converting the inline ChatPanel on the ProjectDetailPage to a right-side slideover panel, inspired by the Azure Portal's detail panels. The slideover should support two modes:

1. **Slideover** (default) — overlays content from the right, closes on click-outside/Escape
2. **Pinned sidebar** — locks to the right, content area shrinks, resizable via drag handle

The user's mode choice and pinned width must persist to `localStorage`.

You're also adding a searchable repo combobox on the ProjectsPage that queries `GET /api/github/repos` (being built by Dev Agent B in parallel on `feature/JOB-025-github-api`). When GitHub isn't connected, the "Create New Repository" mode should be disabled with a hint.

### Key Files You'll Touch

- **NEW** `src/Stewie.Web/src/components/ChatSlideover.tsx`
- **NEW** `src/Stewie.Web/src/components/RepoCombobox.tsx`
- **MODIFY** `src/Stewie.Web/src/pages/ProjectDetailPage.tsx` — replace `<ChatPanel>` with `<ChatSlideover>`
- **MODIFY** `src/Stewie.Web/src/pages/ProjectsPage.tsx` — add feature gating + combobox
- **MODIFY** `src/Stewie.Web/src/index.css` — slideover, backdrop, resize handle styles
- **MODIFY** `src/Stewie.Web/src/api/client.ts` — add `fetchGitHubRepos()` function
- **MODIFY** `src/Stewie.Web/src/types/index.ts` — add `GitHubRepo` type

### Design Decisions Already Made

- Raw inline SVGs for icons (see `Icons.tsx` — add new icons there if needed)
- No emoji anywhere
- CSS custom properties for all values (see existing design system in `index.css`)
- Chat panel width: 440px default, 320px min, 600px max
- localStorage keys: `stewie:chatMode`, `stewie:chatWidth`

### What NOT to Touch

- `ChatPanel.tsx` — wrap it, don't modify it
- Backend code — Dev B handles `GitHubController.cs`
- `SettingsPage.tsx` — JOB-026 is modifying that

---

## Dev Agent B — JOB-025: GitHub Repos API

**Branch:** `feature/JOB-025-github-api`
**Sprint doc:** `CODEX/05_PROJECT/JOB-025_Chat_Slideover_GitHub.md`

### Context

You're building a `GitHubController.cs` with a `GET /api/github/repos` endpoint that proxies the user's stored GitHub PAT to the GitHub API. The frontend (being built by Dev A) will consume this to populate a searchable repo combobox.

### Key Files You'll Touch

- **NEW** `src/Stewie.Api/Controllers/GitHubController.cs`
- **MODIFY** `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` — document new endpoint
- **NEW** `src/Stewie.Tests/Integration/GitHubControllerTests.cs`

### Requirements

- `GET /api/github/repos` — `[Authorize]`, proxies to `GET https://api.github.com/user/repos?per_page=100&sort=updated`
- Response: `{ name, fullName, htmlUrl, isPrivate }[]`
- 5-minute in-memory cache per user (`IMemoryCache`)
- 401 if no PAT configured, 502 if GitHub API fails
- Use `IHttpClientFactory` for the outbound GitHub call
- Set `User-Agent: Stewie` header on GitHub requests (required by GitHub API)

### What NOT to Touch

- Frontend code — Dev A handles that
- `SettingsPage.tsx` — JOB-026 is modifying that
- `InvitesController.cs` — JOB-026 handles that

---

## Dev Agent C — JOB-026: Admin User Management (Frontend)

**Branch:** `feature/JOB-026-admin-ui`
**Sprint doc:** `CODEX/05_PROJECT/JOB-026_Admin_User_Management.md`

### Context

You're adding admin-only panels to the SettingsPage for invite code management and user management. The backend endpoints (being built by Dev D) already partially exist — `POST /api/invites` and `GET /api/invites` work. You need to build the UI and connect to both existing and new endpoints.

### Key Files You'll Touch

- **MODIFY** `src/Stewie.Web/src/pages/SettingsPage.tsx` — add admin-only sections
- **MODIFY** `src/Stewie.Web/src/api/client.ts` — add invite/user CRUD functions
- **MODIFY** `src/Stewie.Web/src/types/index.ts` — add `InviteCode`, `UserInfo` types
- **MODIFY** `src/Stewie.Web/src/index.css` — styles for invite/user panels

### Design Decisions

- Admin sections only visible when `user.role === "admin"` (check AuthContext)
- Copy-to-clipboard on generated invite codes
- Confirmation step before user deletion (inline confirm, not a modal)
- Use existing card/form patterns from the design system
- Use `Icons.tsx` for any icons needed (add new SVGs there)

### What NOT to Touch

- `ChatPanel.tsx` or `ProjectDetailPage.tsx` — JOB-025 handles those
- Backend controllers — Dev D handles those

---

## Dev Agent D — JOB-026: Admin User Management (Backend)

**Branch:** `feature/JOB-026-admin-api`
**Sprint doc:** `CODEX/05_PROJECT/JOB-026_Admin_User_Management.md`

### Context

You're adding admin-only endpoints for invite revocation and user management. `InvitesController` already has `POST` (generate) and `GET` (list). `UsersController` exists but only has a `GET /me` endpoint.

### Key Files You'll Touch

- **MODIFY** `src/Stewie.Api/Controllers/InvitesController.cs` — add `DELETE /api/invites/{id}`
- **MODIFY** `src/Stewie.Api/Controllers/UsersController.cs` — add `GET /api/users`, `DELETE /api/users/{id}`
- **MODIFY** `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` — document new endpoints
- **NEW** `src/Stewie.Tests/Integration/UserManagementTests.cs`

### Requirements

- `DELETE /api/invites/{id}` — admin-only, 404 if not found, 409 if already used
- `GET /api/users` — admin-only, returns `{ id, username, role, createdAt }[]`
- `DELETE /api/users/{id}` — admin-only, 400 if self-delete, 403 if target is admin
- Check if `IInviteCodeRepository.GetAllAsync()` exists — you may need to add it
- Check if `IUserRepository` needs a `GetAllAsync()` method

### What NOT to Touch

- Frontend code — Dev C handles that
- `GitHubController` — JOB-025 handles that
- `ChatPanel` or `ProjectDetailPage` — JOB-025 handles those

---

## Infrastructure Notes

- SQL Server: `stewie-sqlserver` container on port 1433
- RabbitMQ: `stewie-rabbitmq` container on port 5672
- Backend env vars: `Stewie__AdminPassword=admin`, `Stewie__JwtSecret`, `Stewie__EncryptionKey`
- Test count baseline: 247 passed, 0 failed, 5 skipped
- Current commit: `684ebd5` on `main`
