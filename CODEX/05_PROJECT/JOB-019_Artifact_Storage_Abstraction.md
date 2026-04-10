---
id: JOB-019
title: "Job 019 — Artifact Storage Abstraction"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-6, architecture, storage]
related: [PRJ-001, BLU-001, CON-001]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Decouple generic data payloads (`task.json`, `result.json`, `governance-report.json`, and raw binaries) from the physical block storage used for Git clones. This lays the infrastructural groundwork to safely scale the API and persist artifacts into Cloud Blob Storage (like S3) when Stewie moves to production, while preserving the local-disk MVP for now.

# Job 019 — Artifact Storage Abstraction

---

## 1. Context

Currently, `IWorkspaceService` assumes that all operations happen on a local disk volume (`workspaces/`). It clones repositories, writes JSON configurations, and parses JSON results from the exact same directory.
As Stewie scales, Developer Agents running in isolated containers across different VMs will not share block storage. Git native operations inherently require an ephemeral POSIX block-storage volume, but permanent data artifacts (like outputs, screenshots, logs, and instruction parameters) must be decoupled into an `IArtifactWorkspaceStore` so they can eventually be offloaded to universally accessible cloud object storage.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Define interfaces, implement `LocalDiskArtifactStore`, refactor `IWorkspaceService` | `feature/JOB-019-artifact-store` |

**Dependency: None.**

---

## 3. Tasks

### Dev A — Backend

#### T-180: Define `IArtifactWorkspaceStore`

**Create** `src/Stewie.Application/Interfaces/IArtifactWorkspaceStore.cs`:

Define the abstraction layer spanning both text (JSON) and generic binary operations:

```csharp
namespace Stewie.Application.Interfaces;

/// <summary>
/// Abstract artifact storage. Used to push/pull operational parameters and task results
/// independently of the physical block storage used for repository cloning.
/// </summary>
public interface IArtifactWorkspaceStore
{
    // Text / JSON operations
    Task<string> ReadTextArtifactAsync(string taskId, string filename);
    Task WriteTextArtifactAsync(string taskId, string filename, string content);

    // Generic Binary operations
    Task<Stream> ReadBinaryArtifactAsync(string taskId, string filename);
    Task WriteBinaryArtifactAsync(string taskId, string filename, Stream content);
}
```

**Acceptance criteria:**
- [ ] Interface established in the Application layer.
- [ ] Supports both string-based and stream-based data payloads.

---

#### T-181: Implement `LocalDiskArtifactStore`

**Create** `src/Stewie.Infrastructure/Services/LocalDiskArtifactStore.cs`:

Implement the MVP version of the abstraction. It should mimic the exact same behavior as the current `WorkspaceService` JSON functions, saving files physically into the `workspaces/` root directory so that `docker compose` development remains unaffected.

**Acceptance criteria:**
- [ ] Reads the `Stewie:WorkspaceRoot` config variable.
- [ ] Safely creates the target sub-directories (`input`, `output`) if they don't exist before writing.
- [ ] Registers in `Program.cs` Dependency Injection via `builder.Services.AddScoped<IArtifactWorkspaceStore, LocalDiskArtifactStore>()`.

---

#### T-182: Strip I/O Logic from `WorkspaceService`

**Modify** `src/Stewie.Application/Interfaces/IWorkspaceService.cs` and `src/Stewie.Infrastructure/Services/WorkspaceService.cs`:

- **Remove:** `WriteTaskJson`, `ReadResult`, and `ReadGovernanceReport`.
- Keep `IWorkspaceService` laser-focused strictly on physical repository state: `PrepareWorkspaceForRun` (which generates the folders), `CloneRepositoryAsync`, `CreateBranchAsync`, `CaptureDiffAsync`, and `CommitChangesAsync`.

**Acceptance criteria:**
- [ ] `WorkspaceService` no longer owns JSON serialization or deserialization of agent packets.

---

#### T-183: Refactor Task Orchestrator and Consumers

**Modify** `src/Stewie.Application/Services/JobOrchestrationService.cs`:

- Inject `IArtifactWorkspaceStore` into the constructor.
- Find all calls where `_workspaceService.WriteTaskJson(...)` occurs and replace them with `_artifactStore.WriteTextArtifactAsync(taskId, "input/task.json", serializedString)`.
- Replace all calls parsing `result.json` and `governance-report.json` to utilize the new `ReadTextArtifactAsync` utility.

*Hint: Remember to manually serialize your `TaskPacket` or deserialize your `ResultPacket` in the orchestrator before/after pushing the raw JSON string to the store.*

**Acceptance criteria:**
- [ ] Orchestration logic successfully integrates reading/writing via the abstracted Store.
- [ ] The app compiles with zero errors.

---

## 4. Verification

```bash
# Verify compilation completely succeeds
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Run Backend Unit Tests (Expect some `JobOrchestrationServiceTests` tests to require mock updates)
dotnet test src/Stewie.Tests/Stewie.Tests.csproj
```

**Exit criteria:**
- All tests pass after updating Mocks to inject `IArtifactWorkspaceStore`.
- The governance scan passes successfully.

---

## 5. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-019 created for Artifact Storage Abstraction. |
