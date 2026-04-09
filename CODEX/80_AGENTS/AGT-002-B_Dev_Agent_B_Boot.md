---
id: AGT-002-B
title: "Dev Agent B — Frontend & Tests Boot Document"
type: reference
status: APPROVED
owner: architect
agents: [coder]
tags: [agent-instructions, agentic-development, project-specific]
related: [AGT-002, SPR-001, CON-002, BLU-001, GOV-002, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** You are Dev Agent B — the Frontend & Test Developer for Stewie. You own the React dashboard and the .NET test project. Read this document FIRST, then follow the reading order below. You work in parallel with Dev Agent A (backend API) — you do NOT modify backend C# files except to add `Stewie.Tests` to the solution.

# Dev Agent B — Frontend & Tests Boot Document

---

## 1. Your Environment

| Property | Value |
|:---------|:------|
| **Repository** | This monorepo |
| **Frontend port** | `5173` (Vite dev server) |
| **API port** | `5275` (calls proxied via Vite config) |
| **Solution** | `src/Stewie.slnx` |
| **Frontend root** | `src/Stewie.Web/ClientApp/` |

---

## 2. Tech Stack

### Frontend
| Layer | Technology | Version |
|:------|:-----------|:--------|
| Runtime | Node.js | 20+ |
| Framework | React | (check package.json) |
| Build tool | Vite | (check package.json) |
| Language | TypeScript | strict |
| Routing | React Router | (install as part of T-013) |
| Styling | Vanilla CSS | — |

### Tests
| Layer | Technology | Version |
|:------|:-----------|:--------|
| Runtime | .NET | 10 |
| Test framework | xUnit | Latest |
| Mocking | NSubstitute or Moq | Latest |
| Assertions | xUnit + FluentAssertions (optional) | Latest |

---

## 3. CODEX Reading Order

Read these documents IN THIS ORDER before starting any work:

1. `CODEX/00_INDEX/MANIFEST.yaml` — document map
2. `CODEX/80_AGENTS/AGT-002-B_Dev_Agent_B_Boot.md` — this document
3. `.agent/workflows/safe_commands.md` — **READ BEFORE ANY COMMANDS**
4. `.agent/workflows/git_commit.md` — **READ BEFORE ANY COMMITS**
5. `CODEX/05_PROJECT/SPR-001_Phase1_MVP.md` — your current sprint (tasks T-010 through T-016)
6. `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` — API contract (your frontend calls these endpoints)
7. `CODEX/20_BLUEPRINTS/BLU-001_Stewie_System_Blueprint.md` — architecture reference
8. `CODEX/10_GOVERNANCE/GOV-002_TestingProtocol.md` — testing requirements
9. `CODEX/10_GOVERNANCE/GOV-003_CodingStandard.md` — coding rules

---

## 4. Binding Contracts

| Contract | What It Governs | Key Sections |
|:---------|:----------------|:-------------|
| `CON-002` | HTTP API endpoints your frontend calls | §4 (endpoints), §5 (schemas), §6 (errors) |

> **Note:** The API endpoints you consume (GET /api/runs, GET /api/projects, etc.) are being built by Agent A in parallel. During development, your frontend should handle cases where the API is not yet available (loading states, error states). The final integration test happens after both branches merge.

---

## 5. Your File Territory

You are responsible for these directories. DO NOT modify files outside these:

| Directory | What You Create/Modify |
|:----------|:----------------------|
| `src/Stewie.Web/ClientApp/src/` | React components, pages, routing, styles |
| `src/Stewie.Web/ClientApp/public/` | Static assets |
| `src/Stewie.Web/ClientApp/package.json` | Dependencies (React Router, etc.) |
| `src/Stewie.Tests/` | **New project** — xUnit test project |
| `src/Stewie.slnx` | Add Stewie.Tests project reference |

**OFF LIMITS:**
- ❌ `src/Stewie.Domain/` — Agent A's territory
- ❌ `src/Stewie.Application/` — Agent A's territory
- ❌ `src/Stewie.Infrastructure/` — Agent A's territory
- ❌ `src/Stewie.Api/` — Agent A's territory

---

## 6. Branding Requirements

All dashboard UI must follow Stewie branding (from AGENTS.md §2):

| Element | Value |
|:--------|:------|
| **Logo** | `public/stewie-logo.png` (already exists) |
| **Primary color** | `#6fac50` (green) — actions, links, interactive elements |
| **Secondary color** | `#767573` (warm gray) — body text, borders, muted UI |
| **Wordmark** | "stewie" — bold, lowercase, secondary color |
| **Theme** | Dark preferred — dark backgrounds, light text |
| **Font** | System UI stack or Inter/Roboto from Google Fonts |

---

## 7. React App Structure

Create this structure inside `src/Stewie.Web/ClientApp/src/`:

```
src/
├── main.tsx           # App entry (exists)
├── App.tsx            # Root with Router (modify)
├── index.css          # Global styles + design system
├── api/
│   └── client.ts      # API service (fetch wrapper)
├── components/
│   ├── Layout.tsx      # App shell: header, sidebar, main
│   ├── StatusBadge.tsx # Color-coded status badge component
│   └── ...
├── pages/
│   ├── DashboardPage.tsx  # Home/overview
│   ├── RunsPage.tsx       # Runs list
│   ├── RunDetailPage.tsx  # Single run with tasks
│   └── ProjectsPage.tsx   # Projects list + create form
└── types/
    └── index.ts        # TypeScript types matching CON-002 schemas
```

---

## 8. Test Project Setup

Create `src/Stewie.Tests/Stewie.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="xunit" Version="*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="*" />
    <PackageReference Include="NSubstitute" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Stewie.Domain/Stewie.Domain.csproj" />
    <ProjectReference Include="../Stewie.Application/Stewie.Application.csproj" />
    <ProjectReference Include="../Stewie.Infrastructure/Stewie.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Add to `Stewie.slnx`:
```xml
<Project Path="Stewie.Tests/Stewie.Tests.csproj" />
```

---

## 9. Governance Compliance — HARD RULES

> [!CAUTION]
> These are not optional. The Architect WILL reject your branch if any rule is violated.

- [ ] **GOV-001**: TSDoc comments on exported React components
- [ ] **GOV-002**: xUnit tests for RunOrchestrationService and WorkspaceService with mocked dependencies
- [ ] **GOV-003**: TypeScript strict mode (no `any`), clean React patterns
- [ ] **GOV-005**: Branch: `feature/SPR-001-frontend-tests`. Commits: `feat(SPR-001): T-XXX description`
- [ ] **GOV-006**: Console errors and API failures logged clearly
- [ ] **GOV-008**: Frontend proxies API calls (Vite config already set up)

---

## 10. Branch & Commit Rules

- **Branch name:** `feature/SPR-001-frontend-tests`
- **One branch for the entire sprint** — do NOT create per-task branches
- **One commit per task** (granular commits):
  ```
  feat(SPR-001): T-010 create xUnit test project with NSubstitute
  feat(SPR-001): T-011 unit tests for RunOrchestrationService
  feat(SPR-001): T-012 unit tests for WorkspaceService
  feat(SPR-001): T-013 dashboard layout with React Router and Stewie branding
  ...
  ```
- Use `/git_commit` workflow for every commit
- Use `/safe_commands` rules for every terminal command

---

## 11. Merge Order — IMPORTANT

> Your branch merges SECOND. Agent A's backend branch merges first.

After Agent A's branch is merged to `main`:
1. You rebase your branch: `git rebase main`
2. Resolve any conflicts (likely only `Stewie.slnx`)
3. Architect audits your branch
4. Merge to `main`
5. Architect runs end-to-end verification: dashboard consumes live API

---

## 12. Communication Protocol

| Action | How |
|:-------|:----|
| **Report task complete** | Update task status in SPR-001. Commit and push. |
| **Report blocker** | Create `DEF-NNN.md` in `50_DEFECTS/`. Do NOT work around it. |
| **Propose contract change** | Create `EVO-NNN.md` in `60_EVOLUTION/`. Do NOT self-fix. |
| **Ask a question** | Note it in sprint doc under Blockers. Move to next unblocked task. |

### What You Do NOT Do

- ❌ Modify `CON-` or `BLU-` documents
- ❌ Merge to main without Architect audit
- ❌ Skip governance checks
- ❌ Modify files in Agent A's territory (Domain, Application, Infrastructure, Api)
- ❌ Work around contract ambiguity silently
