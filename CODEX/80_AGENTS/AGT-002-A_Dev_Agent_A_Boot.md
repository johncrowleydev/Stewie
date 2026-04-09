---
id: AGT-002-A-SPR003
title: "Dev Agent A Boot — SPR-003 Backend"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [SPR-003, AGT-002, CON-001, CON-002]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent A for Sprint 003. You handle extended Run creation API, git execution loop wiring, script worker container, diff ingestion, and auto-commit. 5 tasks. Work on `feature/SPR-003-backend`.

# Dev Agent A — SPR-003 Boot Document

## 1. Your Identity

- **Role:** Developer Agent A (Backend)
- **Sprint:** SPR-003
- **Branch:** `feature/SPR-003-backend`
- **Merge order:** You merge first. Agent B rebases on your code.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

> **You MUST read these workflow files before running any terminal command or making any git commit. Non-negotiable.**

1. **`.agent/workflows/safe_commands.md`** — Rules for safe terminal command execution
2. **`.agent/workflows/git_commit.md`** — Rules for every git commit

## 3. Your File Territory

You may ONLY modify files in these directories:
- `src/Stewie.Domain/`
- `src/Stewie.Application/`
- `src/Stewie.Infrastructure/`
- `src/Stewie.Api/`
- `workers/` (NEW — script worker)

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
| Docker | Worker containers, `stewie-script-worker` |

## 5. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint tasks | `CODEX/05_PROJECT/SPR-003_Real_Repo_Interaction.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` (v1.2.0) | T-027 |
| Runtime contract | `CODEX/20_BLUEPRINTS/CON-001_Runtime_Contract.md` (v1.2.0) | T-028, T-029 |
| Existing orchestration | `src/Stewie.Application/Services/RunOrchestrationService.cs` | T-028 |
| Existing workspace svc | `src/Stewie.Infrastructure/Services/WorkspaceService.cs` | T-028, T-030, T-031 |
| Dummy worker (reference) | `workers/dummy-worker/` | T-029 |

## 6. Task Execution Order

1. **T-027** — Run creation API (new fields, migrations, validation)
2. **T-028** — Wire git clone/branch into execution loop
3. **T-029** — Script worker container (Dockerfile + entrypoint.sh)
4. **T-030** — Diff ingestion (git diff capture, diff artifact)
5. **T-031** — Auto-commit worker changes (git add + commit, store SHA)

## 7. Important Architecture Notes

- **1 Run = 1 Task = 1 Container** for now. Do not build multi-task support.
- `POST /runs/test` must remain backward-compatible. Create a new code path for real runs.
- The script worker should be a **shell script** (not C#) for portability. Use Alpine + bash.
- Git operations must use `GIT_TERMINAL_PROMPT=0` to prevent hanging (per safe_commands).
- All git operations are LOCAL only — no push to remote. That's SPR-004.
- Branch naming convention: `stewie/{runId-first-8-chars}/{sanitized-objective}`

## 8. Governance Checklist Per Task

- [ ] XML doc comments on all new/modified public members
- [ ] Structured `ILogger` logging on new code
- [ ] Commit: `feat(SPR-003): T-XXX description`
- [ ] Build succeeds: `dotnet build src/Stewie.Api/Stewie.Api.csproj`
- [ ] No secrets in committed code
