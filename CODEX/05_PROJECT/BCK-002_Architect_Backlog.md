---
id: BCK-002
title: "Architect Agent Backlog"
type: planning
status: ACTIVE
owner: architect
agents: [architect]
tags: [project-management, backlog, architect, audit, deployment]
related: [BCK-001, GOV-007, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** The Architect Agent has its own work stream separate from developer sprints. This backlog tracks: CODEX bootstrap, sprint planning, audit preparation, contract compliance testing, and agent onboarding. The Architect works **in parallel** with developer agents — never idle.

# Architect Agent Backlog

---

## Work Categories

| Category | Code | Description |
|:---------|:-----|:------------|
| **CODEX** | ARCH-CODEX | Document creation, MANIFEST sync, template updates |
| **Sprint Mgmt** | ARCH-SPRINT | Sprint creation, task assignment, progress monitoring |
| **Audit** | ARCH-AUDIT | Sprint audit against contracts + GOV docs |
| **Integration** | ARCH-INTEG | Cross-component contract compliance testing |
| **Agent Ops** | ARCH-AGENT | Agent boot docs, onboarding, workflow enforcement |

---

## Current Tasks

| ID | Task | Category | Status | Notes |
|:---|:-----|:---------|:-------|:------|
| A-001 | ~~Fill GOV-008~~ | ARCH-CODEX | [x] | Infrastructure decisions confirmed |
| A-002 | ~~Create PRJ-001 Roadmap~~ | ARCH-CODEX | [x] | Drafted from constitution |
| A-003 | ~~Create CON-001 Runtime Contract~~ | ARCH-CODEX | [x] | Formalized from code |
| A-004 | ~~Create CON-002 API Contract~~ | ARCH-CODEX | [x] | Current + planned endpoints |
| A-005 | ~~Create BLU-001 System Blueprint~~ | ARCH-CODEX | [x] | Architecture documented |
| A-006 | ~~Create BCK-001 Dev Backlog~~ | ARCH-CODEX | [x] | Phase 1 items prioritized |
| A-007 | ~~Create BCK-002 (this doc)~~ | ARCH-CODEX | [x] | — |
| A-008 | Update MANIFEST.yaml | ARCH-CODEX | [ ] | Register all new docs |
| A-009 | Fix ROAD-001 misplacement | ARCH-CODEX | [ ] | Wrong prefix/location |
| A-010 | Enforce workflow references in agent docs | ARCH-AGENT | [ ] | safe_commands + git_commit |
| A-011 | Create SPR-001 (first dev sprint) | ARCH-SPRINT | [ ] | Pull from BCK-001 |
| A-012 | Create developer agent boot docs | ARCH-AGENT | [ ] | 2 developer agents |
| A-013 | Build sprint audit checklist (VER-001) | ARCH-AUDIT | [ ] | Reusable audit template |

---

## Recurring Tasks (Per Sprint)

| ID | Task | Category |
|:---|:-----|:---------|
| A-R01 | Monitor developer agent progress | ARCH-SPRINT |
| A-R02 | Audit completed sprint against contracts | ARCH-AUDIT |
| A-R03 | Update MANIFEST.yaml with any new docs | ARCH-CODEX |
| A-R04 | Resolve any DEF- reports from testers | ARCH-AUDIT |

---

## Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-09 | Initial architect backlog created |
