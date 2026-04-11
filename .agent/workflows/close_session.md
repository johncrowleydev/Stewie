---
description: End an Architect agent session — capture state, update handoff doc, verify clean repo, commit, and push.
---

# /close_session

> Run this workflow when ending an Architect session.
> It captures everything the next session needs to pick up where you left off.

## Step 0: Safe Command Rules

Read `.agent/workflows/safe_commands.md` if you haven't already this session.
All commands in this workflow follow those rules.

---

// turbo
## Step 1: Verify Clean Working State

Before capturing state, make sure all work is committed:

```bash
echo "=== Pre-Close State ==="
echo "Branch: $(git branch --show-current)"
echo "HEAD: $(git log --oneline -1)"
echo "Uncommitted changes:"
git status --short
echo "=== End Pre-Close State ==="
```

**If there are uncommitted changes:**
- If they represent completed work → run `/git_commit` first
- If they are scratch/temp → discard with `git restore .`
- If unclear → ask the Human before proceeding

**Do NOT proceed to Step 2 until the working tree is clean** (except for
the files this workflow will create/modify).

---

// turbo
## Step 2: Gather Current Metrics

Collect the data you'll need for the handoff doc:

```bash
echo "=== Metrics Collection ==="
echo "--- Git ---"
echo "Commit: $(git log --oneline -1)"
echo "Branch: $(git branch --show-current)"

echo "--- Tests ---"
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --verbosity quiet 2>&1 | tail -5

echo "--- CODEX Doc Count ---"
TOTAL=$(find CODEX -name '*.md' ! -path '*_templates*' ! -name 'README.md' ! -path '*90_ARCHIVE*' | wc -l)
echo "Total CODEX docs: $TOTAL"

echo "--- Frontend Build ---"
cd src/Stewie.Web/ClientApp && npx vite build --mode production 2>&1 | tail -5; cd ../../..

echo "--- Open Jobs ---"
grep -l 'status: OPEN' CODEX/05_PROJECT/JOB-*.md 2>/dev/null || echo "None"

echo "--- Contract Versions ---"
for con in CODEX/20_BLUEPRINTS/CON-*.md; do
  CON_ID=$(head -20 "$con" | grep '^id:' | sed 's/id: *//' | tr -d '"')
  CON_VER=$(head -20 "$con" | grep '^version:' | sed 's/version: *//' | tr -d '"')
  echo "  $CON_ID: v$CON_VER"
done

echo "=== End Metrics Collection ==="
```

Record all of these values — you'll use them in Step 3.

---

## Step 3: Write SESSION_HANDOFF.md

Overwrite `CODEX/05_PROJECT/SESSION_HANDOFF.md` with a fresh snapshot.

**Use this exact structure** (adapt content to actual state):

```markdown
---
id: SESSION_HANDOFF
title: "Architect Session Handoff"
type: reference
status: ACTIVE
owner: architect
agents: [architect]
tags: [handoff, session-context]
related: [PRJ-001, BCK-001]
created: 2026-04-11
updated: [TODAY'S DATE]
version: [INCREMENT from previous version]
---

> **BLUF:** [One-sentence summary of current project state and what the next session should focus on.]

# Architect Session Handoff

## Current State

- **Commit:** `[short hash]` on `[branch]`
- **Phase [N] ([name]):** [status — e.g., "IN PROGRESS — 3 of 7 tasks complete"]
- **Tests:** [N] passed, [N] failed, [N] skipped
- **Frontend:** [module count] modules ([JS size], [CSS size])
- **CODEX compliance:** [N]/[N] documents passing

## What This Session Accomplished

[Describe ALL significant work done this session. Group by topic. Be specific —
the next agent has zero memory of what happened. Include:]
- Features built or completed
- Documents created or updated
- Decisions made and why
- Bugs found or fixed
- Jobs created, audited, or closed

## Key Design Decisions

[Table of important decisions made this session with rationale.]

| Decision | Rationale |
|:---------|:----------|
| ... | ... |

## Infrastructure

- SQL Server: `stewie-sqlserver` (port 1433)
- RabbitMQ: `stewie-rabbitmq` (port 5672)
- Backend env vars: [list the required env vars with values used]
- Login credentials: [username / password]

## Next Steps (for the next architect session)

[Numbered priority list. Be specific and actionable. Include:]
1. The most important thing to do next
2. ...
3. ...

[For each item, include enough context that a cold-start agent can execute
without asking clarifying questions.]

## Contracts (current versions)

| Contract | Version |
|:---------|:--------|
| CON-001 | v[x.y.z] |
| CON-002 | v[x.y.z] |
| CON-003 | v[x.y.z] |
| CON-004 | v[x.y.z] |
```

