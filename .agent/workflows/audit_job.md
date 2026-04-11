---
description: Run the Architect's job audit checklist — verify build, governance compliance, contract compliance, and generate audit report.
---

# /audit_job

> Run this workflow after a developer agent completes a job.
> Generates a VER-001 audit report for the completed job.

## Step 1: Identify the Job

1. Ask: Which job is being audited? (e.g., `JOB-001`)
2. Read the job document from `CODEX/05_PROJECT/JOB-NNN.md`
3. Identify all tasks and their acceptance criteria
4. Identify which repos need to be checked

---

## Step 2: Pull Latest Code

// turbo
1. For each repo being audited:
```bash
git pull origin main
git submodule update --recursive
```

---

## Step 3: Build Verification

// turbo
1. Run these checks against each repo:
```bash
echo "=== Build Verification ==="
npm install && echo "✅ install" || echo "❌ install FAILED"
npm run build && echo "✅ build" || echo "❌ build FAILED"
npm run lint && echo "✅ lint" || echo "❌ lint FAILED"
npm run typecheck 2>/dev/null && echo "✅ typecheck" || echo "⏭️ typecheck (not configured)"
npm run test && echo "✅ tests" || echo "❌ tests FAILED"
echo "=== End Build Verification ==="
```

2. Record pass/fail for each check.

---

## Step 4: Governance Compliance

For each GOV doc (001-008), verify the job's specific requirement was met:

### GOV-001 (Documentation)
- [ ] README.md present and updated
- [ ] TSDoc/JSDoc on all exported functions

### GOV-002 (Testing)
- [ ] Test framework configured
- [ ] Tests exist and pass
- [ ] Coverage meets thresholds

### GOV-003 (Coding Standard)
- [ ] TypeScript strict mode enabled
- [ ] No `any` types (grep for `: any`)
```bash
grep -rn ": any" src/ --include="*.ts" --include="*.tsx" | grep -v node_modules | head -20
```
- [ ] ESLint configured and passing

### GOV-004 (Error Handling)
- [ ] Error middleware/boundary present
- [ ] Structured error responses

### GOV-005 (Dev Lifecycle)
- [ ] Branch names follow `feature/JOB-NNN-TXXX-description` pattern
- [ ] Commit messages follow `feat(JOB-NNN): description` format

### GOV-006 (Logging)
- [ ] Structured JSON logging configured (pino or similar)
- [ ] Correlation IDs on requests

### GOV-008 (Infrastructure)
- [ ] `.env.example` present with all required vars
- [ ] Correct port configured
- [ ] CODEX submodule linked

---

## Step 5: Contract Compliance (if applicable)

For each CON-NNN referenced in the job:

1. Read the contract document
2. Verify routes/endpoints match contract schemas
3. Verify error codes match contract
4. Verify auth mechanism matches contract

---

## Step 6: Health Check

// turbo
1. Start the service(s) locally and verify health:
```bash
echo "=== Health Check ==="
curl -sf http://localhost:3000/api/health && echo "✅ Web health OK" || echo "❌ Web health FAIL"
curl -sf http://localhost:4000/health && echo "✅ Trust health OK" || echo "❌ Trust health FAIL"
echo "=== End Health Check ==="
```

---

## Step 7: Generate Audit Report

Create `CODEX/40_VERIFICATION/VER-001_JOB-NNN_Audit.md` using the job audit template (`_templates/template_job_audit.md`):

1. Fill in all checklist items from steps 3-6
2. Set verdict: PASS or FAIL
3. If FAIL: list DEF-NNN IDs to file
4. If PASS: set deploy approved = YES

---

## Step 8: File Defects (if any)

For each failure found:

1. Create `CODEX/50_DEFECTS/DEF-NNN.md` using the defect template
2. Reference the failing task and acceptance criterion
3. Include the specific error output
4. Assign back to the developer agent

---

## Step 9: Update Job Status

1. If PASS: Update job doc `status:` to `CLOSED`
2. If FAIL: Update job doc `status:` to `BLOCKED`
3. Mark audited tasks in the job checklist

