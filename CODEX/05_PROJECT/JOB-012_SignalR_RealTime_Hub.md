---
id: JOB-012
title: "Job 012 — SignalR Real-Time Hub"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5a, signalr, real-time]
related: [PRJ-001, CON-002, BLU-001, JOB-011]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Establish the WebSocket transport layer for Stewie. Replace 5-second polling with SignalR server-push for job/task updates. This is the foundation for chat (JOB-013) and container output streaming (JOB-014).

# Job 012 — SignalR Real-Time Hub

---

## 1. Context

The dashboard currently polls `GET /api/jobs` every 5 seconds via a `usePolling` hook. This wastes bandwidth and introduces visible latency. Phase 5a replaces this with SignalR WebSocket push. This job builds the backend hub and wires it into the orchestration service, then updates the frontend to use WebSocket with polling as a graceful fallback.

**This job has no external dependencies** — it builds on the existing codebase directly.

**JOB-013 (chat) and JOB-014 (container streaming) both depend on this job being merged first.**

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Backend (SignalR hub, notifier, orchestration wiring, Program.cs) | `feature/JOB-012-backend` |
| Dev B | Frontend (useSignalR hook, page updates, fallback logic) | `feature/JOB-012-frontend` |

**Merge order: Dev A first, Dev B rebases onto main after Dev A merges.**

---

## 3. Dependencies

- None — builds on current `main`.

---

## 4. Tasks

### Dev A — Backend

#### T-120: IStewieHubClient Typed Interface

**Create** `Stewie.Application/Hubs/IStewieHubClient.cs`:

```csharp
/// <summary>
/// Typed SignalR hub client — defines push methods the server can call on connected clients.
/// </summary>
public interface IStewieHubClient
{
    Task JobUpdated(Guid jobId, string status);
    Task TaskUpdated(Guid jobId, Guid taskId, string status);
    Task ChatMessageReceived(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, string createdAt);
    Task ContainerOutput(Guid taskId, string line);
}
```

**Notes:**
- `ChatMessageReceived` and `ContainerOutput` are defined now for JOB-013/014 — they won't be called yet, but the interface should be complete.
- All parameters are primitives/strings to avoid serialization issues across the WebSocket.

**Acceptance criteria:**
- [ ] Interface compiles with no dependencies beyond ASP.NET Core

---

#### T-121: StewieHub SignalR Hub

**Create** `Stewie.Application/Hubs/StewieHub.cs`:

```csharp
[Authorize]
public class StewieHub : Hub<IStewieHubClient>
{
    Task JoinProject(Guid projectId);     // group: "project:{id}"
    Task LeaveProject(Guid projectId);
    Task JoinJob(Guid jobId);             // group: "job:{id}"
    Task LeaveJob(Guid jobId);
    Task JoinDashboard();                 // group: "dashboard"
    Task LeaveDashboard();
}
```

**Group naming convention:**
- `project:{guid}` — chat messages + job-level updates for a project
- `job:{guid}` — task-level updates + container output for a job
- `dashboard` — all job state changes (for the main dashboard/jobs pages)

**Requires `[Authorize]`** — JWT authentication enforced on hub connection.

**Acceptance criteria:**
- [ ] Hub compiles and registers correctly
- [ ] Requires authentication (unauthenticated connections rejected)
- [ ] Group join/leave works

---

#### T-122: IRealTimeNotifier + SignalRNotifier

**Create** `Stewie.Application/Interfaces/IRealTimeNotifier.cs`:

```csharp
public interface IRealTimeNotifier
{
    Task NotifyJobUpdatedAsync(Guid? projectId, Guid jobId, string status);
    Task NotifyTaskUpdatedAsync(Guid jobId, Guid taskId, string status);
    Task NotifyChatMessageAsync(Guid projectId, Guid messageId, string senderRole,
        string senderName, string content, DateTime createdAt);
    Task NotifyContainerOutputAsync(Guid jobId, Guid taskId, string line);
}
```

