---
id: BCK-001
title: "Development Backlog"
type: planning
status: ACTIVE
owner: architect
agents: [all]
tags: [project-management, backlog, agentic-development]
related: [PRJ-001, BCK-002, GOV-007]
created: 2026-04-09
updated: 2026-04-10
version: 1.2.0
---

> **BLUF:** Prioritized development backlog for Stewie. Items are pulled from here into sprint documents. Two developer agents execute against this backlog. Items are ordered by dependency and priority. Phases 1–4 are complete (110 tests passing).

# Development Backlog

---

## Priority Legend

| Priority | Definition |
|:---------|:-----------|
| **P0** | Blocking — must be done before anything else |
| **P1** | High — core MVP functionality |
| **P2** | Medium — important but not blocking |
| **P3** | Low — nice to have |

---

## Phase 1: Core Orchestration (MVP) ✅ COMPLETE

### P0 — Foundation

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-001 | ~~**Project entity**~~ | ✅ Done (JOB-001 T-001) | CON-002 §5.1 | Small |
| B-002 | ~~**Event entity**~~ | ✅ Done (JOB-001 T-002) | BLU-001 §3.2 | Small |
| B-003 | ~~**Workspace entity**~~ | ✅ Done (JOB-001 T-003) | BLU-001 §3.2 | Small |
| B-004 | ~~**Link Run to Project**~~ | ✅ Done (JOB-001 T-004) | CON-002 §5.2 | Small |

### P1 — API Endpoints

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-010 | ~~**Health endpoint**~~ | ✅ Done (JOB-001 T-006) | CON-002 §4.4 | Trivial |
| B-011 | ~~**Project CRUD**~~ | ✅ Done (JOB-001 T-007) | CON-002 §4.1 | Small |
| B-012 | ~~**Run endpoints**~~ | ✅ Done (JOB-001 T-008) | CON-002 §4.2 | Medium |
| B-013 | ~~**Task endpoints**~~ | ✅ Done (JOB-001 T-009) | CON-002 §4.3 | Small |
| B-014 | ~~**Standardized error responses**~~ | ✅ Done (JOB-001 T-005) | CON-002 §6 | Medium |

### P1 — Event Emission

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-020 | ~~**Emit events on state changes**~~ | ✅ Done (JOB-002 T-017/T-018) | BLU-001 §4 | Medium |

### P2 — React Dashboard

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-030 | ~~**Dashboard layout**~~ | ✅ Done (JOB-001 T-013) | — | Medium |
| B-031 | ~~**Runs list page**~~ | ✅ Done (JOB-001 T-014) | CON-002 §4.2 | Medium |
| B-032 | ~~**Run detail page**~~ | ✅ Done (JOB-001 T-015) | CON-002 §4.2, §4.3 | Medium |
| B-033 | ~~**Projects page**~~ | ✅ Done (JOB-001 T-016) | CON-002 §4.1 | Medium |

### P2 — Test Infrastructure

| ID | Task | Description | Contracts | Est. Complexity |
|:---|:-----|:------------|:----------|:----------------|
| B-040 | ~~**Test project setup**~~ | ✅ Done (JOB-001 T-010) | GOV-002 | Small |
| B-041 | ~~**Unit tests for RunOrchestrationService**~~ | ✅ Done (JOB-001 T-011) | GOV-002 | Medium |
| B-042 | ~~**Unit tests for WorkspaceService**~~ | ✅ Done (JOB-001 T-012) | GOV-002 | Small |
| B-043 | ~~**Integration tests for API endpoints**~~ | ✅ Done (JOB-002 T-023/T-024) | GOV-002 | Medium |

---

