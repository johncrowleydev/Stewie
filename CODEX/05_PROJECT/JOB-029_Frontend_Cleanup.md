---
id: JOB-029
title: "Frontend Cleanup — Dead Features + Visual Fixes"
type: planning
status: CLOSED
owner: architect
agents: [developer]
tags: [frontend, cleanup, phase-7]
related: [JOB-027, JOB-028, PRJ-001, GOV-003]
created: 2026-04-12
updated: 2026-04-12
version: 1.0.0
---

> **BLUF:** Small cleanup job. Remove the manual job creation flow (Architect creates jobs now), fix known visual bugs from the Tailwind migration, and do a quick responsive check. Closes out Phase 7.

# JOB-029: Frontend Cleanup — Dead Features + Visual Fixes

## Context

With the Tailwind migration (JOB-027) and component library (JOB-028) done, Phase 7 needs a final cleanup pass:

1. **Manual job creation is dead code.** Since Phase 6, the Architect Agent creates jobs via chat. The `CreateJobPage`, its route, and the "+ New Job" buttons on Dashboard/Jobs pages are vestigial. They confuse the UX — users should chat to create work, not fill out a form.

2. **JOB-027 left some minor issues.** DEF-012 (hardcoded hex in ContainerOutputPanel), and potentially other small visual bugs.

3. **Responsive hasn't been verified** at mobile breakpoints since the Tailwind migration touched every page.

## Branch

`feature/JOB-029-cleanup`

---

## Tasks

### T-510: Remove Manual Job Creation Flow

**Files to modify:**
- `src/pages/CreateJobPage.tsx` — **DELETE**
- `src/App.tsx` — remove route and import
- `src/pages/DashboardPage.tsx` — remove "+ New Job" link
- `src/pages/JobsPage.tsx` — remove "+ New Job" link
- `src/components/Layout.tsx` — remove "Create Job" title mapping
- `src/api/client.ts` — remove `createJob()` function
- `src/types/index.ts` — remove `CreateJobRequest` interface

**Acceptance criteria:**
- `CreateJobPage.tsx` deleted
- No references to `/jobs/new` anywhere in the codebase
- No `createJob` function or `CreateJobRequest` type remaining
- Dashboard and Jobs pages render without the button
- `npm run build` succeeds

**Commit:** `feat(JOB-029): remove manual job creation flow (T-510)`

---

### T-511: Fix ContainerOutputPanel Hardcoded Colors (DEF-012)

**File:** `src/components/ContainerOutputPanel.tsx`

The terminal emulator panel has 13 hardcoded hex values from the migration. These are intentional "terminal chrome" colors but should be documented and organized:

1. Extract terminal-specific colors to `tw.ts` as named constants:
   - `termBg` — `#0d1117` (terminal background)
   - `termHeaderBg` — `#161b22` (terminal header/footer)
   - `termText` — `#c9d1d9` (terminal stdout text)
   - `termMuted` — `#8b949e` (terminal muted text)
   - `termLineNum` — `#484f58` (line numbers)
   - `termError` — `#f97583` (stderr text)
   - Traffic light dots stay inline (they're one-off UI chrome: `#ff5f57`, `#febc2e`, `#28c840`)

2. Replace all 13 hardcoded values in ContainerOutputPanel with the `tw.ts` references

**Acceptance criteria:**
- Zero bare hex values in ContainerOutputPanel.tsx (except the 3 traffic light dots)
- Terminal renders identically before and after
- `npm run build` succeeds

**Commit:** `fix(JOB-029): extract terminal colors to tw.ts (T-511, DEF-012)`

---

### T-512: Quick Responsive Smoke Test + Fixes

**No specific file list** — this is an audit task.

1. Open each page at 375px width (iPhone) and 768px width (tablet) in browser dev tools
2. Fix any layout breaks:
   - Sidebar must collapse (already has `max-md:` breakpoints)
   - Tables must not overflow horizontally
   - Cards must stack vertically
   - Text must not clip

3. Document any issues found that are too complex to fix inline — file as DEF- for Phase 9 JOB-036

**Acceptance criteria:**
- All pages load without horizontal scroll at 375px
- No overlapping or clipped content
- Any unfixable issues documented in commit message
- `npm run build` succeeds

**Commit:** `fix(JOB-029): responsive fixes from smoke test (T-512)`

---

## Exit Criteria

- [ ] `CreateJobPage.tsx` deleted and all references removed
- [ ] ContainerOutputPanel hardcoded colors extracted to `tw.ts`
- [ ] Responsive smoke test passed at 375px and 768px
- [ ] `npm run build` succeeds with zero errors
- [ ] One commit per task (3 commits total)

## Phase 7 Completion

When JOB-028 and JOB-029 are both CLOSED, Phase 7 is complete. Update:
- PRJ-001 Roadmap: mark Phase 7 ✅ COMPLETE
- BCK-001 Backlog: add JOB-028/029 backlog items as done
- README.md: update phase status
