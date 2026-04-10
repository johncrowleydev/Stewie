---
id: JOB-018
title: "Job 018 — Chat-to-RabbitMQ Bridge + Architect Lifecycle"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5b, chat, rabbitmq, architect]
related: [PRJ-001, BLU-001, CON-004, JOB-013, JOB-016, JOB-017]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Wire the Human chat system (JOB-013) into the RabbitMQ messaging backbone and add Architect Agent lifecycle management to the frontend. After this job, a Human can send a chat message that reaches a stub Architect Agent via RabbitMQ, and the agent's response appears back in the chat panel — completing the full round-trip from Human → API → RabbitMQ → Agent → RabbitMQ → API → SignalR → Browser.

# Job 018 — Chat-to-RabbitMQ Bridge + Architect Lifecycle

---

## 1. Context

JOB-013 built the chat system (REST + SignalR, Human messages persisted). JOB-016 built the RabbitMQ backbone. JOB-017 built the agent runtime abstraction. This job connects them:

1. **Chat relay** — when a Human sends a chat message, also publish it to the Architect's RabbitMQ queue
2. **Agent chat persistence** — when the consumer receives an `agent.chat` event, persist it as a ChatMessage with role "Architect"
3. **Architect session management** — explicit "Start Architect" / "Stop Architect" UI controls
4. **Frontend indicators** — Architect online/offline status, agent session list

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Chat relay service, agent event → ChatMessage persistence, architect session endpoints | `feature/JOB-018-backend` |
| Dev B | Frontend: Architect controls, status indicators, agent session UI | `feature/JOB-018-frontend` |

**Merge order: Dev A first, Dev B second.**

**Dependency: JOB-016 and JOB-017 must be merged to main.**

---

## 3. Tasks

### Dev A — Backend

#### T-170: Chat Relay Service

**Modify** `Stewie.Api/Controllers/ChatController.cs` (SendMessage method):

After persisting the chat message and pushing via SignalR, also relay to RabbitMQ:

```csharp
// Existing: persist + SignalR push
// ...

// NEW: relay to Architect via RabbitMQ (if Architect is running)
var architectSession = await _agentSessionRepo
    .GetActiveByProjectAndRoleAsync(projectId, "architect");

if (architectSession is not null)
{
    var agentMessage = new AgentMessage
    {
        Type = "chat.message",
        AgentId = architectSession.Id.ToString(),
        ProjectId = projectId,
        Payload = new Dictionary<string, object>
        {
            ["messageId"] = message.Id.ToString(),
            ["senderName"] = senderName,
            ["content"] = message.Content
        }
    };

    await _rabbitMqService.PublishCommandAsync(
        $"agent.{architectSession.Id}", agentMessage);
}
```

**Important:** The relay is best-effort. If RabbitMQ is down or the Architect isn't running, the chat message is still persisted and visible in the UI — the relay failure is logged at Warning level but doesn't fail the HTTP request.

**Acceptance criteria:**
- [ ] Chat messages relayed to Architect's RabbitMQ queue when Architect is running
- [ ] No relay attempted when no Architect session exists (silent skip)
- [ ] RabbitMQ failures don't break the chat endpoint
- [ ] Relay only occurs for Human messages (not System or Architect)

---

#### T-171: Agent Chat Event → ChatMessage Persistence

**Modify** `RabbitMqConsumerHostedService` (from JOB-016 T-160):

Add handler for `agent.chat` event type:

```csharp
case "agent.chat":
    var chatMessage = new ChatMessage
    {
        Id = Guid.NewGuid(),
        ProjectId = agentMessage.ProjectId,
        SenderRole = "Architect",
        SenderName = "Architect",
        Content = agentMessage.Payload["content"]?.ToString() ?? "",
        CreatedAt = DateTime.UtcNow
    };

    _unitOfWork.BeginTransaction();
    await _chatRepo.SaveAsync(chatMessage);
    await _unitOfWork.CommitAsync();

    await _notifier.NotifyChatMessageAsync(
        chatMessage.ProjectId, chatMessage.Id,
        chatMessage.SenderRole, chatMessage.SenderName,
        chatMessage.Content, chatMessage.CreatedAt);
    break;
```

