# AGENTS.md — Project Context for AI Coding Agents

> **What this file does:** This is the universal agent briefing for any AI coding tool working in this repository. It is tool-agnostic — Gemini, Claude, Cursor, Copilot, and any other AGENTS.md-compatible tool will read it automatically when opening this project.
>
> **Related files:**
> - `.agent/context.md` — legacy context file, now delegates here
> - `CODEX/80_AGENTS/` — detailed role templates for multi-agent orchestration

---

## 1. Project Overview

**Stewie** is a GitHub-native orchestration system that enables multiple AI agents to collaboratively develop software in parallel under strict governance. It acts as a steward of software development — coordinating work, enforcing standards, and ensuring high-quality outcomes.

**Tech stack:** C# .NET 10 (API), React/Vite (frontend), SQL Server 2022, NHibernate, Docker containers for worker runtimes. See `CODEX/10_GOVERNANCE/GOV-008_InfrastructureAndOperations.md` for full infrastructure decisions.

### Three-Tier Hierarchy

```
Human (final authority)
    ↓ works with
Architect Agent (AI project manager)
    ↓ assigns work to
Developer Agents + Tester Agents (execution)
```

The Human brings vision and final decisions. The Architect Agent translates vision into concrete work and audits output. Developer and Tester Agents execute and verify.

---

## 2. Branding

| Element | Value |
|:--------|:------|
| **Logo** | `docs/assets/stewie-logo.png` — a green turtle-armadillo hybrid character with an antenna |
| **Primary color** | `#6fac50` (green) |
| **Secondary color** | `#767573` (warm gray) |
| **Font for "stewie" wordmark** | Bold, lowercase, secondary color |

Use the primary green for actions, links, and interactive elements. Use the secondary gray for body text, borders, and muted UI. The logo should appear on white or light backgrounds.

Frontend assets: `src/Stewie.Web/ClientApp/public/stewie-logo.png`

---

## 3. Repository Structure

All project documentation lives in `CODEX/`. Do **not** create docs outside this structure.

```
CODEX/
├── 00_INDEX/          ← MANIFEST.yaml is your document map. Start here.
├── 05_PROJECT/        ← Active ops: roadmaps, sprints, backlogs
├── 10_GOVERNANCE/     ← Standards and rules — read FIRST
├── 20_BLUEPRINTS/     ← Design specs (BLU-) and interface contracts (CON-)
├── 30_RUNBOOKS/       ← Step-by-step operational procedures
├── 40_VERIFICATION/   ← Test specs, QA standards, verification reports (VER-)
├── 50_DEFECTS/        ← Bug reports and root cause analysis (DEF-)
├── 60_EVOLUTION/      ← Feature proposals and change requests (EVO-)
├── 70_RESEARCH/       ← Whitepapers and investigations
├── 80_AGENTS/         ← Agent role definitions (AGT-)
├── 90_ARCHIVE/        ← Deprecated docs — do not use
└── _templates/        ← Templates for new docs
```

Other top-level directories:

```
.agent/                ← Agent config (workflows)
bin/                   ← Utility scripts
```

---

## 4. How to Find Documents

1. **Parse** `CODEX/00_INDEX/MANIFEST.yaml`
2. **Filter** by `tags`, `type`, `status`, or `agents` field
3. **Read** only the docs that match your current task

Do not scan the entire CODEX. Use MANIFEST.yaml as your index — it's kept in sync by the Architect Agent.

---

## 5. Governance — The Laws

All governance documents live in `CODEX/10_GOVERNANCE/`. **Read these first** — they are mandatory, not advisory.

| ID | Document | What It Governs |
|:---|:---------|:----------------|
| GOV-001 | `DocumentationStandard.md` | Doc formatting, frontmatter schema, CODEX taxonomy |
| GOV-002 | `TestingProtocol.md` | Testing tiers, coverage thresholds, forensic reports |
| GOV-003 | `CodingStandard.md` | Code quality rules (stack-agnostic principles) |
| GOV-004 | `ErrorHandlingProtocol.md` | Structured error handling requirements |
| GOV-005 | `AgenticDevelopmentLifecycle.md` | End-to-end development workflow |
| GOV-006 | `LoggingSpecification.md` | Structured logging standards |
| GOV-007 | `AgenticProjectManagement.md` | PM system, sprint/backlog/defect management |
| GOV-008 | `InfrastructureAndOperations.md` | Infra and ops standards |

### Key Rules (Quick Reference)

- Every `.md` file requires YAML frontmatter (see GOV-001 for schema)
- Stay under 10KB per document — split large docs
- Use templates from `CODEX/_templates/` for new docs
- Use controlled tags from `CODEX/00_INDEX/TAG_TAXONOMY.yaml` only
- Update `MANIFEST.yaml` when creating or modifying docs
- Never change a `CON-` contract unilaterally — propose via `EVO-`

