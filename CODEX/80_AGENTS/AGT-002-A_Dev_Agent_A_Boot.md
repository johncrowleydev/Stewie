---
id: AGT-002-A-SPR002
title: "Dev Agent A Boot — SPR-002 Backend"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [SPR-002, AGT-002, CON-001, CON-002]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent A for Sprint 002. You handle backend event emission, workspace tracking, Events API endpoint, and git clone/branch plumbing. 5 tasks. Work on `feature/SPR-002-backend`.

# Dev Agent A — SPR-002 Boot Document

## 1. Your Identity

- **Role:** Developer Agent A (Backend)
- **Sprint:** SPR-002
- **Branch:** `feature/SPR-002-backend`
- **Merge order:** You merge first. Agent B rebases on your code.

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

## 5. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint tasks | `CODEX/05_PROJECT/SPR-002_Phase1_Closure.md` | Starting work |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` | T-020 (Events endpoint) |
| Runtime contract | `CODEX/20_BLUEPRINTS/CON-001_Runtime_Contract.md` | T-021 (git fields) |
| System blueprint | `CODEX/20_BLUEPRINTS/BLU-001_Stewie_System_Blueprint.md` | T-017, T-018, T-019 |
| Event entity | `src/Stewie.Domain/Entities/Event.cs` | T-017, T-018 |
| EventType enum | `src/Stewie.Domain/Enums/EventType.cs` | T-017, T-018 |
| Workspace entity | `src/Stewie.Domain/Entities/Workspace.cs` | T-019 |
| Orchestration service | `src/Stewie.Application/Services/RunOrchestrationService.cs` | T-017-T-019 |
| IWorkspaceService | `src/Stewie.Application/Interfaces/IWorkspaceService.cs` | T-021 |

## 6. Task Execution Order

1. **T-017** — Event emission: Run lifecycle (modify RunOrchestrationService)
2. **T-018** — Event emission: Task lifecycle (same file, add task events)
3. **T-019** — Workspace tracking (save Workspace records in orchestration)
4. **T-020** — Events API endpoint (new EventsController + repo methods)
5. **T-021** — Git clone/branch in WorkspaceService (plumbing, no wiring)

## 7. Governance Checklist Per Task

- [ ] XML doc comments on all new/modified public members
- [ ] Structured `ILogger` logging on new code
- [ ] Commit: `feat(SPR-002): T-XXX description`
- [ ] Build succeeds: `dotnet build src/Stewie.Api/Stewie.Api.csproj`
- [ ] No secrets in committed code
