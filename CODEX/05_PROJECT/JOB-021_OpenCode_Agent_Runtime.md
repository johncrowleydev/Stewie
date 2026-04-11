---
id: JOB-021
title: "Job 021 — OpenCode Agent Runtime"
type: how-to
status: CLOSED
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-6, agent-runtime, opencode, containers]
related: [PRJ-001, BLU-001, CON-004, JOB-017]
created: 2026-04-11
updated: 2026-04-11
version: 1.0.0
---

> **BLUF:** Build the first real `IAgentRuntime` — a Docker image using OpenCode CLI backed by Gemini Flash. After this job, the API can launch an LLM-powered agent container that receives tasks via RabbitMQ, invokes OpenCode to write code, and publishes results. Includes file-based secret injection, credential storage, and a mock LLM responder for CI testing.

# Job 021 — OpenCode Agent Runtime

---

## 1. Context

Phase 5b built the agent infrastructure: `IAgentRuntime` interface, `AgentLifecycleService`, `StubAgentRuntime`, and the RabbitMQ messaging backbone. The stub runtime proved the messaging loop works end-to-end without any LLM dependency.

This job plugs in real intelligence by building an `OpenCodeAgentRuntime` that:

1. Launches a Docker container with OpenCode CLI installed
2. Configures it with LLM API keys via file-based secret mounting (not env vars)
3. Runs a Python entrypoint harness that brokers between RabbitMQ and OpenCode CLI
4. Supports a mock LLM mode for CI testing without API keys
5. Extends the credential store for multi-provider API key management

**Why OpenCode?** Open-source, provider-agnostic (75+ providers), supports headless mode (`opencode run "prompt"`), and can use Gemini Flash (~$0.10/1M input tokens — 8x cheaper than Claude Haiku).

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | All tasks (T-180 through T-188) | `feature/JOB-021-opencode-runtime` |

**Dependency: None — this job builds on existing main branch code.**

---

## 3. Prerequisites

Before starting, read these files to understand the existing patterns:

| File | Why |
|:-----|:----|
| `src/Stewie.Infrastructure/AgentRuntimes/StubAgentRuntime.cs` | Reference `IAgentRuntime` implementation — follow this pattern exactly |
| `src/Stewie.Application/Interfaces/IAgentRuntime.cs` | The interface you're implementing |
| `src/Stewie.Application/Services/AgentLifecycleService.cs` | How the lifecycle service calls your runtime |
| `src/Stewie.Domain/Messaging/AgentLaunchRequest.cs` | Launch request DTO you'll extend |
| `src/Stewie.Domain/Entities/UserCredential.cs` | Existing credential entity to extend |
| `docker/stub-agent/` | Reference Docker image structure |
| `CODEX/20_BLUEPRINTS/CON-004_Agent_Messaging_Contract.md` | Message schemas your harness must produce/consume |

---

## 4. Tasks

### T-180: OpenCodeAgentRuntime.cs

**Create** `src/Stewie.Infrastructure/AgentRuntimes/OpenCodeAgentRuntime.cs`:

Implements `IAgentRuntime` with `RuntimeName = "opencode"`. Follow the `StubAgentRuntime` pattern exactly — same `RunDockerCommandAsync` helper, same container naming, same Docker CLI approach.

**Key differences from StubAgentRuntime:**

1. **Image name:** `stewie-opencode-agent` (not `stewie-stub-agent`)
2. **Secret file mount:** Instead of passing the LLM API key as `-e LLM_API_KEY=...`, write the key to a temp file and mount it:
   ```
   -v /tmp/stewie-secrets-{sessionId}/llm_api_key:/run/secrets/llm_api_key:ro
   ```
3. **Model config:** Pass `LLM_PROVIDER` and `MODEL_NAME` as env vars (these are non-secret config)
4. **Workspace mount:** Same as stub: `-v "{workspacePath}:/workspace"`

**Constructor dependencies:** `ILogger<OpenCodeAgentRuntime>`, optional `string? imageName`