## Phase 2: Real Repo Interaction ✅ COMPLETE

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-100 | ~~Git clone into workspace~~ | ✅ Done (JOB-003 T-028) | P1 | Done |
| B-101 | ~~Branch creation for tasks~~ | ✅ Done (JOB-003 T-028) | P1 | Done |
| B-102 | ~~Diff ingestion from result~~ | ✅ Done (JOB-003 T-030) | P2 | Done |
| B-103 | ~~Commit worker changes~~ | ✅ Done (JOB-003 T-031) | P2 | Done |
| B-104 | ~~Script worker container~~ | ✅ Done (JOB-003 T-029) | P1 | Done |
| B-105 | ~~Extended Run creation API~~ | ✅ Done (JOB-003 T-027) | P1 | Done |
| B-106 | ~~Create Run form (frontend)~~ | ✅ Done (JOB-003 T-032) | P2 | Done |
| B-107 | ~~Run detail git/diff viewer~~ | ✅ Done (JOB-003 T-033) | P2 | Done |
| B-108 | ~~Dashboard auto-refresh~~ | ✅ Done (JOB-003 T-034) | P2 | Done |

---

## Phase 2.5: GitHub Integration + User System ✅ COMPLETE

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-200 | ~~GitHub repo creation~~ | ✅ Done (JOB-004 T-040) | P1 | Done |
| B-201 | ~~GitHub PR automation~~ | ✅ Done (JOB-004 T-041) | P1 | Done |
| B-202 | ~~User entity + authentication~~ | ✅ Done (JOB-004 T-037/T-038) | P1 | Done |
| B-203 | ~~Encrypted credential storage~~ | ✅ Done (JOB-004 T-039) | P1 | Done |
| B-204 | ~~Push branch to remote~~ | ✅ Done (JOB-004 T-041) | P1 | Done |
| B-205 | ~~GitHub settings UI~~ | ✅ Done (JOB-004 T-045) | P2 | Done |
| B-206 | ~~Auth UI~~ | ✅ Done (JOB-004 T-043/T-044) | P2 | Done |

---

## Phase 2.75: Repository Automation + Platform Abstraction ✅ COMPLETE

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-210 | ~~Platform abstraction~~ | ✅ Done (JOB-005 T-048) | P0 | Done |
| B-211 | ~~Project repo link-or-create~~ | ✅ Done (JOB-005 T-049/T-050) | P1 | Done |
| B-212 | ~~Container timeout enforcement~~ | ✅ Done (JOB-005 T-051) | P1 | Done |
| B-213 | ~~Retry logic + error taxonomy~~ | ✅ Done (JOB-005 T-052) | P1 | Done |
| B-214 | ~~Project creation form (frontend)~~ | ✅ Done (JOB-005 T-053) | P2 | Done |
| B-215 | ~~Integration tests (projects)~~ | ✅ Done (JOB-005 T-054) | P2 | Done |
| B-216 | ~~Unit tests (timeout + retry)~~ | ✅ Done (JOB-005 T-055) | P2 | Done |

---

## Phase 3: Governance Engine ✅ COMPLETE

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-300 | ~~Sequential task chains~~ | ✅ Done (JOB-007 T-060–T-067) | P0 | Done |
| B-301 | ~~Governance worker + reports~~ | ✅ Done (JOB-007 T-068–T-071) | P0 | Done |
| B-302 | ~~Governance retry loop~~ | ✅ Done (JOB-007 T-072–T-073) | P1 | Done |
| B-303 | ~~Governance dashboard UI~~ | ✅ Done (JOB-008 T-076–T-077) | P1 | Done |
| B-304 | ~~Governance integration tests~~ | ✅ Done (JOB-008 T-078–T-079) | P1 | Done |

---

## Phase 4: Multi-Task Jobs ✅ COMPLETE

