---
description: Scan, lint, and synchronize all CODEX documentation — regenerate MANIFEST.yaml, validate frontmatter, enforce tag taxonomy, and flag compliance issues.
---

# /manage_documents

> Run this workflow whenever you create, edit, move, or archive documents in the CODEX.
> This workflow ensures MANIFEST.yaml stays in sync and all docs pass compliance checks.

## Phase 1: Scan & Inventory

// turbo
1. Find all markdown files in CODEX (excluding templates and READMEs):
```bash
find CODEX/ -name "*.md" ! -path "*/\_templates/*" ! -name "README.md" | sort
```

2. For each file found, extract the YAML frontmatter. Verify every file has a frontmatter block starting and ending with `---`. Record the following fields from each file:
   - `id`
   - `title`
   - `type`
   - `status`
   - `owner`
   - `agents`
   - `tags`
   - `related`
   - `created`
   - `updated`
   - `version`

3. Record the relative path of each file (relative to project root).

---

## Phase 2: Doc-Lint — Compliance Checks

Run these checks against every scanned doc. Report ALL violations at the end — do not stop at the first failure.

### 2.1 Frontmatter Validation

For each markdown file (excluding `_templates/` and `README.md` files):

- [ ] **Has frontmatter**: File starts with `---` and has a closing `---`
- [ ] **Required fields present**: All 11 required fields exist (`id`, `title`, `type`, `status`, `owner`, `agents`, `tags`, `related`, `created`, `updated`, `version`)
- [ ] **`type` is valid**: Must be one of: `reference`, `how-to`, `tutorial`, `explanation`
- [ ] **`status` is valid**: Must be one of: `DRAFT`, `REVIEW`, `APPROVED`, `DEPRECATED`
- [ ] **`agents` is a list**: Must be a YAML list (e.g., `[all]`, `[coder, tester]`)
- [ ] **`tags` is a list**: Must be a YAML list with ≥1 entry
- [ ] **`id` matches filename**: The `id` field must match the `CATEGORY-NNN` prefix of the filename

### 2.2 Tag Taxonomy Enforcement

// turbo
1. Load the controlled vocabulary from `CODEX/00_INDEX/TAG_TAXONOMY.yaml`

2. Extract all tags from all tag groups into a flat list of valid tags.

3. For each doc, check that EVERY tag in its `tags` field exists in the taxonomy. Flag any tags that are NOT in the taxonomy as violations.

### 2.3 Content Quality Checks

- [ ] **BLUF present**: First non-frontmatter content line contains `> **BLUF:**`
- [ ] **File size check** (tiered):
  - **≤10KB** → ✅ ideal
  - **10–30KB** → ⚠️ warning: "consider splitting if doc covers multiple topics"
  - **30KB+** → 🔴 violation: "must justify or split into focused pieces"
- [ ] **No placeholder text**: File does not contain `TODO`, `TBD`, `FIXME`, or `PLACEHOLDER` (case-insensitive)

### 2.4 Cross-Reference Validation

For each doc that has entries in its `related` field:
- [ ] Every referenced ID actually exists in another doc's `id` field
- [ ] Flag any broken cross-references

### 2.5 Root README.md Staleness (automated)

> [!IMPORTANT]
> The root `README.md` is the public face of the project. Run these checks to detect drift.
> If any check fails, flag it as a violation in the Phase 4 report.

// turbo
**Contract versions — README.md vs actual contract frontmatter:**
```bash
echo "=== Root README Contract Version Check ==="
ERRORS=0
for con in CODEX/20_BLUEPRINTS/CON-*.md; do
  CON_ID=$(head -20 "$con" | grep '^id:' | sed 's/id: *//' | tr -d '"')
  CON_VER=$(head -20 "$con" | grep '^version:' | sed 's/version: *//' | tr -d '"')
  if [ -n "$CON_ID" ] && [ -n "$CON_VER" ]; then
    README_VER=$(grep "$CON_ID" README.md | grep -o 'v[0-9.]*' | head -1)
    if [ -n "$README_VER" ] && [ "$README_VER" != "v$CON_VER" ]; then
      echo "  ❌ $CON_ID: README.md says $README_VER, actual is v$CON_VER"
      ERRORS=$((ERRORS + 1))
    elif [ -z "$README_VER" ]; then
      echo "  ❌ $CON_ID: not mentioned in README.md at all"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done
[ "$ERRORS" -eq 0 ] && echo "  ✅ All contract versions match" || echo "  ⚠️  $ERRORS mismatch(es) — update README.md Contracts table"
```

