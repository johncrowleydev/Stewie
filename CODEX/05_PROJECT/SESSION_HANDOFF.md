# Session Handoff: Stewie Project

> **Date:** 2026-04-09
> **Branch State:** `main` (Clean — 0 stale branches, fully merged, tested)
> **Last Completed:** Phase 3 — Governance Engine (JOB-006, JOB-007, JOB-008)

## 1. Current State

Stewie is a governance-first AI agent orchestration system. It coordinates developer agents in isolated Docker containers, manages git branches, and opens pull requests — all under automated governance enforcement.

**Phase completion:** Phases 0–3 are done. Phase 4 (Multi-Task Jobs) is next.

| Metric | Value |
|:-------|:------|
| Tests | **76 passing**, 5 skipped |
| Jobs completed | JOB-001 through JOB-008 (all CLOSED) |
| API endpoints | 14 endpoints (CON-002 v1.6.0) |
| Governance rules | 15 deterministic checks (CON-001 v1.4.0) |
| Worker images | 3 (dummy, script, governance) |

## 2. What Phase 3 Delivered

Every worker's output is now automatically validated via a **tester task** that runs after every developer task:

```
Developer task → Governance tester → PASS → Push + PR → Job complete
                                   → FAIL → Retry with feedback (up to 2x)
                                          → Final FAIL → GovernanceFailed
```

Key infrastructure:
- **Sequential task chains** — WorkTask gained `ParentTaskId`, `AttemptNumber`, `GovernanceViolationsJson`
- **GovernanceReport entity** — per-rule pass/fail results persisted to DB
- **Governance worker container** — `stewie-governance-worker` image with 15 rules across all 8 GOV docs + SEC-001 secret scanning
- **Rejection + retry loop** — `Stewie:MaxGovernanceRetries` (default 2), `Stewie:WarningsBlockAcceptance` (default false)
- **Dashboard** — task chain timeline + GovernanceReportPanel with expandable per-rule details
- **API** — `GET /api/jobs/{id}/governance`, `GET /api/tasks/{id}/governance`

## 3. Critical Context

### Environment
- **Required env vars:** `STEWIE_JWT_SECRET`, `STEWIE_ENCRYPTION_KEY`, `STEWIE_ADMIN_PASSWORD`
- **Ports:** API on `:5275`, frontend on `:5173`
- **DB:** SQL Server 2022 via docker-compose (14 migrations)
- **Test Factory:** `StewieWebApplicationFactory` uses in-memory SQLite with auto-injected credentials. Test user: `admin` / `Admin@Stewie123!`

### Operational Rules
- **File-redirect workflow:** ALL heavy processes (`dotnet test`, `dotnet build`, `npm run build`) must redirect output to `/tmp/` and read with `tail`/`grep`. Do NOT use bash pipe chains for long-running processes — causes phantom terminal hangs.
- **`GIT_TERMINAL_PROMPT=0`**: Required on all git network commands.
- **Migration timing:** First test run after new migration may show transient failures (5 tests). Second run is always clean.

### Key Files
| What | Where |
|:-----|:------|
| Roadmap | `CODEX/05_PROJECT/PRJ-001_Roadmap.md` |
| Runtime contract | `CODEX/20_BLUEPRINTS/CON-001_Runtime_Contract.md` (v1.4.0) |
| API contract | `CODEX/20_BLUEPRINTS/CON-002_API_Contract.md` (v1.6.0) |
| Orchestration service | `src/Stewie.Application/Services/JobOrchestrationService.cs` |
| Governance worker | `workers/governance-worker/` |
| MANIFEST | `CODEX/00_INDEX/MANIFEST.yaml` |

## 4. Next Steps — Phase 4: Multi-Task Jobs

Phase 4 generalizes the sequential task chains from Phase 3 into full parallel execution:

**Exit criteria (from PRJ-001):**
- [ ] Job with N tasks executing in parallel containers
- [ ] Task dependency DAG (sequential and parallel)
- [ ] Aggregated Job status from constituent Tasks
- [ ] Dashboard shows multi-task Job progress
- [ ] Governance failure analytics (trending violations, GOV update suggestions)

### User Preferences (from Phase 3 discussions)
- **Stack priority:** C#/.NET + React first, Elixir + React next
- **Stack detection:** User dislikes file-based heuristic, prefers `stewie.json` project config — consider implementing in Phase 4
- **Governance analytics:** User wants failure data tracked to suggest GOV updates over time
- **"Job" is the term** — not Sprint, not Run. This was settled in JOB-006.

**To resume work**, begin Phase 4 planning against the exit criteria above.
