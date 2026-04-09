#!/bin/bash
# Script Worker — Reads task.json, executes script commands in /workspace/repo/,
# writes result.json with status and command output.
#
# REF: CON-001 §4, §5
#
# Exit codes: always 0 (success/failure encoded in result.json)

set -euo pipefail

TASK_FILE="/workspace/input/task.json"
RESULT_FILE="/workspace/output/result.json"
REPO_DIR="/workspace/repo"

# ---- Read task.json ----
if [ ! -f "$TASK_FILE" ]; then
    cat > "$RESULT_FILE" <<EOF
{
  "taskId": "00000000-0000-0000-0000-000000000000",
  "status": "failure",
  "summary": "task.json not found at $TASK_FILE",
  "filesChanged": [],
  "testsPassed": false,
  "errors": ["task.json not found"],
  "notes": "",
  "nextAction": "review"
}
EOF
    exit 0
fi

TASK_ID=$(jq -r '.taskId' "$TASK_FILE")
OBJECTIVE=$(jq -r '.objective // "No objective"' "$TASK_FILE")

# ---- Check for script field ----
SCRIPT_COUNT=$(jq '.script // [] | length' "$TASK_FILE")

if [ "$SCRIPT_COUNT" -eq 0 ]; then
    cat > "$RESULT_FILE" <<EOF
{
  "taskId": "$TASK_ID",
  "status": "success",
  "summary": "No script provided — nothing to execute",
  "filesChanged": [],
  "testsPassed": false,
  "errors": [],
  "notes": "Script worker received no script commands. Task completed as no-op.",
  "nextAction": "review"
}
EOF
    exit 0
fi

# ---- Execute each script command sequentially ----
NOTES=""
ERRORS="[]"
STATUS="success"
CMD_INDEX=0

cd "$REPO_DIR" 2>/dev/null || cd /workspace

while [ $CMD_INDEX -lt "$SCRIPT_COUNT" ]; do
    CMD=$(jq -r ".script[$CMD_INDEX]" "$TASK_FILE")
    CMD_INDEX=$((CMD_INDEX + 1))

    echo "==== Running command $CMD_INDEX/$SCRIPT_COUNT: $CMD ===="

    # Capture output and exit code
    set +e
    OUTPUT=$(bash -c "$CMD" 2>&1)
    EXIT_CODE=$?
    set -e

    NOTES="${NOTES}--- Command $CMD_INDEX: $CMD ---\nExit code: $EXIT_CODE\n$OUTPUT\n\n"

    if [ $EXIT_CODE -ne 0 ]; then
        STATUS="failure"
        # Build errors JSON array
        ERROR_MSG=$(echo "$OUTPUT" | tail -5 | jq -Rs .)
        ERRORS=$(echo "$ERRORS" | jq ". + [\"Command $CMD_INDEX failed (exit $EXIT_CODE): $(echo "$CMD" | head -c 100)\"]")
        echo "Command $CMD_INDEX FAILED with exit code $EXIT_CODE"
        break
    fi

    echo "Command $CMD_INDEX completed successfully"
done

# ---- Collect changed files from git ----
FILES_CHANGED="[]"
if [ -d "$REPO_DIR/.git" ]; then
    cd "$REPO_DIR"
    FILES_CHANGED=$(git diff --name-only 2>/dev/null | jq -R . | jq -s . 2>/dev/null || echo "[]")
fi

# ---- Write result.json ----
NOTES_ESCAPED=$(printf '%s' "$NOTES" | jq -Rs .)

cat > "$RESULT_FILE" <<EOF
{
  "taskId": "$TASK_ID",
  "status": "$STATUS",
  "summary": "Script worker executed $CMD_INDEX/$SCRIPT_COUNT commands: $STATUS",
  "filesChanged": $FILES_CHANGED,
  "testsPassed": false,
  "errors": $ERRORS,
  "notes": $NOTES_ESCAPED,
  "nextAction": "review"
}
EOF

echo "Result written to $RESULT_FILE"
echo "Script worker finished with status: $STATUS"