**Secret file lifecycle:**
- `LaunchAgentAsync`: Create `/tmp/stewie-secrets-{sessionId}/` directory, write API key to `llm_api_key` file
- `TerminateAgentAsync`: Delete the secret directory after stopping the container
- Use `try/finally` to ensure cleanup even on errors

**Acceptance criteria:**
- [ ] Implements all 3 `IAgentRuntime` methods
- [ ] Uses file-based secret mounting, NOT env vars for API keys
- [ ] Cleans up secret files on termination
- [ ] Falls back to env var if `SecretsMountPath` is empty (for backwards compat)
- [ ] Full XML doc comments per GOV-003

---

### T-181: Dockerfile (stewie-opencode-agent)

**Create** `docker/opencode-agent/Dockerfile`:

```dockerfile
FROM node:22-slim

# Install OpenCode CLI globally
RUN npm install -g opencode

# Install Python 3 + pika for the entrypoint harness
RUN apt-get update && apt-get install -y python3 python3-pip && \
    pip3 install --break-system-packages pika==1.3.2 && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Copy entrypoint harness
COPY entrypoint.py /app/entrypoint.py
COPY opencode.json.template /app/opencode.json.template
COPY mock_llm.py /app/mock_llm.py

WORKDIR /workspace

ENTRYPOINT ["python3", "/app/entrypoint.py"]
```

**Create** `docker/opencode-agent/build.sh`:
```bash
#!/bin/bash
docker build -t stewie-opencode-agent ./docker/opencode-agent/
```

**Acceptance criteria:**
- [ ] Image builds successfully
- [ ] `opencode` CLI is available in PATH
- [ ] Python 3 and pika are available
- [ ] Image size < 500MB

---

### T-182: Agent Entrypoint Harness (entrypoint.py)

**Create** `docker/opencode-agent/entrypoint.py`:

This is the bridge between RabbitMQ and OpenCode CLI. It's role-agnostic — works for developer, tester, and architect agents.

**Flow:**

1. Read env vars: `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USER`, `RABBITMQ_PASS`, `RABBITMQ_VHOST`, `AGENT_QUEUE`, `AGENT_ID`, `PROJECT_ID`, `AGENT_ROLE`, `LLM_PROVIDER`, `MODEL_NAME`, `MOCK_LLM`
2. Read API key from `/run/secrets/llm_api_key` (fall back to `LLM_API_KEY` env var)
3. Generate `opencode.json` from template with provider config
4. Connect to RabbitMQ
5. Publish `event.started` to `stewie.events` exchange
6. Consume from `AGENT_QUEUE`
7. On `command.assign_task`:
   a. Extract `taskDescription` from payload
   b. If `MOCK_LLM=true`: delegate to `mock_llm.py` instead of OpenCode
   c. Else: run `opencode run --model {provider}/{model} "{taskDescription}"` as subprocess
   d. Stream stdout lines as `event.stdout` messages (real-time)
   e. On process exit 0: publish `event.completed` with summary
   f. On process exit != 0: publish `event.failed` with error details
8. On `command.terminate`: publish `event.stopped`, disconnect, exit(0)
9. SIGTERM handler: same as `command.terminate`

**Message format:** Follow CON-004 §5 envelope format exactly:
```json
{
  "messageId": "uuid",
  "timestamp": "ISO-8601",
  "type": "event.started",
  "source": "agent.{AGENT_ID}",
  "payload": { ... }
}
```

**Acceptance criteria:**
- [ ] Connects to RabbitMQ on startup
- [ ] Publishes `event.started` immediately after connecting
- [ ] Handles `command.assign_task` → invokes OpenCode → publishes result
- [ ] Streams stdout in real-time as `event.stdout` messages
- [ ] Clean shutdown on SIGTERM
- [ ] Mock LLM mode works without any API key
- [ ] Well-documented with docstrings

---

### T-183: LLM Credential Storage

