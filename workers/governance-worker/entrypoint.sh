#!/bin/bash
# Governance Worker — Reads task.json, detects stack, runs governance checks,
# writes governance-report.json with check results.
#
# SKELETON VERSION: All checks pass as placeholders.
# JOB-008 will populate /rules/ with real check scripts.
#
# REF: CON-001 §6, JOB-007 T-070
#
# Exit codes: always 0 (pass/fail encoded in governance-report.json)

set -euo pipefail

TASK_FILE="/workspace/input/task.json"
REPORT_FILE="/workspace/output/governance-report.json"
REPO_DIR="/workspace/repo"

# ---- Read task.json ----
if [ ! -f "$TASK_FILE" ]; then
    echo "ERROR: task.json not found at $TASK_FILE"
    exit 1
fi

TASK_ID=$(jq -r '.taskId' "$TASK_FILE")
echo "Governance worker starting for task: $TASK_ID"

# ---- Detect stack ----
STACK="unknown"
if [ -d "$REPO_DIR" ]; then
    if find "$REPO_DIR" -maxdepth 3 -name "*.csproj" -o -name "*.slnx" -o -name "*.sln" 2>/dev/null | head -1 | grep -q .; then
        STACK="dotnet"
        echo "Detected stack: dotnet"
    elif [ -f "$REPO_DIR/package.json" ]; then
        STACK="node"
        echo "Detected stack: node"
    else
        echo "Detected stack: unknown (no .csproj or package.json found)"
    fi
else
    echo "WARNING: No repo directory at $REPO_DIR"
fi

# ---- Initialize check results ----
CHECKS="[]"
TOTAL=0
PASSED=0
FAILED=0

# ---- Helper: add check result ----
add_check() {
    local rule_id="$1"
    local rule_name="$2"
    local category="$3"
    local passed="$4"
    local details="${5:-null}"
    local severity="${6:-error}"

    if [ "$details" != "null" ]; then
        details="\"$details\""
    fi

    CHECKS=$(echo "$CHECKS" | jq \
        --arg rid "$rule_id" \
        --arg rn "$rule_name" \
        --arg cat "$category" \
        --argjson p "$passed" \
        --argjson det "$details" \
        --arg sev "$severity" \
        '. + [{"ruleId": $rid, "ruleName": $rn, "category": $cat, "passed": $p, "details": $det, "severity": $sev}]')

    TOTAL=$((TOTAL + 1))
    if [ "$passed" = "true" ]; then
        PASSED=$((PASSED + 1))
    else
        FAILED=$((FAILED + 1))
    fi
}

# ==================================================================
# PLACEHOLDER CHECKS — JOB-008 will replace these with real rules
# ==================================================================

# Check 1: Workspace exists
if [ -d "$REPO_DIR" ]; then
    add_check "GOV-002-001" "Workspace Exists" "GOV-002" "true"
else
    add_check "GOV-002-001" "Workspace Exists" "GOV-002" "false" "No repo directory found at $REPO_DIR"
fi

# Check 2: No secret patterns in diff (placeholder)
add_check "SEC-001-001" "No Secrets in Diff" "SEC-001" "true" "null" "error"

# Check 3: Result.json produced by dev worker
if [ -f "/workspace/output/result.json" ]; then
    add_check "CON-001-001" "Dev Worker Produced Result" "CON-001" "true"
else
    add_check "CON-001-001" "Dev Worker Produced Result" "CON-001" "true" "null" "warning"
fi

# Check 4: Stack-specific build check (placeholder — always passes for now)
if [ "$STACK" = "dotnet" ]; then
    add_check "GOV-002-002" "Build Succeeds (dotnet)" "GOV-002" "true"
elif [ "$STACK" = "node" ]; then
    add_check "GOV-002-002" "Build Succeeds (node)" "GOV-002" "true"
else
    add_check "GOV-002-002" "Build Check Skipped (unknown stack)" "GOV-002" "true" "null" "warning"
fi

# ==================================================================
# Run any rule scripts from /rules/ directory
# ==================================================================
if [ -d "/rules" ]; then
    for rule_script in /rules/*.sh; do
        [ -f "$rule_script" ] || continue
        echo "Running rule: $rule_script"
        # Rule scripts output JSON: {"ruleId":"...", "ruleName":"...", "category":"...", "passed": true/false, "details": "...", "severity": "error/warning"}
        set +e
        RULE_OUTPUT=$(bash "$rule_script" "$REPO_DIR" "$TASK_FILE" 2>&1)
        RULE_EXIT=$?
        set -e

        if [ $RULE_EXIT -eq 0 ] && echo "$RULE_OUTPUT" | jq . >/dev/null 2>&1; then
            CHECKS=$(echo "$CHECKS" | jq --argjson rule "$RULE_OUTPUT" '. + [$rule]')
            TOTAL=$((TOTAL + 1))
            RULE_PASSED=$(echo "$RULE_OUTPUT" | jq -r '.passed')
            if [ "$RULE_PASSED" = "true" ]; then
                PASSED=$((PASSED + 1))
            else
                FAILED=$((FAILED + 1))
            fi
        else
            echo "WARNING: Rule script $rule_script failed or produced invalid output"
        fi
    done
fi

# ---- Determine verdict ----
# Pass if zero error-severity checks failed
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

echo "Governance report written to $REPORT_FILE"
echo "Verdict: $STATUS ($SUMMARY)"
