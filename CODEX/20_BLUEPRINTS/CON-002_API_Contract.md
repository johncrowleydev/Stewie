---
id: CON-002
title: "API Contract — HTTP Endpoints"
type: reference
status: DRAFT
owner: architect
agents: [all]
tags: [standards, specification, project-management, governance]
related: [BLU-001, CON-001, GOV-004]
created: 2026-04-09
updated: 2026-04-09
version: 1.5.0
---

> **BLUF:** This contract defines the HTTP API surface of Stewie.Api. All frontend and external consumers MUST conform to these routes, request/response shapes, and error formats. No deviation without Human approval.

# API Contract — HTTP Endpoints

> **"The contract is truth. The code is an attempt to match it."**

---

## 1. Contract Scope

**What this covers:**
- HTTP endpoints exposed by `Stewie.Api`
- Request/response JSON schemas
- Error response format
- Status codes and semantics

**What this does NOT cover:**
- Worker container I/O (see `CON-001`)
- Database schema
- Frontend routing

**Parties:**

| Role | Description |
|:-----|:------------|
| **Producer** | `Stewie.Api` (ASP.NET Core) |
| **Consumer** | `Stewie.Web` (React frontend), CLI tools, external callers |

---

## 2. Version & Stability

| Field | Value |
|:------|:------|
| Contract version | `1.4.0` |
| Stability | `EXPERIMENTAL` |
| Base URL | `http://localhost:5275` |
| Content-Type | `application/json` |
| Breaking change policy | MAJOR version bump; old routes kept for 1 minor version |

---

## 3. Endpoints — Current (Milestone 0)

### 3.1 POST /jobs/test

Triggers a test run: creates a Run, creates a Task, launches the dummy worker, ingests the result.

**Request:** No body required.

**Response (200 OK):**

```json
{
  "jobId": "uuid",
  "taskId": "uuid",
  "artifactId": "uuid",
  "status": "Completed",
  "summary": "Dummy worker executed successfully. Runtime contract verified.",
  "resultPayload": {
    "taskId": "uuid",
    "status": "success",
    "summary": "...",
    "filesChanged": [],
    "testsPassed": false,
    "errors": [],
    "notes": "...",
    "nextAction": "review"
  }
}
```

**Error responses:**

| Status | Condition |
|:-------|:---------|
| 200 with `status: "Failed"` | Container failed or result ingestion failed |
| 500 | Unhandled server error |

---

## 4. Endpoints — Phase 1 (Planned)

> These endpoints are implemented across Phase 1-3 sprints.

### 4.0 Authentication

> `/api/auth/*` and `/health` do NOT require authentication. All other endpoints require `Authorization: Bearer {jwt}` header.

| Method | Path | Description |
|:-------|:-----|:------------|
| `POST` | `/api/auth/register` | Register with invite code |
| `POST` | `/api/auth/login` | Login, receive JWT |

**POST /api/auth/register** request body:
```json
{
  "username": "string",
  "password": "string",
  "inviteCode": "string"
}
```

**POST /api/auth/login** request body:
```json
{
  "username": "string",
  "password": "string"
}
```

**Auth response (both endpoints):**
```json
{
  "token": "string (JWT)",
  "expiresAt": "ISO 8601 datetime",
  "user": {
    "id": "uuid",
    "username": "string",
    "role": "admin | user"
  }
}
```

### 4.0.1 Users

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/api/users/me` | Get current user profile |
| `PUT` | `/api/users/me/github-token` | Store encrypted GitHub PAT |
| `DELETE` | `/api/users/me/github-token` | Remove GitHub PAT |
| `GET` | `/api/users/me/github-status` | Check GitHub connection status |

**PUT /api/users/me/github-token** request body:
```json
{
  "token": "string (GitHub PAT)"
}
```

### 4.0.2 Invite Codes (admin only)

| Method | Path | Description |
|:-------|:-----|:------------|
| `POST` | `/api/invites` | Generate a new invite code |
| `GET` | `/api/invites` | List invite codes |

### 4.1 Projects

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/api/projects` | List all projects |
| `POST` | `/api/projects` | Create a new project (link existing or create GitHub repo) |
| `GET` | `/api/projects/{id}` | Get project by ID |

**POST /api/projects** request body:

```json
{
  "name": "string (required)",
  "repoUrl": "string | null",
  "createRepo": "boolean (default: false)",
  "repoName": "string | null",
  "isPrivate": "boolean (default: true)",
  "description": "string | null"
}
```

| Field | Type | Required | Description |
|:------|:-----|:--------:|:------------|
| `name` | `string` | ✅ | Human-readable project name |
| `repoUrl` | `string` | Conditional | Required when `createRepo` is `false`. URL of existing repo to link. |
| `createRepo` | `boolean` | ❌ | If `true`, create a new repo on the user's git platform. Default: `false`. |
| `repoName` | `string` | Conditional | Required when `createRepo` is `true`. Name for the new repo. |
| `isPrivate` | `boolean` | ❌ | Repo visibility. Default: `true` (private). Only used when `createRepo` is `true`. |
| `description` | `string` | ❌ | Repo description. Only used when `createRepo` is `true`. |

> **Validation:** If `createRepo` is `true`, the user must have a configured platform PAT (currently GitHub). Returns 400 if no PAT is configured.