**Rules for writing the handoff:**
- Write for a **cold-start agent with zero context** — assume the reader knows nothing
- Be **specific** — "update the README" is useless; "update README.md Contracts table to v2.1.0" is useful
- Include **infrastructure details** — the next agent needs to know how to start the system
- Include **branch names** for any in-progress work
- Don't describe things that are obvious from reading the codebase — focus on state, decisions, and intent

---

## Step 4: Verify CODEX Compliance (quick check)

Don't run the full `/manage_documents` workflow, but do a quick sanity check:

// turbo
```bash
echo "=== Quick CODEX Check ==="
# Orphan check
ORPHANS=0
for f in $(find CODEX -name '*.md' -not -path '*_templates*' -not -path '*/README.md' -not -path '*90_ARCHIVE*' | sort); do
  basename_no_ext=$(basename "$f" .md)
  if ! grep -q "$basename_no_ext" CODEX/00_INDEX/MANIFEST.yaml 2>/dev/null; then
    echo "  ❌ ORPHAN: $f"
    ORPHANS=$((ORPHANS + 1))
  fi
done
[ "$ORPHANS" -eq 0 ] && echo "  ✅ No MANIFEST orphans" || echo "  ⚠️  $ORPHANS orphan(s) — add to MANIFEST before closing"

# Root README staleness
ERRORS=0
for con in CODEX/20_BLUEPRINTS/CON-*.md; do
  CON_ID=$(head -20 "$con" | grep '^id:' | sed 's/id: *//' | tr -d '"')
  CON_VER=$(head -20 "$con" | grep '^version:' | sed 's/version: *//' | tr -d '"')
  if [ -n "$CON_ID" ] && [ -n "$CON_VER" ]; then
    README_VER=$(grep "$CON_ID" README.md | grep -o 'v[0-9.]*' | head -1)
    if [ -n "$README_VER" ] && [ "$README_VER" != "v$CON_VER" ]; then
      echo "  ❌ README.md: $CON_ID says $README_VER, actual is v$CON_VER"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done
[ "$ERRORS" -eq 0 ] && echo "  ✅ Root README contract versions current" || echo "  ⚠️  $ERRORS README mismatch(es) — update before closing"
echo "=== End Quick CODEX Check ==="
```

If orphans or mismatches are found, fix them before proceeding.

---

## Step 5: Commit and Push

Stage and commit the handoff (and any other docs updated during this step):

```bash
git add CODEX/05_PROJECT/SESSION_HANDOFF.md
# Add any other files modified during close (README.md, MANIFEST.yaml, etc.)
git add -A
```

Commit with a structured message:

```bash
git commit -m "docs(SESSION_HANDOFF): v[N] — [one-line summary of session]" \
  -m "Why: Capture session state for next Architect agent" \
  -m "What: Updated handoff with [brief description of what changed]" \
  -m "Agent: architect" \
  -m "Refs: PRJ-001"
```

Push:

```bash
GIT_TERMINAL_PROMPT=0 git push origin $(git branch --show-current)
```

---

// turbo
## Step 6: Final Verification

```bash
echo "=== Session Close Verification ==="
echo "Tree clean: $(git status --short | wc -l) uncommitted files"
echo "Last commit: $(git log --oneline -1)"
echo "Push status: $(GIT_TERMINAL_PROMPT=0 git log --oneline origin/$(git branch --show-current)..HEAD 2>/dev/null | wc -l) unpushed commits"
echo "=== End Verification ==="
```

**All three must be zero/clean:**
- [ ] Working tree clean (0 uncommitted files)
- [ ] Last commit is the handoff commit
- [ ] 0 unpushed commits

---

## Step 7: Brief the Human

Present a session close summary:

```
## Session Closed

### What Was Done
- [bullet list of accomplishments]

### Handoff Written
- SESSION_HANDOFF.md updated to v[N]
- Committed and pushed: [short hash]

### Next Session Should
1. [top priority]
2. [second priority]
3. ...
```

---

## Quick Reference

| What | Where |
|:-----|:------|
| Session handoff | `CODEX/05_PROJECT/SESSION_HANDOFF.md` |
| Init session (reverse of this) | `.agent/workflows/init_session.md` |
| Git commit workflow | `.agent/workflows/git_commit.md` |
| Safe commands | `.agent/workflows/safe_commands.md` |
| Manage documents (full scan) | `.agent/workflows/manage_documents.md` |