**Create** `src/Stewie.Domain/Enums/CredentialType.cs`:

```csharp
/// <summary>
/// Classifies credential types stored in the UserCredential entity.
/// REF: JOB-021 T-183
/// </summary>
public enum CredentialType
{
    /// <summary>GitHub Personal Access Token (existing).</summary>
    GitHubPat = 0,

    /// <summary>Anthropic API key for Claude models.</summary>
    AnthropicApiKey = 1,

    /// <summary>OpenAI API key for GPT models.</summary>
    OpenAiApiKey = 2,

    /// <summary>Google AI API key for Gemini models.</summary>
    GoogleAiApiKey = 3
}
```

**Modify** `src/Stewie.Domain/Entities/UserCredential.cs`:
- Add `public virtual CredentialType CredentialType { get; set; } = CredentialType.GitHubPat;`

**Create** FluentMigrator migration `Migration_021_AddCredentialType`:
- Add column `CredentialType` (int, not null, default 0) to `UserCredentials` table

**Modify** NHibernate mapping for `UserCredential` — map the new column.

**Modify** `src/Stewie.Application/Interfaces/IUserCredentialRepository.cs`:
- Add `Task<UserCredential?> GetByTypeAsync(Guid userId, CredentialType type);`

**Implement** in the repository class.

**Acceptance criteria:**
- [ ] Migration runs clean on existing DB (default 0 = GitHubPat for existing rows)
- [ ] Repository can query by credential type
- [ ] Existing GitHub PAT logic is unaffected (default enum value = GitHubPat)

---

### T-184: File-Based Secret Injection

**Modify** `src/Stewie.Application/Services/AgentLifecycleService.cs`:

After resolving the runtime and before calling `LaunchAgentAsync`, resolve the LLM API key:

1. Look up the project's configured LLM provider (from `stewie.json` or project settings)
2. Resolve `CredentialType` from provider name → enum mapping
3. Call `IUserCredentialRepository.GetByTypeAsync(userId, credentialType)`
4. If found: decrypt the key via `IEncryptionService`
5. Write the decrypted key to `/tmp/stewie-secrets-{sessionId}/llm_api_key`
6. Set `request.SecretsMountPath = /tmp/stewie-secrets-{sessionId}`
7. After container terminates: delete the secrets directory

**Add new constructor dependency:** `IUserCredentialRepository`, `IEncryptionService`

**Error handling:**
- If no credential found for the provider → throw with clear message: "No {provider} API key configured. Add one in Settings."
- If decryption fails → log error, throw

**Acceptance criteria:**
- [ ] API key is resolved from encrypted credential store
- [ ] Key is written to temp file, mounted read-only
- [ ] Cleanup happens in both success and error paths
- [ ] Clear error message when credential is missing

---

### T-185: AgentLaunchRequest Extension

**Modify** `src/Stewie.Domain/Messaging/AgentLaunchRequest.cs`:

Add three new properties:

```csharp
/// <summary>LLM provider identifier (e.g., "google", "anthropic", "openai").</summary>
public string LlmProvider { get; init; } = string.Empty;

/// <summary>Model name (e.g., "gemini-2.0-flash", "claude-3-haiku").</summary>
public string ModelName { get; init; } = string.Empty;

/// <summary>
/// Host path to the secrets directory to mount at /run/secrets/ inside the container.
/// When empty, the runtime should fall back to environment variables.
/// </summary>
public string SecretsMountPath { get; init; } = string.Empty;
```

**Acceptance criteria:**
- [ ] Fields added with XML doc comments
- [ ] Existing code compiles without changes (properties have defaults)

---

### T-186: DI Registration

**Modify** the DI registration file (find where `StubAgentRuntime` is registered) to also register `OpenCodeAgentRuntime`:

```csharp
services.AddSingleton<IAgentRuntime, StubAgentRuntime>();
services.AddSingleton<IAgentRuntime, OpenCodeAgentRuntime>();
```

