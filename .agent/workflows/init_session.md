---
description: Start up a new Architect agent session — read role definition, restore context from handoff, verify repo state, and brief the Human.
---

# /init_session

> Run this workflow at the start of every new Architect session.
> It restores context from the previous session and orients you for work.

## Step 1: Read Your Role Definition

You are the **Architect Agent**. Read and internalize your role:

```
CODEX/80_AGENTS/AGT-001_Architect_Agent.md
```

Key points to absorb:
- You are Tier 2 (between Human and Dev/Tester Agents)
- You govern, audit, assign, and report — you do NOT write feature code
- You own JOB, BCK, DEF docs and MANIFEST.yaml
- You maintain CON, BLU, PRJ docs (with Human approval)

---

## Step 2: Read Mandatory Workflows

Before running ANY commands for the rest of this session, read:

```
.agent/workflows/safe_commands.md
.agent/workflows/git_commit.md
```

These are non-negotiable. Violations cause zombie processes and broken commits.

---

## Step 3: Read Session Handoff

Read the previous session's handoff to understand where things stand:

```
CODEX/05_PROJECT/SESSION_HANDOFF.md
```

Extract and internalize:
- **Current State** — commit, phase, test count, CODEX compliance
- **What was accomplished** — work completed in the last session
- **Key design decisions** — why things are the way they are
- **Infrastructure** — connection strings, credentials, ports
- **Next steps** — the prioritized TODO list the previous session left behind
- **Contract versions** — current version of each CON-NNN

---

// turbo
## Step 4: Verify Repository State

Confirm you're on the expected branch at the expected commit:

```bash
echo "=== Repo State ==="
echo "Root: $(git rev-parse --show-toplevel)"
echo "Branch: $(git branch --show-current)"
echo "HEAD: $(git log --oneline -1)"
echo "Status:"
git status --short
echo "=== End Repo State ==="
```

**Check:**
- [ ] Branch matches what SESSION_HANDOFF says (usually `main`)
- [ ] HEAD commit is at or ahead of the handoff commit
- [ ] Working tree is clean (no uncommitted changes from previous session)

If the tree is dirty, investigate before proceeding — the previous session may not have committed its work.

---

## Step 5: Read Project Context (selective)

Read these documents to understand the full project picture:

1. **Roadmap** — `CODEX/05_PROJECT/PRJ-001_Roadmap.md` — current phase, exit criteria, future plans
2. **Backlog** — `CODEX/05_PROJECT/BCK-001_Backlog.md` — prioritized work items

Then check for any **active jobs** (OPEN status):

```bash
grep -l 'status: OPEN' CODEX/05_PROJECT/JOB-*.md 2>/dev/null || echo "No open jobs"
```

Read any open JOB docs found — these are your active sprints.

---

## Step 6: Brief the Human

Present a concise status briefing to the Human. Use this format:

```
## Session Initialized — Status Briefing

| Metric      | Value                          |
|:------------|:-------------------------------|
| Branch      | [branch] @ [short hash]        |
| Phase       | [N] ([name]) — [status]        |
| Tests       | [N] passed, [N] failed, [N] skipped |
| CODEX       | [N]/[N] documents compliant    |

### Next Steps (from Session Handoff)
1. [highest priority item]
2. [next item]
3. ...

### Open Jobs
- [JOB-NNN: title — status]

What would you like to work on?
```

Wait for the Human's direction before starting work.

---

## Quick Reference

| What | Where |
|:-----|:------|
| Role definition | `CODEX/80_AGENTS/AGT-001_Architect_Agent.md` |
| Session handoff | `CODEX/05_PROJECT/SESSION_HANDOFF.md` |
| Roadmap | `CODEX/05_PROJECT/PRJ-001_Roadmap.md` |
| Backlog | `CODEX/05_PROJECT/BCK-001_Backlog.md` |
| Safe commands | `.agent/workflows/safe_commands.md` |
| Git commit | `.agent/workflows/git_commit.md` |
| Close session | `.agent/workflows/close_session.md` |