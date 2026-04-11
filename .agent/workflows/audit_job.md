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

## Step 10: Housekeeping (PASS only)

If the audit **passed**, update all project-level documents before committing:

### README.md
- [ ] Test count matches actual (`dotnet test` / `npm test` output)
- [ ] Architecture diagram reflects current capabilities
- [ ] API table includes any new/changed endpoints
- [ ] Configuration table includes any new settings
- [ ] Contract versions match current (`CON-NNN` frontmatter)
- [ ] Roadmap table reflects current phase status

### BCK-001 Backlog
- [ ] All tasks delivered in this job are marked `✅ Done`
- [ ] Phase section header marked `✅ COMPLETE` if all items in phase are done
- [ ] Future backlog updated if any items were pulled forward or new items emerged

### PRJ-001 Roadmap
- [ ] Current phase exit criteria checked off
- [ ] Phase marked `✅ COMPLETE` with completion date if all criteria met

### MANIFEST.yaml — Full Sync Check (MANDATORY)

> [!CAUTION]
> Do NOT skip this. Run all three checks. Drift accumulates silently.

// turbo
**1. Orphan detection** — docs on disk but NOT registered in MANIFEST:
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
**2. Phantom detection** — entries in MANIFEST pointing to files that don't exist:
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
**3. ID collision detection** — duplicate document IDs in MANIFEST:
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

// turbo
**4. Frontmatter ID vs filename consistency** — the `id:` in each file's frontmatter should match the filename prefix:
```bash
echo "=== Frontmatter ID vs Filename Check ==="
MISMATCHES=0
for f in $(find CODEX -name '*.md' -not -path '*_templates*' -not -path '*/README.md' -not -path '*90_ARCHIVE*' -not -name 'SESSION_HANDOFF*' | sort); do
  FILE_PREFIX=$(basename "$f" .md | sed 's/_.*//')
  FRONTMATTER_ID=$(head -20 "$f" | grep '^id:' | sed 's/id: *//' | tr -d '"' | head -1)
  if [ -n "$FRONTMATTER_ID" ] && [ "$FILE_PREFIX" != "$FRONTMATTER_ID" ]; then
    echo "  ❌ MISMATCH: $f (file=$FILE_PREFIX, frontmatter=$FRONTMATTER_ID)"
    MISMATCHES=$((MISMATCHES + 1))
  fi
done
[ "$MISMATCHES" -eq 0 ] && echo "  ✅ All IDs match" || echo "  ⚠️  $MISMATCHES mismatch(es) — fix frontmatter or rename files"
```

**Manual checks:**
- [ ] Job status updated (ACTIVE → CLOSED) in MANIFEST entry
- [ ] VER-NNN audit report entry added to MANIFEST
- [ ] Any new CON-NNN, BLU-NNN, DEF-NNN, or EVO-NNN documents registered
- [ ] MANIFEST summaries for PRJ-001 and BCK-001 still accurate (not stale)
- [ ] GOV-003 summary updated if new sections were added (e.g., §8.10)

### SESSION_HANDOFF.md
- [ ] Updated with current state, completed phases, and next phase context

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

