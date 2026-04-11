---
id: JOB-027-HANDOFF
title: "Developer Agent Handoff — JOB-027 Tailwind CSS v4 Migration"
type: planning
status: READY
owner: architect
agents: [developer]
tags: [handoff, frontend, tailwind, migration]
related: [JOB-027, PRJ-001, AGT-002]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

# Developer Agent Handoff — JOB-027: Tailwind CSS v4 Migration

## Context

You are a Developer Agent assigned to JOB-027. Your sprint document is at `CODEX/05_PROJECT/JOB-027_Tailwind_Migration.md`. Read it first.

**What's already done (T-400, T-401):**
- Tailwind CSS v4 is installed (`tailwindcss` + `@tailwindcss/vite`)
- Vite plugin is configured in `vite.config.ts`
- `src/app.css` exists with `@import "tailwindcss"`, `@theme` block (brand tokens), `@font-face` declarations, and `@layer base` defaults
- Self-hosted fonts are in `public/fonts/` (Inter 400/500/600, JetBrains Mono 400)
- `main.tsx` imports both `app.css` (Tailwind) and `index.css` (legacy) — both active
- The app runs correctly at http://localhost:5173 with no regressions

**What you need to do (T-402 through T-408):**
Migrate every component and page from vanilla CSS classes to Tailwind v4 utility classes, then delete the legacy `index.css`.

## Your Working Branch

```bash
git checkout -b feature/JOB-027-tailwind-migration
```

## Key Design Decisions (from Architect)

1. **Brand colors are in `@theme`** — use `text-primary-400`, `bg-primary-50`, `border-primary-300`, etc. Do NOT use raw hex values.
2. **Font family is in `@theme`** — `font-sans` maps to Inter, `font-mono` maps to JetBrains Mono. The base `html` element already sets `font-family: var(--font-sans)`.
3. **Dark mode uses `[data-theme="dark"]`** — The app toggles a `data-theme` attribute on the `<html>` element (see `useTheme.ts` hook). Use Tailwind's `dark:` variant, BUT you need to configure the dark mode selector. In `app.css`, add within the `@import "tailwindcss"` scope: `@variant dark (&:where([data-theme="dark"], [data-theme="dark"] *))`. Check `useTheme.ts` to confirm the exact attribute used.
4. **14px base font** — `html { font-size: 14px }` is set in `app.css` base layer. All `rem` values are relative to this.
5. **Responsive breakpoints** — Use Tailwind defaults: `sm:` (640px), `md:` (768px), `lg:` (1024px), `xl:` (1280px), `2xl:` (1536px). Mobile-first: base styles apply to smallest screens, add breakpoint prefixes for larger.

## Migration Order (Task by Task)

### T-402: Layout Component (`components/Layout.tsx`)
**This is the highest-risk migration — the shell that wraps everything.**

Current structure:
- `.app-layout` — flex container (sidebar + main)
- `.sidebar` — 220px fixed, logo + nav links
- `.sidebar.open` — mobile overlay mode
- `.sidebar-overlay` — backdrop for mobile
- `.main-content` — flex-grow area with header + page content
- `.main-header` — page title + live indicator + user menu
- `.user-dropdown` — positioned absolute dropdown menu
- `.mobile-menu-trigger` — hamburger button, hidden on desktop

Key CSS classes to find and replace in `index.css`:
- `.app-layout`, `.sidebar`, `.sidebar-brand`, `.sidebar-nav`, `.main-content`, `.main-header`, `.page-content`
- `.mobile-menu-trigger`, `.sidebar-overlay`
- `.user-menu-trigger`, `.user-dropdown`, `.user-dropdown-item`
- `.live-indicator`, `.live-dot`

**Responsive requirements:**
- Desktop (≥1024px): sidebar visible, 220px width
- Mobile (<1024px): sidebar hidden, hamburger shows, sidebar overlays on tap

