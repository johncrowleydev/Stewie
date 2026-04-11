---
id: JOB-023
title: "Job 023 — Agent Intelligence Dashboard + End-to-End"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-6, dashboard, frontend, e2e]
related: [PRJ-001, CON-002, CON-003, JOB-021, JOB-022]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

> **BLUF:** Dashboard enhancements for model selection, provider key management, plan approval UI, and conversation context visibility. Plus full end-to-end validation of the autonomous loop: Human chats → Architect plans → Dev executes → Architect reviews → Human sees results.

# Job 023 — Agent Intelligence Dashboard + End-to-End

---

## 1. Context

JOB-021 delivers the OpenCode runtime. JOB-022 delivers the Architect Agent loop. This job builds the frontend integration and validates the full system.

**What this job adds to the dashboard:**
1. Model/provider selector on the ArchitectControls component
2. LLM API key management in Settings
3. Plan approval buttons in the ChatPanel (approve/reject Architect plans)
4. Conversation context panel showing what the Architect "knows"
5. End-to-end smoke test documenting the full autonomous loop

**Dependencies: JOB-021 and JOB-022 must be merged to main.**

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | T-200, T-201, T-204, T-207 (Frontend components) | `feature/JOB-023-dashboard` |
| Dev B | T-202, T-203, T-205, T-206 (API + E2E + docs) | `feature/JOB-023-e2e` |

**Merge order: Either first — no code dependencies between agents.**

---

## 3. Tasks

### T-200: Model/Provider Selector

**Modify** `src/Stewie.Web/src/components/ArchitectControls.tsx`:

Add a runtime and model selector above the Start/Stop buttons.

**UI Design:**
```
┌─────────────────────────────────────┐
│ 🤖 Architect Agent                  │
│                                     │
│  Runtime: [opencode ▾]              │
│  Model:   [google/gemini-2.0-flash ▾]│
│                                     │
│  [▶ Start Architect]  [■ Stop]      │
│                                     │
│  Status: ● Active (running 2m 30s)  │
└─────────────────────────────────────┘
```

**Runtime options:** Populated from a static list initially: `stub`, `opencode`
**Model options:** Filtered by selected runtime:
- `opencode`: `google/gemini-2.0-flash`, `google/gemini-2.5-pro`, `anthropic/claude-3-haiku`, `openai/gpt-4o-mini`
- `stub`: (no model selection needed)

**Persistence:** When the user changes runtime/model, `PUT /api/projects/{id}/config` with updated `stewie.json` values.

**Acceptance criteria:**
- [ ] Dropdowns render and are functional
- [ ] Selection persists to project config
- [ ] Start button sends selected runtime to the launch API
- [ ] Follows existing CSS design system (dark/light theme compatible)

---

### T-201: Provider Key Management UI

**Modify** `src/Stewie.Web/src/pages/SettingsPage.tsx`:

Add a new section below the existing GitHub PAT section.

**UI Design:**
```
┌─────────────────────────────────────┐
│ 🔑 LLM Provider Keys               │
│                                     │
│  Google AI (Gemini)                 │
│  [••••••••••••aBcD]  [✕ Remove]     │
│                                     │
│  Anthropic (Claude)                 │
│  Not configured                     │
│  [+ Add Key]                        │
│                                     │
│  OpenAI (GPT)                       │
│  Not configured                     │
│  [+ Add Key]                        │
└─────────────────────────────────────┘
```

**Behavior:**
- Add Key: modal/inline input, masked after save
- Remove: confirmation dialog, then `DELETE /api/settings/credentials/{id}`
- Keys are shown masked (last 4 chars only)
- Uses existing Settings page styling

**Acceptance criteria:**
- [ ] Add/remove LLM API keys
- [ ] Keys displayed masked
- [ ] Visual feedback on save/delete
- [ ] Follows existing design system

---

### T-202: Provider Key API Endpoints

**Create** `src/Stewie.Api/Controllers/CredentialController.cs`:

```csharp
[ApiController]
[Route("api/settings/credentials")]
[Authorize]
public class CredentialController : ControllerBase
{
    /// <summary>List all credentials for the current user (masked values).</summary>
    [HttpGet]
    public async Task<IActionResult> List();

    /// <summary>Add a new credential.</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddCredentialRequest request);

    /// <summary>Delete a credential.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id);
}
```

**Request body for POST:**
```json
{
    "credentialType": "GoogleAiApiKey",
    "value": "AIza..."
}
```

**Response for GET:**
```json
[
    {
        "id": "guid",
        "credentialType": "GoogleAiApiKey",
        "maskedValue": "••••••••aBcD",
        "createdAt": "ISO-8601"
    }
]
```

**Business rules:**
- Only one credential per type per user
- Values are encrypted via `IEncryptionService` before storage
- GET returns masked values (last 4 chars only)
- DELETE removes the credential permanently

**Acceptance criteria:**
- [ ] POST encrypts and stores credential
- [ ] GET returns masked values
- [ ] DELETE removes credential
- [ ] 409 for duplicate credential type
- [ ] Follows existing controller patterns

---

### T-203: Plan Approval UI

**Modify** `src/Stewie.Web/src/components/ChatPanel.tsx`:

When a chat message has `messageType === "plan_proposal"`, render it with approval controls.

