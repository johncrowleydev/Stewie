---
id: IDX-000
title: "Documentation Master Index"
type: reference
status: APPROVED
owner: architect
agents: [all]
tags: [documentation, index, governance]
related: [GOV-001]
created: 2026-03-04
updated: 2026-04-11
version: 3.0.0
---

> **BLUF:** This is the single entry point for all project documentation. Humans start here. Agents read `MANIFEST.yaml`.

# Documentation Master Index

> **"If it isn't documented, it doesn't exist."**

Welcome to the Stewie project knowledge base. This documentation system is designed for **dual-audience** consumption: human architects and AI coding agents.

---

## System Overview

| Component | Purpose |
|:----------|:--------|
| `MANIFEST.yaml` | Machine-readable registry of ALL docs — the agent's map |
| `_templates/` | Doc templates for each Diátaxis type |
| `GOV-001` | The meta-standard governing this entire system |

### How to Use This System

**Humans**: Browse the index below. Each area is numbered for deterministic sort order.

**Agents**: Parse `00_INDEX/MANIFEST.yaml`. Filter by `tags`, `status`, `type`, or `agents` field to find relevant docs without directory crawling.

---

## 05. PROJECT — *The Active State*

> Active project management state. Roadmap, sprints, backlog.

| ID | Title | Status |
|:---|:------|:-------|
| [PRJ-001](../05_PROJECT/PRJ-001_Roadmap.md) | Project Roadmap | APPROVED |
| [BCK-001](../05_PROJECT/BCK-001_Backlog.md) | Product Backlog | DRAFT |
| [BCK-002](../05_PROJECT/BCK-002_Architect_Backlog.md) | Architect Backlog | DRAFT |

**Jobs:** 26 total (JOB-001 through JOB-026). All CLOSED through Phase 7.

**Category Codes**: `PRJ` (Roadmap), `JOB` (Job), `BCK` (Backlog)

---

## 10. GOVERNANCE — *The Laws*

> Standards, protocols, coding rules. These docs **govern** how agents and humans operate.

| ID | Title | Status |
|:---|:------|:-------|
| [GOV-001](../10_GOVERNANCE/GOV-001_DocumentationStandard.md) | Documentation Standard | APPROVED |
| [GOV-002](../10_GOVERNANCE/GOV-002_TestingProtocol.md) | Testing Protocol | APPROVED |
| [GOV-003](../10_GOVERNANCE/GOV-003_CodingStandard.md) | Coding Standard | APPROVED |
| [GOV-004](../10_GOVERNANCE/GOV-004_ErrorHandlingProtocol.md) | Error Handling Protocol | APPROVED |
| [GOV-005](../10_GOVERNANCE/GOV-005_AgenticDevelopmentLifecycle.md) | Agentic Development Lifecycle | APPROVED |
| [GOV-006](../10_GOVERNANCE/GOV-006_LoggingSpecification.md) | Logging Specification | APPROVED |
| [GOV-007](../10_GOVERNANCE/GOV-007_AgenticProjectManagement.md) | Agentic Project Management | APPROVED |
| [GOV-008](../10_GOVERNANCE/GOV-008_InfrastructureAndOperations.md) | Infrastructure and Operations | APPROVED |

---

## 20. BLUEPRINTS — *The Designs*

> Component specifications, system designs, API contracts.

| ID | Title | Status |
|:---|:------|:-------|
| [BLU-001](../20_BLUEPRINTS/BLU-001_Stewie_System_Blueprint.md) | Stewie System Blueprint | DRAFT |
| [BLU-020](../20_BLUEPRINTS/BLU-020_CODEX_System_Blueprint.md) | CODEX System Blueprint | DRAFT |
| [CON-001](../20_BLUEPRINTS/CON-001_Runtime_Contract.md) | Runtime Contract (task.json / result.json) | DRAFT — v1.6.0 |
| [CON-002](../20_BLUEPRINTS/CON-002_API_Contract.md) | API Contract (HTTP endpoints) | DRAFT — v2.0.0 |
| [CON-003](../20_BLUEPRINTS/CON-003_ProjectConfig_Contract.md) | Project Configuration (stewie.json) | DRAFT — v1.1.0 |
| [CON-004](../20_BLUEPRINTS/CON-004_Agent_Messaging_Contract.md) | Agent Messaging Contract (RabbitMQ) | DRAFT — v1.1.0 |

