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
version: 1.0.0
---

> **BLUF:** Migrate the entire frontend from 4,349 lines of vanilla CSS in a single `index.css` file to Tailwind CSS v4 with brand design tokens, self-hosted fonts, and dark mode support. This is the foundation for the component library (JOB-028) and all subsequent UI work.

# JOB-027: Tailwind CSS v4 Migration

## Objective

Replace all vanilla CSS with Tailwind CSS v4 utility classes. Establish design tokens (colors, typography, spacing, shadows, radii) via Tailwind's `@theme` directive. Self-host Inter and JetBrains Mono fonts. Ensure full dark mode and responsive support.

## Tasks

### T-400: Tailwind v4 Installation + Configuration
- Install `tailwindcss` and `@tailwindcss/vite`
- Add Tailwind Vite plugin to `vite.config.ts`
- Create `src/app.css` with `@import "tailwindcss"` and `@theme` block
- Configure brand tokens:
  - Primary: `#6fac50`, hover: `#5d9442`, subtle: `rgba(111,172,80,0.1)`
  - Semantic: success `#22c55e`, warning `#f59e0b`, error `#ef4444`, info `#3b82f6`
  - Surface light: bg `#f8f9fb`, surface `#ffffff`, border `#e2e5ea`
  - Spacing scale, shadow scale, border-radius scale, transition tokens
- Configure dark mode token overrides via `@variant dark`
- Update `main.tsx` to import `app.css` instead of `index.css`

### T-401: Self-Hosted Fonts
- Download Inter (400, 500, 600) and JetBrains Mono (400) WOFF2 files
- Place in `public/fonts/`
- Add `@font-face` declarations in `app.css`
- Configure `@theme` `--font-sans` and `--font-mono`

### T-402: Migrate Layout Component
- Convert `Layout.tsx` (sidebar, header, main content) to Tailwind utilities
- Responsive sidebar: fixed on desktop (≥1024px), hamburger on mobile
- Mobile overlay sidebar with backdrop
- Header with user menu dropdown

### T-403: Migrate Auth Pages
- `LoginPage.tsx` — centered card on gradient bg, form inputs, button
- `RegisterPage.tsx` — same pattern, invite code field

### T-404: Migrate Dashboard + Job Pages
- `DashboardPage.tsx` — stat cards, recent jobs table
- `JobsPage.tsx` — jobs list/table
- `JobDetailPage.tsx` — task chain, governance reports, container output

### T-405: Migrate Project Pages
- `ProjectsPage.tsx` — project cards grid, new project form
- `ProjectDetailPage.tsx` — project info, architect controls

### T-406: Migrate Settings + Events Pages
- `SettingsPage.tsx` — GitHub, LLM keys, admin panels
- `EventsPage.tsx` — event list, filters

### T-407: Migrate Shared Components
- `ChatPanel.tsx`, `ChatSlideover.tsx`
- `ArchitectControls.tsx`
- `ContainerOutputPanel.tsx`
- `GovernanceReportPanel.tsx`, `GovernanceAnalyticsPanel.tsx`
- `JobProgressPanel.tsx`, `StatusBadge.tsx`, `TaskDagView.tsx`
- `RepoCombobox.tsx`, `Icons.tsx`

### T-408: Delete index.css + Final Verification
- Remove `src/index.css` entirely
- Verify all pages render in light and dark mode
- Responsive spot-check at 320px, 768px, 1024px, 1440px
- Run existing test suite to verify nothing broke

## Contracts Referenced
- CON-002 (API Contract) — no changes required
- GOV-003 (Coding Standard) — Tailwind class ordering convention

## Exit Criteria
- [ ] Zero vanilla CSS remaining (only `@font-face`, `@import "tailwindcss"`, `@theme`)
- [ ] All pages render correctly in light and dark mode
- [ ] Responsive at all breakpoints (320px–1920px)
- [ ] Self-hosted Inter + JetBrains Mono rendering
- [ ] Existing tests pass
