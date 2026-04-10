---
id: JOB-011
title: "Job 011 — Dashboard + Analytics + stewie.json"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-4, dashboard, analytics]
related: [PRJ-001, CON-002, CON-003, JOB-010]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Display multi-task job progress on the dashboard with a DAG visualization, add governance failure analytics (trending violations, GOV update suggestions), and implement `stewie.json` project configuration to replace file-based stack detection. Creates new contract CON-003.

# Job 011 — Dashboard + Analytics + stewie.json

---

## 1. Context

JOB-010 built the parallel execution engine. JOB-011 surfaces it in the UI and adds the analytics layer that closes out Phase 4's exit criteria:

- ✅ Dashboard shows multi-task Job progress ← **this job**
- ✅ Governance failure analytics ← **this job**
- ✅ `stewie.json` project config ← **this job**

**User preferences (from Phase 3):**
- User dislikes file-based heuristic for stack detection — prefers `stewie.json`
- User wants failure data tracked to suggest GOV updates over time

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Backend services (analytics API, config parser, workspace wiring) | `feature/JOB-011-backend` |
| Dev B | Frontend components (progress panel, DAG view, analytics panel, CSS) | `feature/JOB-011-frontend` |

**Separate branches per agent (GOV-005). Merge order: Dev A first, Dev B rebases onto main after Dev A merges.**

---

## 3. Dependencies

- **Requires JOB-010 merged to main** — multi-task execution, updated API contract.

---

## 4. Tasks

### Dev A — Backend Services

#### T-105: Governance Analytics — Violation Trending API

**Create** `Stewie.Api/Controllers/GovernanceAnalyticsController.cs`:

```csharp
[HttpGet("/api/governance/analytics")]
public async Task<IActionResult> GetAnalytics(
    [FromQuery] int? days = 30,
    [FromQuery] Guid? projectId = null)
```

**Create** `Stewie.Application/Services/GovernanceAnalyticsService.cs`:

```csharp
public class GovernanceAnalyticsService
{
    /// <summary>
    /// Computes governance violation statistics from all GovernanceReports.
    /// </summary>
    public async Task<GovernanceAnalytics> GetAnalyticsAsync(int days = 30, Guid? projectId = null);
}
```

**Response schema:**
```json
{
  "totalJobs": 45,
  "totalGovernanceRuns": 67,
  "passRate": 0.82,
  "topFailingRules": [
    {
      "ruleId": "GOV-003-001",
      "ruleName": "No TypeScript any Types",
      "failCount": 12,
      "trend": "increasing"
    }
  ],
  "suggestedGovUpdates": [
    {
      "govDoc": "GOV-003",
      "reason": "GOV-003-001 fails 26% of runs — consider adding TypeScript strict mode enforcement"
    }
  ]
}
```

**Trending logic:**
- Query all `GovernanceReport` entities within the time window
- Deserialize `CheckResultsJson` from each report
- Aggregate failures by `ruleId`
- Compute trend: compare last 7d vs previous 7d — `increasing`, `decreasing`, `stable`

**Acceptance criteria:**
- [ ] Returns valid analytics for existing governance data
- [ ] Empty DB returns zeroed-out analytics (not errors)
- [ ] Filterable by projectId and time window

---

#### T-107: `stewie.json` Parser Service

**Create** `Stewie.Application/Services/ProjectConfigService.cs`:

```csharp
public class ProjectConfigService
{
    /// <summary>
    /// Reads and parses stewie.json from the repo root.
    /// Returns null if file doesn't exist (fallback to heuristic).
    /// </summary>
    public StewieProjectConfig? LoadFromRepo(string repoPath);
}

public class StewieProjectConfig
{
    public string Version { get; set; } = "1.0";
    public string Stack { get; set; } = string.Empty;       // "dotnet", "node", "python"
    public string Language { get; set; } = string.Empty;     // "csharp", "typescript", "python"
    public string? BuildCommand { get; set; }                // "dotnet build"
    public string? TestCommand { get; set; }                 // "dotnet test"
    public GovernanceConfig? Governance { get; set; }
    public PathConfig? Paths { get; set; }
}

public class GovernanceConfig
{
    public string Rules { get; set; } = "all";               // "all" or explicit list
    public bool WarningsBlockAcceptance { get; set; } = false;
    public int MaxRetries { get; set; } = 2;
}

public class PathConfig
{
    public List<string> Source { get; set; } = [];
    public List<string> Tests { get; set; } = [];
    public List<string> Forbidden { get; set; } = [];
}
```

