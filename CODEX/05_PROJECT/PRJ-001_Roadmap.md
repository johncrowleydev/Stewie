---
id: PRJ-001
title: "Stewie — Project Roadmap"
type: explanation
status: APPROVED
owner: human
agents: [all]
tags: [project-management, roadmap, governance, agentic-development]
related: [BCK-001, GOV-008, BLU-001]
created: 2026-04-09
updated: 2026-04-11
version: 3.0.0
---

> **BLUF:** Stewie is an autonomous AI development platform where a Human interacts exclusively with an Architect Agent through a chat interface. The Architect Agent plans work, creates jobs, spins up Developer Agent containers, monitors their output, and enforces governance — all without the Human touching code, jobs, or tasks directly. Stewie is the control plane; the agents are the intelligence.

# Stewie — Project Roadmap

> **This document is authored by the Human. The Architect Agent maintains it.**

---

## 1. Project Vision

Stewie automates the process of turning a Human's vision into working, governed software.

**The end-state experience:**
1. Human opens a project → sees a chat window
2. Human describes what they want in natural language
3. The Architect Agent (an LLM) plans the work, breaks it into jobs and tasks
4. The Architect spins up Developer Agent containers to execute the work in parallel
5. Dev Agents write code, commit, push, and report back via a message bus
6. The Architect reviews output, enforces governance, and iterates
7. When a Dev Agent is blocked, the Architect answers — or escalates to the Human
8. The Human watches progress in real time and provides guidance via chat

**The Human never creates jobs, writes task specs, or manages agents directly.** They chat with the Architect; the Architect handles everything else.

### What Stewie Is

- **Control plane** — manages state, persistence, messaging, and the dashboard
- **Agent orchestrator** — spins up, monitors, and tears down LLM agent containers
- **Communication hub** — routes messages between Human, Architect, and Dev Agents via RabbitMQ
- **Governance engine** — enforces CODEX standards on all agent output
- **System of record** — SQL Server is the single source of truth for all state

### What Stewie Is NOT

- Stewie is not an AI itself — it orchestrates AI agents
- Stewie does not write code — agents running inside containers do
- Stewie is not an IDE replacement — it's the infrastructure behind autonomous development

### Architecture

```
Human ←—— chat (SignalR) ——→ Stewie.Api (control plane)
                                     ↕
                               RabbitMQ (message bus)
                            ↙        ↓         ↘
                     Architect     Dev A       Dev B
                     Agent         Agent       Agent
                     (container)  (container) (container)
                     [claude-code] [aider]    [open-code]
```

- **Stewie.Api** — the control plane. Serves the dashboard, persists state, manages SignalR connections, publishes/subscribes to RabbitMQ. Contains zero AI.
- **Architect Agent** — an LLM agent running in a container. Connected to RabbitMQ. Receives Human messages, plans work, creates jobs via Stewie API, monitors Dev Agents, reports back.
- **Dev Agents** — ephemeral LLM agent containers. Spun up per task, connected to RabbitMQ. Work on assigned tasks, can ask the Architect questions, exit when done.

### Agent Runtime Abstraction

Agent runtimes are pluggable — the same way `IGitPlatformService` abstracts GitHub/GitLab/Bitbucket:

```
IAgentRuntime
├── ClaudeCodeRuntime     (Claude Code CLI in a container)
├── OpenCodeRuntime       (OpenCode CLI in a container)
├── AiderRuntime          (Aider CLI in a container)
└── DirectApiRuntime      (raw LLM API calls, no framework)
```

Each runtime knows how to build/launch a container, configure it with the right LLM provider and API keys, wire it to RabbitMQ, and manage its lifecycle. The model and runtime can be configured per project.

---

## 2. Guiding Principles

- **Governance First:** All work must follow CODEX governance, respect contracts, and pass validation before integration.
- **Control Plane Owns Truth:** Stewie.Api is the system-of-record. Agents are stateless and replaceable.
- **Chat is the Primary Interface:** The Human interacts only through conversation with the Architect Agent.
- **Agents Communicate Through the Message Bus:** All agent-to-agent coordination flows through RabbitMQ. Agents never talk directly to each other.
- **Isolation by Default:** Each task runs in its own workspace and its own container.
- **Git is the Integration Layer:** All code changes flow through Git branches and PRs.
- **Agents are Replaceable:** Agent runtimes and LLM providers are pluggable and vendor-agnostic. No lock-in.
- **Human Retains Final Authority:** The Architect can plan and execute autonomously, but the Human can intervene, redirect, or override at any time.

---

## 3. Scope

