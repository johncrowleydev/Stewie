---
id: DEF-001
title: "Dashboard is dark-mode only — no light theme toggle"
type: reference
status: OPEN
owner: coder
agents: [coder]
tags: [defect, frontend, ux]
related: [SPR-001, VER-002]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** The React dashboard shipped with a hardcoded dark theme and no mechanism for users to switch to a light theme. This is a UX defect — users in bright environments or with accessibility needs have no option to use a lighter color scheme.

# Defect Report: Dark Mode Only — No Light Theme Toggle

## 1. Summary

| Field | Value |
|:------|:------|
| **Priority** | P3 |
| **Severity** | 4-MINOR |
| **Status** | OPEN |
| **Discovered By** | Human (UAT) |
| **Discovered During** | UAT — Sprint 001 verification |
| **Component** | `Stewie.Web/ClientApp` — `index.css`, Layout component |
| **Branch** | `fix/DEF-001-light-theme` or rolled into SPR-002 |

## 2. Steps to Reproduce

1. Open the Stewie dashboard at `http://localhost:5173/`
2. Observe the UI is dark-themed (dark background, light text)
3. Look for a theme toggle (light/dark switch) — none exists
4. Check OS-level `prefers-color-scheme` — value is ignored

**Expected Result**: User can toggle between dark and light themes, OR the dashboard respects `prefers-color-scheme` from the OS.
**Actual Result**: Dashboard is hardcoded to dark theme only. All CSS custom properties in `:root` use dark palette values with no alternative.

## 3. Evidence

- `index.css` line 10-74: All CSS custom properties define dark-mode-only values (`--color-bg: #0f1117`, `--color-surface: #1a1d27`, etc.)
- No `@media (prefers-color-scheme: light)` query exists
- No `[data-theme="light"]` or `.light-theme` class selector exists
- No toggle component exists in Layout.tsx

## 4. Root Cause Analysis

SPR-001 task T-013 specified "Dark theme preferred" without requiring a theme toggle. The sprint spec was ambiguous — it should have required both themes with dark as default.

## 5. Fix

**Proposed approach:**
1. Define a light-mode palette as CSS custom properties under `[data-theme="light"]`
2. Add a theme toggle button to the sidebar or header in `Layout.tsx`
3. Persist theme preference in `localStorage`
4. Optionally respect `prefers-color-scheme` as the initial default

**Affected files:**
- `src/Stewie.Web/ClientApp/src/index.css` — add light theme variables
- `src/Stewie.Web/ClientApp/src/components/Layout.tsx` — add toggle button
- New: `src/Stewie.Web/ClientApp/src/hooks/useTheme.ts` — theme state management

## 6. Verification

- [ ] Light theme renders correctly on all pages
- [ ] Theme toggle persists across page reloads (localStorage)
- [ ] Dark theme is still the default
- [ ] No contrast or readability issues in light mode
- [ ] Architect UAT approved
