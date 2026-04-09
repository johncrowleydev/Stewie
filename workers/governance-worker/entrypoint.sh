#!/bin/bash
# Governance Worker — Full Implementation
# Reads task.json, detects stack, runs governance checks via rule scripts,
# writes governance-report.json per CON-001 §6.
#
# REF: CON-001 §6, JOB-007 T-070, JOB-008 T-075
#
# Exit codes: always 0 (pass/fail encoded in governance-report.json)

set -euo pipefail

TASK_FILE="/workspace/input/task.json"
REPORT_FILE="/workspace/output/governance-report.json"
REPO_DIR="/workspace/repo"
RULES_DIR="/rules"

# ---- Read task.json ----
if [ ! -f "$TASK_FILE" ]; then
    echo "ERROR: task.json not found at $TASK_FILE"
    exit 1
fi

TASK_ID=$(jq -r '.taskId' "$TASK_FILE")
TASK_ROLE=$(jq -r '.role // "tester"' "$TASK_FILE")
echo "============================================"
echo "Governance Worker v1.0.0"
echo "Task ID: $TASK_ID"
echo "Role: $TASK_ROLE"
echo "============================================"

# ---- Initialize check results ----
CHECKS="[]"
TOTAL=0
PASSED=0
FAILED=0

# ---- Helper: add check result ----
# Usage: add_check "ruleId" "ruleName" "category" "true/false" "details|null" "error|warning"
add_check() {
    local rule_id="$1"
    local rule_name="$2"
    local category="$3"
    local passed="$4"
    local details="${5:-null}"
    local severity="${6:-error}"

    # Escape details for JSON safety
    if [ "$details" = "null" ]; then
        local det_json="null"
    else
        local det_json
        det_json=$(printf '%s' "$details" | jq -Rs . | head -c 1000)
    fi

    CHECKS=$(echo "$CHECKS" | jq \
        --arg rid "$rule_id" \
        --arg rn "$rule_name" \
        --arg cat "$category" \
        --argjson p "$passed" \
        --argjson det "$det_json" \
        --arg sev "$severity" \
        '. + [{"ruleId": $rid, "ruleName": $rn, "category": $cat, "passed": $p, "details": $det, "severity": $sev}]')

    TOTAL=$((TOTAL + 1))
    if [ "$passed" = "true" ]; then
        PASSED=$((PASSED + 1))
    else
        FAILED=$((FAILED + 1))
    fi

    local icon="✅"
    if [ "$passed" = "false" ]; then
        icon="❌"
    fi
    echo "  $icon [$severity] $rule_id: $rule_name"
}

# ---- Detect stack ----
HAS_DOTNET=false
HAS_NODE=false

if [ -d "$REPO_DIR" ]; then
    if find "$REPO_DIR" -maxdepth 3 \( -name "*.csproj" -o -name "*.slnx" -o -name "*.sln" \) 2>/dev/null | head -1 | grep -q .; then
        HAS_DOTNET=true
        echo "Detected stack: .NET"
    fi

    if [ -f "$REPO_DIR/package.json" ] || find "$REPO_DIR" -maxdepth 3 -name "package.json" 2>/dev/null | head -1 | grep -q .; then
        HAS_NODE=true
        echo "Detected stack: Node/React"
    fi

    if [ "$HAS_DOTNET" = false ] && [ "$HAS_NODE" = false ]; then
        echo "WARNING: No recognized stack detected"
    fi
else
    echo "WARNING: No repo directory at $REPO_DIR"
fi

# ==================================================================
# MANDATORY CHECKS — Always run regardless of stack
# ==================================================================

echo ""
echo "--- Mandatory Checks ---"