### 3.1 In Scope
- Job/Task lifecycle management
- Workspace creation and cleanup
- Container-based agent execution with pluggable runtimes
- Human ↔ Architect real-time chat (SignalR)
- Agent ↔ Agent messaging (RabbitMQ)
- Live container output streaming
- SQL Server persistence (NHibernate)
- React dashboard for chat + monitoring
- CODEX governance enforcement
- Multi-model, multi-provider LLM support

### 3.2 Out of Scope (current)
- Full autonomy without Human oversight
- IDE replacement
- Complex infrastructure (Kubernetes, cloud orchestration)
- Multi-repo orchestration
- Self-modifying governance (governance updates require Human approval)

---

## 4. Delivery Phases

Phases are **scope-bounded**, not time-bounded.

### Phase 0: Foundation ✅ COMPLETE
**Goal:** Prove the end-to-end execution loop works.
**Exit criteria:**
- [x] Run → Task → workspace → container → result ingestion → SQL persistence
- [x] Container runtime validated (dummy worker)
- [x] Runtime contracts validated (task.json / result.json)
- [x] Database auto-bootstrap (FluentMigrator)

---

### Phase 1: Core Orchestration (MVP) ✅ COMPLETE
**Goal:** Build a functional single-repo orchestrator with real task management.
**Exit criteria:**
- [x] Project entity with repo association
- [x] Job supports multiple task states (not just test run)
- [x] Event entity for audit trail
- [x] Workspace entity for lifecycle tracking
- [x] API endpoints for CRUD on all core entities
- [x] React dashboard shows Runs, Tasks, and their statuses
- [x] Health check endpoint (`GET /health`)

**Key deliverables:**
- `CON-001` — Runtime Contract (task.json / result.json) — v1.1.0
- `CON-002` — API Contract (HTTP endpoints) — v1.1.0
- `BLU-001` — Stewie System Blueprint
- `JOB-001` — Foundation sprint (CLOSED)
- `JOB-002` — Phase 1 closure + Phase 2 plumbing (CLOSED)

---

### Phase 2: Real Repo Interaction ✅ COMPLETE
**Goal:** Workers can clone, modify, and commit to real Git repositories.
**Exit criteria:**
- [x] Workspace prepares a real Git clone from a target repo
- [x] Workers can read and mutate files in the cloned workspace
- [x] Result ingestion includes file diff summary
- [x] Git integration for branch creation and commit

**Key deliverables:**
- `CON-001` — Runtime Contract v1.2.0 (script field, repoUrl, branch)
- `CON-002` — API Contract v1.2.0 (Run creation body, diff/branch fields)
- `JOB-003` — Real Repo Interaction sprint (CLOSED)

---

### Phase 2.5: GitHub Integration + User System ✅ COMPLETE
**Goal:** Secure the platform with authentication, add encrypted credential storage, and integrate with GitHub for automated push/PR workflows.
**Exit criteria:**
- [x] JWT-based authentication (BCrypt hashing, 24-hr sessions)
- [x] Invite-only user registration
- [x] AES-256-CBC encrypted credential storage for GitHub PATs
- [x] GitHub API integration (push branch, create PR, create repo)
- [x] Auth UI (login, registration, settings pages)
- [x] All API endpoints secured with `[Authorize]`

**Key deliverables:**
- `CON-002` — API Contract v1.3.0 (auth endpoints, user endpoints, GitHub token)
- `JOB-004` — GitHub Integration + User System sprint (CLOSED)

---

### Phase 2.75: Repository Automation + Platform Abstraction ✅ COMPLETE
**Goal:** Abstract the git hosting interface for multi-provider support, wire repo creation into project creation, and harden the worker pipeline with timeout enforcement and retry logic.
**Exit criteria:**
- [x] Platform-agnostic `IGitPlatformService` interface (GitHub as first implementation)
- [x] Project creation supports both linking existing repos and creating new repos via platform API
- [x] 300s container timeout enforced (CON-001 §7)
- [x] Retry logic for transient container failures with error taxonomy

**Key deliverables:**
- `CON-002` — API Contract v1.4.0 (extended project creation, repoProvider field)
- `JOB-005` — Repository Automation sprint (CLOSED)

---

### Phase 3: Governance Engine ✅ COMPLETE
**Goal:** Stewie enforces CODEX governance on worker output automatically via a tester task that runs after every developer task. Includes sequential task chains (dev → tester → retry loop).
**Exit criteria:**
- [x] Sequential task chains: dev task → tester task → accept/reject/retry (JOB-007)
- [x] Governance worker container image (stack-extensible, .NET first) (JOB-007, JOB-008)
- [x] All 8 GOV docs encoded as automated deterministic rules (JOB-008, 15 rules)
- [x] GovernanceReport entity with per-rule pass/fail results (JOB-007)
- [x] Rejection workflow: governance failure → re-run worker with violation feedback (JOB-007)
- [x] Governance audit trail in Events (GovernanceStarted, Passed, Failed) (JOB-007)
- [x] Dashboard displays task chain + governance report per job (JOB-008)
- [x] Configurable max retry attempts (default: 2) (JOB-007)