**Create** `Stewie.Infrastructure/Services/SignalRNotifier.cs`:
- Wraps `IHubContext<StewieHub, IStewieHubClient>`
- Routes `NotifyJobUpdatedAsync` to both `dashboard` group AND `project:{id}` group (if projectId is set)
- Routes `NotifyTaskUpdatedAsync` to `job:{id}` group
- Routes `NotifyChatMessageAsync` to `project:{id}` group
- Routes `NotifyContainerOutputAsync` to `job:{id}` group
- **All methods must swallow exceptions** — SignalR push failures must never break orchestration. Log as `Warning`.

**Acceptance criteria:**
- [ ] All methods are fire-and-forget safe
- [ ] Routes to correct groups
- [ ] Failures logged but not thrown

---

#### T-123: Wire SignalR Into Program.cs

**Modify** `Stewie.Api/Program.cs`:

1. Add `using Stewie.Application.Hubs;`
2. Add `builder.Services.AddSignalR();`
3. Add CORS policy for SignalR WebSocket upgrade:
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("StewieCors", policy =>
       {
           policy.WithOrigins("http://localhost:5173", "http://localhost:5275")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();  // Required for SignalR
       });
   });
   ```
4. Configure JWT to accept token from query string (SignalR WebSocket handshake sends JWT via `?access_token=`):
   ```csharp
   options.Events = new JwtBearerEvents
   {
       OnMessageReceived = context =>
       {
           var accessToken = context.Request.Query["access_token"];
           var path = context.HttpContext.Request.Path;
           if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
           {
               context.Token = accessToken;
           }
           return Task.CompletedTask;
       }
   };
   ```
5. Register `IRealTimeNotifier`: `builder.Services.AddSingleton<IRealTimeNotifier, SignalRNotifier>();`
6. Add `IRealTimeNotifier` to `JobOrchestrationService` DI factory
7. Map hub: `app.MapHub<StewieHub>("/hubs/stewie");`
8. Add `app.UseCors("StewieCors");` before `UseAuthentication()`

**NuGet packages needed:**
- Add `FrameworkReference` for `Microsoft.AspNetCore.App` to **Stewie.Application.csproj** and **Stewie.Infrastructure.csproj** (for Hub<T>, IHubContext<T>, [Authorize])

**Acceptance criteria:**
- [ ] API starts successfully with SignalR hub mapped
- [ ] WebSocket connections accepted at `/hubs/stewie`
- [ ] Unauthenticated connections rejected

---

#### T-124: Wire IRealTimeNotifier Into JobOrchestrationService

**Modify** `Stewie.Application/Services/JobOrchestrationService.cs`:

1. Add `IRealTimeNotifier _realTimeNotifier` field
2. Add constructor parameter (after `taskDependencyRepository`, before string parameters)
3. **Add push call inside `EmitEventAsync`** — this is the cleanest approach since all 40+ event sites call this method:

```csharp
private async Task EmitEventAsync(string entityType, Guid entityId, EventType eventType, object payload)
{
    // ... existing event persistence ...

    // Push real-time notification to connected clients
    await PushRealTimeNotificationAsync(entityType, entityId, eventType);
}

