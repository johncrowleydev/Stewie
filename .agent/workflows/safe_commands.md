---
description: Mandatory rules for running shell commands safely without hanging. ALL agents must follow these rules for every command.
---

# Safe Command Execution — Anti-Hang Rules

> **ALL agents MUST follow these rules.** Violations cause zombie processes that block the system.

## Rule 1: NEVER Walk the Full Repo Tree

The Stewie project has heavy directories (`node_modules`, `.git`, `bin`, `obj`). Walking them causes multi-minute hangs.

**❌ BANNED:**
```bash
# Never do this — hangs for 10+ minutes
find . -name "*.py" -exec python3 -c "..." {} \;
python3 -c "for root, dirs, files in os.walk('.')..."
ruff check .
```

**✅ REQUIRED: Scope to changed files or specific directories:**
```bash
# Use git to get only the files you care about
git diff --name-only HEAD~1 -- '*.py' | xargs python3 -m py_compile
git diff --name-only main -- '*.py' | xargs -I{} ruff check {}

# Or scope to specific directories
dotnet build src/Stewie.Api/Stewie.Api.csproj
dotnet build src/Stewie.Domain/Stewie.Domain.csproj
```

## Rule 2: Set GIT_TERMINAL_PROMPT=0 for All Git Network Commands

Prevents git from ever blocking on an interactive credential/passphrase prompt.

```bash
# Always prefix git network commands:
GIT_TERMINAL_PROMPT=0 git fetch origin
GIT_TERMINAL_PROMPT=0 git push origin main
GIT_TERMINAL_PROMPT=0 git pull origin main
```

Local-only git commands (status, log, diff, branch, merge, commit) don't need this.

## Rule 3: Use Reasonable WaitMsBeforeAsync Values

| Command type | WaitMsBeforeAsync |
|---|---|
| `git status`, `git log`, `git branch` | 3000 |
| `git fetch`, `git push`, `git pull` | 10000 |
| `python3 -m py_compile <file>` | 5000 |
| `python3 -c "import ast; ..."` (AST check) | 5000 |
| `pytest` (single file) | 10000 |
| `pytest` (full suite) | 10000 (will go async) |
| Any `os.walk` or `find` on repo root | **BANNED** |
| `dotnet build src/Stewie.Api/` | 10000 (full build) |
| `cmd > file; echo "EXIT=$?"` | 10000 (File-redirect pattern) |
| `head / tail -n file.txt` | 3000 (Reading redirect output) |

## Rule 4: Kill Before Re-running

If a command hung and you need to retry, **always kill the old one first**:
```
send_command_input(CommandId=..., Terminate=true)
```
Then wait before retrying. Never leave zombie processes.

## Rule 5: Syntax Checking — Use Targeted Builds

Full solution builds can be slow. Scope to the project you changed:

```bash
# ✅ FAST: Build a single project
dotnet build src/Stewie.Domain/Stewie.Domain.csproj

# ❌ SLOW: Build entire solution when you only changed one file
dotnet build src/Stewie.slnx
```

## Rule 6: Use fd/ripgrep Instead of find/grep

```bash
# Use fd (respects .gitignore, skips .venv/.git automatically)
fd -e py --max-depth 3 src/ | xargs python3 -m py_compile

# Use rg instead of grep
rg "some_pattern" src/Stewie.Domain/
```

## Rule 7: NEVER Poll command_status More Than Twice

The terminal metadata can show commands as "running" even after completion. This is a **phantom hang**.

**❌ BANNED:**
```
command_status(id, wait=30)  → "RUNNING" 
command_status(id, wait=60)  → "RUNNING"
command_status(id, wait=120) → "RUNNING"  ← you are stuck in a loop!
```

**✅ REQUIRED: Max 2 polls, then verify directly:**
```
command_status(id, wait=10)  → "RUNNING, no output"
command_status(id, wait=15)  → "RUNNING, no output"
# STOP POLLING. Run a new verification command:
run_command("git log --oneline -1")   # Did the commit happen?
run_command("python3 -c 'import ast; ...'")  # Does it compile?
```

## Rule 8: Verify Outcomes, Don't Trust Process Status

Always verify the **result** of an operation rather than its **process status**:

| Instead of checking... | Verify by running... |
|---|---|
| "Is git commit still running?" | `git log --oneline -1` |
| "Did the file compile?" | `dotnet build src/Stewie.Api/Stewie.Api.csproj` |
| "Did tests pass?" | Check the test report or re-run a single test |
| "Is the module importable?" | `dotnet build` the relevant project |

## Rule 9: Use File-Redirect Pattern for Build and Test Commands

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

**Cleanup:** Always delete temp files after reading:
```bash
rm -f /tmp/stewie-build.txt
```
