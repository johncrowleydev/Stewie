---
id: JOB-022
title: "Job 022 — Architect Agent Loop"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-6, architect-agent, chat, llm]
related: [PRJ-001, BLU-001, CON-004, CON-002, JOB-021, JOB-018]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

> **BLUF:** Build the Architect Agent's brain — a containerized process that receives human chat, plans work via LLM, presents plans for approval, creates jobs on approval, monitors Dev Agents, and reports results. Uses the plan-first workflow: Architect proposes, Human approves, then Architect executes.

# Job 022 — Architect Agent Loop

---

## 1. Context

JOB-021 delivers the OpenCode agent runtime and Docker image. This job builds on top of it to create the Architect Agent — the core intelligence loop that:

1. Listens for human chat messages on the `architect.{projectId}` RabbitMQ queue
2. Builds context from project state + chat history
3. Calls LLM (via OpenCode) to generate a structured plan
4. Sends the plan to the Human for approval via chat
5. On approval: creates jobs and tasks via the Stewie REST API
6. Launches Dev Agent containers to execute tasks
7. Monitors Dev Agent completion and reports back to the Human

**Architect autonomy:** Uses `plan_first` mode by default (configurable via `stewie.json`). The Architect proposes plans and waits for explicit Human approval before creating or executing anything.

**Dependency: JOB-021 must be merged to main.**

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev B | All tasks (T-190 through T-199) | `feature/JOB-022-architect-loop` |

---

## 3. Prerequisites

| File | Why |
|:-----|:----|
| `docker/opencode-agent/entrypoint.py` | Base harness this extends (from JOB-021) |
| `src/Stewie.Api/Controllers/AgentsController.cs` | Agent launch API |
| `src/Stewie.Api/Controllers/ChatController.cs` | Chat API the Architect calls |
| `src/Stewie.Api/Controllers/JobsController.cs` | Job creation API for plan execution |
| `CODEX/20_BLUEPRINTS/CON-004_Agent_Messaging_Contract.md` | Message schemas |
| `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` | REST API contract |
| `src/Stewie.Infrastructure/Services/RabbitMqConsumerHostedService.cs` | How API consumes agent events |

---

## 4. Tasks

### T-190: Architect Entry Script

**Create** `docker/architect-agent/architect_main.py`:

The Architect Agent's main loop. This is the brain.

**Flow:**

```
start() →
  connect_to_rabbitmq() →
  consume_chat_queue(architect.{projectId}) →
    on_human_message(msg):
      context = build_context(project_id)
      plan = call_llm(context + msg.content)
      send_plan_to_chat(plan)
      wait_for_approval():
        on_approved: execute_plan(plan)
        on_rejected: acknowledge, wait for next message
    
    execute_plan(plan):
      jobs = parse_plan_to_jobs(plan)
      for job in jobs:
        create_job_via_api(job)
        for task in job.tasks:
          create_task_via_api(task)
          launch_dev_agent(task)
      monitor_agents(jobs)
    
    monitor_agents(jobs):
      # Subscribe to event.completed / event.failed from dev agents
      # Report progress to human via chat
      # On all tasks complete: summarize results in chat
```

**Approval waiting mechanism:** After sending a plan, the Architect subscribes to its command queue for `command.plan_approved` or `command.plan_rejected` messages. This is blocking — the Architect does nothing until Human responds.

**LLM invocation:** Uses `opencode run --model {provider/model} "prompt"` subprocess (inherited from JOB-021 entrypoint pattern).

**System prompt for the LLM:**
```
You are the Architect Agent for the Stewie project management system.
Given the project context and human request, produce a structured job plan.
Output your plan as a JSON object with this schema:
{
  "summary": "human-readable plan description",
  "jobs": [{
    "title": "...",
    "tasks": [{
      "title": "...",
      "description": "...",
      "role": "developer | tester",
      "dependsOn": []
    }]
  }]
}
Include a plain-text explanation before the JSON block.
```