### T-403: Auth Pages (`pages/LoginPage.tsx`, `pages/RegisterPage.tsx`)
- Centered card on gradient background
- Logo + wordmark above form
- Form fields with labels
- Primary button
- "Don't have an account? Register" / "Have an account? Login" footer link

### T-404: Dashboard + Job Pages
- `DashboardPage.tsx` — stat cards grid (4-col on desktop, 2-col tablet, 1-col mobile), recent jobs table, "+ New Job" button (will be removed in JOB-029, keep for now)
- `JobsPage.tsx` — jobs list table
- `JobDetailPage.tsx` — complex: task timeline, governance panels, container output, diffs. This is the largest page.

### T-405: Project Pages
- `ProjectsPage.tsx` — project cards grid, inline new project form
- `ProjectDetailPage.tsx` — project info cards, architect controls

### T-406: Settings + Events Pages
- `SettingsPage.tsx` — 819 lines, the largest component. GitHub section, LLM keys, admin invite panel, admin user panel. Be systematic.
- `EventsPage.tsx` — event list table with status badges

### T-407: Shared Components
Migrate in this order (dependencies first):
1. `Icons.tsx` — SVG icons. These use `className="nav-icon"` etc. Convert to Tailwind sizing.
2. `StatusBadge.tsx` — status pill component
3. `ChatPanel.tsx`, `ChatSlideover.tsx` — chat UI
4. `ArchitectControls.tsx` — project-level architect management
5. `ContainerOutputPanel.tsx` — terminal-style output viewer
6. `GovernanceReportPanel.tsx`, `GovernanceAnalyticsPanel.tsx` — governance UI
7. `JobProgressPanel.tsx` — job progress bars
8. `TaskDagView.tsx` — task dependency graph
9. `RepoCombobox.tsx` — GitHub repo search combobox

### T-408: Delete `index.css` + Verification
1. Remove `import "./index.css"` from `main.tsx`
2. Delete `src/index.css` entirely
3. Verify ALL pages render correctly in light and dark mode
4. Responsive spot-check at 320px, 768px, 1024px, 1440px
5. Run `npm run build` to verify TypeScript + Vite build succeeds

## Rules

1. **One commit per task** — e.g., `feat(JOB-027): migrate Layout component to Tailwind (T-402)`
2. **Do NOT modify component logic or behavior** — this is a CSS-only migration. If you see a bug in the logic, file a `DEF-` report.
3. **Do NOT add new features** — no new buttons, no new pages, no layout changes. Just CSS → Tailwind.
4. **Preserve all existing dark mode behavior** — every element that has a dark mode style must have a `dark:` Tailwind equivalent.
5. **Preserve all responsive behavior** — if something collapses on mobile today, it must still collapse.
6. **Delete CSS from `index.css` as you migrate each component** — don't leave dead CSS behind. This lets you verify incrementally.
7. **Test after each migration** — reload the page and verify visually that the component looks identical.

## Reference Files

- `src/Stewie.Web/src/app.css` — Tailwind config + tokens (DO NOT MODIFY unless adding dark variant config)
- `src/Stewie.Web/src/index.css` — Legacy CSS to be migrated and deleted (4,349 lines)
- `src/Stewie.Web/src/hooks/useTheme.ts` — Dark mode toggle mechanism
- `CODEX/05_PROJECT/JOB-027_Tailwind_Migration.md` — Your sprint document
- `CODEX/10_GOVERNANCE/GOV-003_CodingStandard.md` — Coding standards

## Exit Criteria

- [ ] Zero lines remaining in `index.css` (file deleted)
- [ ] `main.tsx` only imports `app.css`
- [ ] All pages render identically to current state in light and dark mode
- [ ] Responsive behavior preserved at all breakpoints
- [ ] `npm run build` succeeds with zero errors
- [ ] All commits on `feature/JOB-027-tailwind-migration` branch with proper messages
