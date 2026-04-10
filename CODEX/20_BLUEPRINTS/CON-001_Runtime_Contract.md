---
id: CON-001
title: "Runtime Contract — Task & Result Packets"
type: reference
status: DRAFT
owner: architect
agents: [all]
tags: [standards, specification, project-management, governance]
related: [BLU-001, CON-002, GOV-004]
created: 2026-04-09
updated: 2026-04-10
version: 1.5.0
---

> **BLUF:** This contract defines the binding interface between Stewie (orchestrator) and worker containers. All communication flows through two JSON files: `task.json` (input) and `result.json` (output). Workers MUST conform to this contract. No deviation without Human approval.

# Runtime Contract — Task & Result Packets

> **"The contract is truth. The code is an attempt to match it."**

---

## 1. Contract Scope

**What this covers:**
- The JSON schema for `task.json` (orchestrator → worker)
- The JSON schema for `result.json` (worker → orchestrator)
- File paths and directory structure within the worker container
- Success/failure semantics

**What this does NOT cover:**
- HTTP API endpoints (see `CON-002`)
- Database schema or ORM mappings
- Container image build process

**Parties:**

| Role | Description |
|:-----|:------------|
| **Producer (task.json)** | Stewie orchestrator (`WorkspaceService`) |
| **Consumer (task.json)** | Worker container runtime |
| **Producer (result.json)** | Worker container runtime |
| **Consumer (result.json)** | Stewie orchestrator (`RunOrchestrationService`) |

---

## 2. Version & Stability