Both runtimes are registered. `AgentLifecycleService` already resolves by `RuntimeName` — the `LaunchAgentAsync` method selects the right one based on the `runtimeName` parameter.

**Acceptance criteria:**
- [ ] Both runtimes registered
- [ ] Selecting `runtimeName = "opencode"` → `OpenCodeAgentRuntime`
- [ ] Selecting `runtimeName = "stub"` → `StubAgentRuntime`

---

### T-187: Mock LLM Responder

**Create** `docker/opencode-agent/mock_llm.py`:

A lightweight Python function that simulates OpenCode output without calling any LLM API. Used when `MOCK_LLM=true`.

**Behavior:**
1. Receives task description as input
2. Prints realistic-looking progress output (simulating OpenCode):
   ```
   ⠋ Analyzing task...
   ⠋ Reading workspace files...
   ⠋ Planning changes...
   ✓ Created src/Example.cs
   ✓ Modified src/Program.cs
   ✓ Task complete
   ```
3. Creates a dummy file in the workspace: `stewie-mock-output.txt` with the task description
4. Returns exit code 0 (success)
5. Sleeps 2-5 seconds to simulate LLM thinking time

**Acceptance criteria:**
- [ ] Runs without any API key or network access
- [ ] Produces stdout that the entrypoint harness can stream
- [ ] Creates a verifiable artifact in the workspace
- [ ] Returns exit code 0

---

### T-188: Unit Tests

**Create** `src/Stewie.Tests/Services/OpenCodeAgentRuntimeTests.cs`:

Follow the pattern in `StubAgentRuntimeTests.cs`.

| Test | Description |
|:-----|:------------|
| `LaunchAgent_BuildsCorrectDockerCommand` | Verify image name, env vars, secret mount, workspace mount |
| `LaunchAgent_CreatesSecretDirectory` | Verify temp directory and secret file are created |
| `TerminateAgent_CleansUpSecrets` | Verify secret directory is deleted after terminate |
| `GetStatus_ReturnsCorrectState` | Verify Docker inspect parsing |
| `LaunchAgent_NoSecretPath_FallsBackToEnvVar` | Verify env var fallback when `SecretsMountPath` is empty |
| `RuntimeName_ReturnsOpenCode` | Property returns `"opencode"` |

**Also create** `src/Stewie.Tests/Services/CredentialTypeTests.cs`:

| Test | Description |
|:-----|:------------|
| `GetByType_ReturnsCorrectCredential` | Repository returns credential matching type |
| `GetByType_ReturnNull_WhenNotFound` | Repository returns null for missing type |

**Acceptance criteria:**
- [ ] All tests pass
- [ ] Mocked Docker process — no real containers launched
- [ ] Tests cover happy path, error path, and fallback

---

## 5. Contracts Affected

| Contract | Change | Version |
|:---------|:-------|:--------|
| CON-001 | No change | — |
| CON-002 | No change (agent endpoints already exist from JOB-017) | — |
| CON-004 | No change (entrypoint uses existing message schemas) | — |

---

## 6. Verification

```bash
# Build opencode agent image
cd docker/opencode-agent && docker build -t stewie-opencode-agent .

# Backend build
dotnet build src/Stewie.Api/Stewie.Api.csproj

# Run tests
dotnet test src/Stewie.Tests/Stewie.Tests.csproj

# Manual: launch with mock LLM
curl -X POST http://localhost:5275/api/agents \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"projectId":"<guid>","role":"developer","runtimeName":"opencode"}'

# Verify mock output in container logs
docker logs stewie-agent-<sessionId>
```

**Exit criteria:**
- OpenCode agent image builds < 500MB
- Runtime launches container with correct env vars and secret mount
- Mock LLM mode works end-to-end: assign task → mock output → event.completed
- All unit tests pass
- Secret files cleaned up after container termination
- Existing `StubAgentRuntime` and all 203 tests still pass

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-11 | JOB-021 created for Phase 6 OpenCode Agent Runtime |