**Jobs:**
- `JOB-007` — Sequential Task Chains + Governance Infrastructure (CLOSED)
- `JOB-008` — Governance Rule Engine + Frontend (CLOSED)

---

### Phase 4: Multi-Task Jobs ✅ COMPLETE
**Goal:** A single Job can spawn and coordinate multiple parallel Tasks with dependency graphs.
**Completed:** 2026-04-10 (JOB-009, JOB-010, JOB-011)
**Exit criteria:**
- [x] Job with N tasks executing in parallel containers (JOB-010)
- [x] Task dependency DAG (sequential and parallel) (JOB-009)
- [x] Aggregated Job status from constituent Tasks (JOB-010)
- [x] Dashboard shows multi-task Job progress (JOB-011)
- [x] Governance failure analytics (trending violations, GOV update suggestions) (JOB-011)
- [x] `stewie.json` project config (JOB-011, CON-003)

---

### Phase 5a: Chat + Real-Time UI ✅ COMPLETE
**Goal:** Human ↔ Architect chat interface and real-time dashboard updates.
**Completed:** 2026-04-10 (JOB-012, JOB-013, JOB-014, JOB-015)
**Exit criteria:**
- [x] SignalR WebSocket hub replacing polling for job/task updates (JOB-012)
- [x] Per-project chat persistence (ChatMessage entity, GET/POST endpoints) (JOB-013)
- [x] Chat UI panel as the primary project interface (JOB-013)
- [x] Live container output streaming to dashboard (JOB-014)
- [x] Graceful fallback to polling if WebSocket disconnects (JOB-012)
- [x] CON-002 updated with WebSocket and chat endpoints (JOB-012)

---

### Phase 5b: Message Bus + Agent Lifecycle ✅ COMPLETE
**Goal:** RabbitMQ messaging backbone and agent container lifecycle management.
**Completed:** 2026-04-10 (JOB-016, JOB-017, JOB-018)
**Exit criteria:**
- [x] RabbitMQ Docker setup (compose, connection config)
- [x] Message exchange topology (task assignment, progress, blocker, completion)
- [x] `IAgentRuntime` interface for pluggable agent runtimes
- [x] Agent container lifecycle: create → connect → work → exit
- [x] Architect Agent receives events from Dev Agents via RabbitMQ
- [x] Dev Agent publishes progress, blockers, and completion via RabbitMQ
- [x] CON-004: Agent Messaging Contract

---

### Phase 6: AI Agent Intelligence ✅ COMPLETE
**Goal:** Plug actual LLM brains into the agent infrastructure.
**Completed:** 2026-04-11 (JOB-021, JOB-022, JOB-023)
**Exit criteria:**
- [x] First `IAgentRuntime` implementation (OpenCode) (JOB-021)
- [x] Architect Agent container: receives chat, plans work, creates jobs, monitors (JOB-022)
- [x] Dev Agent container: receives task, writes code, asks questions when blocked (JOB-021)
- [x] Model/provider selector in dashboard (per project) (JOB-023 T-200)
- [x] Conversation history persistence for Architect context (JOB-022)
- [x] End-to-end autonomous loop: Human chats → Architect plans → Dev executes → Architect reviews (JOB-022/023)

---

### Phase 7: Design System Foundation 🔄 IN PROGRESS
**Goal:** Migrate to Tailwind CSS v4, build reusable component library, fix visual bugs, remove dead features.
**Exit criteria:**
- [x] Replace all emoji with flat SVG icons (JOB-024)
- [x] Delete deprecated ConversationContextPanel (JOB-024)
- [x] Chat slideover with pin-to-sidebar and localStorage persistence (JOB-025)
- [x] GitHub repo combobox with feature gating (JOB-025)
- [x] Admin invite code generation/revocation UI (JOB-026)
- [x] Admin user list/deletion UI (JOB-026)
- [x] Dark mode outline buttons, anchor hover fix (hotfix)
- [ ] Tailwind CSS v4 migration with brand tokens and dark mode (JOB-027)
- [ ] Reusable component library: Button, Card, Input, Badge, Select, DataTable, Dropdown, Modal (JOB-028)
- [ ] Remove manual job creation, fix visual bugs, responsive audit (JOB-029)

---

