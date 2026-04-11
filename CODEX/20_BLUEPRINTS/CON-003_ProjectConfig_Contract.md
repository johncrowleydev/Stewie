---
id: CON-003
title: "Project Configuration Contract — stewie.json"
type: reference
status: DRAFT
owner: architect
agents: [all]
tags: [standards, specification, project-management, governance]
related: [CON-001, CON-002, GOV-003]
created: 2026-04-10
updated: 2026-04-10
version: 1.1.0
---

> **BLUF:** This contract defines the `stewie.json` project configuration file format. When present in a repository root, it replaces file-based heuristic stack detection and provides explicit build/test commands, governance settings, and path constraints to worker containers.

# Project Configuration Contract — stewie.json

> **"Explicit is better than implicit."**

---

## 1. Contract Scope

**What this covers:**
- The JSON schema for `stewie.json`
- Field definitions, types, constraints, and defaults
- Relationship to CON-001 (`task.json` injection) and CON-002 (API)

**What this does NOT cover:**
- Governance rule definitions (see GOV-003)
- Worker container behavior (see CON-001)
- API endpoints (see CON-002)

**Parties:**

| Role | Description |
|:-----|:------------|
| **Author** | Human (project owner) or Architect Agent |
| **Consumer** | Orchestrator (`WorkspaceService`, `ProjectConfigService`), Governance Worker |

---

## 2. Version & Stability

| Field | Value |
|:------|:------|
| Contract version | `1.0.0` |
| Stability | `EXPERIMENTAL` |
| Breaking change policy | MAJOR version bump for field removal/type change |
| File location | Repository root: `stewie.json` |

---

## 3. File Format

### 3.1 Location

`stewie.json` MUST be placed in the root directory of the repository. If the file is absent, the system falls back to heuristic stack detection (existing behavior).

### 3.2 Schema

```json
{
  "version": "1.0",
  "stack": "dotnet",
  "language": "csharp",
  "buildCommand": "dotnet build",
  "testCommand": "dotnet test",
  "governance": {
    "rules": "all",
    "warningsBlockAcceptance": false,
    "maxRetries": 2
  },
  "paths": {
    "source": ["src/"],
    "tests": ["tests/"],
    "forbidden": ["CODEX/", "docs/"]
  },
  "architectMode": "plan_first",
  "defaultRuntime": "opencode",
  "defaultModel": "google/gemini-2.0-flash"
}
```

### 3.3 Fields

| Field | Type | Required | Default | Description | Constraints |
|:------|:-----|:--------:|:--------|:------------|:------------|
| `version` | `string` | ❌ | `"1.0"` | Config schema version | Semantic version |
| `stack` | `string` | ✅ | `""` | Technology stack identifier | `"dotnet"`, `"node"`, `"python"`, or custom |
| `language` | `string` | ❌ | `""` | Primary programming language | `"csharp"`, `"typescript"`, `"python"`, or custom |
| `buildCommand` | `string` | ❌ | `null` | Shell command to build the project | Valid shell command |
| `testCommand` | `string` | ❌ | `null` | Shell command to run tests | Valid shell command |
| `governance` | `object` | ❌ | `null` | Governance configuration | See §3.4 |
| `paths` | `object` | ❌ | `null` | Path restrictions | See §3.5 |
| `architectMode` | `string` | ❌ | `"plan_first"` | Architect operating mode | `"plan_first"` or `"auto_execute"` |
| `defaultRuntime` | `string` | ❌ | `"opencode"` | Default agent runtime | `"opencode"`, `"stub"`, or custom |
| `defaultModel` | `string` | ❌ | `"google/gemini-2.0-flash"` | Default LLM model | Provider/model identifier |

### 3.4 Governance Configuration

| Field | Type | Required | Default | Description |
|:------|:-----|:--------:|:--------|:------------|
| `governance.rules` | `string` | ❌ | `"all"` | `"all"` to enforce all rules, or future: explicit list |
| `governance.warningsBlockAcceptance` | `boolean` | ❌ | `false` | If `true`, warning-severity failures also block acceptance |
| `governance.maxRetries` | `integer` | ❌ | `2` | Max governance retry attempts before permanent failure |