### JOB-009 — Task DAG Infrastructure ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-400 | ~~TaskDependency entity + migration~~ | ✅ Done (JOB-009 T-081) | P0 | Done |
| B-401 | ~~TaskDependency repository~~ | ✅ Done (JOB-009 T-082) | P0 | Done |
| B-402 | ~~TaskGraph service~~ | ✅ Done (JOB-009 T-084/T-085) | P0 | Done |
| B-403 | ~~Blocked + Cancelled WorkTask states~~ | ✅ Done (JOB-009 T-086) | P0 | Done |
| B-404 | ~~PartiallyCompleted Job status~~ | ✅ Done (JOB-009 T-087) | P0 | Done |
| B-405 | ~~TaskGraph unit tests~~ | ✅ Done (JOB-009 T-088/T-089) | P1 | Done |

### JOB-010 — Parallel Execution Engine + API ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-410 | ~~Multi-task execution loop~~ | ✅ Done (JOB-010 T-090) | P0 | Done |
| B-411 | ~~Parallel container launcher~~ | ✅ Done (JOB-010 T-092) | P0 | Done |
| B-412 | ~~Per-task workspace isolation~~ | ✅ Done (JOB-010 T-093) | P0 | Done |
| B-413 | ~~Multi-task API (POST /api/jobs)~~ | ✅ Done (JOB-010 T-095) | P0 | Done |
| B-414 | ~~CON-002 v1.7.0~~ | ✅ Done (JOB-010 T-096) | P0 | Done |
| B-415 | ~~Per-task governance cycle~~ | ✅ Done (JOB-010 T-101) | P1 | Done |
| B-416 | ~~Integration tests (multi-task)~~ | ✅ Done (JOB-010 T-098/T-099/T-100) | P1 | Done |

### JOB-011 — Dashboard + Analytics + stewie.json ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-420 | ~~Multi-task progress UI~~ | ✅ Done (JOB-011 T-102) | P1 | Done |
| B-421 | ~~Task DAG visualization~~ | ✅ Done (JOB-011 T-103) | P1 | Done |
| B-422 | ~~Governance analytics API~~ | ✅ Done (JOB-011 T-105) | P1 | Done |
| B-423 | ~~Governance analytics UI~~ | ✅ Done (JOB-011 T-106) | P2 | Done |
| B-424 | ~~stewie.json parser~~ | ✅ Done (JOB-011 T-107) | P1 | Done |
| B-425 | ~~CON-003 stewie.json contract~~ | ✅ Done (JOB-011 T-108) | P1 | Done |
| B-426 | ~~GOV update suggestions~~ | ✅ Done (JOB-011 T-110) | P2 | Done |

---

## Phase 5a: Chat + Real-Time UI ✅ COMPLETE

### JOB-012 — SignalR Real-Time Hub ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-500 | ~~SignalR WebSocket hub~~ | ✅ Done (JOB-012) | P0 | Done |
| B-501 | ~~IRealTimeNotifier abstraction~~ | ✅ Done (JOB-012) | P0 | Done |
| B-502 | ~~Frontend useSignalR hook~~ | ✅ Done (JOB-012) | P1 | Done |

### JOB-013 — Project Chat System ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-510 | ~~ChatMessage entity + migration~~ | ✅ Done (JOB-013 T-130) | P0 | Done |
| B-511 | ~~ChatController REST API~~ | ✅ Done (JOB-013 T-132) | P0 | Done |
| B-512 | ~~ChatPanel frontend component~~ | ✅ Done (JOB-013 T-135) | P1 | Done |
| B-513 | ~~SignalR chat push~~ | ✅ Done (JOB-013 T-133) | P1 | Done |

### JOB-014 — Container Output Streaming ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-520 | ~~ContainerOutputBuffer~~ | ✅ Done (JOB-014 T-142) | P0 | Done |
| B-521 | ~~Streaming DockerContainerService~~ | ✅ Done (JOB-014 T-141) | P0 | Done |
| B-522 | ~~ContainerOutputPanel frontend~~ | ✅ Done (JOB-014 T-147) | P1 | Done |
| B-523 | ~~Container output REST endpoint~~ | ✅ Done (JOB-014 T-144) | P1 | Done |