**Acceptance criteria:**
- [ ] Parses valid `stewie.json` correctly
- [ ] Returns null for missing file (no exception)
- [ ] Invalid JSON throws descriptive error
- [ ] All fields have sensible defaults

---

#### T-108: `stewie.json` Schema Definition (CON-003)

**Create** `CODEX/20_BLUEPRINTS/CON-003_ProjectConfig_Contract.md`:

```
id: CON-003
title: "Project Configuration Contract — stewie.json"
version: 1.0.0
```

Documents the `stewie.json` file format, field definitions, validation rules, defaults, and relationship to CON-001 (where stack detection was previously heuristic).

**Acceptance criteria:**
- [ ] All fields documented with types and constraints
- [ ] Default values specified
- [ ] Relationship to governance worker documented
- [ ] Added to MANIFEST.yaml

---

#### T-109: Wire `stewie.json` Into Workspace Prep

**Modify** `WorkspaceService` or orchestration flow:

After cloning the repo, check for `stewie.json` in the repo root:
1. If present → parse it, inject config into `task.json` (new optional field)
2. If absent → governance worker falls back to heuristic detection (existing behavior)

**Changes to** `TaskPacket` (CON-001):
```csharp
[JsonPropertyName("projectConfig")]
public StewieProjectConfig? ProjectConfig { get; set; }
```

**Acceptance criteria:**
- [ ] `stewie.json` values flow through to the governance worker
- [ ] Missing `stewie.json` → null field, no errors
- [ ] Governance worker can read stack/buildCommand/testCommand from config

---

#### T-110: Governance Analytics — GOV Update Suggestions

**Add to** `GovernanceAnalyticsService.cs`:

Logic for generating GOV update suggestions:
1. Rules that fail >25% of governance runs → suggest review
2. Rules that fail 100% → suggest relaxation or removal
3. New rules that were never triggered → suggest validation
4. Failures trending upward → suggest developer training or tooling

**Acceptance criteria:**
- [ ] Suggestions are non-empty when failure rate exceeds threshold
- [ ] Suggestions reference specific GOV documents
- [ ] Empty when governance pass rate is >95%

---

### Dev B — Frontend Components

#### T-102: Multi-Task Job Progress Component

**Create** `components/JobProgressPanel.tsx`:

Displays N-task job progress:
- Total tasks, completed count, failed count, running count
- Progress bar (filled proportional to completed+failed / total)
- Per-task status list with role badges + status badges
- Auto-refresh via polling (consistent with existing dashboard pattern)

**Design specification:**
- Use existing CSS design system from `index.css`
- Green progress bar for completed, red for failed, amber for running
- Collapse to simple badge for single-task jobs (backward compat)

**Acceptance criteria:**
- [ ] Renders correctly for 1-task jobs (matches existing behavior)
- [ ] Multi-task jobs show per-task progress
- [ ] Progress bar fills as tasks complete
- [ ] Real-time updates via polling

---

#### T-103: Task DAG Visualization

**Create** `components/TaskDagView.tsx`:

Visual representation of the task dependency graph:

**Approach:** Grid-based layout (not a full graph library dependency):
- Tasks arranged in columns by "depth" (topological layer)
- Dependency arrows drawn with CSS borders/connectors
- Each task node shows: objective (truncated), status badge, role icon
- Clicking a task navigates to task detail or expands in-place

**For jobs with no deps (all parallel):** Show tasks in a single horizontal row.
**For linear chains:** Show tasks in a single vertical column (matches Phase 3 timeline).

**Acceptance criteria:**
- [ ] Renders linear chain correctly
- [ ] Renders diamond DAG correctly
- [ ] Renders independent parallel tasks correctly
- [ ] Task status updates in real-time
- [ ] Clickable task nodes