**Acceptance criteria:**
- [ ] Connects to RabbitMQ and consumes chat messages
- [ ] Calls LLM with project context
- [ ] Sends structured plan to chat
- [ ] Waits for approval before executing
- [ ] Creates jobs and tasks via Stewie API on approval
- [ ] Reports results back to Human via chat
- [ ] Works in mock LLM mode

---

### T-191: Stewie API Client (Python)

**Create** `docker/architect-agent/stewie_api_client.py`:

Lightweight HTTP client for agent containers to interact with the Stewie REST API.

**Methods:**
```python
class StewieApiClient:
    def __init__(self, base_url: str, agent_token: str)
    
    # Project
    def get_project(self, project_id: str) -> dict
    
    # Jobs
    def create_job(self, project_id: str, title: str, description: str) -> dict
    def get_jobs(self, project_id: str) -> list
    
    # Tasks
    def create_task(self, job_id: str, title: str, description: str, role: str) -> dict
    def update_task_status(self, task_id: str, status: str) -> dict
    
    # Chat
    def get_chat_history(self, project_id: str, limit: int = 50) -> list
    def send_chat_message(self, project_id: str, content: str, sender_role: str = "Architect") -> dict
    
    # Agents
    def launch_agent(self, project_id: str, role: str, runtime: str, task_id: str = None) -> dict
    def get_agent_sessions(self, project_id: str) -> list
```

**Auth:** All requests include `Authorization: Bearer {agent_token}`. Token is read from `/run/secrets/agent_token`.

**Error handling:** Raise descriptive exceptions on 4xx/5xx responses. Log request/response for debugging.

**Acceptance criteria:**
- [ ] All listed methods implemented
- [ ] Proper auth header on every request
- [ ] Error handling with clear messages
- [ ] Docstrings on all methods

---

### T-192: Agent Session Tokens

**Create** `src/Stewie.Application/Services/AgentTokenService.cs`:

Issues short-lived JWT tokens scoped to agent sessions.

```csharp
public class AgentTokenService
{
    /// <summary>Generate a JWT for an agent session. Expires in 24h.</summary>
    public string GenerateAgentToken(Guid sessionId, Guid projectId, string agentRole);
    
    /// <summary>Validate an agent token and return claims.</summary>
    public ClaimsPrincipal? ValidateAgentToken(string token);
}
```

**Token claims:**
- `sub` = session ID
- `project_id` = project ID
- `role` = "agent"
- `agent_role` = "architect" | "developer" | "tester"
- `exp` = 24 hours from issue

**Modify** `src/Stewie.Api/Program.cs`:
- Add a second JWT validation scheme for agent tokens (name: "AgentBearer")
- Agent tokens use the same secret but a different `Issuer` claim ("stewie-agent" vs "stewie")

**Modify** `src/Stewie.Application/Services/AgentLifecycleService.cs`:
- After launching the container, generate an agent token
- Write the token to the secrets mount directory: `/tmp/stewie-secrets-{sessionId}/agent_token`

**Acceptance criteria:**
- [ ] Agent tokens generated on launch
- [ ] Token written to secrets mount path
- [ ] API accepts agent tokens for job/task/chat endpoints
- [ ] Agent tokens cannot access admin endpoints (Settings, Users)

---

### T-193: Conversation Context Builder

**Create** `docker/architect-agent/context_builder.py`:

Assembles LLM context from multiple sources.

```python
class ContextBuilder:
    def __init__(self, api_client: StewieApiClient)
    
    def build_context(self, project_id: str, human_message: str) -> str:
        """Build a complete LLM prompt with project context."""
        # 1. Project metadata (name, repo URL, description)
        # 2. Chat history (last 20 messages)
        # 3. Active jobs and their status
        # 4. Recent governance reports (if any)
        # 5. The human's current message
        # Returns: formatted prompt string
    
    def estimate_tokens(self, text: str) -> int:
        """Rough token estimate (chars / 4)."""
    
    def truncate_to_budget(self, text: str, max_tokens: int = 100000) -> str:
        """Truncate oldest context to fit within token budget."""
```