// turbo
**Roadmap phase accuracy — README.md vs PRJ-001:**
```bash
echo "=== Root README Roadmap Check ==="
PRJ_COMPLETE=$(grep -c '✅ COMPLETE' CODEX/05_PROJECT/PRJ-001_Roadmap.md || true)
README_COMPLETE=$(grep -c '✅ Complete' README.md || true)
PRJ_PROGRESS=$(grep -c '🔄 IN PROGRESS\|IN PROGRESS' CODEX/05_PROJECT/PRJ-001_Roadmap.md || true)
README_PROGRESS=$(grep -c '🔄 In Progress\|In Progress' README.md || true)
if [ "$PRJ_COMPLETE" != "$README_COMPLETE" ] || [ "$PRJ_PROGRESS" != "$README_PROGRESS" ]; then
  echo "  ❌ Roadmap mismatch: PRJ-001 has $PRJ_COMPLETE complete/$PRJ_PROGRESS active, README has $README_COMPLETE complete/$README_PROGRESS active"
else
  echo "  ✅ Roadmap phase counts match"
fi
```

**Manual README checks (flag if stale):**
- [ ] Architecture diagram reflects current system components
- [ ] API Overview table covers all controller endpoints
- [ ] Project Structure section lists all top-level directories

---

## Phase 3: Regenerate MANIFEST.yaml

Using the data collected in Phase 1, regenerate `CODEX/00_INDEX/MANIFEST.yaml`.

### 3.1 Preserve Static Sections

Keep the following sections exactly as they are (do not regenerate):
- The file header comment block
- `schema_version`
- `project`
- The `areas` section (area definitions are static)

### 3.2 Regenerate Documents Section

Rebuild the `documents:` list from the scanned frontmatter. For each doc, write:

```yaml
  - id: [from frontmatter]
    path: [relative path from project root, e.g., CODEX/10_GOVERNANCE/GOV-001_DocumentationStandard.md]
    title: [from frontmatter]
    type: [from frontmatter]
    status: [from frontmatter]
    tags: [from frontmatter]
    agents: [from frontmatter]
    summary: >
      [Read the BLUF from the doc and use it as the summary. If no BLUF, use the title.]
```

Sort documents by:
1. Area number (10, 20, 30, ..., 90)
2. Then by ID alphabetically within each area

### 3.3 Update Generated Timestamp

Set `generated:` to today's date in `YYYY-MM-DD` format.

---

## Phase 4: Report

Print a summary report with:

```
=== CODEX Document Management Report ===

Scanned: [N] documents
Passing: [N] documents
Violations: [N] total across [N] documents

MANIFEST.yaml: [UPDATED | NO CHANGES NEEDED]
Root README.md: [CURRENT | STALE — list issues]

--- Violations ---
[List each violation with file path, field, and issue]

--- Root README.md Staleness ---
[List any contract version, roadmap, or structural mismatches]

--- New Documents Added to MANIFEST ---
[List any docs that were in the filesystem but not in MANIFEST]

--- Orphaned MANIFEST Entries ---
[List any MANIFEST entries whose files no longer exist]
```

If there are violations, list them all but do NOT auto-fix them. Report them so the architect can decide how to handle each one.

If MANIFEST.yaml was updated, show the diff of what changed.

---

## Quick Reference

| What | Command |
|:-----|:--------|
| Run full workflow | `/manage_documents` |
| Just check compliance | Run Phase 1 + Phase 2 only |
| Just sync manifest | Run Phase 1 + Phase 3 only |