| Field | Value |
|:------|:------|
| Contract version | `1.5.0` |
| Stability | `EXPERIMENTAL` |
| Breaking change policy | MAJOR version bump required for any field removal or type change |
| Backward compatibility | Workers must handle unknown fields gracefully (ignore, don't fail) |

---

## 3. Container Filesystem Layout

Workers receive three mounted directories:

```
/workspace/
├── input/          # Read-only — contains task.json
│   └── task.json
├── output/         # Read-write — worker writes result.json here
│   ├── result.json
│   └── log.txt     # Optional worker log
└── repo/           # Read-only — cloned repository (when applicable)
```

| Mount | Container Path | Host Path | Access |
|:------|:--------------|:----------|:-------|
| Input | `/workspace/input/` | `workspaces/{taskId}/input/` | Read-only |
| Output | `/workspace/output/` | `workspaces/{taskId}/output/` | Read-write |
| Repo | `/workspace/repo/` | `workspaces/{taskId}/repo/` | Read-only |

### 3.1 Multi-Task Workspace Isolation (v1.5.0)

For multi-task jobs, each task gets its own isolated workspace keyed by `taskId`. The `PrepareWorkspaceForRun` method creates a per-task directory under the shared workspace root. Parallel tasks do NOT share filesystem state — each independently clones the repository.

```
workspaces/
├── {taskId-A}/        # Task A workspace
│   ├── input/
│   ├── output/
│   └── repo/          # Independent clone
├── {taskId-B}/        # Task B workspace (parallel)
│   ├── input/
│   ├── output/
│   └── repo/          # Independent clone
└── {taskId-C}/        # Task C workspace
    ├── input/
    ├── output/
    └── repo/
```

> **Backward compatibility:** Single-task jobs continue to use the existing `workspaces/{taskId}/` layout. The workspace per-task isolation is transparent to the worker — the container always sees the same `/workspace/` mount points regardless of whether it is part of a multi-task job.

---

## 4. task.json — Input Schema

The orchestrator writes this file before launching the container.

### 4.1 Fields

| Field | Type | Required | Description | Constraints |
|:------|:-----|:--------:|:------------|:------------|
| `taskId` | `string (UUID)` | ✅ | Unique identifier for this task | Valid UUID v4 |
| `jobId` | `string (UUID)` | ✅ | Parent Job identifier | Valid UUID v4 |
| `role` | `string` | ✅ | Agent role executing this task | One of: `developer`, `tester`, `researcher` |
| `objective` | `string` | ✅ | What the worker should accomplish | Non-empty, max 2000 chars |
| `scope` | `string` | ✅ | Boundaries of the work | Non-empty, max 2000 chars |
| `allowedPaths` | `string[]` | ✅ | File paths the worker may read/modify | Empty array = no restrictions |
| `forbiddenPaths` | `string[]` | ✅ | File paths the worker must NOT touch | Empty array = no restrictions |
| `acceptanceCriteria` | `string[]` | ✅ | Conditions that must be met for success | At least 1 criterion |
| `repoUrl` | `string` | ❌ | Git repository URL to clone into workspace | Valid HTTPS or SSH URL |
| `branch` | `string` | ❌ | Branch name to create after clone | Valid git branch name |
| `script` | `string[]` | ❌ | Bash commands to execute sequentially in `/workspace/repo/` | Each entry is a shell command |
| `parentTaskId` | `string (UUID)` | ❌ | ID of the developer task this tester is validating | Present when role=tester |
| `attemptNumber` | `integer` | ❌ | Retry attempt counter (1-based) | Default: 1. Incremented on governance retry |
| `governanceViolations` | `object[]` | ❌ | Violations from previous governance check (for retry feedback) | Present on retry tasks (attempt > 1) |
| `governanceViolations[].ruleId` | `string` | ✅ | Rule identifier (e.g., "GOV-003-001") | |
| `governanceViolations[].ruleName` | `string` | ✅ | Human-readable rule name | |
| `governanceViolations[].details` | `string` | ✅ | Specific violation details | |

### 4.2 Example

```json
{
  "taskId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "jobId": "f0e1d2c3-b4a5-6789-0fed-cba987654321",
  "role": "developer",
  "objective": "Implement the health check endpoint",
  "scope": "Add GET /health to Stewie.Api returning 200 OK with version info",
  "allowedPaths": ["src/Stewie.Api/"],
  "forbiddenPaths": ["src/Stewie.Domain/Entities/"],
  "acceptanceCriteria": [
    "GET /health returns 200 with JSON body",
    "Response includes version field",
    "Endpoint requires no authentication"
  ],
  "repoUrl": "https://github.com/johncrowleydev/Stewie.git",
  "branch": "stewie/a1b2c3d4/health-endpoint",
  "script": [
    "echo 'Implementing health endpoint'",
    "touch src/Stewie.Api/Controllers/HealthController.cs"
  ]
}
```

### 4.3 C# Schema

```csharp
public class TaskPacket
{
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    [JsonPropertyName("jobId")]
    public Guid RunId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("allowedPaths")]
    public List<string> AllowedPaths { get; set; } = [];

    [JsonPropertyName("forbiddenPaths")]
    public List<string> ForbiddenPaths { get; set; } = [];

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];

    [JsonPropertyName("repoUrl")]
    public string? RepoUrl { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("script")]
    public List<string>? Script { get; set; }
}
```

---

## 5. result.json — Output Schema

The worker writes this file before exiting.

### 5.1 Fields

| Field | Type | Required | Description | Constraints |
|:------|:-----|:--------:|:------------|:------------|
| `taskId` | `string (UUID)` | ✅ | Must match the `taskId` from task.json | Valid UUID v4 |
| `status` | `string` | ✅ | Execution outcome | One of: `success`, `failure`, `partial` |
| `summary` | `string` | ✅ | Human-readable summary of what happened | Non-empty, max 5000 chars |
| `filesChanged` | `string[]` | ✅ | List of files created/modified/deleted | Relative paths |
| `testsPassed` | `boolean` | ✅ | Whether the worker's own tests passed | `true` or `false` |
| `errors` | `string[]` | ✅ | Error messages (empty if success) | Max 50 entries |
| `notes` | `string` | ✅ | Additional context for the Architect | Max 5000 chars |
| `nextAction` | `string` | ✅ | Suggested next step | One of: `review`, `retry`, `escalate`, `done` |

### 5.2 Example (Success)

```json
{
  "taskId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "success",
  "summary": "Health check endpoint implemented and tested.",
  "filesChanged": [
    "src/Stewie.Api/Controllers/HealthController.cs"
  ],
  "testsPassed": true,
  "errors": [],
  "notes": "Added GET /health returning version from assembly metadata.",
  "nextAction": "review"
}
```

### 5.3 Example (Failure)

```json
{
  "taskId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "failure",
  "summary": "Build failed due to missing NuGet dependency.",
  "filesChanged": [],
  "testsPassed": false,
  "errors": [
    "error CS0246: The type or namespace name 'HealthChecks' could not be found"
  ],
  "notes": "The project file does not reference Microsoft.Extensions.Diagnostics.HealthChecks.",
  "nextAction": "retry"
}
```

### 5.4 C# Schema

```csharp
public class ResultPacket
{
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("filesChanged")]
    public List<string> FilesChanged { get; set; } = [];

    [JsonPropertyName("testsPassed")]
    public bool TestsPassed { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("nextAction")]
    public string NextAction { get; set; } = string.Empty;
}
```

---

## 6. governance-report.json — Governance Output Schema

The governance worker (role=tester) writes this file to `/workspace/output/governance-report.json` after running all governance checks.

### 6.1 Fields

| Field | Type | Required | Description |
|:------|:-----|:--------:|:------------|
| `taskId` | `string (UUID)` | ✅ | Must match the tester task's `taskId` |
| `status` | `string` | ✅ | Overall verdict: `pass` or `fail` |
| `summary` | `string` | ✅ | Human-readable summary (e.g., "14/16 checks passed") |
| `totalChecks` | `integer` | ✅ | Total number of checks executed |
| `passedChecks` | `integer` | ✅ | Number of checks that passed |
| `failedChecks` | `integer` | ✅ | Number of checks that failed |
| `checks` | `object[]` | ✅ | Array of individual check results |
| `checks[].ruleId` | `string` | ✅ | Rule identifier (e.g., "GOV-002-001") |
| `checks[].ruleName` | `string` | ✅ | Human-readable rule name (e.g., "Build Succeeds") |
| `checks[].category` | `string` | ✅ | GOV document reference (e.g., "GOV-002") |
| `checks[].passed` | `boolean` | ✅ | Whether this check passed |
| `checks[].details` | `string` | ❌ | Error output, line numbers, or details on failure |
| `checks[].severity` | `string` | ✅ | `error` (blocks acceptance) or `warning` (reported only) |

### 6.2 Example (Pass)

```json
{
  "taskId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "status": "pass",
  "summary": "16/16 checks passed",
  "totalChecks": 16,
  "passedChecks": 16,
  "failedChecks": 0,
  "checks": [
    { "ruleId": "GOV-002-001", "ruleName": "Build Succeeds", "category": "GOV-002", "passed": true, "details": null, "severity": "error" },
    { "ruleId": "GOV-002-002", "ruleName": "Tests Pass", "category": "GOV-002", "passed": true, "details": null, "severity": "error" }
  ]
}
```

### 6.3 Example (Fail)

```json
{
  "taskId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "status": "fail",
  "summary": "14/16 checks passed, 2 failed",
  "totalChecks": 16,
  "passedChecks": 14,
  "failedChecks": 2,
  "checks": [
    { "ruleId": "GOV-003-001", "ruleName": "No TypeScript any Types", "category": "GOV-003", "passed": false, "details": "src/pages/DashboardPage.tsx:42: const data: any = ...", "severity": "error" },
    { "ruleId": "SEC-001-001", "ruleName": "No Secrets in Diff", "category": "SEC-001", "passed": false, "details": "src/config.ts:10: const API_KEY = 'sk-live-...'", "severity": "error" }
  ]
}
```

### 6.4 Verdict Logic

- **`pass`**: Zero checks with `severity: "error"` failed. Warning failures are allowed.
- **`fail`**: One or more checks with `severity: "error"` failed.
- The `warningsBlockAcceptance` setting (user-configurable, default `false`) can override this to also fail on warnings.

---

## 7. Error Behavior

| Scenario | Worker Behavior | Orchestrator Behavior |
|:---------|:---------------|:---------------------|
| task.json missing | Exit code 1, no result.json | Mark Task as `Failed` |
| task.json invalid JSON | Exit code 1, no result.json | Mark Task as `Failed` |
| Worker crashes | Non-zero exit code, no result.json | Mark Task as `Failed` |
| Worker succeeds | Exit code 0, writes result.json with `status: "success"` | Ingest result, mark Task as `Completed` |
| Worker fails gracefully | Exit code 0, writes result.json with `status: "failure"` | Ingest result, mark Task as `Failed` |
| result.json missing after exit 0 | N/A | Mark Task as `Failed` |
| result.json invalid JSON | N/A | Mark Task as `Failed` |

---

## 7. Performance Requirements

| Metric | Requirement |
|:-------|:------------|
| Container startup | < 10s |
| task.json read | < 100ms |
| result.json write | < 100ms |
| Total task timeout | 300s (5 min hard limit — not yet enforced) |

---

## 8. Change Protocol

> **This contract is immutable without Human approval.**

To propose a contract change:
1. Developer or Tester opens `60_EVOLUTION/EVO-NNN.md` describing the proposed change
2. Architect reviews and drafts the contract update
3. Human approves the updated contract
4. Version is bumped. All consuming agents are notified.
5. A transition sprint is opened if the change is breaking

---

## 9. Verification Checklist

- [ ] Worker reads task.json from `/workspace/input/task.json`
- [ ] Worker writes result.json to `/workspace/output/result.json`
- [ ] All required fields present in task.json
- [ ] All required fields present in result.json
- [ ] `taskId` in result.json matches `taskId` in task.json
- [ ] `status` field uses only allowed values
- [ ] `nextAction` field uses only allowed values
- [ ] Exit code 0 on success, non-zero on crash
- [ ] Worker handles missing task.json gracefully (exit 1)