**Context template:**
```
## Project: {project.name}
Repository: {project.repoUrl}

## Recent Conversation
{formatted chat history}

## Active Jobs
{job summaries with task status}

## Human's Request
{human_message}

## Your Instructions
You are the Architect Agent. Based on the above context, produce a plan...
```

**Acceptance criteria:**
- [ ] Assembles context from project state + chat history
- [ ] Handles empty state gracefully (new project, no history)
- [ ] Token estimation is reasonable (within 2x of actual)
- [ ] Truncation preserves most recent messages

---

### T-194: Plan Approval Protocol

**Extend CON-004** with two new message types:

#### `chat.plan_proposed` (Architect → API → Human)

Published by the Architect when it has a plan ready for review.

```json
{
  "type": "chat.plan_proposed",
  "payload": {
    "agentId": "uuid",
    "projectId": "uuid",
    "planId": "uuid",
    "summary": "I propose creating 2 jobs with 5 tasks...",
    "planMarkdown": "# Plan\n\n## Job 1: ...",
    "planJson": { "jobs": [...] }
  }
}
```

#### `command.plan_decision` (API → Architect)

Sent when the Human approves or rejects a plan.

```json
{
  "type": "command.plan_decision",
  "payload": {
    "planId": "uuid",
    "decision": "approved | rejected",
    "feedback": "optional human feedback text"
  }
}
```

**Modify** `src/Stewie.Infrastructure/Services/RabbitMqConsumerHostedService.cs`:
- Handle `chat.plan_proposed` events — persist as `ChatMessage` with `SenderRole = "Architect"` and a special `MessageType = "plan_proposal"` marker

**Modify** `src/Stewie.Domain/Entities/ChatMessage.cs`:
- Add `public virtual string? MessageType { get; set; }` (nullable, for backwards compat)
- Values: `null` (plain chat), `"plan_proposal"`, `"plan_approved"`, `"plan_rejected"`

**Create** FluentMigrator migration for the new column.

**Modify** `src/Stewie.Api/Controllers/ChatController.cs`:
- Add `POST /api/chat/{projectId}/plan-decision` endpoint
- This endpoint publishes `command.plan_decision` to the Architect's command queue via RabbitMQ

**Acceptance criteria:**
- [ ] CON-004 updated with new message types
- [ ] API persists plan proposals as chat messages
- [ ] API can send plan decisions to Architect via RabbitMQ
- [ ] Migration adds new column without breaking existing data

---

### T-195: Job Creation from Plan

**Create** `docker/architect-agent/job_parser.py`:

Parses structured LLM output into Stewie API calls.

```python
class JobParser:
    def __init__(self, api_client: StewieApiClient)
    
    def parse_and_create(self, project_id: str, plan_json: dict) -> list:
        """Parse a plan JSON and create jobs/tasks via the API.
        Returns list of created job IDs.
        """
        # 1. Validate plan structure
        # 2. For each job in plan:
        #    a. Create job via API
        #    b. Create tasks for the job
        #    c. Create task dependencies
        # 3. Return created job IDs
    
    def validate_plan(self, plan_json: dict) -> list[str]:
        """Validate plan structure. Returns list of errors (empty = valid)."""
```

**Validation rules:**
- Each job must have a title
- Each task must have a title, description, and valid role
- Dependency references must point to tasks within the same plan
- Maximum 10 tasks per job, maximum 3 jobs per plan (guardrails for early testing)

**Acceptance criteria:**
- [ ] Parses valid plan JSON into API calls
- [ ] Validates plan structure before creating anything
- [ ] Returns clear errors for invalid plans
- [ ] Creates tasks with correct dependency relationships

---

### T-196: Dev Agent Monitoring

**Add to** `docker/architect-agent/architect_main.py`:

After creating jobs and launching Dev Agents, the Architect monitors their progress.