---

## Step 10: Housekeeping (PASS only) — BLOCKING

> [!CAUTION]
> This step is **BLOCKING**. Every sub-section must pass before you commit.
> Each sub-section has automated checks that MUST be run. The manual items
> are listed alongside them. Do NOT skip to the commit without running every
> script and verifying every checkbox.
>
> **If any check fails, FIX IT before committing. No exceptions.**

---

### 10.1 MANIFEST.yaml — Full Sync (automated)

// turbo
**Orphan detection** — docs on disk but NOT registered in MANIFEST:
```bash
echo "=== Orphan Check (on-disk but not in MANIFEST) ==="
ORPHANS=0
for f in $(find CODEX -name '*.md' -not -path '*_templates*' -not -path '*/README.md' -not -path '*90_ARCHIVE*' | sort); do
  basename_no_ext=$(basename "$f" .md)
  if ! grep -q "$basename_no_ext" CODEX/00_INDEX/MANIFEST.yaml 2>/dev/null; then
    echo "  ❌ ORPHAN: $f"
    ORPHANS=$((ORPHANS + 1))
  fi
done
[ "$ORPHANS" -eq 0 ] && echo "  ✅ No orphans" || echo "  ⚠️  $ORPHANS orphan(s) found — add to MANIFEST"
```

// turbo
**Phantom detection** — entries in MANIFEST pointing to files that don't exist:
```bash
echo "=== Phantom Check (in MANIFEST but not on disk) ==="
PHANTOMS=0
grep '^\s*path:' CODEX/00_INDEX/MANIFEST.yaml | sed 's/.*path: //' | while read p; do
  if [ ! -f "CODEX/$p" ]; then
    echo "  ❌ PHANTOM: CODEX/$p"
    PHANTOMS=$((PHANTOMS + 1))
  fi
done
[ "$PHANTOMS" -eq 0 ] && echo "  ✅ No phantoms" || echo "  ⚠️  Phantom(s) found — remove from MANIFEST or restore file"
```

// turbo
**ID collision detection** — duplicate document IDs in MANIFEST:
```bash
echo "=== ID Collision Check ==="
DUPES=$(grep '^\s*- id:' CODEX/00_INDEX/MANIFEST.yaml | sed 's/.*id: //' | sort | uniq -d)
if [ -n "$DUPES" ]; then
  echo "  ❌ DUPLICATE IDs:"
  echo "$DUPES" | sed 's/^/    /'
else
  echo "  ✅ No duplicate IDs"
fi
```

**Manual MANIFEST checks:**
- [ ] Audited job status updated (ACTIVE → CLOSED) in MANIFEST entry
- [ ] VER-NNN audit report entry added to MANIFEST
- [ ] Any new CON-NNN, BLU-NNN, DEF-NNN, or EVO-NNN documents registered
- [ ] MANIFEST summaries still accurate

---

### 10.2 Contract Versions — Cross-Check (automated)

// turbo
**Verify PRJ-001 contract table matches actual contract frontmatter:**
```bash
echo "=== Contract Version Cross-Check ==="
ERRORS=0
for con in CODEX/20_BLUEPRINTS/CON-*.md; do
  CON_ID=$(head -20 "$con" | grep '^id:' | sed 's/id: *//' | tr -d '"')
  CON_VER=$(head -20 "$con" | grep '^version:' | sed 's/version: *//' | tr -d '"')
  if [ -n "$CON_ID" ] && [ -n "$CON_VER" ]; then
    # Only check the Key Contracts table (lines starting with "| `CON-")
    PRJ_VER=$(grep "^| \`$CON_ID\`" CODEX/05_PROJECT/PRJ-001_Roadmap.md | grep -o 'v[0-9.]*' | head -1)
    README_VER=$(grep "$CON_ID" CODEX/00_INDEX/README.md | grep -o 'v[0-9.]*' | head -1)
    if [ -n "$PRJ_VER" ] && [ "$PRJ_VER" != "v$CON_VER" ]; then
      echo "  ❌ $CON_ID: Roadmap says $PRJ_VER, actual is v$CON_VER"
      ERRORS=$((ERRORS + 1))
    fi
    if [ -n "$README_VER" ] && [ "$README_VER" != "v$CON_VER" ]; then
      echo "  ❌ $CON_ID: README says $README_VER, actual is v$CON_VER"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done
