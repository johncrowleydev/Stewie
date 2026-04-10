---
id: AGT-019-A
title: "Dev Agent A Boot — JOB-019 Artifact Storage Abstraction"
type: how-to
status: ACTIVE
owner: architect
agents: [coder]
tags: [agent, boot, sprint]
related: [JOB-019, AGT-002, BLU-001]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** You are Dev Agent A for JOB-019. Your mission is to abstract artifact payload storage (`task.json`, `result.json`) into a new `IArtifactWorkspaceStore`, liberating `IWorkspaceService` to handle strictly Git operations. 4 tasks. Work on `feature/JOB-019-artifact-store`.

# Dev Agent A — JOB-019 Boot Document

## 1. Your Identity

- **Role:** Developer Agent A (Backend API)
- **Sprint:** JOB-019
- **Branch:** `feature/JOB-019-artifact-store`
- **Merge order:** You are the sole agent for this job. Merge immediately upon passing tests.

## 2. ⚠️ MANDATORY: Read These Before ANY Action

1. **`.agent/workflows/safe_commands.md`** — Rules for safe terminal command execution
2. **`.agent/workflows/git_commit.md`** — Rules for every git commit

## 3. Your File Territory

You may ONLY modify files in these directories (The Core .NET API):
- `src/Stewie.Application/`
- `src/Stewie.Infrastructure/`
- `src/Stewie.Api/`
- `src/Stewie.Tests/`

## 4. Key References

| Document | Path | Read Before |
|:---------|:-----|:------------|
| Sprint instructions | `CODEX/05_PROJECT/JOB-019_Artifact_Storage_Abstraction.md` | Starting work |
| Existing Orchestrator | `src/Stewie.Application/Services/JobOrchestrationService.cs` | T-183 |
| Existing Workspace Service | `src/Stewie.Infrastructure/Services/WorkspaceService.cs` | T-182 |

## 5. Task Execution Order

1. **T-180** — Define `IArtifactWorkspaceStore` in the Application layer (supporting generic byte streams and strings).
2. **T-181** — Implement `LocalDiskArtifactStore` in Infrastructure matching current JSON drop logic.
3. **T-182** — Strip `WriteTaskJson`, `ReadResult`, and `ReadGovernanceReport` out of `IWorkspaceService`.
4. **T-183** — Inject `IArtifactWorkspaceStore` across all orchestration pathways (like `JobOrchestrationService` and tests).

## 6. Important Architecture Notes

- **Git still lives on local disk:** `WorkspaceService` still manages physical directory scaffolding and Git cloning block-storage. Do not move `git clone` or `CaptureDiffAsync` into `IArtifactWorkspaceStore`.
- **MVP Requirement:** `LocalDiskArtifactStore` MUST continue to output to the `Stewie:WorkspaceRoot` config path (i.e., `workspaces/{taskId}/output/`) in its MVP implementation so that existing Docker Compose mounts are preserved.
- **Serialization boundary:** The Store interface operates on strings and binary streams, meaning `JobOrchestrationService` is now responsible for `JsonSerializer.Serialize` before passing data down to the storage layer!

## 7. Governance Checklist Per Task

- [ ] XML doc comments on `IArtifactWorkspaceStore`.
- [ ] Commit: `feat(JOB-019): T-XXX description`
- [ ] Build succeeds: `dotnet build src/Stewie.Api/Stewie.Api.csproj`
- [ ] All tests pass: `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