---

## 6. Agent Roles

Detailed role definitions live in `CODEX/80_AGENTS/`. Copy the relevant template into a new agent session's context and fill in the placeholders.

| Template | Role | Use When |
|:---------|:-----|:---------|
| `AGT-001_Architect_Agent.md` | Architect (AI PM) | Starting a new project or new Architect session |
| `AGT-002_Developer_Agent.md` | Developer (execution) | Assigning a developer to a sprint |
| `AGT-003_Tester_Agent.md` | Tester (verification) | Beginning a verification sprint |

Agent definitions are **role templates**, not project-specific profiles. They are coding-language agnostic — agents learn the tech stack from blueprints and contracts.

### Role Boundaries

- **Architect:** Governs, audits, assigns. Does not write feature code.
- **Developer:** Executes sprint tasks. Does not modify contracts or blueprints.
- **Tester:** Verifies output against contracts. Does not fix defects or communicate directly with developers.

---

## 7. Document Types and Prefixes

| Prefix | Type | Lives In | Created By |
|:-------|:-----|:---------|:-----------|
| `BLU-` | Blueprint (design spec) | `20_BLUEPRINTS/` | Human + Architect |
| `CON-` | Interface Contract | `20_BLUEPRINTS/` | Human (Architect maintains) |
| `SPR-` | Sprint | `05_PROJECT/` | Architect |
| `BCK-` | Backlog | `05_PROJECT/` | Architect |
| `PRJ-` | Roadmap | `05_PROJECT/` | Human (Architect maintains) |
| `DEF-` | Defect Report | `50_DEFECTS/` | Developer or Tester |
| `EVO-` | Evolution Proposal | `60_EVOLUTION/` | Any agent |
| `VER-` | Verification Report | `40_VERIFICATION/` | Tester |
| `RUN-` | Runbook | `30_RUNBOOKS/` | Architect |
| `GOV-` | Governance | `10_GOVERNANCE/` | Human |
| `AGT-` | Agent Definition | `80_AGENTS/` | Human |

---

## 8. Mandatory Workflows — CRITICAL

> **⚠️ ALL agents MUST follow these two workflows. Violations cause zombie processes, broken commits, and governance failures. These are NON-NEGOTIABLE.**

### `/safe_commands` — Read BEFORE Running ANY Command

File: `.agent/workflows/safe_commands.md`

**Every agent MUST read this workflow before executing any terminal command.** Key rules:
- ❌ NEVER walk the full repo tree (`find .`, `dotnet build` on full solution for single-file changes)
- ✅ Scope commands to specific directories or changed files
- ✅ Use `GIT_TERMINAL_PROMPT=0` for all git network commands
- ✅ Never poll `command_status` more than twice — verify outcomes directly
- ✅ Kill hung commands before retrying

### `/git_commit` — Follow for EVERY Commit

File: `.agent/workflows/git_commit.md`

**Every agent MUST follow this workflow for every git commit.** Key rules:
- Run hygiene checks (junk files, secrets scan) before staging
- Structured commit messages with `Agent:`, `Why:`, `What:`, `Refs:` fields
- Secret scanning is mandatory and blocking
- One branch per sprint (developers), granular commits per task
- Never commit runtime artifacts, test output, or secrets

### Other Workflows

| Command | What It Does |
|:--------|:-------------|
| `/test` | Run all applicable GOV-002 tiers (auto-detect stack) |
| `/manage_documents` | Scan, lint, and sync all CODEX docs |
| `/audit_sprint` | Run Architect sprint audit checklist |
| `/deploy` | Deploy to production (Architect only) |

---

## 9. Configuration

- **Agent workflows**: `.agent/workflows/`

---

## 10. Commit Conventions

Use conventional commits with CODEX references:

```
feat(SPR-NNN): description of feature
fix(DEF-NNN): description of fix
docs(GOV-NNN): description of doc change
```

---

## 11. New Session Reading Order

When starting a fresh session on this project:

1. `AGENTS.md` — this file (project context)
2. `CODEX/00_INDEX/MANIFEST.yaml` — build your document map
3. **`.agent/workflows/safe_commands.md`** — READ BEFORE ANY COMMANDS
4. **`.agent/workflows/git_commit.md`** — READ BEFORE ANY COMMITS
5. `CODEX/10_GOVERNANCE/` — read the governance docs relevant to your role
6. `CODEX/80_AGENTS/AGT-NNN` — read your role definition
7. `CODEX/05_PROJECT/` — check active sprints, backlog, roadmap
8. Referenced `BLU-` and `CON-` docs — your execution constraints
