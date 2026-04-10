---
id: JOB-013
title: "Job 013 — Project Chat System"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5a, chat, real-time]
related: [PRJ-001, CON-002, BLU-001, JOB-012]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Build the persistent, project-scoped chat system that will serve as the Human↔Architect communication channel. Messages are persisted to SQL Server, pushed in real-time via SignalR (JOB-012), and rendered in a chat panel UI. This job builds the data layer and UI only — actual AI/LLM integration is Phase 6.

# Job 013 — Project Chat System

---

## 1. Context

Stewie's core interaction model is a **chat between the Human and the Architect Agent**. Each project has its own persistent conversation. This job builds:

1. **ChatMessage entity** — persisted to SQL Server
2. **Chat API** — REST endpoints for sending and retrieving messages
3. **Real-time delivery** — messages pushed to project subscribers via SignalR
4. **Chat UI** — a chat panel for the project page

The Architect Agent is NOT wired in this job — messages are stored and displayed, but no AI responds. That's Phase 6. For now, the system supports `Human` and `System` sender roles. The `Architect` role will be used once AI is integrated.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Backend (entity, migration, repository, controller, SignalR integration) | `feature/JOB-013-backend` |
| Dev B | Frontend (ChatPanel component, ProjectChatPage, CSS) | `feature/JOB-013-frontend` |

**Merge order: Dev A first, Dev B rebases onto main after Dev A merges.**

**Dependency: JOB-012 must be merged to main before starting this job.**

---

## 3. Dependencies

- **Requires JOB-012 merged to main** — SignalR hub infrastructure, `IRealTimeNotifier`, `useSignalR` hook.

---

## 4. Tasks

### Dev A — Backend

#### T-130: ChatMessage Entity

**Create** `Stewie.Domain/Entities/ChatMessage.cs`:

```csharp
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>Who sent the message: "Human", "Architect", or "System"</summary>
    public string SenderRole { get; set; } = string.Empty;

    /// <summary>Display name of the sender (username for Human, "Architect" for agent, etc.)</summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Message text content. Supports markdown.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was created.</summary>
    public DateTime CreatedAt { get; set; }
}
```

**Acceptance criteria:**
- [ ] Entity follows existing domain patterns
- [ ] No FK navigation properties (NHibernate mapped via separate mapping file)

---

#### T-131: ChatMessage NHibernate Mapping + Migration

**Create** `Stewie.Infrastructure/Persistence/Mappings/ChatMessageMap.cs`:
- Map to `ChatMessages` table
- `Id` as GUID primary key
- `ProjectId` indexed (frequent lookups)
- `CreatedAt` indexed (for ordering)

**Create** FluentMigrator migration:
- `ChatMessages` table: `Id` (uniqueidentifier PK), `ProjectId` (uniqueidentifier NOT NULL), `SenderRole` (nvarchar(50) NOT NULL), `SenderName` (nvarchar(100) NOT NULL), `Content` (nvarchar(max) NOT NULL), `CreatedAt` (datetime2 NOT NULL)
- Index on `ProjectId`
- Index on `CreatedAt`

**Acceptance criteria:**
- [ ] Migration runs clean on fresh DB
- [ ] NHibernate mapping verified by integration test

---

#### T-132: IChatMessageRepository + Implementation

**Create** `Stewie.Application/Interfaces/IChatMessageRepository.cs`:

```csharp
public interface IChatMessageRepository
{
    Task<ChatMessage> SaveAsync(ChatMessage message);
    Task<IList<ChatMessage>> GetByProjectIdAsync(Guid projectId, int limit = 100, int offset = 0);
    Task<int> GetCountByProjectIdAsync(Guid projectId);
}
```

**Create** `Stewie.Infrastructure/Repositories/ChatMessageRepository.cs`:
- Standard NHibernate implementation
- `GetByProjectIdAsync` orders by `CreatedAt` ascending (oldest first — chat convention)
- Supports pagination via `limit`/`offset`

**Register** in `Program.cs` DI container.

**Acceptance criteria:**
- [ ] Pagination works correctly
- [ ] Default ordering is oldest-first
- [ ] Empty project returns empty list (not null)

---

#### T-133: ChatController REST Endpoints

**Create** `Stewie.Api/Controllers/ChatController.cs`:

```csharp
[ApiController]
[Route("api/projects/{projectId}/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    /// <summary>Get chat history for a project (paginated).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMessages(
        Guid projectId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0);

    /// <summary>Send a new chat message to a project.</summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage(
        Guid projectId,
        [FromBody] SendChatMessageRequest request);
}
```

**Request body** for POST:
```json
{
    "content": "string (required, max 10000 chars)"
}
```

**Response** for POST (201 Created):
```json
{
    "id": "guid",
    "projectId": "guid",
    "senderRole": "Human",
    "senderName": "admin",
    "content": "Hello architect!",
    "createdAt": "2026-04-10T17:00:00Z"
}
```

**Response** for GET:
```json
{
    "messages": [...],
    "total": 42,
    "limit": 100,
    "offset": 0
}
```

**Business rules:**
- `senderRole` is always `"Human"` for HTTP API calls (agent messages will come via RabbitMQ in Phase 5b)
- `senderName` is extracted from JWT claims (the authenticated username)
- After persisting, call `IRealTimeNotifier.NotifyChatMessageAsync(...)` to push via SignalR
- Validate `projectId` exists (return 404 if not)
- Validate content is not empty and ≤ 10000 chars

