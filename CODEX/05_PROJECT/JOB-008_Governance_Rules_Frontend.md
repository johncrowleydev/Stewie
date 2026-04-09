---
id: JOB-008
title: "Governance Rule Engine + Frontend"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, governance]
related: [JOB-007, CON-001, CON-002, GOV-001, GOV-002, GOV-003, GOV-004, GOV-005, GOV-006, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Populate the governance worker with actual deterministic checks for all 8 GOV documents + secret scanning. Build the frontend task chain timeline and governance report panel. This completes Phase 3 — after this job, every worker's output is automatically validated.

# Job 008: Governance Rule Engine + Frontend

**Phase:** Phase 3 — Governance Engine
**Target:** Scope-bounded
**Agent(s):** Dev Agent A (Governance Worker Rules), Dev Agent B (Frontend + Tests)
**Dependencies:** JOB-007 complete (merged — entities, orchestration, worker skeleton all live)
**Contracts:** CON-001 v1.4.0 (governance-report.json), CON-002 v1.6.0

---

## Design Decisions (Approved)

1. **Secret scanning (SEC-001):** YES — governance worker scans diffs for credential patterns
2. **Warning severity:** User-configurable. Default: only errors block acceptance. Setting: `Stewie:WarningsBlockAcceptance` (bool, default false)
3. **Stack detection:** File heuristic (`*.csproj` → dotnet, `package.json` → react). Future: `stewie.json` override.

---

## Dev Agent A Tasks (Governance Worker Rules)

> **Branch:** `feature/JOB-008-rules`

### T-074: Governance Rule Scripts for .NET Stack

Implement the following rules in `workers/governance-worker/rules/dotnet-rules.sh`:

| Rule ID | GOV Doc | Check | Type | Severity |
|:--------|:--------|:------|:-----|:---------|
| GOV-001-001 | GOV-001 | README.md exists | file_exists | error |
| GOV-001-002 | GOV-001 | Public members have XML doc comments (`///`) | grep | warning |
| GOV-002-001 | GOV-002 | `dotnet build` succeeds (exit 0) | command | error |
| GOV-002-002 | GOV-002 | `dotnet test` succeeds (exit 0) | command | error |
| GOV-002-003 | GOV-002 | Test count > 0 | parse_output | error |
| GOV-003-001 | GOV-003 | No `: any` in .ts/.tsx files | grep | error |
| GOV-003-002 | GOV-003 | No `console.log` in .ts/.tsx source files | grep | warning |
| GOV-003-003 | GOV-003 | No `Console.WriteLine` in .cs service files | grep | warning |
| GOV-004-001 | GOV-004 | Error handling middleware present | grep | error |
| GOV-005-001 | GOV-005 | Branch name matches `feature/JOB-*` pattern | regex | error |
| GOV-005-002 | GOV-005 | Latest commit message matches conventional format | regex | error |
| GOV-006-001 | GOV-006 | Services use `ILogger` injection | grep | warning |
| GOV-006-002 | GOV-006 | No bare `Console.Write` in service files | grep | warning |
| GOV-008-001 | GOV-008 | Dockerfile or docker-compose present | file_exists | warning |
| SEC-001-001 | SEC-001 | No secrets in git diff (API keys, passwords, tokens) | regex | error |

Each rule function should:
1. Run the check
2. Capture output on failure
3. Return a JSON object: `{"ruleId":"...","ruleName":"...","category":"...","passed":true/false,"details":"...","severity":"..."}`

Also create `workers/governance-worker/rules/react-rules.sh` with the TypeScript-specific checks (GOV-003-001, GOV-003-002) separated for stacks that don't include .NET.

- **AC:** Each rule runs independently. `entrypoint.sh` calls `dotnet-rules.sh`, collects JSON results, writes valid `governance-report.json` per CON-001 §6.

### T-075: Governance Worker Entrypoint (Full Implementation)

Update `workers/governance-worker/entrypoint.sh` to:
1. Read `/workspace/input/task.json` for `taskId` and role confirmation
2. Detect stack from `/workspace/repo/` contents:
   - `*.csproj` or `*.sln` → source `rules/dotnet-rules.sh`
   - `package.json` → source `rules/react-rules.sh`
   - Both → source both
3. Run all applicable rules, collect JSON results
4. Compute verdict: `pass` if zero `severity: "error"` checks failed, else `fail`
5. Write `/workspace/output/governance-report.json` per CON-001 §6
6. Always exit 0 (report communicates verdict, not exit code)
- **AC:** Governance worker container builds (`docker build -t stewie-governance-worker workers/governance-worker/`), runs, and produces valid governance-report.json

---

## Dev Agent B Tasks (Frontend + Tests)

> **Branch:** `feature/JOB-008-frontend-tests`

### T-076: Task Chain Timeline on JobDetailPage

Update `JobDetailPage.tsx` to render the task chain as a vertical timeline when a job has multiple tasks:
- Each task is a node on the timeline
- Node shows: role icon (🔧 developer / 🔍 tester), status badge, attempt number, duration
- Sequential flow: Task 1 (dev) → Task 2 (tester) → Task 3 (dev, attempt 2) → Task 4 (tester)
- Use Stewie brand colors: `#6fac50` for pass, red for fail, amber for in-progress
- Clicking a tester node expands the GovernanceReportPanel below it
- **AC:** Timeline renders correctly for jobs with 1-4 tasks. Single-task legacy jobs still display correctly.

### T-077: GovernanceReportPanel Component

New component `GovernanceReportPanel.tsx`:
- Overall verdict badge (PASS ✅ / FAIL ❌)
- Pass rate bar (e.g., "14/16 checks passed")
- Grouped by GOV category (GOV-001, GOV-002, etc.)
- Each rule shows: rule name, pass/fail icon, severity (error/warning badge)
- Expandable details per failed rule (shows error output in code block)
- Color scheme: green pass, red error fail, amber warning fail
- **AC:** Component renders governance report data from `GET /api/jobs/{id}/governance`. Handles empty/null report gracefully.

### T-078: Integration Test — Dev → Tester → Accept Cycle

Test the complete happy path:
1. Create a job via API
2. Verify dev task spawns
3. Mock/stub the governance container to produce a PASS governance-report.json
4. Verify tester task is created with correct `parentTaskId` and `role=tester`
5. Verify GovernanceReport entity is persisted with `passed=true`
6. Verify job completes successfully
- **AC:** Test passes in the existing test framework using `StewieWebApplicationFactory`

### T-079: Integration Test — Dev → Tester → Fail → Retry → Accept Cycle

Test the retry flow:
1. Create a job via API
2. First governance check returns FAIL with specific violations
3. Verify new dev task spawns with `attemptNumber=2` and `governanceViolationsJson` populated
4. Second governance check returns PASS
5. Verify job completes after retry
6. Verify all events emitted (GovernanceStarted, GovernanceFailed, GovernanceRetry, GovernancePassed)
- **AC:** Test verifies the full retry loop including violation feedback propagation

### T-080: Unit Tests — Governance Report Parsing

Test `GovernanceReportPacket` deserialization:
1. Valid governance-report.json → parses correctly
2. Missing fields → handles gracefully
3. Empty checks array → valid (0 checks)
4. Mixed severity results → verdict computed correctly
- **AC:** All parsing edge cases covered

---

## Job Checklist

| Task | Agent | Status | Description |
|:-----|:------|:-------|:------------|
| T-074 | A | [ ] | .NET governance rule scripts |
| T-075 | A | [ ] | Governance worker entrypoint |
| T-076 | B | [ ] | Task chain timeline on JobDetailPage |
| T-077 | B | [ ] | GovernanceReportPanel component |
| T-078 | B | [ ] | Integration test: happy path |
| T-079 | B | [ ] | Integration test: retry flow |
| T-080 | B | [ ] | Unit tests: report parsing |

---

## Merge Strategy

1. Both agents can work in parallel (no entity dependencies)
2. Agent A merges `feature/JOB-008-rules` first (worker changes only)
3. Agent B rebases `feature/JOB-008-frontend-tests` and merges
4. Verify: `docker build -t stewie-governance-worker workers/governance-worker/`, full test suite, frontend build

---

## Audit Notes (Architect)

_[Pending — will be filled after developer work completes]_