**UI Design:**
```
┌─────────────────────────────────────┐
│ 🤖 Architect                        │
│                                     │
│  I propose the following plan:      │
│                                     │
│  ┌─────────────────────────────┐    │
│  │ # Plan: User Authentication │    │
│  │                             │    │
│  │ ## Job 1: JWT Infrastructure│    │
│  │ - T1: Create JwtService     │    │
│  │ - T2: Add auth middleware   │    │
│  │                             │    │
│  │ ## Job 2: Login UI          │    │
│  │ - T3: Login page component  │    │
│  └─────────────────────────────┘    │
│                                     │
│  [✓ Approve Plan]  [✕ Reject]       │
│                                     │
│  Feedback: [optional notes...    ]  │
└─────────────────────────────────────┘
```

**Behavior:**
- Plan markdown rendered in a styled container (code-block-like styling)
- Approve button: sends `POST /api/chat/{projectId}/plan-decision` with `decision: "approved"`
- Reject button: same endpoint with `decision: "rejected"` + optional feedback text
- After decision: buttons replaced with status text ("✓ Approved" or "✕ Rejected")
- Decision is persisted (buttons don't reappear on page refresh)

**Acceptance criteria:**
- [ ] Plan proposals render with markdown formatting
- [ ] Approve/Reject buttons functional
- [ ] Optional feedback text field
- [ ] Buttons disabled after decision
- [ ] Follows existing ChatPanel styling

---

### T-204: Conversation Context Panel

**Create** `src/Stewie.Web/src/components/ConversationContextPanel.tsx`:

Displays what the Architect "knows" about the project — its context window.

**UI Design:**
```
┌─────────────────────────────────────┐
│ 🧠 Architect Context                │
│                                     │
│  Context usage: ████████░░ 72%      │
│  ~72,000 / 100,000 tokens           │
│                                     │
│  📋 Project: Stewie                  │
│  💬 Chat history: 24 messages        │
│  📦 Active jobs: 2                   │
│  ✓  Completed tasks: 8/12           │
│  📊 Governance reports: 4           │
│                                     │
│  Last updated: 30s ago              │
└─────────────────────────────────────┘
```

**Data source:** New API endpoint `GET /api/agents/project/{projectId}/context` that returns:
```json
{
    "tokenEstimate": 72000,
    "maxTokens": 100000,
    "chatMessageCount": 24,
    "activeJobCount": 2,
    "completedTaskCount": 8,
    "totalTaskCount": 12,
    "governanceReportCount": 4,
    "lastUpdated": "ISO-8601"
}
```

**Placement:** On `ProjectDetailPage`, below the ArchitectControls panel.

**Acceptance criteria:**
- [ ] Shows context usage as a progress bar
- [ ] Displays summary counts
- [ ] Updates when Architect session is active
- [ ] Graceful empty state when no Architect is running
- [ ] Follows existing design system

---

### T-205: CON-002 v1.9.0

**Modify** `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md`:

Add documentation for:
- `POST/GET/DELETE /api/settings/credentials` (T-202)
- `POST /api/chat/{projectId}/plan-decision` (T-194 / JOB-022)
- `GET /api/agents/project/{projectId}/context` (T-204)
- Agent token auth scheme (T-192 / JOB-022)

**Acceptance criteria:**
- [ ] All new endpoints documented with request/response schemas
- [ ] Version bumped to v1.9.0

---

### T-206: End-to-End Smoke Test

**Create** `CODEX/30_RUNBOOKS/RUN-003_End_to_End_Agent_Loop.md`:

Step-by-step runbook for validating the full autonomous loop:

1. Start infrastructure (Docker: SQL Server, RabbitMQ)
2. Build agent Docker images (opencode-agent, architect-agent)
3. Start API + Frontend (`/run_app`)
4. Configure LLM API key (or enable mock mode)
5. Select runtime + model on a project
6. Start Architect Agent
7. Send a chat message: "Create a simple REST endpoint that returns hello world"
8. Verify: Architect responds with a plan
9. Approve the plan
10. Verify: Job created, Dev Agent launched
11. Verify: Container stdout streams to dashboard
12. Verify: Dev Agent completes, Architect reports summary
13. Verify: All events persisted in Events page

**Also create** a scripted version that can be run with curl commands for CI.

**Acceptance criteria:**
- [ ] Runbook covers the full loop
- [ ] Both mock and real LLM paths documented
- [ ] Troubleshooting section for common failures

---

### T-207: Update PRJ-001 Roadmap

**Modify** `CODEX/05_PROJECT/PRJ-001_Roadmap.md`:
- Mark Phase 6 as COMPLETE
- Update all exit criteria checkboxes
- Add Phase 7 placeholder (if applicable)

**Modify** `CODEX/05_PROJECT/BCK-001_Backlog.md`:
- Add Phase 6 section with all items marked done

**Modify** `CODEX/05_PROJECT/SESSION_HANDOFF.md`:
- Update "Current State" to reflect Phase 6 completion

**Acceptance criteria:**
- [ ] Roadmap updated
- [ ] Backlog updated
- [ ] Session handoff current

---

## 4. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add credential CRUD, plan-decision, context endpoints | → v1.9.0 |

---

## 5. Verification

```bash
# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Frontend build check
cd src/Stewie.Web && npm run build

# Run tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Manual E2E: follow RUN-003 runbook
```

**Exit criteria:**
- Model selector works and persists choice
- API keys can be added/removed in Settings
- Plan approval buttons appear and function in ChatPanel
- Context panel shows Architect state
- Full end-to-end loop validated (mock or real LLM)
- All 203+ tests pass
- CON-002 v1.9.0 documented

---

## 6. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-11 | JOB-023 created for Phase 6 Dashboard + E2E |