**Monitoring loop:**
1. Subscribe to `stewie.events` exchange for events matching launched agent IDs
2. On `event.progress`: log and optionally report to Human
3. On `event.completed`: mark task complete, check if all tasks done
4. On `event.failed`: log error, report to Human, suggest next steps
5. On all tasks complete: send summary to Human via chat

**Human updates:** Don't spam the Human with every progress event. Send updates on:
- First task starting
- Task completion
- Task failure
- All tasks complete (summary)

**Acceptance criteria:**
- [ ] Monitors all launched agent sessions
- [ ] Reports meaningful updates to Human (not every event)
- [ ] Handles both completion and failure gracefully
- [ ] Sends final summary when all tasks are done

---

### T-197: ArchitectMode Config

**Modify** `CODEX/20_BLUEPRINTS/CON-003_Project_Configuration.md`:
- Add `architectMode` field to `stewie.json` schema:
```json
{
  "architectMode": "plan_first",  // "plan_first" (default) | "auto_execute"
  "defaultRuntime": "opencode",
  "defaultModel": "google/gemini-2.0-flash"
}
```

**Modify** `src/Stewie.Application/Services/ProjectConfigService.cs`:
- Parse `architectMode`, `defaultRuntime`, `defaultModel` from `stewie.json`
- Expose via properties

**Acceptance criteria:**
- [ ] Config parsed from stewie.json
- [ ] Missing fields use sensible defaults
- [ ] Architect entry script reads config via API

---

### T-198: Architect Dockerfile

**Create** `docker/architect-agent/Dockerfile`:

```dockerfile
FROM stewie-opencode-agent:latest

# Copy Architect-specific scripts
COPY architect_main.py /app/architect_main.py
COPY stewie_api_client.py /app/stewie_api_client.py
COPY context_builder.py /app/context_builder.py
COPY job_parser.py /app/job_parser.py

# Install additional Python dependencies
RUN pip3 install --break-system-packages requests==2.31.0

# Override entrypoint to use Architect main
ENTRYPOINT ["python3", "/app/architect_main.py"]
```

**Acceptance criteria:**
- [ ] Builds successfully on top of stewie-opencode-agent
- [ ] All Architect scripts accessible
- [ ] `requests` library available for HTTP client

---

### T-199: Integration Tests

**Create** `src/Stewie.Tests/Integration/ArchitectLoopTests.cs`:

End-to-end tests using `WebApplicationFactory` with mock LLM responses.

| Test | Description |
|:-----|:------------|
| `HumanChat_ArchitectResponds_WithPlan` | Send chat → verify plan message returned |
| `PlanApproval_CreatesJob` | Approve plan → verify job created in DB |
| `PlanRejection_ArchitectAcknowledges` | Reject plan → verify acknowledgment in chat |
| `DevAgentCompletion_ArchitectReports` | Simulate dev completion → verify summary in chat |

> **Note:** These tests will use the mock LLM and may require RabbitMQ. Mark with `[Trait("Category", "Integration")]`.

**Acceptance criteria:**
- [ ] All 4 tests pass with mock LLM
- [ ] Tests skip gracefully without RabbitMQ

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-002 | Add plan-decision endpoint | → v1.9.0 (JOB-023) |
| CON-003 | Add architectMode, defaultRuntime, defaultModel | → v1.1.0 |
| CON-004 | Add chat.plan_proposed, command.plan_decision | → v1.1.0 |

---

## 6. Verification

```bash
# Build architect agent image (requires JOB-021 image built first)
cd docker/architect-agent && docker build -t stewie-architect-agent .

# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Run tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Manual end-to-end (with mock LLM):
# 1. Start API + Frontend with /run_app
# 2. Launch architect agent with mock mode
# 3. Send chat message from dashboard
# 4. Verify plan appears in chat
# 5. Approve plan → verify job created
```

**Exit criteria:**
- Architect Agent receives Human chat and produces a plan
- Plan appears in the dashboard chat panel
- Human approval triggers job creation
- Dev Agents are launched for created tasks
- Architect reports completion to Human
- All tests pass

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-11 | JOB-022 created for Phase 6 Architect Agent Loop |