---

#### T-104: Aggregated Status Badges

**Modify** `JobDetailPage.tsx`, `StatusBadge.tsx`, `index.css`:

- Add `PartiallyCompleted` status badge (amber/yellow)
- Add `Blocked` status badge (gray with lock icon)
- Add `Cancelled` status badge (strikethrough gray)
- Update job detail page to show `taskCount`, `completedTaskCount`, `failedTaskCount`

**Acceptance criteria:**
- [ ] New status values render with distinct visual treatment
- [ ] Color palette consistent with existing design system
- [ ] No visual regression for existing status badges

---

#### T-106: Governance Analytics Panel

**Create** `components/GovernanceAnalyticsPanel.tsx`:

Dashboard panel (accessible from nav or dashboard page) showing:
- Pass rate trend (simple bar chart or sparkline, no heavy charting library)
- Top failing rules table (ruleId, name, count, trend arrow)
- GOV update suggestions list (with severity indicator)
- Time filter selector (7d, 30d, 90d)

**Design specification:**
- Use existing CSS variables for colors
- Trend indicators: ↑ (increasing, red), → (stable, gray), ↓ (decreasing, green)
- Responsive layout (stacks vertically on mobile)

**Acceptance criteria:**
- [ ] Renders analytics data from `GET /api/governance/analytics`
- [ ] Handles empty data gracefully (empty state message)
- [ ] Time filter changes refresh the data
- [ ] Suggestions section highlights actionable items

---

### Shared — Testing

#### T-111: Unit Tests — ProjectConfigService

**Create** `Stewie.Tests/Services/ProjectConfigServiceTests.cs`:

| Test | Description |
|:-----|:------------|
| `ValidConfig_ParsesAllFields` | Full stewie.json → all fields populated |
| `MinimalConfig_DefaultsFilled` | Only `stack` present → defaults for everything else |
| `MissingFile_ReturnsNull` | No stewie.json → null, no exception |
| `InvalidJson_ThrowsDescriptive` | Malformed JSON → descriptive error |
| `UnknownStack_AcceptsAnyString` | Custom stack value → parsed as-is |

---

#### T-112: Integration Tests — Analytics Endpoints

**Create** `Stewie.Tests/Integration/GovernanceAnalyticsTests.cs`:

| Test | Description |
|:-----|:------------|
| `EmptyDb_ReturnsZeroedAnalytics` | No reports → passRate=0, empty lists |
| `WithReports_ComputesCorrectStats` | Seed reports → verify aggregation |
| `ProjectFilter_ReturnsOnlyMatchingProject` | Filter by projectId |
| `TimePeriodFilter_ExcludesOldReports` | 7-day filter excludes 30-day-old data |

---

### Architect — Documentation

#### T-113: Update BCK-001, PRJ-001, MANIFEST

- Mark Phase 4 backlog items as completed
- Update PRJ-001 roadmap: Phase 4 = ✅ COMPLETE
- Update MANIFEST with JOB-010, JOB-011, CON-003
- Close JOB-010 and JOB-011

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-003 | **NEW** — `stewie.json` project configuration | v1.0.0 |
| CON-001 | Add optional `projectConfig` field to `task.json` | → v1.5.0 (completed in JOB-010 T-097) |
| CON-002 | Add `GET /api/governance/analytics` endpoint | → v1.8.0 |

---

## 6. Verification

```bash
# Build
dotnet build src/Stewie.Application/Stewie.Application.csproj
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Full test suite
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Analytics + config specific tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --filter "FullyQualifiedName~Analytics|ProjectConfig"

# Frontend
cd src/Stewie.Web/ClientApp && npm run build
```

**Exit criteria:**
- All existing tests pass (no regressions)
- All new analytics and config tests pass
- Dashboard displays multi-task jobs correctly
- `stewie.json` parsed and forwarded to workers
- CON-003 created and added to MANIFEST

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-011 created for Phase 4 Dashboard + Analytics + stewie.json |
| 2026-04-10 | JOB-011 CLOSED — 110 tests pass (9 new), Phase 4 COMPLETE |