**Acceptance criteria:**
- [ ] GET returns paginated messages ordered oldest-first
- [ ] POST persists message AND pushes via SignalR
- [ ] 404 for nonexistent project
- [ ] 400 for empty or oversized content
- [ ] 401 for unauthenticated requests

---

#### T-134: Chat Integration Tests

**Create** `Stewie.Tests/Integration/ChatControllerTests.cs`:

| Test | Description |
|:-----|:------------|
| `SendMessage_Returns201_PersistsMessage` | POST → 201, GET returns the message |
| `GetMessages_EmptyProject_ReturnsEmptyList` | No messages → empty array, total=0 |
| `GetMessages_Pagination_RespectsLimitOffset` | Send 5, query limit=2 offset=2 → correct slice |
| `SendMessage_InvalidProject_Returns404` | Nonexistent projectId → 404 |
| `SendMessage_EmptyContent_Returns400` | Empty body → 400 |
| `GetMessages_Unauthenticated_Returns401` | No JWT → 401 |

**Acceptance criteria:**
- [ ] All 6 tests pass
- [ ] No regressions in existing integration tests

---

### Dev B — Frontend

#### T-135: Chat API Client Functions

**Add to** `api/client.ts`:

```typescript
export async function fetchChatMessages(
    projectId: string,
    limit?: number,
    offset?: number
): Promise<ChatMessagesResponse>;

export async function sendChatMessage(
    projectId: string,
    content: string
): Promise<ChatMessage>;
```

**Types** (add to `types.ts`):
```typescript
export interface ChatMessage {
    id: string;
    projectId: string;
    senderRole: "Human" | "Architect" | "System";
    senderName: string;
    content: string;
    createdAt: string;
}

export interface ChatMessagesResponse {
    messages: ChatMessage[];
    total: number;
    limit: number;
    offset: number;
}
```

**Acceptance criteria:**
- [ ] Functions work with real API
- [ ] Types match backend response schema

---

#### T-136: ChatPanel Component

**Create** `components/ChatPanel.tsx`:

A scrollable chat panel with:
- Message list (scrolled to bottom on new messages)
- Each message bubble shows: sender name, role badge, content, timestamp
- Human messages aligned right, Architect/System messages aligned left
- Input area at the bottom: textarea + send button
- Connects to SignalR project group for real-time updates
- Loads history on mount via `fetchChatMessages`
- Appends new messages received via `ChatMessageReceived` SignalR event
- Auto-scrolls to latest message

**Design specification:**
- Use existing CSS design system (variables from `index.css`)
- Message bubbles: Human = primary green, Architect = secondary gray, System = muted
- Typing indicator placeholder (for future AI integration)
- Responsive: full-width on mobile, constrained max-width on desktop
- Empty state: "Start a conversation with the Architect"

**Acceptance criteria:**
- [ ] Renders chat history on mount
- [ ] New messages appear instantly via WebSocket
- [ ] Auto-scrolls to bottom on new message
- [ ] Human messages visually distinct from Architect/System
- [ ] Input validates (non-empty, ≤ 10000 chars)
- [ ] Send button disabled while submitting

---

#### T-137: ChatPanel CSS

**Add to** `index.css` (or create `chat.css` and import):

Chat-specific styles:
- `.chat-panel` — full-height flex container
- `.chat-messages` — scrollable area with `overflow-y: auto`
- `.chat-bubble` — message bubble with role-based colors
- `.chat-bubble--human` — right-aligned, green tint
- `.chat-bubble--architect` — left-aligned, gray tint
- `.chat-bubble--system` — left-aligned, muted, italic
- `.chat-input` — sticky bottom input area
- `.chat-sender` — name + role badge
- `.chat-time` — timestamp
- Smooth scroll animations
- Responsive at mobile breakpoints

**Acceptance criteria:**
- [ ] Visually polished chat interface
- [ ] Dark mode compatible (uses CSS variables)
- [ ] Responsive layout

---

#### T-138: Project Chat Page / Route

**Option A:** Add a chat tab/section to the existing project detail page.
**Option B:** Create a new `ProjectChatPage` at `/projects/:id/chat`.

**Recommended: Option A** — add a chat section below the project jobs list on the existing project page. The chat panel should be collapsible or always-visible in a side panel.

**Modify** `pages/ProjectDetailPage.tsx` (or equivalent):
- Add `ChatPanel` component below project metadata
- Pass `projectId` as prop
- Handle loading/error states

**Add route** if creating a separate page: `/projects/:id/chat`

**Acceptance criteria:**
- [ ] Chat accessible from project context
- [ ] Works with existing navigation
- [ ] Loads project chat history on mount

---

#### T-139: TypeScript Build Verification

**Run:** `npx tsc --noEmit` — must produce zero errors.

**Acceptance criteria:**
- [ ] Clean TypeScript build

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add `GET/POST /api/projects/{id}/chat` endpoints to §4 | → v2.0.0 |

---

## 6. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# All tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Chat-specific tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --filter "FullyQualifiedName~Chat"

# Frontend
cd src/Stewie.Web/ClientApp && npx tsc --noEmit
```

**Exit criteria:**
- All existing tests pass (no regressions)
- 6 new chat integration tests pass
- Chat messages persist and display correctly
- Real-time SignalR push works for new messages
- TypeScript builds clean

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-013 created for Phase 5a Project Chat System |
| 2026-04-10 | JOB-013 CLOSED. Passed audit VER-013 with 59 tests passing. |
