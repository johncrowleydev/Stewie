---
id: JOB-014
title: "Job 014 — Live Container Output Streaming"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5a, streaming, containers, real-time]
related: [PRJ-001, CON-002, BLU-001, JOB-012]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Enable real-time streaming of container stdout/stderr to the dashboard. When a developer agent is working inside a Docker container, the Human can watch its output live — like `docker logs -f` in the browser.

# Job 014 — Live Container Output Streaming

---

## 1. Context

Currently, the orchestration service launches Docker containers and waits for the exit code. Container output (stdout/stderr) is lost unless the worker writes to `result.json`. This job:

1. **Captures** container stdout/stderr as it flows (not just at exit)
2. **Buffers** output lines in memory
3. **Pushes** lines to connected dashboard clients via SignalR
4. **Displays** output in a terminal-like panel in the UI

This gives the Human visibility into what dev agents are doing in real time — critical for the future autonomous orchestration model.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Backend (container service refactor, output buffer, SignalR push) | `feature/JOB-014-backend` |
| Dev B | Frontend (ContainerOutputPanel component, integration, CSS) | `feature/JOB-014-frontend` |

**Merge order: Dev A first, Dev B rebases onto main after Dev A merges.**

**Dependency: JOB-012 must be merged to main before starting this job.**

---

## 3. Dependencies

- **Requires JOB-012 merged to main** — SignalR hub infrastructure, `IRealTimeNotifier`.
- Does NOT depend on JOB-013 — can run in parallel with chat.

---

## 4. Tasks

### Dev A — Backend

#### T-140: IContainerService Streaming Refactor

**Modify** `Stewie.Application/Interfaces/IContainerService.cs`:

Current signature:
```csharp
Task<int> LaunchWorkerAsync(WorkTask task);
```

Add an overload that accepts a line callback:
```csharp
/// <summary>
/// Launches a worker container, streaming stdout/stderr line-by-line
/// via the provided callback. Returns exit code on completion.
/// </summary>
Task<int> LaunchWorkerAsync(WorkTask task, Func<string, Task>? onOutputLine = null);
```

**Do NOT remove** the existing parameterless overload — it should delegate to the new one with `onOutputLine: null`.

**Acceptance criteria:**
- [ ] Existing callers unaffected (backward compatible)
- [ ] New overload compiles

---

#### T-141: DockerContainerService Streaming Implementation

**Modify** `Stewie.Infrastructure/Services/DockerContainerService.cs`:

Currently uses `docker run --rm` which blocks until exit. Refactor to:

1. Use `docker create` + `docker start` + `docker logs -f` pattern, OR
2. Use `Process` class with `StandardOutput.ReadLineAsync()` redirect, OR
3. Use Docker.DotNet library with `ContainerAttachAsync` stream

**Recommended approach (simplest):** Use `Process` with output redirect:
```csharp
process.StartInfo.RedirectStandardOutput = true;
process.StartInfo.RedirectStandardError = true;
process.OutputDataReceived += async (_, e) =>
{
    if (e.Data != null && onOutputLine != null)
        await onOutputLine(e.Data);
};
process.ErrorDataReceived += async (_, e) =>
{
    if (e.Data != null && onOutputLine != null)
        await onOutputLine($"[stderr] {e.Data}");
};
process.BeginOutputReadLine();
process.BeginErrorReadLine();
```

**Important:** The callback invocation must be exception-safe — wrap in try/catch so a failed SignalR push doesn't kill the container.

**Acceptance criteria:**
- [ ] Stdout lines streamed as they arrive (not buffered until exit)
- [ ] Stderr lines tagged with `[stderr]` prefix
- [ ] Callback exceptions don't crash the container process
- [ ] Exit code still returned correctly
- [ ] Null callback = existing behavior (no streaming)

---

#### T-142: ContainerOutputBuffer

**Create** `Stewie.Application/Services/ContainerOutputBuffer.cs`:

In-memory ring buffer that holds the last N lines per task:

```csharp
public class ContainerOutputBuffer
{
    private readonly ConcurrentDictionary<Guid, CircularBuffer> _buffers = new();

    /// <summary>Append a line for a task.</summary>
    public void AppendLine(Guid taskId, string line);

    /// <summary>Get all buffered lines for a task (for late-joining clients).</summary>
    public IReadOnlyList<string> GetLines(Guid taskId);

    /// <summary>Clear buffer for a completed/failed task.</summary>
    public void Clear(Guid taskId);
}
```

**Buffer size:** 500 lines per task (configurable). Use a simple circular buffer or `ConcurrentQueue` with trim.

Register as **singleton** in DI.

**Why this exists:** When a client connects mid-execution, they need the backlog of output. The buffer provides that. After the task completes, the buffer is cleared to free memory.

**Acceptance criteria:**
- [ ] Thread-safe (concurrent writes from container + reads from API)
- [ ] Respects max line limit (oldest lines dropped)
- [ ] Clear releases memory

---

#### T-143: Wire Streaming Into Orchestration

**Modify** `JobOrchestrationService.cs`:

Where `LaunchWorkerAsync` is called, pass a callback that:
1. Appends the line to `ContainerOutputBuffer`
2. Calls `IRealTimeNotifier.NotifyContainerOutputAsync(jobId, taskId, line)`

```csharp
var exitCode = await _containerService.LaunchWorkerAsync(task, async line =>
{
    _containerOutputBuffer.AppendLine(task.Id, line);
    await _realTimeNotifier.NotifyContainerOutputAsync(job.Id, task.Id, line);
});
```

Add `ContainerOutputBuffer` as a constructor dependency.

