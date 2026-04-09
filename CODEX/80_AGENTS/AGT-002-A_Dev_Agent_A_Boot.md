---
id: AGT-002-A-SPR004
title: "Dev Agent A Boot — SPR-004 Backend"
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

> **BLUF:** You are Dev Agent A for Sprint 004. You build JWT authentication, user management, encrypted credential storage, GitHub API integration (Octokit.net), and wire GitHub into the post-run flow. 6 tasks. Work on `feature/SPR-004-backend`.

# Dev Agent A — SPR-004 Boot Document

## 1. Your Identity

- **Role:** Developer Agent A (Backend)
- **Sprint:** SPR-004
- **Branch:** `feature/SPR-004-backend`
- **Merge order:** You merge first. Agent B rebases on your code.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

1. **`.agent/workflows/safe_commands.md`** — Rules for safe terminal command execution
2. **`.agent/workflows/git_commit.md`** — Rules for every git commit

## 3. Your File Territory

You may ONLY modify files in these directories:
- `src/Stewie.Domain/`
- `src/Stewie.Application/`
- `src/Stewie.Infrastructure/`
- `src/Stewie.Api/`

**DO NOT** touch:
- `src/Stewie.Web/ClientApp/` (Agent B)
- `src/Stewie.Tests/` (Agent B)

## 4. Tech Stack

| Component | Technology |
|:----------|:-----------|
| Language | C# .NET 10 |
| ORM | NHibernate |
| Migrations | FluentMigrator |
| Database | SQL Server 2022 (`localhost:1433`, password: `Stewie_Dev_P@ss1`) |
| API Port | `http://localhost:5275` |
| Auth | JWT (Microsoft.AspNetCore.Authentication.JwtBearer) |
| Password hashing | BCrypt.Net-Next |
| Encryption | AES-256-CBC (System.Security.Cryptography) |
| GitHub API | Octokit.net |

## 5. NuGet Packages to Add

```
BCrypt.Net-Next
Microsoft.AspNetCore.Authentication.JwtBearer
System.IdentityModel.Tokens.Jwt
Octokit
```

## 6. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint tasks | `CODEX/05_PROJECT/SPR-004_GitHub_Integration.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` (v1.3.0) | All tasks |
| Existing orchestration | `src/Stewie.Application/Services/RunOrchestrationService.cs` | T-041 |
| Existing workspace svc | `src/Stewie.Infrastructure/Services/WorkspaceService.cs` | T-041 |

## 7. Task Execution Order

1. **T-037** — User + InviteCode + UserCredential entities, migrations, repositories, admin seeding
2. **T-038** — JWT auth middleware (AuthController: register + login, JWT generation + validation)
3. **T-039** — Encrypted credential storage (AesEncryptionService, UsersController)
4. **T-040** — IGitHubService (Octokit: push, PR, repo creation, token validation)
5. **T-041** — Wire GitHub into post-run (push branch, create PR, store URL)
6. **T-042** — [Authorize] on all controllers, update existing endpoints to use JWT claims

## 8. Configuration

Add these to `appsettings.Development.json`:

```json
{
  "Stewie": {
    "JwtSecret": "dev-jwt-secret-change-in-production-minimum-32-chars!",
    "EncryptionKey": "ZGV2LWVuY3J5cHRpb24ta2V5LWNoYW5nZS1pbi1wcm9k",
    "AdminUsername": "admin",
    "AdminPassword": "Admin@Stewie123!"
  }
}
```

In production, these come from environment variables: `STEWIE_JWT_SECRET`, `STEWIE_ENCRYPTION_KEY`, `STEWIE_ADMIN_PASSWORD`, `STEWIE_ADMIN_USERNAME`.

## 9. Important Architecture Notes

- **Invite-only registration.** No open signup. Admin creates invite codes.
- **First admin is auto-seeded** on startup if no users exist. Use the config values above.
- **PATs are encrypted at rest** using AES-256-CBC with random IV per encryption. IV is prepended to ciphertext.
- **Git push** uses `Process.Start("git", "push")` with `GIT_TERMINAL_PROMPT=0` — Octokit doesn't do git push. Configure the remote to use HTTPS with the PAT embedded: `https://{pat}@github.com/{owner}/{repo}.git`
- **Run ownership:** Add `CreatedByUserId` to the Run entity so GitHub ops can look up the user's PAT.
- **No secrets in appsettings.json** — only in `appsettings.Development.json` (which is already in `.gitignore` if properly configured; verify this).

## 10. Governance Checklist Per Task

- [ ] XML doc comments on all new/modified public members
- [ ] Structured `ILogger` logging on new code
- [ ] Commit: `feat(SPR-004): T-XXX description`
- [ ] Build succeeds: `dotnet build src/Stewie.Api/Stewie.Api.csproj`
- [ ] No secrets in committed code
