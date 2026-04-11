---
description: Prepare and hand off a job to Developer Agents — verify docs are ready, push to main, and compose copy-paste chat messages for each dev agent.
---

# /start_job

> Run this workflow when a job is fully planned and ready for developer execution.
> It validates the job docs, pushes them to main, and generates handoff messages.

## Step 1: Identify Ready Jobs

// turbo
Scan for jobs with `status: OPEN`:

```bash
echo "=== Open Jobs ==="
for f in CODEX/05_PROJECT/JOB-*.md; do
  STATUS=$(head -15 "$f" | grep '^status:' | sed 's/status: *//')
  if [ "$STATUS" = "OPEN" ]; then
    TITLE=$(head -15 "$f" | grep '^title:' | sed 's/title: *//' | tr -d '"')
    ID=$(head -15 "$f" | grep '^id:' | sed 's/id: *//' | tr -d '"')
    echo "  ✅ $ID: $TITLE ($f)"
  fi
done
echo "=== End Open Jobs ==="
```

**If no OPEN jobs are found → STOP.** Tell the Human:
> "No jobs with `status: OPEN` found. Plan and document a job first, then re-run `/start_job`."

If OPEN jobs are found, confirm with the Human which job(s) to start. Do not proceed
without explicit Human approval on which job to hand off.

---

## Step 2: Validate Job Readiness

For each job being started, read the full `JOB-NNN.md` and verify ALL of the following.
If any check fails, report it to the Human and **do NOT proceed** until fixed.

### 2.1 Frontmatter Completeness

- [ ] `id` matches filename prefix (e.g., `JOB-027`)
- [ ] `title` is descriptive
- [ ] `status` is `OPEN`
- [ ] `agents` includes `[developer]` or `[coder]`
- [ ] `related` lists PRJ-001 and any referenced CON/BLU docs
- [ ] `tags` has at least one entry

### 2.2 Content Completeness

- [ ] **BLUF** present — first content line has `> **BLUF:**`
- [ ] **Objective** section — clearly states what the job accomplishes
- [ ] **Tasks** section — at least one task defined with:
  - Task ID (e.g., T-402)
  - Clear description of what to do
  - Enough detail that a cold-start agent can execute without asking
- [ ] **Contracts Referenced** section — lists which CON-NNN docs the dev must read
- [ ] **Exit Criteria** section — defines what "done" looks like
- [ ] **Rules** section (if applicable) — constraints the dev must follow

### 2.3 Referenced Documents Exist

// turbo
```bash
echo "=== Referenced Doc Check ==="
# Extract related IDs from the job doc frontmatter
JOB_FILE="CODEX/05_PROJECT/JOB-NNN.md"  # Replace NNN with actual job number
RELATED=$(head -15 "$JOB_FILE" | grep '^related:' | sed 's/related: *\[//' | sed 's/\]//' | tr ',' '\n' | tr -d ' ')
ERRORS=0
for ref in $RELATED; do
  # Skip PRJ and BCK refs — they're always there
  case "$ref" in PRJ-*|BCK-*|JOB-*) continue ;; esac
  FOUND=$(find CODEX -name "${ref}*.md" | head -1)
  if [ -z "$FOUND" ]; then
    echo "  ❌ Referenced doc $ref not found on disk"
    ERRORS=$((ERRORS + 1))
  else
    echo "  ✅ $ref → $FOUND"
  fi
done
[ "$ERRORS" -eq 0 ] && echo "  ✅ All referenced docs exist" || echo "  ❌ $ERRORS missing reference(s) — fix before proceeding"
echo "=== End Referenced Doc Check ==="
```

### 2.4 MANIFEST Registered

// turbo
```bash
echo "=== MANIFEST Registration Check ==="
JOB_ID="JOB-NNN"  # Replace with actual job ID
if grep -q "$JOB_ID" CODEX/00_INDEX/MANIFEST.yaml; then
  echo "  ✅ $JOB_ID registered in MANIFEST"
else
  echo "  ❌ $JOB_ID NOT in MANIFEST — add it before proceeding"
fi
echo "=== End MANIFEST Check ==="
```

**If any check in Step 2 fails → STOP.** Fix the issue, then re-run validation.

---

## Step 3: Push to Main

The dev agent will pull from `main` as their very first action. All job docs
MUST be on `main` before we hand off.

