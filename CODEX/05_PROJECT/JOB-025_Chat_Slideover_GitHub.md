---
id: JOB-025
title: "Job 025 — Chat Slideover + GitHub Repo Picker"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-7, chat, github, frontend, backend]
related: [JOB-024, PRJ-001, CON-002]
created: 2026-04-11
updated: 2026-04-11
version: 1.1.0
---

> **BLUF:** Convert inline ChatPanel to right-side slideover with pin-to-sidebar option and localStorage persistence. Add GitHub repo combobox with search-as-you-type for project creation. Feature-gate GitHub-dependent UI when no PAT is configured.

# JOB-025 — Chat Slideover + GitHub Repo Picker

## Objective

Convert the inline ChatPanel on ProjectDetailPage to a right-side slideover panel with a pin-to-sidebar option (Azure Portal style). Add a GitHub repo combobox that replaces manual URL input when a GitHub token is configured.

## Branch

`feature/JOB-025-chat-github` — single developer agent owns all frontend + backend work.

## Tasks

### T-300: Chat Slideover Component
- Create `ChatSlideover.tsx` wrapping existing `ChatPanel`
- Two modes: **slideover** (overlays content) and **pinned sidebar** (shrinks content area)
- Toggle between modes with a pin/unpin button in the chat header
- Persist mode choice to `localStorage` key `stewie:chatMode` (`"slideover"` | `"pinned"`)
- Persist pinned width to `localStorage` key `stewie:chatWidth` (default 440px, min 320px, max 600px)
- Slideover: slides from right edge, ~200ms ease-out `transform: translateX()` transition
- Backdrop overlay with click-to-close and Escape key handler
- Pinned: resizable via drag handle on left edge, content area shrinks
- Full-screen on mobile (≤768px), always slideover on mobile
- Floating trigger button on ProjectDetailPage when in slideover mode

### T-301: ProjectDetailPage Integration
- Replace inline `<ChatPanel>` with `<ChatSlideover>` on ProjectDetailPage
- Add floating chat trigger button (bottom-right or header area)
- Pass `projectId` and `architectActive` through to inner ChatPanel

### T-302: GitHub Feature Gating
- On ProjectsPage, check `gitHubStatus.connected` before enabling creation modes
- When disconnected: disable "Create New Repository" mode, show hint "Connect GitHub in Settings to create repos"
- "Link Existing" mode still works (manual URL input)

### T-303: Repo Combobox Component
- Create `RepoCombobox.tsx` — searchable dropdown
- Queries `GET /api/github/repos` on mount (or on focus)
- Type-to-filter with debounce (300ms)
- Shows repo name, full name, and private badge
- On select, populates the repo URL field
- Fallback: if API call fails, fall back to manual URL input with error hint
- Only rendered when GitHub is connected

### T-304: GitHub Repos Endpoint
- Create `GitHubController.cs` with `GET /api/github/repos`
- Proxies the user's stored GitHub PAT to GitHub API (`GET /user/repos?per_page=100&sort=updated`)
- Returns `{ name, fullName, htmlUrl, isPrivate }[]`
- 5-minute in-memory cache per user (use `IMemoryCache`)
- Returns 401 if no PAT configured, 502 if GitHub API fails
- Full XML doc on all methods
- Require `[Authorize]` attribute

### T-305: CON-002 Update
- Document `GET /api/github/repos` endpoint in CON-002
- Request/response schemas
- Error codes (401 no PAT, 502 GitHub unavailable)

### T-306: Integration Tests
- Test repos endpoint with mock HTTP handler
- Test 401 when no PAT
- Test caching behavior (second call hits cache)
- Test 502 on GitHub failure

## Acceptance Criteria

- [ ] Chat opens as slideover from right side
- [ ] Pin/unpin toggles between overlay and fixed sidebar
- [ ] Mode and width persist across page reloads
- [ ] GitHub "Create New Repo" disabled when no token configured
- [ ] Repo combobox shows user's repos when GitHub connected
- [ ] `npm run build` — zero errors
- [ ] `dotnet build` — zero errors
- [ ] All existing + new tests pass

## Dependencies

- JOB-024 must be merged first (emoji cleanup, context panel removal) ✅
- Existing `ChatPanel.tsx` component (not modified, wrapped by new slideover)
- Existing GitHub PAT storage (`/api/users/me/github-token`)
