---
id: RUN-003
title: "End-to-End Agent Loop Validation"
type: how-to
status: ACTIVE
owner: architect
agents: [all]
tags: [runbook, e2e, testing, agent-loop, validation]
related: [JOB-022, JOB-023, CON-002, CON-004]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

> **BLUF:** Step-by-step runbook for validating the full autonomous agent loop: Human chats → Architect plans → Dev executes → Architect reviews → Human sees results. Covers both mock-LLM and real-LLM paths, plus a curl-based CI smoke test.

# RUN-003 — End-to-End Agent Loop Validation

---

## 1. Purpose

This runbook validates that the complete Stewie agent loop operates correctly from Human chat input through Architect planning, Developer execution, and result display on the dashboard. It covers:

- Infrastructure startup
- API and frontend readiness
- LLM configuration (mock or real)
- Full autonomous loop execution
- Event persistence verification

---

## 2. Prerequisites

| Requirement | How to Verify |
|:------------|:-------------|
| Docker Engine ≥ 24.x | `docker --version` |
| Docker Compose ≥ 2.x | `docker compose version` |
| .NET 10 SDK | `dotnet --version` |
| Node.js ≥ 20.x | `node --version` |
| SQL Server container running | `docker ps \| grep stewie-sql` |
| RabbitMQ container running | `docker ps \| grep stewie-rabbitmq` |
| Repository cloned with CODEX | `ls CODEX/00_INDEX/MANIFEST.yaml` |

---

## 3. Full Validation — 13-Step Procedure

### Step 1: Start Infrastructure (Docker)

```bash
docker compose up -d sql rabbitmq
```

Wait for both containers to become healthy:

```bash
docker compose ps --format "table {{.Name}}\t{{.Status}}"
# Both should show "Up ... (healthy)"
```

### Step 2: Build Agent Docker Images

```bash
# Architect agent
docker build -t stewie-architect-agent:latest docker/architect-agent/

# OpenCode agent (for real LLM path)
docker build -t stewie-opencode-agent:latest docker/opencode-agent/
```

### Step 3: Start API + Frontend

Use the `/run_app` workflow, or manually:

```bash
# Terminal 1: API
cd src/Stewie.Api && dotnet run

# Terminal 2: Frontend
cd src/Stewie.Web && npm run dev
```

Wait for:
- API: `http://localhost:5275/health` returns `{ "status": "healthy" }`
- Frontend: `http://localhost:5173` loads

### Step 4: Authenticate

```bash
# Login (admin user is seeded on first run)
TOKEN=$(curl -s -X POST http://localhost:5275/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@Stewie123!"}' \
  | jq -r '.token')

echo "Auth token: ${TOKEN:0:20}..."
```

### Step 5: Configure LLM API Key (Real LLM Path Only)

> **Skip this step for mock (stub) mode.**

```bash
# Store a Google AI API key
curl -s -X POST http://localhost:5275/api/settings/credentials \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"credentialType":"GoogleAiApiKey","value":"YOUR_ACTUAL_API_KEY"}' \
  | jq .

# Verify it's stored (masked)
curl -s http://localhost:5275/api/settings/credentials \
  -H "Authorization: Bearer $TOKEN" \
  | jq .
```

### Step 6: Create a Test Project

```bash
PROJECT_ID=$(curl -s -X POST http://localhost:5275/api/projects \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"E2E Test Project","repoUrl":"https://github.com/test/e2e-test"}' \
  | jq -r '.id')

echo "Project: $PROJECT_ID"
```

### Step 7: Select Runtime + Model and Start Architect Agent

For **mock/stub mode** (no real LLM needed):

```bash
curl -s -X POST "http://localhost:5275/api/projects/$PROJECT_ID/architect/start" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"runtimeName":"stub"}' \
  | jq .
```

For **real LLM mode** (requires API key from Step 5):

```bash
curl -s -X POST "http://localhost:5275/api/projects/$PROJECT_ID/architect/start" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"runtimeName":"opencode","workspacePath":"/tmp/stewie-e2e-workspace"}' \
  | jq .
```

**Verify:** Status should be `"Active"`.

### Step 8: Send a Chat Message

```bash
curl -s -X POST "http://localhost:5275/api/projects/$PROJECT_ID/chat" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"content":"Create a simple REST endpoint that returns hello world"}' \
  | jq .
```