### 3.5 Path Configuration

| Field | Type | Required | Default | Description |
|:------|:-----|:--------:|:--------|:------------|
| `paths.source` | `string[]` | ❌ | `[]` | Source directories the worker should focus on |
| `paths.tests` | `string[]` | ❌ | `[]` | Test directories for validation |
| `paths.forbidden` | `string[]` | ❌ | `[]` | Directories the worker must not modify |

---

## 4. Injection into task.json

When the orchestrator detects `stewie.json` in a cloned repository, it:

1. Parses the file using `ProjectConfigService.LoadFromRepo(repoPath)`
2. Injects the parsed `StewieProjectConfig` into `TaskPacket.ProjectConfig`
3. Serializes to `task.json` as the `"projectConfig"` field

**CON-001 integration:** The `projectConfig` field is optional in `task.json` (CON-001 v1.6.0). Workers MUST handle it being `null` (no config file).

### 4.1 task.json Example with projectConfig

```json
{
  "taskId": "...",
  "jobId": "...",
  "role": "developer",
  "objective": "...",
  "projectConfig": {
    "version": "1.0",
    "stack": "dotnet",
    "language": "csharp",
    "buildCommand": "dotnet build src/MyProject.sln",
    "testCommand": "dotnet test src/MyProject.Tests/",
    "governance": {
      "rules": "all",
      "warningsBlockAcceptance": false,
      "maxRetries": 2
    },
    "paths": {
      "source": ["src/"],
      "tests": ["tests/"],
      "forbidden": ["CODEX/"]
    }
  }
}
```

---

## 5. Error Behavior

| Scenario | Behavior |
|:---------|:---------|
| `stewie.json` missing | `ProjectConfigService` returns `null`. Heuristic detection used. No error. |
| `stewie.json` is invalid JSON | `JsonException` thrown with descriptive message and file path. |
| `stewie.json` is empty `{}` | Parsed with all defaults. `stack` will be empty string. |
| Unknown fields in `stewie.json` | Ignored (forward-compatible). |
| Comments in JSON | Supported (parser enables `ReadCommentHandling.Skip`). |
| Trailing commas | Supported (parser enables `AllowTrailingCommas`). |

---

## 6. Stack Values

The following stack values are recognized by the governance worker:

| Stack | Language | Default Build | Default Test |
|:------|:---------|:-------------|:-------------|
| `dotnet` | `csharp` | `dotnet build` | `dotnet test` |
| `node` | `typescript` | `npm run build` | `npm test` |
| `python` | `python` | `pip install -e .` | `pytest` |

Custom stack values are accepted as-is. The governance worker uses `buildCommand`/`testCommand` if provided, otherwise falls back to defaults based on `stack`.

---

## 7. C# Schema

```csharp
public class StewieProjectConfig
{
    public string Version { get; set; } = "1.0";
    public string Stack { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? BuildCommand { get; set; }
    public string? TestCommand { get; set; }
    public GovernanceConfig? Governance { get; set; }
    public PathConfig? Paths { get; set; }
}

public class GovernanceConfig
{
    public string Rules { get; set; } = "all";
    public bool WarningsBlockAcceptance { get; set; } = false;
    public int MaxRetries { get; set; } = 2;
}

public class PathConfig
{
    public List<string> Source { get; set; } = [];
    public List<string> Tests { get; set; } = [];
    public List<string> Forbidden { get; set; } = [];
}
```

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

- [ ] `ProjectConfigService.LoadFromRepo` returns `null` for missing file
- [ ] Valid `stewie.json` parses to `StewieProjectConfig` with correct values
- [ ] Invalid JSON throws `JsonException` with file path in message
- [ ] All fields have documented defaults
- [ ] `projectConfig` field appears in `task.json` when `stewie.json` exists
- [ ] Workers handle `null` `projectConfig` without error