// turbo
```bash
echo "=== Pre-Push Check ==="
echo "Branch: $(git branch --show-current)"
echo "Uncommitted:"
git status --short
echo "Unpushed:"
GIT_TERMINAL_PROMPT=0 git log --oneline origin/main..HEAD 2>/dev/null
echo "=== End Pre-Push Check ==="
```

**If on `main` and tree is clean with 0 unpushed commits** → docs are already pushed, skip to Step 4.

**If there are uncommitted changes** to job docs or CODEX:
1. Stage the job doc and any related CODEX changes
2. Commit following `/git_commit` workflow:
   ```
   docs(JOB-NNN): sprint doc ready for developer handoff
   ```
3. Push:
   ```bash
   GIT_TERMINAL_PROMPT=0 git push origin main
   ```

**If on a feature branch** → the job doc should be on `main`. Merge or cherry-pick
the doc commits to `main` first.

---

## Step 4: Determine Agent Assignment

For each job, determine:

1. **How many dev agents** are needed? (Usually 1 per job, but parallel tasks
   with no dependencies can be split across 2 agents)
2. **Working branch name** — follows pattern: `feature/JOB-NNN-short-description`
3. **Which tasks** each agent is responsible for (if splitting)
4. **Which CON/BLU docs** each agent needs to read

Present the assignment plan to the Human for approval:

```
### Agent Assignment Plan

**JOB-NNN: [Title]**
- Agent count: [1 or 2]
- Branch: `feature/JOB-NNN-short-description`
- Tasks: T-NNN through T-NNN
- Must read: CON-001, BLU-003 (list specific docs)

Approve?
```

Wait for Human approval before composing messages.

---

## Step 5: Compose Handoff Messages

For each dev agent, compose a **copy-paste-ready** chat message. The Human will
paste this directly into the dev agent's chat window.

**Use this template** (adapt to the specific job):

````
------- HANDOFF MESSAGE START -------

Pull from main and sync your workspace:
```
git checkout main && git pull origin main
```

You are a Developer Agent. Read your role definition:
```
CODEX/80_AGENTS/AGT-002_Developer_Agent.md
```

Read the mandatory command safety and commit workflows BEFORE running any commands:
```
.agent/workflows/safe_commands.md
.agent/workflows/git_commit.md
```

Your assignment is **JOB-NNN: [Title]**.

Read the sprint document:
```
CODEX/05_PROJECT/JOB-NNN_[Filename].md
```

Read these referenced contracts/blueprints:
```
CODEX/20_BLUEPRINTS/CON-NNN_[Name].md
[list each referenced doc]
```

Create your working branch and start:
```
git checkout -b feature/JOB-NNN-short-description
```

Key rules:
- One commit per task, using format: `feat(JOB-NNN): description (T-NNN)`
- Follow all rules in the sprint doc
- Push your branch when complete: `git push origin feature/JOB-NNN-short-description`
- Do NOT merge to main — the Architect will audit and merge

[Any job-specific instructions or warnings go here]

Start with task T-NNN and work through the task list in order.

------- HANDOFF MESSAGE END -------
````

**Rules for handoff messages:**
- The `git checkout main && git pull origin main` line MUST be the very first instruction
- List EVERY document the agent needs to read — don't say "read the related docs"
- Include the exact branch name to create
- Include any job-specific rules or constraints from the sprint doc
- Keep it concise — the dev agent will read the full sprint doc for details

**If splitting a job across multiple agents**, compose separate messages for each agent
with their specific task assignments.

---

## Step 6: Present to Human

Present the composed message(s) to the Human in a clearly delimited format:

```
## Ready to Start: JOB-NNN

**Branch:** `feature/JOB-NNN-short-description`
**Agents:** [count]
**Tasks:** T-NNN through T-NNN

### Dev Agent [A] Message
[paste the composed message]

### Dev Agent [B] Message (if applicable)
[paste the composed message]

Copy each message and paste it into the respective dev agent's chat window.
```

---

## Quick Reference

| What | Where |
|:-----|:------|
| Dev agent role definition | `CODEX/80_AGENTS/AGT-002_Developer_Agent.md` |
| Job docs | `CODEX/05_PROJECT/JOB-NNN.md` |
| Audit workflow (after job completes) | `.agent/workflows/audit_job.md` |
| Safe commands (referenced in handoff) | `.agent/workflows/safe_commands.md` |
| Git commit (referenced in handoff) | `.agent/workflows/git_commit.md` |
