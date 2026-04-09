---
id: AGT-002-B-SPR002
title: "Dev Agent B Boot — SPR-002 Frontend + Tests"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [SPR-002, AGT-002, CON-002, DEF-001]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent B for Sprint 002. You handle DEF-001 light theme fix, integration tests (WebApplicationFactory + SQLite in-memory), and Events timeline UI. 5 tasks. Work on `feature/SPR-002-frontend-tests`.

# Dev Agent B — SPR-002 Boot Document

## 1. Your Identity

- **Role:** Developer Agent B (Frontend + Tests)
- **Sprint:** SPR-002
- **Branch:** `feature/SPR-002-frontend-tests`
- **Merge order:** Agent A merges first. You rebase onto updated `main` before merging.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

> **You MUST read these workflow files before running any terminal command or making any git commit. Non-negotiable.**

1. **`.agent/workflows/safe_commands.md`** — Rules for safe terminal command execution
2. **`.agent/workflows/git_commit.md`** — Rules for every git commit

**Key rules from safe_commands:**
- Never walk the full repo tree
- Use `GIT_TERMINAL_PROMPT=0` for git network commands
- Kill hung commands before retrying

**Key rules from git_commit:**
- Secret scanning is mandatory before every commit
- Commit format: `feat(SPR-002): T-XXX description`
- One branch per sprint

## 3. Your File Territory

You may ONLY modify files in these directories:
- `src/Stewie.Web/ClientApp/` (frontend)
- `src/Stewie.Tests/` (test project)

**DO NOT** touch:
- `src/Stewie.Domain/` (Agent A)
- `src/Stewie.Application/` (Agent A)
- `src/Stewie.Infrastructure/` (Agent A)
- `src/Stewie.Api/` (Agent A)

**Exception:** You may ADD a `<ProjectReference>` to `Stewie.Api` in `Stewie.Tests.csproj` for integration tests.

## 4. Tech Stack

| Component | Technology |
|:----------|:-----------|
| Frontend | React 19, TypeScript, Vite 6 |
| CSS | Vanilla CSS with custom properties |
| Branding | Primary: `#6fac50`, Secondary: `#767573`, Font: Inter |
| Testing | xUnit, NSubstitute, WebApplicationFactory |
| Integration DB | SQLite in-memory (for integration tests — no Docker required) |
| API Proxy | Vite dev server → `http://localhost:5275` |

## 5. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint tasks | `CODEX/05_PROJECT/SPR-002_Phase1_Closure.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` | T-025, T-026 (Events schema §4.5/§5.5) |
| Defect report | `CODEX/50_DEFECTS/DEF-001_Dark_Mode_Only.md` | T-022 |
| Current CSS | `src/Stewie.Web/ClientApp/src/index.css` | T-022 |
| Layout component | `src/Stewie.Web/ClientApp/src/components/Layout.tsx` | T-022 |
| API client | `src/Stewie.Web/ClientApp/src/api/client.ts` | T-025, T-026 |
| Types | `src/Stewie.Web/ClientApp/src/types/index.ts` | T-025, T-026 |

## 6. Task Execution Order

1. **T-022** — DEF-001 light/dark theme toggle (no backend dependency)
2. **T-023** — Integration tests: Project endpoints (WebApplicationFactory + SQLite)
3. **T-024** — Integration tests: Run/Task/Health endpoints (same fixture)
4. **T-025** — Events timeline page (build against CON-002 §4.5 contract)
5. **T-026** — Run detail events mini-timeline (reuse T-025 types)

## 7. Integration Test Strategy

Use `WebApplicationFactory<Program>` with an **SQLite in-memory** database:

- Override `NHibernateHelper.BuildSessionFactory()` to use SQLite
- Or use a custom `IServiceCollection` override in the fixture
- Each test class gets a fresh in-memory DB (no state leakage)
- This allows tests to run **without Docker**
- Reference: add `Microsoft.Data.Sqlite` NuGet package to test project

**Important:** NHibernate's SQLite dialect may have minor differences from SQL Server. If you hit a dialect issue, mock the repository instead and file a `DEF-` report.

## 8. Events API Contract (for T-025, T-026)

Build the UI against this contract definition from CON-002 §4.5/§5.5:

**Endpoint:** `GET /api/events?entityType=Run&entityId={id}&limit=100`

**Response schema:**
```json
[
  {
    "id": "uuid",
    "entityType": "Run",
    "entityId": "uuid",
    "eventType": "RunCreated",
    "payload": "{}",
    "timestamp": "ISO 8601"
  }
]
```

**Event types:** `RunCreated`, `RunStarted`, `RunCompleted`, `RunFailed`, `TaskCreated`, `TaskStarted`, `TaskCompleted`, `TaskFailed`

The endpoint is built by Agent A (T-020). You can build the UI first — it will show error/empty states until Agent A's code is merged.

## 9. Governance Checklist Per Task

- [ ] JSDoc comments on all exported components and functions
- [ ] No `any` types in TypeScript
- [ ] Commit: `feat(SPR-002): T-XXX description`
- [ ] Frontend build: `cd src/Stewie.Web/ClientApp && npm run build`
- [ ] Test build: `dotnet build src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] Tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] No secrets in committed code
