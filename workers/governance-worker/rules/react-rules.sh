#!/bin/bash
# Governance Rule Scripts — React/TypeScript Stack Checks
# Called by entrypoint.sh when a package.json is detected.
# Each function runs a check and calls add_check() with the result.
#
# REF: JOB-008 T-074, CON-001 §6
# Checks: GOV-001-001, GOV-003-001, GOV-003-002, GOV-005-001, GOV-005-002, GOV-008-001

# Expects: REPO_DIR, add_check() function available via source

# ---- GOV-001-001: README.md exists ----
run_react_gov_001_001() {
    if [ -f "$REPO_DIR/README.md" ]; then
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "true" "null" "error"
    else
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "false" "No README.md found in project root" "error"
    fi
}

# ---- GOV-003-001: No `: any` in .ts/.tsx files ----
run_gov_003_001() {
    local hits=""
    if [ -d "$REPO_DIR/src" ]; then
        hits=$(grep -rn ":\s*any\b\|<any>" "$REPO_DIR/src" \
            --include="*.ts" \
            --include="*.tsx" \
            | grep -v "node_modules" \
            | grep -v ".d.ts" \
            | head -10 2>/dev/null || true)
    fi

    if [ -z "$hits" ]; then
        add_check "GOV-003-001" "No TypeScript any Types" "GOV-003" "true" "null" "error"
    else
        local count
        count=$(echo "$hits" | wc -l)
        local details
        details=$(echo "$hits" | head -3 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-003-001" "No TypeScript any Types" "GOV-003" "false" "$count occurrence(s): $details" "error"
    fi
}

# ---- GOV-003-002: No console.log in .ts/.tsx source files ----
run_gov_003_002() {
    local hits=""
    if [ -d "$REPO_DIR/src" ]; then
        hits=$(grep -rn "console\.log\|console\.warn\|console\.error" "$REPO_DIR/src" \
            --include="*.ts" \
            --include="*.tsx" \
            | grep -v "node_modules" \
            | grep -v "test" \
            | grep -v "Test" \
            | grep -v "spec" \
            | head -10 2>/dev/null || true)
    fi

    if [ -z "$hits" ]; then
        add_check "GOV-003-002" "No console.log in Source Files" "GOV-003" "true" "null" "warning"
    else
        local count
        count=$(echo "$hits" | wc -l)
        local details
        details=$(echo "$hits" | head -3 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-003-002" "No console.log in Source Files" "GOV-003" "false" "$count occurrence(s): $details" "warning"
    fi
}

# ---- GOV-005-001: Branch name matches convention ----
run_react_gov_005_001() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    local branch
    branch=$(cd "$REPO_DIR" && git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")

    if echo "$branch" | grep -qE "^(feature|fix|docs|refactor|test)/"; then
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "error"
    elif [ "$branch" = "main" ] || [ "$branch" = "master" ] || [ "$branch" = "HEAD" ]; then
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "error"
    else
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "false" "Branch '$branch' does not match convention" "error"
    fi
}

# ---- GOV-005-002: Commit message matches conventional format ----
run_react_gov_005_002() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    local message
    message=$(cd "$REPO_DIR" && git log -1 --pretty=format:"%s" 2>/dev/null || echo "")

    if [ -z "$message" ]; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    if echo "$message" | grep -qE "^(feat|fix|docs|refactor|test|chore|ci|build|perf|style)(\(.+\))?:"; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "error"
    else
        local truncated
        truncated=$(echo "$message" | head -c 80)
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "false" "Commit message does not match conventional format: $truncated" "error"
    fi
}

# ---- GOV-008-001: Dockerfile or docker-compose present ----
run_react_gov_008_001() {
    local found=""
    found=$(find "$REPO_DIR" -maxdepth 3 \( -name "Dockerfile" -o -name "docker-compose.yml" -o -name "docker-compose.yaml" \) 2>/dev/null | head -1 || true)

    if [ -n "$found" ]; then
        add_check "GOV-008-001" "Dockerfile or Docker Compose Present" "GOV-008" "true" "null" "warning"
    else
        add_check "GOV-008-001" "Dockerfile or Docker Compose Present" "GOV-008" "false" "No Dockerfile or docker-compose found" "warning"
    fi
}

# ==================================================================
# Runner — execute all React/TypeScript rules
# ==================================================================
run_react_rules() {
    echo "=== Running React/TypeScript governance rules ==="
    run_react_gov_001_001
    run_gov_003_001
    run_gov_003_002
    run_react_gov_005_001
    run_react_gov_005_002
    run_react_gov_008_001
    echo "=== React/TypeScript governance rules complete ==="
}