### JOB-015 — ESProj Migration ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-530 | ~~Migrate csproj to esproj~~ | ✅ Done (JOB-015 T-151) | P1 | Done |

---

## Phase 5b: Message Bus + Agent Lifecycle (IN PROGRESS)

### JOB-016 — RabbitMQ Infrastructure ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-600 | ~~Docker compose (RabbitMQ)~~ | ✅ Done (JOB-016 T-154) | P0 | Done |
| B-601 | ~~CON-004 Agent Messaging Contract~~ | ✅ Done (JOB-016 T-156) | P0 | Done |
| B-602 | ~~IRabbitMqService + implementation~~ | ✅ Done (JOB-016 T-158/T-159) | P0 | Done |
| B-603 | ~~RabbitMqConsumerHostedService~~ | ✅ Done (JOB-016 T-160) | P0 | Done |
| B-604 | ~~RabbitMQ health check~~ | ✅ Done (JOB-016 T-157) | P1 | Done |

### JOB-017 — IAgentRuntime + Stub ✅ CLOSED

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-610 | ~~IAgentRuntime interface~~ | ✅ Done (JOB-017 T-162) | P0 | Done |
| B-611 | ~~AgentSession entity + migration~~ | ✅ Done (JOB-017 T-163) | P0 | Done |
| B-612 | ~~AgentLifecycleService~~ | ✅ Done (JOB-017 T-164) | P0 | Done |
| B-613 | ~~StubAgentRuntime~~ | ✅ Done (JOB-017 T-167) | P0 | Done |
| B-614 | ~~stewie-stub-agent Python image~~ | ✅ Done (JOB-017 T-168) | P1 | Done |
| B-615 | ~~AgentsController REST API~~ | ✅ Done (JOB-017 T-165) | P1 | Done |

### JOB-018 — Chat Bridge + Architect Lifecycle (OPEN)

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-620 | Chat-to-RabbitMQ relay | Chat messages forwarded to Architect queue | P0 | Todo |
| B-621 | Agent chat → ChatMessage persistence | Agent events persisted as chat messages | P0 | Todo |
| B-622 | Architect session management endpoints | Start/stop Architect from API | P0 | Todo |
| B-623 | ArchitectControls frontend component | Start/stop UI + status indicator | P1 | Todo |
| B-624 | ChatPanel Architect integration | Disabled input when offline | P1 | Todo |

---

## Future Backlog

| ID | Task | Description | Priority |
|:---|:-----|:------------|:---------|
| B-700 | Workspace TTL-based cleanup | Auto-delete old workspaces | P3 |
| B-701 | AI agent worker (Phase 6) | Container with LLM API for code generation | P1 → Phase 6 |
| B-702 | GitLab provider | IGitPlatformService implementation for GitLab | P3 |
| B-703 | Bitbucket provider | IGitPlatformService implementation for Bitbucket | P3 |

---

## Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-09 | Initial backlog created from PRJ-001 Phase 1 |
| 2026-04-09 | JOB-001 items marked complete. B-020, B-043 → JOB-002. Phase 2 items unchanged. |
| 2026-04-09 | Phase 2 items assigned to JOB-003. Added Phase 2.5 (GitHub/Users) and future backlog. |
| 2026-04-09 | Phase 2.5 marked complete (JOB-004). Added Phase 2.75 (Repo Automation) for JOB-005. |
| 2026-04-10 | Phase 4 backlog created. B-301, B-304 pulled from Future into JOB-009/010/011. |
| 2026-04-10 | Phase 4 COMPLETE. All B-400 through B-426 marked done. Renumbered future items B-500+. |
| 2026-04-10 | Phase 5a COMPLETE. B-500 through B-530 done (JOB-012/013/014/015). |
| 2026-04-10 | Phase 5b started. JOB-016 and JOB-017 CLOSED. JOB-018 OPEN. B-600 through B-615 done. |

