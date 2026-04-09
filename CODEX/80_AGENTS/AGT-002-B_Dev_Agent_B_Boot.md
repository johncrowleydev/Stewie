---
id: AGT-002-B-SPR003
title: "Dev Agent B Boot — SPR-003 Frontend + Tests"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [SPR-003, AGT-002, CON-002]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent B for Sprint 003. You handle the Create Run form, Run detail git/diff viewer, dashboard auto-refresh, and tests. 5 tasks. Work on `feature/SPR-003-frontend-tests`.

# Dev Agent B — SPR-003 Boot Document

## 1. Your Identity

- **Role:** Developer Agent B (Frontend + Tests)
- **Sprint:** SPR-003
- **Branch:** `feature/SPR-003-frontend-tests`
- **Merge order:** Agent A merges first. You rebase onto updated `main` before merging.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

> **You MUST read these workflow files before running any terminal command or making any git commit. Non-negotiable.**

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
| Sprint tasks | `CODEX/05_PROJECT/SPR-003_Real_Repo_Interaction.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` (v1.2.0) | T-032, T-033, T-035 |
| CSS design system | `src/Stewie.Web/ClientApp/src/index.css` | T-032, T-033, T-034 |
| API client | `src/Stewie.Web/ClientApp/src/api/client.ts` | T-032, T-033 |
| Types | `src/Stewie.Web/ClientApp/src/types/index.ts` | T-033 |
| Test factory | `src/Stewie.Tests/Integration/StewieWebApplicationFactory.cs` | T-035 |

## 6. Task Execution Order

1. **T-032** — Create Run form (build against CON-002 §4.2 —  `POST /api/runs` body)
2. **T-033** — Run detail git/diff viewer (branch badge, diff colors, commit SHA)
3. **T-034** — Dashboard auto-refresh (usePolling hook, live indicator)
4. **T-035** — Integration tests for Run creation API
5. **T-036** — Unit tests for diff/commit services

## 7. Run Creation API Contract (for T-032)

Build the form against this spec from CON-002 §4.2:

**POST /api/runs:**
```json
{
  "projectId": "uuid",
  "objective": "string",
  "scope": "string | null",
  "script": ["string"] | null,
  "acceptanceCriteria": ["string"] | null
}
```

**Updated Run response** (for T-033):
```json
{
  "id": "uuid",
  "projectId": "uuid",
  "status": "Pending | Running | Completed | Failed",
  "branch": "string | null",
  "diffSummary": "string | null",
  "commitSha": "string | null",
  "tasks": [...]
}
```

**Diff artifact** (for T-033):
```json
{
  "type": "diff",
  "contentJson": "{ \"diffStat\": \"...\", \"diffPatch\": \"...\" }"
}
```

## 8. Design Notes

- The Create Run form should fit the existing design system (dark/light theme support)
- Diff viewer: monospaced text, green for `+` lines, red for `-` lines, gray for context
- Polling indicator: small green dot + subtle pulse animation next to "Live" text
- Use `usePolling(fetchFn, intervalMs, enabled)` as a reusable hook pattern

## 9. Governance Checklist Per Task

- [ ] JSDoc comments on all exported components and functions
- [ ] No `any` types in TypeScript
- [ ] Commit: `feat(SPR-003): T-XXX description`
- [ ] Frontend build: `cd src/Stewie.Web/ClientApp && npm run build`
- [ ] Test build: `dotnet build src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] Tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
- [ ] No secrets in committed code