private async Task PushRealTimeNotificationAsync(string entityType, Guid entityId, EventType eventType)
{
    try
    {
        var statusString = eventType.ToString();
        if (entityType == "Job")
        {
            var job = await _jobRepository.GetByIdAsync(entityId);
            await _realTimeNotifier.NotifyJobUpdatedAsync(job?.ProjectId, entityId, statusString);
        }
        else if (entityType == "Task")
        {
            var task = await _workTaskRepository.GetByIdAsync(entityId);
            if (task != null)
                await _realTimeNotifier.NotifyTaskUpdatedAsync(task.JobId, entityId, statusString);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to push real-time notification for {EntityType} {EntityId}",
            entityType, entityId);
    }
}
```

**Also:** Create `Stewie.Tests/Mocks/NullRealTimeNotifier.cs` — a no-op implementation for unit tests. Update the 3 test files that construct `JobOrchestrationService`:
- `JobOrchestrationServiceTests.cs`
- `RetryLogicTests.cs`
- `ContainerTimeoutTests.cs`

**Acceptance criteria:**
- [ ] All 59 existing unit tests pass with the new constructor parameter
- [ ] All integration tests pass
- [ ] Event emission triggers SignalR push

---

### Dev B — Frontend

#### T-125: useSignalR Hook

**Create** `hooks/useSignalR.ts`:

```typescript
export function useSignalR(options?: UseSignalROptions): UseSignalRResult {
  // - Manages HubConnection lifecycle (create, start, reconnect, dispose)
  // - Reads JWT from getToken() in api/client.ts (NOT from AuthContext — there's no token property)
  // - Uses isAuthenticated from useAuth() as the connection trigger
  // - Auto-reconnect with exponential backoff: [0, 2000, 5000, 10000, 30000]
  // - Exposes: connection, state, joinGroup, leaveGroup
}
```

**Install:** `npm install @microsoft/signalr`

**Critical notes:**
- The API runs on port 5275 in dev, frontend on 5173. The hook must build the full URL: `http://localhost:5275/hubs/stewie`
- Auth token comes from `getToken()` function in `api/client.ts`, NOT from `useAuth().token` (no such property exists)
- SignalR sends JWT via query string: `accessTokenFactory: () => getToken() ?? ""`

**Acceptance criteria:**
- [ ] Connects to hub when user is authenticated
- [ ] Reconnects automatically on disconnect
- [ ] Disposes connection on unmount
- [ ] joinGroup/leaveGroup work for project, job, dashboard types

---

#### T-126: Replace Polling in DashboardPage

**Modify** `pages/DashboardPage.tsx`:

- Import and use `useSignalR` hook
- When SignalR state is `"connected"`: join `"dashboard"` group, listen for `JobUpdated` events, re-fetch jobs on each event
- When SignalR is NOT connected: fall back to existing `usePolling` with 5-second interval
- Update the live indicator: show "Live" (green dot) when WebSocket connected, "Polling" when falling back
- Use local state for jobs list (updated by both polling fallback and SignalR events)

**Acceptance criteria:**
- [ ] Dashboard updates instantly when jobs change (no 5s delay)
- [ ] Falls back to polling when WebSocket disconnects
- [ ] Live indicator reflects actual connection state

---

#### T-127: Replace Polling in JobsPage

**Same pattern as T-126** — replace polling with SignalR in `pages/JobsPage.tsx`.

**Acceptance criteria:**
- [ ] Job list updates in real time
- [ ] Polling fallback works

---

#### T-128: Wire JobDetailPage to Job Group

**Modify** `pages/JobDetailPage.tsx`:

- Join job-specific group on mount: `joinGroup("job", id)`
- Listen for `TaskUpdated` and `JobUpdated` events
- Re-fetch job data and events on each event
- Leave group on unmount

**Acceptance criteria:**
- [ ] Task status changes appear instantly
- [ ] Event timeline updates in real time
- [ ] Clean group leave on navigation away

---

#### T-129: TypeScript Build Verification

**Run:** `npx tsc --noEmit` — must produce zero errors.

**Acceptance criteria:**
- [ ] Clean TypeScript build with no new errors

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add §5: WebSocket Contract (hub URL, auth, groups, message shapes) | → v1.9.0 |

---

## 6. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests (existing + new mock)
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Frontend type check
cd src/Stewie.Web/ClientApp && npx tsc --noEmit
```

**Exit criteria:**
- All existing unit tests pass (59 unit + 51 integration = 110 total)
- API starts with SignalR hub mapped at `/hubs/stewie`
- Frontend connects via WebSocket and receives push events
- Polling fallback works when WebSocket is disconnected
- TypeScript builds clean

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-012 created for Phase 5a SignalR Real-Time Hub |