**Verify:** Returns 201 with the message persisted. In stub mode, the Architect stub will echo back a simulated plan.

### Step 9: Verify Architect Responds with a Plan

Wait a few seconds for the Architect to process, then check chat history:

```bash
curl -s "http://localhost:5275/api/projects/$PROJECT_ID/chat" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.messages[] | select(.senderRole == "Architect") | {content, messageType}'
```

**Expected:** At least one message from the Architect. In stub mode, the content will be a simulated plan. Look for `messageType: "plan_proposal"` if the Architect emits structured plans.

### Step 10: Approve the Plan

```bash
# Get the plan ID from the Architect's message (or use a known test ID)
PLAN_ID="test-plan-001"

curl -s -X POST "http://localhost:5275/api/projects/$PROJECT_ID/chat/plan-decision" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"planId\":\"$PLAN_ID\",\"decision\":\"approved\"}" \
  | jq .
```

**Verify:** Returns 200 with `decision: "approved"`.

### Step 11: Verify Job Created and Dev Agent Launched

```bash
# Check jobs for the project
curl -s "http://localhost:5275/api/jobs?projectId=$PROJECT_ID" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.[] | {id, status, taskCount}'

# Check agent sessions
curl -s "http://localhost:5275/api/projects/$PROJECT_ID/agents" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.sessions[] | {id, agentRole, status}'
```

**Expected (stub mode):** Job created with status Pending → Running → Completed. Dev agent session shows Active → Completed.

> In stub mode, the Dev Agent is simulated and completes immediately. In real LLM mode, wait for the container to finish (may take 1-5 minutes).

### Step 12: Verify Dev Agent Completes and Architect Reports Summary

```bash
# Check that the job completed
curl -s "http://localhost:5275/api/jobs?projectId=$PROJECT_ID" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.[] | {id, status}'

# Check for Architect summary in chat
curl -s "http://localhost:5275/api/projects/$PROJECT_ID/chat?limit=50" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.messages[-3:] | .[] | {senderRole, content}'
```

### Step 13: Verify Events Persisted

```bash
curl -s "http://localhost:5275/api/events?limit=20" \
  -H "Authorization: Bearer $TOKEN" \
  | jq '.[] | {entityType, eventType, timestamp}'
```

**Expected events (in order):**
1. `JobCreated`
2. `TaskCreated`
3. `TaskStarted`
4. `TaskCompleted`
5. `JobCompleted`

---

## 4. Mock-LLM Path (Stub Runtime)

The `stub` runtime simulates the full agent loop without requiring any LLM API key:

- **Architect Stub:** Receives chat messages, echoes a simulated plan, creates a job with a single task when plan is approved.
- **Developer Stub:** Receives the task, waits 2 seconds, produces a simulated result with a dummy diff.
- **No API key required.** No network calls to LLM providers.

This is the recommended path for CI/CD pipelines and local development testing.

---

## 5. Real-LLM Path

When using `opencode` runtime:

1. Requires a valid LLM API key stored via `POST /api/settings/credentials`
2. The Architect agent calls the configured LLM provider for planning
3. Developer agents use the same provider for code generation
4. Results are real code changes committed to the project workspace
5. **Cost warning:** Each full loop may consume 10K-100K tokens depending on the task

---

## 6. CI Smoke Test Script

Self-contained curl-based script for automated CI validation using `stub` runtime:

```bash
#!/bin/bash
# RUN-003 CI Smoke Test — validates the full stub agent loop
# Exit on first failure
set -euo pipefail

BASE_URL="${STEWIE_API_URL:-http://localhost:5275}"

echo "=== RUN-003 CI Smoke Test ==="
echo "Base URL: $BASE_URL"

# 1. Health check
echo "[1/8] Health check..."
HEALTH=$(curl -sf "$BASE_URL/health" | jq -r '.status')
[ "$HEALTH" = "healthy" ] || { echo "FAIL: health=$HEALTH"; exit 1; }
echo "  OK: healthy"

# 2. Authenticate
echo "[2/8] Authenticating..."
TOKEN=$(curl -sf -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@Stewie123!"}' \
  | jq -r '.token')
[ -n "$TOKEN" ] || { echo "FAIL: no token"; exit 1; }
AUTH="Authorization: Bearer $TOKEN"
echo "  OK: authenticated"

# 3. Create project
echo "[3/8] Creating project..."
PROJECT_ID=$(curl -sf -X POST "$BASE_URL/api/projects" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"name\":\"CI-E2E-$(date +%s)\",\"repoUrl\":\"https://github.com/test/ci-e2e\"}" \
  | jq -r '.id')
echo "  OK: project=$PROJECT_ID"

# 4. Start stub Architect
echo "[4/8] Starting stub Architect..."
ARCH_STATUS=$(curl -sf -X POST "$BASE_URL/api/projects/$PROJECT_ID/architect/start" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"runtimeName":"stub"}' \
  | jq -r '.status')
[ "$ARCH_STATUS" = "Active" ] || { echo "FAIL: architect status=$ARCH_STATUS"; exit 1; }
echo "  OK: architect active"

# 5. Send chat message
echo "[5/8] Sending chat message..."
CHAT_STATUS=$(curl -sf -X POST "$BASE_URL/api/projects/$PROJECT_ID/chat" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"content":"Create hello world endpoint"}' \
  -o /dev/null -w "%{http_code}")
[ "$CHAT_STATUS" = "201" ] || { echo "FAIL: chat status=$CHAT_STATUS"; exit 1; }
echo "  OK: message sent"

# 6. Submit plan decision
echo "[6/8] Approving plan..."
DECISION_STATUS=$(curl -sf -X POST \
  "$BASE_URL/api/projects/$PROJECT_ID/chat/plan-decision" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"planId":"ci-test-plan","decision":"approved"}' \
  -o /dev/null -w "%{http_code}")
[ "$DECISION_STATUS" = "200" ] || { echo "FAIL: decision status=$DECISION_STATUS"; exit 1; }
echo "  OK: plan approved"

# 7. Verify credentials API
echo "[7/8] Testing credentials API..."
CRED_STATUS=$(curl -sf -X POST "$BASE_URL/api/settings/credentials" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"credentialType":"GoogleAiApiKey","value":"ci-test-key-1234"}' \
  -o /dev/null -w "%{http_code}")
[ "$CRED_STATUS" = "201" ] || { echo "FAIL: credential add status=$CRED_STATUS"; exit 1; }

# Verify masking
MASKED=$(curl -sf "$BASE_URL/api/settings/credentials" \
  -H "$AUTH" | jq -r '.[0].maskedValue')
echo "  OK: credential stored, masked=$MASKED"

# Verify duplicate returns 409
DUP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "$BASE_URL/api/settings/credentials" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"credentialType":"GoogleAiApiKey","value":"another-key"}')
[ "$DUP_STATUS" = "409" ] || { echo "FAIL: duplicate should be 409, got $DUP_STATUS"; exit 1; }
echo "  OK: duplicate prevention works"

# 8. Verify events exist
echo "[8/8] Checking events..."
EVENT_COUNT=$(curl -sf "$BASE_URL/api/events?limit=5" \
  -H "$AUTH" | jq 'length')
[ "$EVENT_COUNT" -ge 1 ] || { echo "FAIL: no events found"; exit 1; }
echo "  OK: $EVENT_COUNT events found"

echo ""
echo "=== RUN-003 CI SMOKE TEST PASSED ==="
```

---

## 7. Troubleshooting

| Symptom | Likely Cause | Fix |
|:--------|:-------------|:----|
| Health endpoint returns 503 | Database not ready | Wait for SQL Server container to finish init; check `docker logs stewie-sql` |
| `401 Unauthorized` on login | Admin user not seeded | Restart API — admin is seeded on startup. Check `Stewie__AdminPassword` env var |
| Chat POST returns 409 | No active Architect | Start Architect first (Step 7) |
| RabbitMQ connection refused | Broker not running | `docker compose up -d rabbitmq` and wait for healthy status |
| Architect never responds | Container crashed | Check `docker logs <architect-container-id>` |
| Dev Agent timeout | Container stuck | Check `docker logs <dev-container-id>`. Terminate via `DELETE /api/agents/{id}` |
| Plan decision returns 404 | No active Architect session | Architect may have crashed. Restart and retry |
| Credential POST returns 400 | Bad credential type | Use exact enum names: `GoogleAiApiKey`, `AnthropicApiKey`, `OpenAiApiKey`, `GitHubPat` |

---

## 8. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-11 | RUN-003 created for JOB-023 T-206 |
