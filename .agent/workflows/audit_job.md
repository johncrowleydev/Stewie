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

## Quick Reference

| What | Command |
|:-----|:--------|
| Full audit | `/audit_job` |
| Build checks only | Run Step 3 |
| Governance checks only | Run Step 4 |
