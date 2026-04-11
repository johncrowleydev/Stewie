---
id: JOB-027
title: "Tailwind CSS v4 Migration"
type: planning
status: OPEN
owner: architect
agents: [developer]
tags: [frontend, design-system, tailwind, migration]
related: [PRJ-001, BCK-001, JOB-024, JOB-028]
created: 2026-04-11
updated: 2026-04-11
version: 1.1.0
---

> **BLUF:** Migrate the entire frontend from 4,349 lines of vanilla CSS in a single `index.css` file to Tailwind CSS v4 with brand design tokens, self-hosted fonts, and dark mode support. This is the foundation for the component library (JOB-028) and all subsequent UI work.

# JOB-027: Tailwind CSS v4 Migration

## Objective

Replace all vanilla CSS with Tailwind CSS v4 utility classes. Establish design tokens (colors, typography, spacing, shadows, radii) via Tailwind's `@theme` directive. Self-host Inter and JetBrains Mono fonts. Ensure full dark mode and responsive support.

## Pre-Completed Work

T-400 and T-401 are already done on `main`. The following are in place:

- Tailwind CSS v4 installed (`tailwindcss` + `@tailwindcss/vite`)
- Vite plugin configured in `vite.config.ts`
- `src/app.css` created with `@import "tailwindcss"`, `@theme` block (full brand tokens), `@font-face` declarations, and `@layer base` defaults
- Self-hosted fonts in `public/fonts/` (Inter 400/500/600, JetBrains Mono 400)
- `main.tsx` imports both `app.css` (Tailwind) and `index.css` (legacy) — both active during migration

## Design Decisions

1. **Brand colors are in `@theme`** — use `text-primary-400`, `bg-primary-50`, `border-primary-300`, etc. Do NOT use raw hex values.
2. **Font family is in `@theme`** — `font-sans` maps to Inter, `font-mono` maps to JetBrains Mono. The base `html` element already sets `font-family: var(--font-sans)`.
3. **Dark mode uses `[data-theme="dark"]`** — The app toggles a `data-theme` attribute on `<html>` (see `useTheme.ts` hook). Configure dark variant in `app.css`: `@variant dark (&:where([data-theme="dark"], [data-theme="dark"] *))`.
4. **14px base font** — `html { font-size: 14px }` is set in `app.css` @layer base. All `rem` values are relative to this.
5. **Responsive breakpoints** — Use Tailwind defaults: `sm:` (640px), `md:` (768px), `lg:` (1024px), `xl:` (1280px), `2xl:` (1536px). Mobile-first approach.

## Tasks

### ~~T-400: Tailwind v4 Installation + Configuration~~ ✅ COMPLETE
### ~~T-401: Self-Hosted Fonts~~ ✅ COMPLETE

### T-402: Migrate Layout Component
**Highest-risk task — the shell that wraps everything.**

Convert `components/Layout.tsx` from vanilla CSS to Tailwind utilities.

Current CSS classes to replace: `.app-layout`, `.sidebar`, `.sidebar-brand`, `.sidebar-nav`, `.main-content`, `.main-header`, `.page-content`, `.mobile-menu-trigger`, `.sidebar-overlay`, `.user-menu-trigger`, `.user-dropdown`, `.user-dropdown-item`, `.live-indicator`, `.live-dot`.

- Sidebar: 220px fixed on desktop (≥lg), hamburger overlay on mobile
- Header: page title + live indicator + user menu dropdown
- Mobile: hamburger shows, sidebar overlays with backdrop

Delete migrated CSS blocks from `index.css` after conversion. Verify visually.

### T-403: Migrate Auth Pages
- `LoginPage.tsx` — centered card on gradient bg, logo + wordmark, form fields, primary button, register link
- `RegisterPage.tsx` — same pattern with invite code field

### T-404: Migrate Dashboard + Job Pages
- `DashboardPage.tsx` — stat cards grid (4-col desktop, 2-col tablet, 1-col mobile), recent jobs table, "+ New Job" button (will be removed in JOB-029, keep for now)
- `JobsPage.tsx` — jobs list table with status badges
- `JobDetailPage.tsx` — largest page: task timeline, governance panels, container output, diff viewer. Be systematic.

### T-405: Migrate Project Pages
- `ProjectsPage.tsx` — project cards grid, inline new project form
- `ProjectDetailPage.tsx` — project info cards, architect controls

### T-406: Migrate Settings + Events Pages
- `SettingsPage.tsx` — 819 lines, largest component. GitHub section, LLM keys, admin invite panel, admin user panel
- `EventsPage.tsx` — event list table with status badges

### T-407: Migrate Shared Components
Migrate in dependency order:
1. `Icons.tsx` — SVG sizing classes
2. `StatusBadge.tsx` — status pill
3. `ChatPanel.tsx`, `ChatSlideover.tsx` — chat UI
4. `ArchitectControls.tsx` — project-level architect management
5. `ContainerOutputPanel.tsx` — terminal output viewer
6. `GovernanceReportPanel.tsx`, `GovernanceAnalyticsPanel.tsx` — governance UI
7. `JobProgressPanel.tsx` — progress bars
8. `TaskDagView.tsx` — task dependency graph
9. `RepoCombobox.tsx` — GitHub repo combobox

### T-408: Delete index.css + Final Verification
1. Remove `import "./index.css"` from `main.tsx`
2. Delete `src/index.css` entirely
3. Verify ALL pages render correctly in light and dark mode
4. Responsive spot-check at 320px, 768px, 1024px, 1440px
5. Run `npm run build` to verify TypeScript + Vite build succeeds

## Rules

1. **One commit per task** — e.g., `feat(JOB-027): migrate Layout component to Tailwind (T-402)`
2. **CSS-only migration** — do NOT modify component logic or behavior. File a `DEF-` for bugs found.
3. **No new features** — no new buttons, pages, or layout changes. Just CSS → Tailwind.
4. **Preserve dark mode** — every element with a dark style must have a `dark:` Tailwind equivalent.
5. **Preserve responsiveness** — anything that collapses on mobile must still collapse.
6. **Delete from `index.css` as you go** — remove migrated CSS blocks incrementally.
7. **Visual verification after each task** — reload and confirm identical appearance.

## Contracts Referenced
- CON-002 (API Contract) — no changes required
- GOV-003 (Coding Standard) — Tailwind class ordering convention

## Reference Files
- `src/Stewie.Web/src/app.css` — Tailwind tokens (DO NOT MODIFY unless adding dark variant)
- `src/Stewie.Web/src/index.css` — Legacy CSS to migrate and delete (4,349 lines)
- `src/Stewie.Web/src/hooks/useTheme.ts` — Dark mode toggle mechanism

## Exit Criteria
- [ ] Zero lines remaining in `index.css` (file deleted)
- [ ] `main.tsx` only imports `app.css`
- [ ] All pages render identically in light and dark mode
- [ ] Responsive at all breakpoints (320px–1920px)
- [ ] Self-hosted Inter + JetBrains Mono rendering correctly
- [ ] `npm run build` succeeds with zero errors
- [ ] All commits on `feature/JOB-027-tailwind-migration` branch