**Acceptance criteria:**
- [ ] Agent `chat` events persisted as ChatMessage with SenderRole = "Architect"
- [ ] Persisted messages pushed via SignalR (appear in ChatPanel in real-time)
- [ ] Malformed payload logged but doesn't crash consumer

---

#### T-172: Architect Session Management Endpoints

**Add to** `AgentsController` (created in JOB-017):

```csharp
/// <summary>Start the Architect Agent for a project.</summary>
[HttpPost("architect/{projectId}/start")]
public async Task<IActionResult> StartArchitect(Guid projectId);

/// <summary>Stop the Architect Agent for a project.</summary>
[HttpPost("architect/{projectId}/stop")]
public async Task<IActionResult> StopArchitect(Guid projectId);

/// <summary>Get the Architect Agent status for a project.</summary>
[HttpGet("architect/{projectId}/status")]
public async Task<IActionResult> GetArchitectStatus(Guid projectId);
```

**StartArchitect:**
- Validates project exists (404 if not)
- Checks no Architect already running (409 if so)
- Calls `AgentLifecycleService.LaunchAgentAsync(projectId, null, "architect")`
- Returns 201 with session info

**StopArchitect:**
- Finds active Architect session for project (404 if none)
- Calls `AgentLifecycleService.TerminateAgentAsync(sessionId)`
- Returns 204

**GetArchitectStatus:**
- Returns `{ "active": true/false, "session": {...} or null }`

**Acceptance criteria:**
- [ ] Start launches Architect container
- [ ] Stop terminates it
- [ ] Status accurately reflects state
- [ ] 409 for duplicate start

---

#### T-173: Backend Unit Tests

**Create/update** test files:

| Test | Description |
|:-----|:------------|
| `ChatController_RelaysToRabbitMQ_WhenArchitectActive` | Verify relay called |
| `ChatController_NoRelay_WhenNoArchitect` | Verify no RabbitMQ call when no Architect |
| `ChatController_RelayFailure_DoesNotFailEndpoint` | RabbitMQ exception → 201 still returned |
| `Consumer_AgentChat_PersistsChatMessage` | agent.chat event → ChatMessage in DB |
| `Consumer_AgentChat_MalformedPayload_Skips` | Bad payload → logged, not thrown |

**Acceptance criteria:**
- [ ] All 5 tests pass
- [ ] Mocked RabbitMQ and repository

---

### Dev B — Frontend

#### T-174: Architect Control Panel

**Create** `components/ArchitectControls.tsx`:

A control panel for the Architect Agent on the ProjectDetailPage:

```
┌─────────────────────────────────────────┐
│ 🤖 Architect Agent          [● Offline] │
│                                         │
│ [  Start Architect  ]                   │
│                                         │
│ Runtime: stub                           │
│ Last active: —                          │
└─────────────────────────────────────────┘
```

When active:

```
┌─────────────────────────────────────────┐
│ 🤖 Architect Agent           [● Online] │
│                                         │
│ [  Stop Architect  ]                    │
│                                         │
│ Runtime: stub                           │
│ Session: abc123...                       │
│ Uptime: 5m 32s                          │
└─────────────────────────────────────────┘
```

**Props:**
```typescript
interface ArchitectControlsProps {
    projectId: string;
}
```

**Behavior:**
- Polls `/api/agents/architect/{projectId}/status` every 10s (or listens via SignalR)
- "Start Architect" button → POST `/api/agents/architect/{projectId}/start`
- "Stop Architect" button → POST `/api/agents/architect/{projectId}/stop`
- Shows confirmation dialog before stop
- Disabled while request is in-flight

