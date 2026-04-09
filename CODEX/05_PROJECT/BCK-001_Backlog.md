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
updated: 2026-04-09
version: 1.1.0
---

> **BLUF:** Prioritized development backlog for Stewie. Items are pulled from here into sprint documents. Two developer agents will execute against this backlog. Items are ordered by dependency and priority.

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

## Phase 1: Core Orchestration (MVP)

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
| B-020 | **Emit events on state changes** | `RunOrchestrationService` emits Events on Run/Task status transitions | BLU-001 §4 | Medium | → JOB-002 T-017/T-018 |

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
| B-043 | **Integration tests for API endpoints** | Test controllers with WebApplicationFactory | GOV-002 | Medium | → JOB-002 T-023/T-024 |

---

## Phase 2: Real Repo Interaction

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-100 | ~~Git clone into workspace~~ | ✅ Done (JOB-002 T-021 plumbing, JOB-003 T-028 wiring) | P1 | → JOB-003 |
| B-101 | ~~Branch creation for tasks~~ | ✅ Done (JOB-002 T-021 plumbing, JOB-003 T-028 wiring) | P1 | → JOB-003 |
| B-102 | Diff ingestion from result | Capture git diff after worker exits | P2 | → JOB-003 T-030 |
| B-103 | Commit worker changes | Auto-commit worker file mutations | P2 | → JOB-003 T-031 |
| B-104 | Script worker container | Shell-based worker that executes bash commands | P1 | → JOB-003 T-029 |
| B-105 | Extended Run creation API | POST /api/jobs with task definitions | P1 | → JOB-003 T-027 |
| B-106 | Create Run form (frontend) | Dashboard form to create runs with objectives | P2 | → JOB-003 T-032 |
| B-107 | Run detail git/diff viewer | Branch, commit SHA, diff display | P2 | → JOB-003 T-033 |
| B-108 | Dashboard auto-refresh | Polling for live status updates | P2 | → JOB-003 T-034 |

---

## Phase 2.5: GitHub Integration + User System (JOB-004) ✅ COMPLETE

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

## Phase 2.75: Repository Automation + Platform Abstraction (JOB-005)

| ID | Task | Description | Priority | Status |
|:---|:-----|:------------|:---------|:-------|
| B-210 | **Platform abstraction** | Rename IGitHubService → IGitPlatformService for multi-provider support | P0 | → JOB-005 T-048 |
| B-211 | **Project repo link-or-create** | POST /api/projects supports both linking existing and creating new repos | P1 | → JOB-005 T-049/T-050 |
| B-212 | **Container timeout enforcement** | Enforce 300s hard limit per CON-001 §7 | P1 | → JOB-005 T-051 |
| B-213 | **Retry logic + error taxonomy** | Retry transient container failures, categorize error types | P1 | → JOB-005 T-052 |
| B-214 | **Project creation form (frontend)** | Link/create toggle with conditional fields | P2 | → JOB-005 T-053 |
| B-215 | **Integration tests (projects)** | Test link and create flows, PAT validation | P2 | → JOB-005 T-054 |
| B-216 | **Unit tests (timeout + retry)** | Test timeout behavior, retry logic | P2 | → JOB-005 T-055 |

---

## Future Backlog

| ID | Task | Description | Priority |
|:---|:-----|:------------|:---------|
| B-300 | Workspace TTL-based cleanup | Auto-delete old workspaces | P3 |
| B-301 | Multi-task runs (Phase 4) | One run spawns N parallel tasks | P2 |
| B-302 | AI agent worker | Container with LLM API for code generation | P2 |
| B-303 | WebSocket/SSE live updates | Replace polling with real-time push | P3 |
| B-304 | Task dependency graph | Sequential and parallel task ordering | P3 |
| B-305 | GitLab provider | IGitPlatformService implementation for GitLab | P3 |
| B-306 | Bitbucket provider | IGitPlatformService implementation for Bitbucket | P3 |

---

## Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-09 | Initial backlog created from PRJ-001 Phase 1 |
| 2026-04-09 | JOB-001 items marked complete. B-020, B-043 → JOB-002. Phase 2 items unchanged. |
| 2026-04-09 | Phase 2 items assigned to JOB-003. Added Phase 2.5 (GitHub/Users) and future backlog. |
| 2026-04-09 | Phase 2.5 marked complete (JOB-004). Added Phase 2.75 (Repo Automation) for JOB-005. |