[ "$ERRORS" -eq 0 ] && echo "  ✅ All contract versions consistent" || echo "  ⚠️  $ERRORS version mismatch(es) — fix PRJ-001 and/or README"
```

---

### 10.3 Test Count Accuracy (automated)

// turbo
**Verify test counts in SESSION_HANDOFF match actual:**
```bash
echo "=== Test Count Accuracy ==="
ACTUAL_TESTS=$(dotnet test src/Stewie.Tests/Stewie.Tests.csproj --verbosity quiet 2>&1 | grep -oP 'Passed:\s+\K\d+')
HANDOFF_TESTS=$(grep -oP 'Passed.*?(\d+)' CODEX/05_PROJECT/SESSION_HANDOFF.md 2>/dev/null | grep -oP '\d+' | tail -1)
echo "  Actual: $ACTUAL_TESTS passed"
echo "  SESSION_HANDOFF reports: $HANDOFF_TESTS"
if [ "$ACTUAL_TESTS" = "$HANDOFF_TESTS" ]; then
  echo "  ✅ Match"
else
  echo "  ⚠️  Mismatch — update SESSION_HANDOFF"
fi
```

---

### 10.4 Roadmap Phase State (manual but MANDATORY)

> [!IMPORTANT]
> Read PRJ-001_Roadmap.md and verify EACH of these. If any is wrong, fix it NOW.

- [ ] Current phase exit criteria checked off (all `[x]` or `[ ]` accurate)
- [ ] Phase marked `✅ COMPLETE` with completion date if all criteria met
- [ ] Next phase labeled correctly (Future/Active)
- [ ] Changelog updated with a new version entry describing what changed

---

### 10.5 README (manual but MANDATORY)

> [!IMPORTANT]
> Open CODEX/00_INDEX/README.md and verify EACH of these. If any is stale, fix it NOW.

- [ ] All document sections populated (no empty "add docs when project grows" placeholders)
- [ ] All CON-NNN listed with correct versions
- [ ] All GOV-NNN listed
- [ ] All RUN-NNN listed
- [ ] DEFECTS, EVOLUTION, RESEARCH sections current
- [ ] Job count accurate
- [ ] No emoji in titles or content

---

### 10.6 BCK-001 Backlog

- [ ] All tasks delivered in this job marked `✅ Done`
- [ ] Phase section header marked `✅ COMPLETE` if all items in phase are done
- [ ] Future backlog updated if any items were pulled forward or new items emerged

---

### 10.7 SESSION_HANDOFF.md

- [ ] Updated with current commit hash
- [ ] Current phase status accurate
- [ ] Test baseline updated
- [ ] Next steps reflect actual state

---

### 10.8 BLOCKING GATE — Final Confirmation

> [!CAUTION]
> Before committing, explicitly confirm each section passed.
> Copy this checklist into your response and mark each one.

```
HOUSEKEEPING GATE:
- [ ] 10.1 MANIFEST sync — PASS
- [ ] 10.2 Contract versions — PASS
- [ ] 10.3 Test count — PASS
- [ ] 10.4 Roadmap phase state — PASS
- [ ] 10.5 README accuracy — PASS
- [ ] 10.6 Backlog updated — PASS
- [ ] 10.7 SESSION_HANDOFF updated — PASS
```

**Only proceed to commit after ALL items above are checked.**

### Commit

Commit all housekeeping changes in a single commit:
```
docs: housekeeping — update manifest and JOB-NNN closure
```

---

## Quick Reference

| What | Command |
|:-----|:--------|
| Full audit | `/audit_job` |
| Build checks only | Run Step 3 |
| Governance checks only | Run Step 4 |
| Housekeeping only | Run Step 10 |
| CODEX full lint | `/manage_documents` |