---

## 30. RUNBOOKS — *The Procedures*

> Operational how-to guides, deployment procedures, workflows.

| ID | Title | Status |
|:---|:------|:-------|
| [RUN-001](../30_RUNBOOKS/RUN-001_Dev_Environment_Setup.md) | Dev Environment Setup | APPROVED |
| [RUN-003](../30_RUNBOOKS/RUN-003_End_to_End_Agent_Loop.md) | End-to-End Agent Loop | DRAFT |

---

## 40. VERIFICATION — *The Proof*

> Job audit reports and verification records.

**24 audit reports** (VER-001 through VER-025-026). See MANIFEST.yaml for the full list.

---

## 50. DEFECTS — *The Forensics*

> Bug reports, root cause analysis, incident forensics.

| ID | Title | Status |
|:---|:------|:-------|
| [DEF-001](../50_DEFECTS/DEF-001_JOB-014_Frontend_Build_Failure.md) | JOB-014 Frontend Build Failure | CLOSED |
| [DEF-002](../50_DEFECTS/DEF-002_AgentB_Missing_Test_Auth.md) | Agent B Missing Test Auth | CLOSED |
| [DEF-003](../50_DEFECTS/DEF-003_Dark_Mode_Only.md) | Dark Mode Only | CLOSED |

---

## 60. EVOLUTION — *The Proposals*

> Feature proposals and change requests.

| ID | Title | Status |
|:---|:------|:-------|
| [EVO-001](../60_EVOLUTION/EVO-001_SafeCommand_FileRedirect_Pattern.md) | SafeCommand File Redirect Pattern | APPROVED |

---

## 70. RESEARCH — *The Science*

> Whitepapers, investigations, lessons learned.

| ID | Title | Status |
|:---|:------|:-------|
| [RES-001](../70_RESEARCH/RES-001_Lessons_Learned_LexFlow.md) | Lessons Learned — LexFlow | DRAFT |

---

## 80. AGENTS — *The Team*

> Agent role definitions. Spin up Architect, Developer, or Tester agents from these templates.

| ID | Title | Status |
|:---|:------|:-------|
| [AGT-001](../80_AGENTS/AGT-001_Architect_Agent.md) | Architect Agent | APPROVED |
| [AGT-002](../80_AGENTS/AGT-002_Developer_Agent.md) | Developer Agent | APPROVED |
| [AGT-003](../80_AGENTS/AGT-003_Tester_Agent.md) | Tester Agent | APPROVED |

---

## 90. ARCHIVE — *The History*

> Deprecated and historical docs. Preserved for reference, not for active use.

*(No archived docs yet.)*

---

## Templates

| Template | Diátaxis Type | Use When |
|:---------|:-------------|:---------|
| [template_reference.md](../_templates/template_reference.md) | Reference | Documenting facts: specs, APIs, schemas, configs |
| [template_how-to.md](../_templates/template_how-to.md) | How-To | Writing step-by-step procedures |
| [template_tutorial.md](../_templates/template_tutorial.md) | Tutorial | Teaching through guided learning |
| [template_explanation.md](../_templates/template_explanation.md) | Explanation | Explaining concepts, architecture, design decisions |
| [template_sprint.md](../_templates/template_sprint.md) | JOB- Sprint | Creating a new sprint document |
| [template_contract.md](../_templates/template_contract.md) | CON- Contract | Defining an interface contract |
| [template_project_roadmap.md](../_templates/template_project_roadmap.md) | PRJ- Roadmap | Writing the project vision and delivery phases |

---

## Document Lifecycle

```
DRAFT → REVIEW → APPROVED → DEPRECATED → ARCHIVE
```

| Status | Meaning |
|:-------|:--------|
| **DRAFT** | Work in progress, not reviewed |
| **REVIEW** | Ready for peer/agent review |
| **APPROVED** | Frozen, ready for use |
| **DEPRECATED** | Superseded — will move to `90_ARCHIVE/` |

---

> **"Documentation is not about recording what you did. It's about enabling what comes next."**