# ---- SEC-001-001: No secrets in git diff ----
run_sec_001_001() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        add_check "SEC-001-001" "No Secrets in Diff" "SEC-001" "true" "No git repository found - skipping diff scan" "error"
        return
    fi

    local secret_hits=""

    # Scan all tracked files for common secret patterns
    set +e
    secret_hits=$(cd "$REPO_DIR" && git diff HEAD~1 --unified=0 2>/dev/null \
        | grep -E '^\+' \
        | grep -v '^\+\+\+' \
        | grep -iE \
            '(sk-[a-zA-Z0-9]{20,}|ghp_[a-zA-Z0-9]{36}|gho_[a-zA-Z0-9]{36}|glpat-[a-zA-Z0-9]{20}|xox[bpas]-[a-zA-Z0-9-]{20,}|-----BEGIN (RSA |EC |DSA )?PRIVATE KEY-----|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{35})' \
        2>/dev/null || true)
    set -e

    # Also scan for hardcoded password/secret assignments
    local password_hits=""
    set +e
    password_hits=$(cd "$REPO_DIR" && git diff HEAD~1 --unified=0 2>/dev/null \
        | grep -E '^\+' \
        | grep -v '^\+\+\+' \
        | grep -v 'test' \
        | grep -v 'Test' \
        | grep -v 'example' \
        | grep -v 'placeholder' \
        | grep -iE '(password|secret|api_key|apiKey|token)\s*[=:]\s*"[^"]{8,}"' \
        2>/dev/null || true)
    set -e

    local all_hits=""
    if [ -n "$secret_hits" ]; then
        all_hits="$secret_hits"
    fi
    if [ -n "$password_hits" ]; then
        if [ -n "$all_hits" ]; then
            all_hits="$all_hits
$password_hits"
        else
            all_hits="$password_hits"
        fi
    fi

    if [ -z "$all_hits" ]; then
        add_check "SEC-001-001" "No Secrets in Diff" "SEC-001" "true" "null" "error"
    else
        local count
        count=$(echo "$all_hits" | wc -l)
        local details
        details=$(echo "$all_hits" | head -3 | sed 's/"/\\"/g' | tr '\n' '; ' | head -c 500)
        add_check "SEC-001-001" "No Secrets in Diff" "SEC-001" "false" "$count potential secret(s) found in diff: $details" "error"
    fi
}

run_sec_001_001

# ==================================================================
# STACK-SPECIFIC RULES
# ==================================================================

# Source and run .NET rules
if [ "$HAS_DOTNET" = true ] && [ -f "$RULES_DIR/dotnet-rules.sh" ]; then
    echo ""
    echo "--- .NET Stack Rules ---"
    # shellcheck source=rules/dotnet-rules.sh
    source "$RULES_DIR/dotnet-rules.sh"
    run_dotnet_rules
fi

# Source and run React/TypeScript rules
if [ "$HAS_NODE" = true ] && [ -f "$RULES_DIR/react-rules.sh" ]; then
    echo ""
    echo "--- React/TypeScript Stack Rules ---"
    # shellcheck source=rules/react-rules.sh
    source "$RULES_DIR/react-rules.sh"
    run_react_rules
fi

# ==================================================================
# If no stack detected, still run basic file checks
# ==================================================================
if [ "$HAS_DOTNET" = false ] && [ "$HAS_NODE" = false ]; then
    echo ""
    echo "--- Basic Checks (no stack detected) ---"

    # README exists
    if [ -f "$REPO_DIR/README.md" ]; then
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "true" "null" "error"
    else
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "false" "No README.md found in project root" "error"
    fi
fi

# ---- Determine verdict ----
ERROR_FAILURES=$(echo "$CHECKS" | jq '[.[] | select(.passed == false and .severity == "error")] | length')
if [ "$ERROR_FAILURES" -eq 0 ]; then
    STATUS="pass"
else
    STATUS="fail"
fi

SUMMARY="${PASSED}/${TOTAL} checks passed"
if [ "$FAILED" -gt 0 ]; then
    SUMMARY="${SUMMARY}, ${FAILED} failed"
fi

# ---- Write governance-report.json ----
mkdir -p /workspace/output

jq -n \
    --arg tid "$TASK_ID" \
    --arg status "$STATUS" \
    --arg summary "$SUMMARY" \
    --argjson total "$TOTAL" \
    --argjson passed "$PASSED" \
    --argjson failed "$FAILED" \
    --argjson checks "$CHECKS" \
    '{
        taskId: $tid,
        status: $status,
        summary: $summary,
        totalChecks: $total,
        passedChecks: $passed,
        failedChecks: $failed,
        checks: $checks
    }' > "$REPORT_FILE"

echo ""
echo "============================================"
echo "Governance Report: $STATUS"
echo "Total: $TOTAL | Passed: $PASSED | Failed: $FAILED"
echo "Error failures blocking acceptance: $ERROR_FAILURES"
echo "Report: $REPORT_FILE"
echo "============================================"
