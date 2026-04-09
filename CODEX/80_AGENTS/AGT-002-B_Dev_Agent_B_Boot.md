---
id: AGT-002-B-SPR004
title: "Dev Agent B Boot — SPR-004 Frontend + Tests"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [SPR-004, AGT-002, CON-002]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent B for Sprint 004. You build the login/registration UI, auth context, GitHub settings page, PR link display, and all tests for auth + GitHub flows. 5 tasks. Work on `feature/SPR-004-frontend-tests`.

# Dev Agent B — SPR-004 Boot Document

## 1. Your Identity

- **Role:** Developer Agent B (Frontend + Tests)
- **Sprint:** SPR-004
- **Branch:** `feature/SPR-004-frontend-tests`
- **Merge order:** Agent A merges first. You rebase onto updated `main` before merging.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

1. **`.agent/workflows/safe_commands.md`** — Rules for safe terminal command execution
2. **`.agent/workflows/git_commit.md`** — Rules for every git commit

## 3. Your File Territory

You may ONLY modify files in these directories:
- `src/Stewie.Web/ClientApp/` (frontend)
- `src/Stewie.Tests/` (test project)

**DO NOT** touch backend source directories.

## 4. Tech Stack

| Component | Technology |
|:----------|:-----------|
| Frontend | React 19, TypeScript, Vite 6 |
| CSS | Vanilla CSS with custom properties (index.css design system) |
| Branding | Primary: `#6fac50`, Secondary: `#767573`, Font: Inter |
| Testing | xUnit, NSubstitute, WebApplicationFactory (SQLite in-memory) |
| API Proxy | Vite dev server → `http://localhost:5275` |

## 5. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint tasks | `CODEX/05_PROJECT/SPR-004_GitHub_Integration.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` (v1.3.0) | All tasks |
| CSS design system | `src/Stewie.Web/ClientApp/src/index.css` | T-043, T-044, T-045 |
| API client | `src/Stewie.Web/ClientApp/src/api/client.ts` | T-043 |
| Types | `src/Stewie.Web/ClientApp/src/types/index.ts` | T-046 |
| Test factory | `src/Stewie.Tests/Integration/StewieWebApplicationFactory.cs` | T-047 |

## 6. Task Execution Order

1. **T-043** — Login page + AuthContext (JWT in localStorage, axios interceptor, route guard)
2. **T-044** — Registration page (invite code, password confirmation, auto-login)
3. **T-045** — GitHub settings page (PAT input, connect/disconnect, status indicator)
4. **T-046** — PR link on Run detail + GitHub-enabled project creation
5. **T-047** — All auth + GitHub integration/unit tests

## 7. Auth API Contract (for T-043, T-044)

**POST /api/auth/login:**
```json
// Request
{ "username": "string", "password": "string" }
// Response (200)
{ "token": "jwt", "expiresAt": "ISO 8601", "user": { "id": "uuid", "username": "string", "role": "admin|user" } }
// Error (401)
{ "error": { "code": "INVALID_CREDENTIALS", "message": "..." } }
```

**POST /api/auth/register:**
```json
// Request
{ "username": "string", "password": "string", "inviteCode": "string" }
// Response (200)
{ "token": "jwt", "expiresAt": "ISO 8601", "user": { ... } }
// Error (400)
{ "error": { "code": "INVALID_INVITE_CODE", "message": "..." } }
```

**GitHub Token (for T-045):**
```json
// PUT /api/users/me/github-token
{ "token": "ghp_xxxxxxxxxxxx" }
// GET /api/users/me/github-status
{ "connected": true, "username": "johncrowleydev" }
```

## 8. Design Notes

- Login + Register pages: centered form card, Stewie logo above, green primary button, dark/light theme compatible
- Auth guard: `<ProtectedRoute>` component wrapping all app routes
- JWT stored in `localStorage` as `stewie_token`
- On 401 response anywhere: clear token, redirect to `/login`
- GitHub settings: show a green/gray connection dot, "Connected as {username}" text when connected
- PR link badge: GitHub icon + "View PR" as a green link/badge on Run detail

## 9. Test Factory Updates (T-047)

The `StewieWebApplicationFactory` needs to be updated for auth:
1. Auto-seed a test admin user + test invite code during test setup
2. Provide `GetAuthToken()` helper method that returns a valid JWT for the test user
3. ALL existing tests must be updated to include `Authorization: Bearer {token}` header
4. This is a breaking change to existing tests — update them all

## 10. Governance Checklist Per Task

- [ ] JSDoc comments on all exported components and functions
- [ ] No `any` types in TypeScript
- [ ] Commit: `feat(SPR-004): T-XXX description`
- [ ] Frontend build: `cd src/Stewie.Web/ClientApp && npm run build`
- [ ] Test build: `dotnet build src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] Tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] No secrets in committed code