After task completes (success or failure), call `_containerOutputBuffer.Clear(task.Id)`.

**Acceptance criteria:**
- [ ] Output lines flow from container → buffer → SignalR
- [ ] Buffer cleared after task finishes
- [ ] Existing tests still pass (NullRealTimeNotifier absorbs calls)

---

#### T-144: Container Output REST Endpoint (Backlog Fetch)

**Create** endpoint for late-joining clients to get buffered output:

```csharp
[HttpGet("/api/tasks/{taskId}/output")]
[Authorize]
public IActionResult GetContainerOutput(Guid taskId)
{
    var lines = _containerOutputBuffer.GetLines(taskId);
    return Ok(new { taskId, lines, lineCount = lines.Count });
}
```

This is a simple GET — clients call it once when first viewing a task, then switch to WebSocket streaming for updates.

**Acceptance criteria:**
- [ ] Returns buffered lines for active tasks
- [ ] Returns empty array for completed/unknown tasks
- [ ] 401 for unauthenticated

---

#### T-145: Backend Unit Tests

**Create** `Stewie.Tests/Services/ContainerOutputBufferTests.cs`:

| Test | Description |
|:-----|:------------|
| `AppendAndGet_ReturnsLines` | Append 3 lines, get returns all 3 in order |
| `MaxLines_OldestDropped` | Append 600 lines with max 500 → only last 500 |
| `Clear_RemovesBuffer` | Append lines, clear, get returns empty |
| `ConcurrentAccess_NoExceptions` | Parallel writes from 10 threads |
| `GetLines_UnknownTask_ReturnsEmpty` | Unknown taskId → empty list |

**Acceptance criteria:**
- [ ] All 5 tests pass

---

### Dev B — Frontend

#### T-146: Container Output API Client

**Add to** `api/client.ts`:

```typescript
export async function fetchContainerOutput(taskId: string): Promise<ContainerOutputResponse>;
```

**Types** (add to `types.ts`):
```typescript
export interface ContainerOutputResponse {
    taskId: string;
    lines: string[];
    lineCount: number;
}
```

---

#### T-147: ContainerOutputPanel Component

**Create** `components/ContainerOutputPanel.tsx`:

A terminal-style panel that displays container output:
- Dark background, monospace font, green-on-black terminal aesthetic
- Auto-scrolls to bottom as new lines arrive
- Scroll lock: if user scrolls up, stop auto-scrolling; resume when they scroll to bottom
- Line numbering (optional, toggleable)
- Stderr lines highlighted in red/amber
- Status indicator: "Streaming..." (pulsing dot) when active, "Completed" when finished
- Fetches backlog on mount via `fetchContainerOutput`
- Subscribes to SignalR `ContainerOutput` events for real-time lines

**Props:**
```typescript
interface ContainerOutputPanelProps {
    taskId: string;
    jobId: string;          // For SignalR group subscription
    isActive?: boolean;     // Whether the task is still running
}
```

**Design specification:**
- Container: `border-radius`, `box-shadow`, dark background `#1a1a2e`
- Font: `JetBrains Mono` or system monospace, 12px
- Lines: `white-space: pre-wrap`, soft wrapping for long lines
- Max height with scroll, or expandable/collapsible
- Stderr prefix `[stderr]` rendered with `color: var(--color-failed)`
- Empty state: "Waiting for output..." with blinking cursor animation

**Acceptance criteria:**
- [ ] Renders terminal-like output
- [ ] Auto-scrolls to bottom on new lines
- [ ] Scroll lock works when user scrolls up
- [ ] Stderr lines visually distinct
- [ ] Connects to SignalR for live updates

---

#### T-148: ContainerOutputPanel CSS

**Add terminal-specific styles** to CSS:

- `.terminal-panel` — dark container with terminal aesthetic
- `.terminal-line` — single line with subtle hover
- `.terminal-line--stderr` — red/amber tint
- `.terminal-status` — streaming indicator
- `.terminal-scroll-lock` — visual indicator when scroll lock is engaged
- Smooth opacity transitions for new lines
- Dark mode compatible (already dark, shouldn't clash)

---

#### T-149: Wire Into JobDetailPage

**Modify** `pages/JobDetailPage.tsx`:

For each task in the task chain:
- If task status is `Running` → show `ContainerOutputPanel` below the task node (expanded by default)
- If task status is `Completed` or `Failed` → show collapsed output panel with "Show output" toggle
- Pass `jobId` for SignalR group subscription

**Acceptance criteria:**
- [ ] Running tasks show live output
- [ ] Completed tasks have collapsible output
- [ ] Output panel integrates visually with task chain timeline

---

#### T-150: TypeScript Build Verification

**Run:** `npx tsc --noEmit` — must produce zero errors.

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add `GET /api/tasks/{id}/output` to §4, add `ContainerOutput` to §5 WebSocket events | → v2.0.0 |
| CON-001 | No changes — streaming is transport-level, not task packet level | — |

---

## 6. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Buffer tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --filter "FullyQualifiedName~ContainerOutput"

# Frontend
cd src/Stewie.Web/ClientApp && npx tsc --noEmit
```

**Exit criteria:**
- All existing tests pass (no regressions)
- 5 new buffer unit tests pass
- Container output streams from container → buffer → SignalR → browser
- Terminal panel renders in JobDetailPage
- Scroll lock and stderr highlighting work
- TypeScript builds clean

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-014 created for Phase 5a Live Container Output Streaming |
| 2026-04-10 | DEF-001 filed: frontend build failure (missing API client + implicit any) |
| 2026-04-10 | DEF-001 resolved. JOB-014 CLOSED. Passed audit VER-014 with 121 tests passing. |