### Phase 8: App Shell + Role-Based Architecture (Planned)
**Goal:** Restructure navigation for admin vs. user roles, add project-scoped context with switcher, build admin system dashboard.
**Exit criteria:**
- [ ] Route restructuring: `/admin/*`, `/p/:projectId/*`, `/projects`, `/settings` (JOB-030)
- [ ] ProjectContext provider with localStorage persistence (JOB-030)
- [ ] Data-driven sidebar config — role → nav items mapping (JOB-031)
- [ ] Project switcher dropdown in header (JOB-031)
- [ ] Chat FAB on all project-scoped pages (JOB-031)
- [ ] Admin System Dashboard: health, agents, stats, activity feed (JOB-032)
- [ ] User Management extraction from Settings to `/admin/users` (JOB-033)
- [ ] Settings page cleanup — personal preferences only (JOB-033)

---

### Phase 9: Code Explorer + Premium Polish (Planned)
**Goal:** Add GitHub-backed code browsing, overhaul events page, premium visual polish pass.
**Exit criteria:**
- [ ] File tree browser via GitHub API proxy (JOB-034)
- [ ] Read-only syntax-highlighted code viewer with Prism (JOB-034)
- [ ] Events page: structured cards, filtering, project scoping (JOB-035)
- [ ] Premium visual polish: stat cards, job timeline, micro-animations, empty states (JOB-036)
- [ ] Full responsive audit at 320px–1920px (JOB-036)
- [ ] Dark mode comprehensive audit (JOB-036)

---

### Phase 10: Production Hardening (Future)
**Goal:** Harden the platform for real-world usage.
**Exit criteria:**
- [ ] Multi-repo orchestration
- [ ] Aider + Claude Code agent runtimes
- [ ] GitLab and Bitbucket providers
- [ ] Workspace TTL-based cleanup
- [ ] Custom Docker network isolation per job
- [ ] All contracts at `STABLE` status

---

## 5. Agent Team

| Role | Agent Type | Count | Notes |
|:-----|:-----------|:------|:------|
| Human | N/A | 1 | Vision, decisions, final authority. Interacts only via chat. |
| Architect Agent | LLM Agent (container) | 1 per project | Plans, assigns, reviews, enforces governance. |
| Developer Agent | LLM Agent (container) | Dynamic per job | Ephemeral. Spun up per task, destroyed on completion. Count determined by Architect. |
| Tester Agent | LLM Agent (container) | Dynamic per job | Ephemeral. Verifies dev output against contracts. |

---

## 6. Key Contracts

| Contract | Description | Status |
|:---------|:------------|:-------|
| `CON-001` | Runtime Contract (task.json / result.json) | `DRAFT` — v1.6.0 |
| `CON-002` | API Contract (HTTP endpoints) | `DRAFT` — v2.0.0 |
| `CON-003` | Project Configuration (stewie.json) | `DRAFT` — v1.1.0 |
| `CON-004` | Agent Messaging Contract (RabbitMQ) | `DRAFT` — v1.1.0 |

---

## 7. System Shape

| Component | Technology | Status |
|:----------|:-----------|:-------|
| Frontend | React (Vite) — `Stewie.Web` | Active |
| Backend / Control Plane | C# .NET 10 — `Stewie.Api` | Active |
| Database | SQL Server 2022 | Active |
| ORM | NHibernate | Active |
| Migrations | FluentMigrator | Active |
| Real-Time (client) | SignalR WebSocket | Phase 5a |
| Messaging (agents) | RabbitMQ | Phase 5b |
| Agent Runtime | Docker containers + `IAgentRuntime` | Phase 5b |
| AI / LLM | Pluggable (Claude, GPT, Gemini, etc.) | Phase 6 |
| Governance | CODEX | Active |

---

## 8. Success Criteria

This project is complete when:
- [ ] A Human can chat with an Architect Agent to build software end-to-end
- [ ] The Architect autonomously creates jobs, spins up Dev Agents, and enforces governance
- [ ] Dev Agents write code, commit, and push without Human intervention
- [ ] Multiple LLM providers and agentic frameworks are supported
- [ ] All contracts at `STABLE` status
- [ ] All `DEF-` defects resolved or explicitly deferred

---

## 9. Change Log

| Date | Version | Change | Author |
|:-----|:--------|:-------|:-------|
| 2026-04-09 | 1.0.0 | Initial roadmap from constitution v0.1 | Architect (drafted), Human (approved) |
| 2026-04-10 | 2.0.0 | Major rewrite — clarified end-state vision (chat-driven, LLM-powered agents, RabbitMQ message bus, IAgentRuntime). Split Phase 5 into 5a/5b/6. Updated agent team, contracts, system shape, success criteria. | Human + Architect |
| 2026-04-11 | 3.0.0 | Phase 6 marked COMPLETE. Added Phase 7 placeholder. Updated contracts to current versions. | Architect |
| 2026-04-11 | 4.0.0 | Phase 7 (UI/UX Refinements) marked COMPLETE. Production Hardening renamed to Phase 8. CON-002 updated to v2.0.0. Component library added to Phase 8 exit criteria. Full CODEX housekeeping pass. | Architect |
