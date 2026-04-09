---
id: EVO-001
title: "Safe Command File-Redirect Pattern for Long-Running Builds"
type: evolution
status: PROPOSED
owner: coder
agents: [coder, architect]
tags: [governance, workflow, devex]
related: [GOV-003, GOV-008]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** Proposes adding a mandatory "file-redirect" execution pattern to `.agent/workflows/safe_commands.md` for all `dotnet build`, `dotnet test`, and other commands whose output is piped. Eliminates the pipe-blocking hang that has repeatedly caused agent stalls across JOB-002, JOB-003, and JOB-004.

# EVO-001: Safe Command File-Redirect Pattern

## 1. Problem Statement

Across sprints JOB-002 through JOB-004, Dev Agent B encountered a recurring command hang with the following root cause chain:

1. Agent runs `dotnet build … 2>&1 | grep … | tail -5`
2. `dotnet build` spends 30–90 seconds on NuGet package restore before producing build output
3. During restore, **no output matches the grep filter**, so grep blocks waiting for input
4. The `WaitMsBeforeAsync` threshold (10000ms) expires while grep is still blocking
5. The command goes async (background)
6. `command_status` polls return `RUNNING` with no output — even after the build has completed
7. Agent exceeds Rule 7 (max 2 polls) and is stuck with a zombie process

**Impact:** Each occurrence wastes 2–5 minutes and requires user intervention to cancel. This happened **at least 4 times** in 3 sprints, making it the single most frequent agent failure mode.

## 2. Root Cause Analysis

The failure occurs specifically when **pipe chains include pattern-matching filters** (`grep`, `awk`, `sed`) on commands whose output is **bursty** (no output for long periods, then a burst at the end). The pipe semantics of Unix mean:

- `tail -N` waits for EOF → **safe** (always terminates when upstream finishes)
- `grep PATTERN` waits for more input → **unsafe** when upstream has long silent periods
- `command > file; echo "EXIT=$?"` redirects to file → **always safe** (no pipe dependency)

## 3. Proposed Change

Add a new **Rule 9** to `.agent/workflows/safe_commands.md`:

---

### Rule 9: Use File-Redirect Pattern for Build and Test Commands

Commands that may produce large output or have long silent periods (NuGet restore, npm install, Docker build) **MUST** use the file-redirect pattern instead of pipe chains with filters.

**❌ BANNED — Pipe chains with grep/awk on build commands:**
```bash
# These can hang when build has silent restore phases
dotnet build src/Foo.csproj 2>&1 | grep -E "(error|warning)" | tail -5
dotnet test src/Foo.csproj 2>&1 | grep "(Passed|Failed)"
npm run build 2>&1 | grep -i error
```

**✅ REQUIRED — File-redirect then read:**
```bash
# Step 1: Run the command, redirect ALL output to a file, echo exit code
dotnet build src/Foo.csproj > /tmp/stewie-build.txt 2>&1; echo "EXIT=$?"

# Step 2: Read the results from the file
tail -5 /tmp/stewie-build.txt              # Quick summary
grep -c "error CS" /tmp/stewie-build.txt   # Count errors
grep "Build succeeded" /tmp/stewie-build.txt  # Verify success
```

**Why this works:**
- The redirect `>` never blocks — output goes to disk immediately
- The `echo "EXIT=$?"` guarantees the command completes synchronously
- The file read is a separate, instant command (never hangs)
- If the build itself is slow, the `echo` still prints when it finishes

**When to use this pattern:**
| Command | Use file-redirect? |
|---|---|
| `dotnet build` | **YES** |
| `dotnet test` | **YES** |
| `npm run build` / `npx vite build` | **YES** (if also piping through grep) |
| `git status`, `git log` | No — fast, predictable output |
| `git push/pull/fetch` | No — use `GIT_TERMINAL_PROMPT=0` per Rule 2 |

**Cleanup:** Always delete temp files after reading:
```bash
rm -f /tmp/stewie-build.txt
```

---

## 4. Additional Recommendation: Update Rule 3 Table

Add the file-redirect pattern to the WaitMsBeforeAsync reference table:

| Command type | WaitMsBeforeAsync | Notes |
|---|---|---|
| `cmd > file; echo "EXIT=$?"` | 10000 | File-redirect pattern — exit code prints quickly |
| `tail -5 /tmp/file.txt` | 3000 | Reading redirect output — instant |

## 5. Scope of Change

| File | Change |
|---|---|
| `.agent/workflows/safe_commands.md` | Add Rule 9, update Rule 3 table |
| `CODEX/80_AGENTS/AGT-002_Developer_Agent.md` | Reference Rule 9 in terminal safety section |
| `CODEX/80_AGENTS/AGT-002-B_Dev_Agent_B_Boot.md` | Add note about file-redirect in governance checklist |

## 6. Backward Compatibility

- **Non-breaking.** Existing pipe-chain commands that use `tail` (no grep) remain valid.
- Agents already following Rule 7/8 will naturally benefit from this pattern.
- The proposal codifies what Dev Agent B already discovered empirically in JOB-004.

## 7. Evidence

Successful executions using file-redirect in JOB-004:

```
# Build — completed in 2.48s, EXIT=0
dotnet build src/Stewie.Tests/Stewie.Tests.csproj > .build-output.txt 2>&1; echo "EXIT=$?"
tail -5 .build-output.txt  →  "0 Error(s)\nTime Elapsed 00:00:02.48"

# Test — completed in 3.04s, EXIT=0  
dotnet test src/Stewie.Tests/Stewie.Tests.csproj --no-build > .test-output.txt 2>&1; echo "EXIT=$?"
grep -E "(Total|Passed|Failed)" .test-output.txt  →  "Total tests: 40\nPassed: 40"
```

Both commands returned results on the first attempt with zero hangs.

## 8. Decision Requested

**Architect:** Please review and either:
1. **Approve** — I'll submit the PR to update `safe_commands.md` with Rule 9
2. **Approve with modifications** — Specify any changes to the pattern
3. **Reject** — Provide alternative approach

---

*Submitted by: Dev Agent B, JOB-004*
*Incident count: 4+ across JOB-002, JOB-003, JOB-004*
*Severity: P2 — recurring productivity blocker for all developer agents*
