---
id: JOB-020
title: "Self-Healing Architect Lifecycle"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, sprint, architecture, health-check, phase-5b]
related: [BLU-001, CON-004, GOV-008]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

# JOB-020: Self-Healing Architect Lifecycle

> **BLUF:** Implement live Docker health checks inside the `AgentLifecycleService` and defensive 409 rejections inside `ChatController` to eliminate "zombie sessions" where the UI believes an Architect is online despite process failures.

## 1. Background
During Phase 5b testing, organic container failures and raw CLI manipulations led to the SQL Server `AgentSessions` table slipping out-of-sync with the physical Docker topology. Because the UI and Chat endpoints trusted the SQL state implicitly, human users were presented an active Chat interface, leading to silently dropped messages and a broken user experience.

## 2. Objective
Convert the static database queries for `Active` Architects into active polling verifications natively within the C# Backend, enabling the `React UI` to automatically repair its state on its 5-second polling tick, while aggressively rejecting human input when containers are inherently offline.

## 3. Tasks

### Task 1: On-Demand Health Verification in `AgentLifecycleService`
- Override the simplistic `GetActiveArchitectAsync` and `GetStatusAsync` methods.
- After lifting the `AgentSession` row from NHibernate, if the session status evaluates to `Active` or `Starting`, load the corresponding `IAgentRuntime` (e.g., `StubAgentRuntime`).
- Invoke `await runtime.GetStatusAsync(session.ContainerId)`.
- If the Docker daemon responds with a Terminated/Failed state, auto-heal the disconnect:
    - Update the session `Status` locally and push to NHibernate as `Terminated`.
    - Append an appropriate `StopReason`.
    - Broadcast a status update event via `IRealTimeNotifier.NotifyAgentStatusChangedAsync`.
    - `GetActiveArchitectAsync` MUST return `null` if the container is conclusively offline.

### Task 2: Defensive Chat Rejections in `ChatController`
- Enhance the `[HttpPost("projects/{projectId}/chat")] SendMessage` route.
- Execute `var architectSession = await _lifecycle.GetActiveArchitectAsync(projectId)`.
- If the result is `null`:
    - Abort processing prior to `_chatRepo.SaveAsync`.
    - Return `StatusCode(409, new { error = "Architect is offline. Please start a new session." })`.
- Ensure all other downstream logic remains untouched.

### Task 3: Regression Auditing & Tests
- Trigger existing Phase 5b unit and integration verification protocols. 
- Build a lightweight `xUnit` mock covering `AgentLifecycleService.GetActiveArchitectAsync` returning an underlying dead process. Verify the NHibernate Commit triggers.

## 4. Exit Criteria
This job is considered structurally complete when:
- [x] A developer agent pulls the `feature/JOB-020-self-healing-architect` working branch.
- [x] Manual deletion of an active Architect Docker container results in the `React UI` gracefully reverting to the default "Start Architect" state within a 5-second window.
- [x] Submitting a Chat Message towards a dead container correctly throws a toast failure on the UI.
- [x] Testing protocol tiers verify unit execution without failures.