**Acceptance criteria:**
- [ ] Renders offline/online state accurately
- [ ] Start/stop buttons work
- [ ] Status updates reflected in real-time (via poll or SignalR)

---

#### T-175: Agent API Client Functions

**Add to** `api/client.ts`:

```typescript
export async function startArchitect(projectId: string): Promise<AgentSession>;
export async function stopArchitect(projectId: string): Promise<void>;
export async function getArchitectStatus(projectId: string): Promise<ArchitectStatus>;
export async function getAgentSessions(projectId: string): Promise<AgentSession[]>;
```

**Types** (add to `types/index.ts`):

```typescript
export interface AgentSession {
    id: string;
    projectId: string;
    taskId: string | null;
    containerId: string;
    runtimeName: string;
    role: string;
    status: string;
    startedAt: string;
    stoppedAt: string | null;
}

export interface ArchitectStatus {
    active: boolean;
    session: AgentSession | null;
}
```

**Acceptance criteria:**
- [ ] Functions work with real API
- [ ] Types match backend response schema

---

#### T-176: Chat Panel Architect Integration

**Modify** `components/ChatPanel.tsx`:

- Show "Architect is offline" muted message in chat when no Architect session active
- When Architect is online, show a subtle green indicator next to "Project Chat" header
- Chat input disabled with hint "Start the Architect to begin chatting" when Architect is offline

This encourages the Human to start the Architect before chatting (since without it, messages go nowhere).

**Acceptance criteria:**
- [ ] Offline Architect → input disabled with helpful hint
- [ ] Online Architect → input enabled, green indicator
- [ ] Status indicator updates when Architect starts/stops

---

#### T-177: Architect Controls CSS

**Add to** `index.css`:

Styles for ArchitectControls component:
- `.architect-controls` — card with subtle border
- `.architect-status-badge` — online (green pulse) / offline (gray)
- `.architect-start-btn` / `.architect-stop-btn` — primary/danger buttons
- `.architect-meta` — runtime, session, uptime labels

**Acceptance criteria:**
- [ ] Visually polished, matches existing design system
- [ ] Dark mode compatible

---

#### T-178: Wire Into ProjectDetailPage

**Modify** `pages/ProjectDetailPage.tsx`:

Add `ArchitectControls` between the project info card and the ChatPanel:

```tsx
<div id="project-detail-page">
  {/* Breadcrumb + Project info card */}
  ...

  {/* Architect controls */}
  <ArchitectControls projectId={project.id} />

  {/* Chat panel */}
  <ChatPanel projectId={project.id} />
</div>
```

Pass Architect status down to ChatPanel so it can disable input when offline.

**Acceptance criteria:**
- [ ] ArchitectControls renders on ProjectDetailPage
- [ ] ChatPanel reflects Architect status

---

#### T-179: TypeScript Build Verification

**Run:** `npx tsc --noEmit` — must produce zero errors.

**Acceptance criteria:**
- [ ] Clean TypeScript build

---

## 4. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add architect lifecycle endpoints (§4.N) | → v2.2.0 |

---

## 5. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Frontend
cd src/Stewie.Web && npx tsc --noEmit

# Manual round-trip test:
# 1. Start API + RabbitMQ
# 2. Login as admin
# 3. Create/open a project
# 4. Click "Start Architect" → stub container launches
# 5. Send chat message → message relayed to RabbitMQ → stub echoes back
# 6. Architect response appears in chat panel
# 7. Click "Stop Architect" → container terminates
```

**Exit criteria:**
- Full Human → API → RabbitMQ → Stub Agent → RabbitMQ → API → SignalR → Browser round-trip works
- Chat messages from agent persisted as "Architect" role
- Architect start/stop lifecycle works from UI
- All tests pass

---

## 6. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-018 created for Phase 5b Chat Bridge + Architect Lifecycle |