### 4.2 Runs

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/api/jobs` | List all runs (filterable by project) |
| `POST` | `/api/jobs` | Create a new run (with task definition) |
| `GET` | `/api/jobs/{id}` | Get run by ID with tasks and artifacts |
| `POST` | `/api/jobs/test` | Trigger a test run (legacy, backward-compatible) |

**POST /api/jobs** request body:

```json
{
  "projectId": "uuid",
  "objective": "string",
  "scope": "string | null",
  "script": ["string"] | null,
  "acceptanceCriteria": ["string"] | null
}
```

| Field | Type | Required | Description |
|:------|:-----|:--------:|:------------|
| `projectId` | `uuid` | ✅ | Links to Project with repoUrl |
| `objective` | `string` | ✅ | What the worker should accomplish |
| `scope` | `string` | ❌ | Boundaries of the work |
| `script` | `string[]` | ❌ | Bash commands for script worker |
| `acceptanceCriteria` | `string[]` | ❌ | Conditions for success |

### 4.3 Tasks

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/api/tasks/{id}` | Get task by ID with artifacts |
| `GET` | `/api/jobs/{jobId}/tasks` | List tasks for a job |

### 4.4 Health

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/health` | Health check (no auth required) |

### 4.5 Events

| Method | Path | Description |
|:-------|:-----|:------------|
| `GET` | `/api/events` | List recent events (default limit 100, most recent first) |
| `GET` | `/api/events?entityType={type}&entityId={id}` | Filter events by entity |

**Query parameters:**

| Param | Type | Required | Description |
|:------|:-----|:--------:|:------------|
| `entityType` | `string` | No | Filter by entity type (e.g. "Run", "Task") |
| `entityId` | `uuid` | No | Filter by entity ID (requires `entityType`) |
| `limit` | `int` | No | Max results (default 100, max 500) |

---

## 5. Request/Response Schemas

### 5.1 Project

```json
{
  "id": "uuid",
  "name": "string",
  "repoUrl": "string",
  "repoProvider": "string | null",
  "createdAt": "ISO 8601 datetime"
}
```

| Field | Type | Description |
|:------|:-----|:------------|
| `repoProvider` | `string \| null` | Platform hosting the repo (e.g., `"github"`, `"gitlab"`). `null` for manually linked repos with unrecognized URLs. |

### 5.2 Run

```json
{
  "id": "uuid",
  "projectId": "uuid | null",
  "status": "Pending | Running | Completed | Failed",
  "branch": "string | null",
  "diffSummary": "string | null",
  "commitSha": "string | null",
  "pullRequestUrl": "string | null",
  "createdAt": "ISO 8601 datetime",
  "completedAt": "ISO 8601 datetime | null",
  "tasks": ["Task[]"]
}
```

### 5.3 Task

```json
{
  "id": "uuid",
  "jobId": "uuid",
  "role": "developer | tester | researcher",
  "status": "Pending | Running | Completed | Failed",
  "objective": "string",
  "scope": "string | null",
  "workspacePath": "string",
  "createdAt": "ISO 8601 datetime",
  "startedAt": "ISO 8601 datetime | null",
  "completedAt": "ISO 8601 datetime | null"
}
```

### 5.6 Artifact (Diff)

```json
{
  "id": "uuid",
  "taskId": "uuid",
  "type": "diff",
  "contentJson": {
    "diffStat": "string (git diff --stat output)",
    "diffPatch": "string (full git diff output)"
  },
  "createdAt": "ISO 8601 datetime"
}
```

### 5.4 Health

```json
{
  "status": "healthy",
  "version": "string",
  "timestamp": "ISO 8601 datetime"
}
```

### 5.5 Event

```json
{
  "id": "uuid",
  "entityType": "string (Run | Task)",
  "entityId": "uuid",
  "eventType": "string (RunCreated | RunStarted | RunCompleted | RunFailed | TaskCreated | TaskStarted | TaskCompleted | TaskFailed)",
  "payload": "string (JSON)",
  "timestamp": "ISO 8601 datetime"
}
```

---

## 6. Error Response Format

All errors follow a consistent structure per GOV-004:

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": {}
  }
}
```

| Error Code | HTTP Status | Description |
|:-----------|:-----------|:------------|
| `NOT_FOUND` | 404 | Resource does not exist |
| `VALIDATION_ERROR` | 400 | Invalid request body |
| `INTERNAL_ERROR` | 500 | Unhandled server error |
| `CONTAINER_FAILED` | 500 | Worker container exited with non-zero code |

---

## 7. Performance Requirements

| Metric | Requirement |
|:-------|:------------|
| p95 latency (non-run endpoints) | < 200ms |
| p95 latency (run execution) | N/A (long-running, async) |
| Timeout | 30s for non-run endpoints |

---

## 8. Change Protocol

> **This contract is immutable without Human approval.**

To propose a contract change:
1. Developer or Tester opens `60_EVOLUTION/EVO-NNN.md`
2. Architect reviews and drafts the contract update
3. Human approves
4. Version is bumped, all consuming agents notified

---

## 9. Verification Checklist

- [ ] All listed endpoints return expected status codes
- [ ] Response bodies match documented schemas
- [ ] Error responses use the standardized error format (§6)
- [ ] Health endpoint returns 200 with version info
- [ ] Content-Type header is `application/json` on all responses
