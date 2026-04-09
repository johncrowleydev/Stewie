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
updated: 2026-04-09
version: 1.1.0
---

> **BLUF:** Stewie is a GitHub-native orchestration system that coordinates multiple AI agents to develop software in parallel under strict governance. It creates and manages Runs/Tasks, prepares isolated workspaces, launches worker containers, ingests structured results, and enforces CODEX governance. Stewie does not write code — it orchestrates agents that do.

# Stewie — Project Roadmap

> **This document is authored by the Human. The Architect Agent maintains it.**

---

## 1. Project Vision

Stewie is an **execution engine for governed software development**. It acts as a steward — coordinating work, enforcing standards, and ensuring high-quality outcomes across multiple AI agents working in parallel.

Stewie does not write software itself. It:
- Creates and manages **Runs** and **Tasks**
- Prepares **isolated workspaces** (filesystem + container)
- Launches **worker runtimes** (Docker containers)
- Ingests **structured results** (result.json)
- Enforces **governance** (CODEX)
- Maintains **system-of-record state** (SQL Server)

The Human interacts with an Architect Agent. The Architect Agent creates tasks. Stewie orchestrates execution. Workers are stateless and ephemeral. Stewie owns truth.

---

## 2. Guiding Principles

- **Governance First:** All work must follow CODEX governance, respect contracts, and pass validation before integration.
- **Orchestrator Owns Truth:** Stewie is the system-of-record. Workers are stateless and replaceable.
- **Structured Work Only:** All work flows through `task.json` (input) and `result.json` (output). No unstructured communication.
- **Isolation by Default:** Each task runs in its own workspace and its own container.
- **Git is the Integration Layer:** All code changes flow through Git.
- **Agents are Replaceable:** Agent runtimes are pluggable and vendor-agnostic.
- **No Agent-to-Agent Chat:** All coordination flows through Stewie. Agents never talk to each other directly.
- **Human Oversight:** Human interacts with the Architect Agent and retains final control.

---

## 3. Scope

### 3.1 In Scope
- Run/Task lifecycle management
- Workspace creation and cleanup
- Container-based worker execution
- Structured I/O (task.json / result.json)
- SQL Server persistence (NHibernate)
- React dashboard for monitoring
- Real-time Human ↔ Architect interaction
- CODEX governance enforcement

### 3.2 Out of Scope
- Full autonomy (Human always in the loop)
- IDE replacement
- Complex infrastructure (Kubernetes, cloud orchestration)
- Multi-repo orchestration (initially)
- Agent-to-agent direct communication

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

### Phase 3: Governance Engine
**Goal:** Stewie enforces CODEX governance on worker output automatically.
**Exit criteria:**
- [ ] Post-task governance validation (automated)
- [ ] Contract compliance checking
- [ ] Rejection workflow for non-compliant results
- [ ] Governance audit trail in Events

---

### Phase 4: Multi-Task Jobs
**Goal:** A single Run can spawn and coordinate multiple parallel Tasks.
**Exit criteria:**
- [ ] Job with N tasks executing in parallel containers
- [ ] Task dependency graph (sequential and parallel)
- [ ] Aggregated Job status from constituent Tasks
- [ ] Dashboard shows multi-task Job progress

---

### Phase 5: Real-Time Interaction
**Goal:** Human ↔ Architect interaction in real-time through the dashboard.
**Exit criteria:**
- [ ] WebSocket or SSE for live Job/Task updates
- [ ] Chat-like interface for Human ↔ Architect
- [ ] Live container output streaming
- [ ] RabbitMQ for async event distribution

---

## 5. Agent Team

| Role | Agent Type | Count | Primary CODEX Docs |
|:-----|:-----------|:------|:-------------------|
| Project manager | Architect Agent | 1 | `AGT-001`, all `JOB-`, all `CON-` |
| Implementation | Developer Agent | 2 | `AGT-002`, assigned `JOB-` |
| Quality | Tester Agent | 0 (future) | `AGT-003`, `VER-`, `40_VERIFICATION/` |

---

## 6. Key Contracts

| Contract | Description | Status |
|:---------|:------------|:-------|
| `CON-001` | Runtime Contract (task.json / result.json) | `DRAFT` |
| `CON-002` | API Contract (HTTP endpoints) | `DRAFT` |

---

## 7. System Shape

| Component | Technology |
|:----------|:-----------|
| Frontend | React (Vite) — `Stewie.Web` |
| Backend | C# .NET 10 — `Stewie.Api` |
| Database | SQL Server 2022 |
| ORM | NHibernate |
| Migrations | FluentMigrator |
| Runtime | Docker containers |
| Messaging | RabbitMQ (future — Phase 5) |
| Governance | CODEX |

---

## 8. Success Criteria

This project is complete when:
- [ ] All phases completed and archived
- [ ] All contracts at `STABLE` status
- [ ] All `DEF-` defects resolved or explicitly deferred
- [ ] Human signs off on final verification report

---

## 9. Change Log

| Date | Version | Change | Author |
|:-----|:--------|:-------|:-------|
| 2026-04-09 | 1.0.0 | Initial roadmap from constitution v0.1 | Architect (drafted), Human (approved) |
