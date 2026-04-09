---
id: JOB-004
title: "GitHub Integration + User System"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, sprint, workflow]
related: [BCK-001, BLU-001, CON-001, CON-002, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Sprint 004 delivers Phase 3 — JWT authentication, invite-only registration, per-user encrypted GitHub PAT storage, and GitHub API integration (push branch, create PR, create repo). Two agents work in parallel. Agent A merges first.

# Sprint 004: GitHub Integration + User System

**Phase:** Phase 3 — GitHub Integration + Auth
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Backend), Dev Agent B (Frontend + Tests)
**Dependencies:** JOB-003 complete (merged)
**Contracts:** CON-002 v1.3.0 (auth endpoints, user endpoints, GitHub token, pullRequestUrl)

---

## ⚠️ Mandatory Compliance — Every Task

| Governance Doc | Sprint Requirement |
|:---------------|:-------------------|
| **GOV-001** | XML doc comments (C#), JSDoc (TS) on all public/exported members |
| **GOV-002** | All new code must have tests |
| **GOV-003** | C# coding standards; TS strict mode, no `any` types |
| **GOV-004** | Error middleware for API; error/loading states for frontend |
| **GOV-005** | Branch: `feature/JOB-004-backend` (A), `feature/JOB-004-frontend-tests` (B). Commits: `feat(JOB-004): T-XXX description` |
| **GOV-006** | Structured `ILogger` logging on all new services and controllers |
| **GOV-008** | All infrastructure per GOV-008 |

---

## Parallel Execution Plan

```
Agent A (Backend)                    Agent B (Frontend + Tests)
─────────────────                    ─────────────────────────
T-037: User + InviteCode entities    T-043: Login page + auth context
T-038: JWT auth middleware           T-044: Registration page (invite)
T-039: Encrypted credential store    T-045: GitHub settings page
T-040: IGitHubService (Octokit)      T-046: PR link + GitHub project UI
T-041: Wire GitHub into post-run     T-047: Auth + GitHub integration tests
T-042: Auth on all endpoints
        │                                    │
        ▼                                    ▼
   Merge A first                    Merge B second (rebase on A)
```

---

## Dev Agent A Tasks (Backend)

> **Branch:** `feature/JOB-004-backend`
> **File territory:** `src/Stewie.Domain/`, `src/Stewie.Application/`, `src/Stewie.Infrastructure/`, `src/Stewie.Api/`

### T-037: User + InviteCode Entities + Migrations
- **Dependencies:** None
- **Contracts:** CON-002 §4.0, §4.0.2
- **Deliverable:**
  - `User` entity: Id (Guid), Username (string, unique), PasswordHash (string), Role (enum: Admin, User), CreatedAt
  - `InviteCode` entity: Id (Guid), Code (string, unique), CreatedByUserId (Guid), UsedByUserId (Guid?, nullable), UsedAt (DateTime?), ExpiresAt (DateTime?), CreatedAt
  - `UserCredential` entity: Id (Guid), UserId (Guid), Provider (string, e.g. "github"), EncryptedToken (string), CreatedAt, UpdatedAt
  - NHibernate mappings for all three
  - FluentMigrator migration (Migration_009)  
  - Repositories: `IUserRepository`, `IInviteCodeRepository`, `IUserCredentialRepository`
  - Seed first admin user on startup if none exists (username from config, password from env var `STEWIE_ADMIN_PASSWORD`)
- **Acceptance criteria:**
  - Entities map correctly, migrations run, admin user auto-seeded
  - Build succeeds
- **Status:** [ ] Not Started

### T-038: JWT Auth Middleware
- **Dependencies:** T-037 (User entity)
- **Contracts:** CON-002 §4.0
- **Deliverable:**
  - `AuthController` with:
    - `POST /api/auth/register` — validates invite code, hashes password (BCrypt.Net-Next), creates user, consumes invite code, returns JWT
    - `POST /api/auth/login` — validates username/password, returns JWT
  - JWT generation using `System.IdentityModel.Tokens.Jwt`:
    - Claims: sub (userId), username, role
    - Signing key from `STEWIE_JWT_SECRET` env var (or appsettings for dev)
    - Expiry: 24 hours (configurable)
  - JWT validation middleware in `Program.cs` using `AddAuthentication().AddJwtBearer()`
  - NuGet packages: `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`
- **Acceptance criteria:**
  - Register with valid invite code → 200 + JWT
  - Register with invalid code → 400
  - Login with valid credentials → 200 + JWT
  - Login with wrong password → 401
  - Build succeeds
- **Status:** [ ] Not Started

### T-039: Encrypted Credential Storage
- **Dependencies:** T-037 (UserCredential entity)
- **Contracts:** CON-002 §4.0.1
- **Deliverable:**
  - `IEncryptionService` interface with `Encrypt(string)` and `Decrypt(string)` methods
  - `AesEncryptionService` implementation:
    - AES-256-CBC with PKCS7 padding
    - Key from `STEWIE_ENCRYPTION_KEY` env var (32-byte base64)
    - Random IV per encryption, prepended to ciphertext
  - `UsersController` with:
    - `PUT /api/users/me/github-token` — encrypts PAT, stores in UserCredential
    - `DELETE /api/users/me/github-token` — removes credential
    - `GET /api/users/me/github-status` — checks if credential exists (does NOT return token)
    - `GET /api/users/me` — returns user profile
  - Requires `[Authorize]` attribute on all user endpoints
- **Acceptance criteria:**
  - PAT stored encrypted, retrievable only by decryption
  - Status endpoint returns connected/disconnected
  - Build succeeds
- **Status:** [ ] Not Started

### T-040: IGitHubService with Octokit.net
- **Dependencies:** T-039 (can decrypt PAT)
- **Contracts:** CON-002 §5.2 (pullRequestUrl)
- **Deliverable:**
  - NuGet: `Octokit` (official GitHub .NET SDK)
  - `IGitHubService` interface:
    - `PushBranchAsync(string workspacePath, string remoteName, string branchName, string patToken)`
    - `CreatePullRequestAsync(string owner, string repo, string branchName, string title, string body, string patToken) → string prUrl`
    - `CreateRepositoryAsync(string name, string description, bool isPrivate, string patToken) → string repoUrl`
    - `ValidateTokenAsync(string patToken) → GitHubUser`
  - Implementation uses `Octokit.GitHubClient` with token auth
  - For push: use `git push` via Process (Octokit doesn't do git push)
  - For PR + repo creation: use Octokit API
- **Acceptance criteria:**
  - `CreateRepositoryAsync` creates a repo and returns URL
  - `CreatePullRequestAsync` creates a PR and returns URL
  - `ValidateTokenAsync` returns user info
  - Build succeeds
- **Status:** [ ] Not Started

### T-041: Wire GitHub into Post-Run Flow
- **Dependencies:** T-040 (IGitHubService), T-039 (decrypt PAT)
- **Contracts:** CON-002 §5.2
- **Deliverable:**
  - In `RunOrchestrationService.ExecuteRunAsync`, after auto-commit:
    - If user has a GitHub PAT stored:
      1. Push the branch to remote: `git push origin {branchName}`
      2. Create a PR: title = objective, body = diff summary
      3. Store `PullRequestUrl` on Run entity
  - Add `PullRequestUrl` field to Run entity + migration (Migration_010)
  - NHibernate mapping update
  - The Run must know which user initiated it → add `CreatedByUserId` to Run entity
  - If no PAT configured, skip GitHub ops silently (log info)
- **Acceptance criteria:**
  - Run with GitHub PAT → branch pushed, PR created, URL stored
  - Run without PAT → completes normally, no PR
  - Build succeeds
- **Status:** [ ] Not Started

### T-042: Auth Middleware on All Existing Endpoints
- **Dependencies:** T-038 (JWT middleware registered)
- **Contracts:** CON-002 §4.0
- **Deliverable:**
  - Add `[Authorize]` attribute on all controllers EXCEPT:
    - `HealthController` (public)
    - `AuthController` (public)
  - Add `[AllowAnonymous]` on health and auth endpoints
  - Inject `UserId` from JWT claims into request context
  - Update `RunsController.Create` to set `CreatedByUserId` from JWT claims
  - Update existing integration tests to include auth header (factory provides test JWT)
  - Add test JWT generation helper to `StewieWebApplicationFactory`
- **Acceptance criteria:**
  - All protected endpoints return 401 without token
  - All protected endpoints work with valid token
  - Health + auth endpoints work without token
  - Existing tests pass with auth enabled
  - Build succeeds
- **Status:** [ ] Not Started

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/JOB-004-frontend-tests`
> **File territory:** `src/Stewie.Web/ClientApp/`, `src/Stewie.Tests/`

### T-043: Login Page + Auth Context
- **Dependencies:** None (build against CON-002 §4.0 contract)
- **Contracts:** CON-002 §4.0
- **Deliverable:**
  - `AuthContext` React context:
    - Stores JWT in localStorage
    - Provides `user`, `isAuthenticated`, `login()`, `logout()`, `register()`
    - Auto-attaches `Authorization: Bearer {jwt}` to all API calls (axios interceptor or fetch wrapper)
    - Redirects to login on 401 response
  - `LoginPage` component:
    - Username + password fields
    - Login button with loading state
    - Error display for invalid credentials
    - Link to registration page
    - Stewie branding (logo, green accent)
  - Route protection: wrap all routes except `/login` and `/register` in an auth guard
  - Add `login()` and `register()` functions to `api/client.ts`
- **Acceptance criteria:**
  - Login with valid credentials → redirects to dashboard
  - Login with invalid credentials → shows error
  - Unauthenticated access → redirects to login
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-044: Registration Page (Invite Code)
- **Dependencies:** T-043 (AuthContext)
- **Contracts:** CON-002 §4.0
- **Deliverable:**
  - `RegisterPage` component:
    - Invite code field
    - Username field
    - Password field + confirmation
    - Client-side validation (password length ≥ 8, passwords match)
    - Register button with loading state
    - On success: auto-login and redirect to dashboard
    - On failure: show specific error (invalid invite code, username taken)
  - Add route `/register`
- **Acceptance criteria:**
  - Register with valid invite → logged in, dashboard loads
  - Register with invalid invite → shows error
  - Password mismatch → client-side validation
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-045: GitHub Settings Page
- **Dependencies:** T-043 (AuthContext for JWT)
- **Contracts:** CON-002 §4.0.1
- **Deliverable:**
  - `SettingsPage` component (or section in user profile):
    - GitHub PAT input field (password type, masked)
    - "Connect GitHub" button → `PUT /api/users/me/github-token`
    - "Disconnect" button → `DELETE /api/users/me/github-token`
    - Connection status indicator (green dot = connected, gray = not connected)
    - Fetch status via `GET /api/users/me/github-status` on mount
    - Token validation feedback (valid/invalid after save)
  - Add navigation link ("Settings" in sidebar)
  - Add API client functions for GitHub token management
- **Acceptance criteria:**
  - User can enter PAT and save
  - Status shows connected/disconnected
  - User can disconnect
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-046: PR Link on Run Detail + GitHub-Enabled Project Creation
- **Dependencies:** Soft dep on T-041 (pullRequestUrl), T-045 (GitHub connected)
- **Contracts:** CON-002 §5.2
- **Deliverable:**
  - **Run detail page**: Show PR link badge when `pullRequestUrl` is populated
    - Green badge with GitHub icon + "View PR" text
    - Opens in new tab
    - Graceful null state (no badge when no PR)
  - **Create Project form**: Add optional "Create on GitHub" toggle
    - When enabled: shows private/public selector
    - Calls a different create flow (API will handle GitHub creation in future, for now store the flag)
  - Update `types/index.ts` with `pullRequestUrl` field on Run type
- **Acceptance criteria:**
  - PR link appears on completed runs with PR URLs
  - No PR link on runs without PR
  - `npm run build` succeeds
- **Status:** [ ] Not Started

### T-047: Integration + Unit Tests for Auth + GitHub
- **Dependencies:** None (tests define expected behavior)
- **Contracts:** CON-002 §4.0, GOV-002
- **Deliverable:**
  - **Auth integration tests** (`AuthControllerTests.cs`):
    - Register with valid invite → 200 + JWT
    - Register with invalid invite → 400
    - Register with duplicate username → 409
    - Login with valid creds → 200 + JWT
    - Login with wrong password → 401
  - **Protected endpoint tests**:
    - GET /api/jobs without token → 401
    - GET /api/jobs with valid token → 200
    - GET /health without token → 200 (public)
  - **Encryption unit tests** (`EncryptionServiceTests.cs`):
    - Encrypt then decrypt returns original
    - Different plaintexts produce different ciphertexts
    - Tampered ciphertext throws
  - Update `StewieWebApplicationFactory` to support auth:
    - Auto-seed a test user + invite code
    - Provide helper to generate test JWT: `factory.GetAuthToken()`
    - Update all existing tests to include auth header
- **Acceptance criteria:**
  - All tests pass
  - Build succeeds
- **Status:** [ ] Not Started

---

## Sprint Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-037 | A | [ ] | User + InviteCode + UserCredential entities |
| T-038 | A | [ ] | JWT auth middleware |
| T-039 | A | [ ] | Encrypted credential storage |
| T-040 | A | [ ] | IGitHubService (Octokit) |
| T-041 | A | [ ] | Wire GitHub into post-run flow |
| T-042 | A | [ ] | Auth on all endpoints |
| T-043 | B | [ ] | Login page + auth context |
| T-044 | B | [ ] | Registration page (invite) |
| T-045 | B | [ ] | GitHub settings page |
| T-046 | B | [ ] | PR link + GitHub project UI |
| T-047 | B | [ ] | Auth + GitHub tests |

---

## Merge Strategy

1. **Agent A completes** → Architect audits → merge `feature/JOB-004-backend` to `main`
2. **Agent B completes** → rebase `feature/JOB-004-frontend-tests` onto updated `main` → Architect audits → merge
3. E2E: Register → Login → Configure GitHub PAT → Create Project → Create Run → Verify PR created

---

## Configuration Requirements

New environment variables for Sprint 004:

| Variable | Description | Default (dev) |
|:---------|:------------|:--------------|
| `STEWIE_JWT_SECRET` | JWT signing key (min 32 chars) | `dev-jwt-secret-change-in-production-minimum-32-chars!` |
| `STEWIE_ENCRYPTION_KEY` | AES-256 key (32-byte base64) | `ZGV2LWVuY3J5cHRpb24ta2V5LWNoYW5nZS1pbi1wcm9k` |
| `STEWIE_ADMIN_PASSWORD` | Initial admin password | `Admin@Stewie123!` |
| `STEWIE_ADMIN_USERNAME` | Initial admin username | `admin` |

> ⚠️ Dev defaults in appsettings.Development.json ONLY. Production must use real secrets via env vars.

---

## Sprint Completion Criteria

- [ ] All 11 tasks pass acceptance criteria
- [ ] `dotnet build src/Stewie.slnx` succeeds with 0 errors
- [ ] `dotnet test src/Stewie.Tests/Stewie.Tests.csproj` passes all tests
- [ ] `npm run build` succeeds in `src/Stewie.Web/ClientApp/`
- [ ] Register + Login flow works E2E
- [ ] GitHub PAT storage + PR creation works E2E (with real GitHub repo)
- [ ] All existing tests still pass (with auth headers)
- [ ] No open `DEF-` reports against this sprint

---

## Audit Notes (Architect)

[Architect: 40/40 tests now passing. Code verified. DEF-001 resolved.]

**Verdict:** ✅ PASS
**Deploy approved:** YES
