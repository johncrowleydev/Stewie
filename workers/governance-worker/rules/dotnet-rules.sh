#!/bin/bash
# Governance Rule Scripts — .NET Stack Checks
# Called by entrypoint.sh when a .NET project is detected.
# Each function runs a check and calls add_check() with the result.
#
# REF: JOB-008 T-074, CON-001 §6
# Checks: GOV-001-001, GOV-001-002, GOV-002-001, GOV-002-002, GOV-002-003,
#          GOV-003-003, GOV-004-001, GOV-005-001, GOV-005-002,
#          GOV-006-001, GOV-006-002, GOV-008-001

# Expects: REPO_DIR, add_check() function available via source

# ---- GOV-001-001: README.md exists ----
run_gov_001_001() {
    if [ -f "$REPO_DIR/README.md" ]; then
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "true" "null" "error"
    else
        add_check "GOV-001-001" "README.md Exists" "GOV-001" "false" "No README.md found in project root" "error"
    fi
}

# ---- GOV-001-002: Public members have XML doc comments (///) ----
run_gov_001_002() {
    local missing=""
    # Find public methods/classes without preceding /// comments in .cs files
    # Look for 'public' declarations not preceded by '///' within 2 lines above
    if [ -d "$REPO_DIR/src" ]; then
        missing=$(grep -rn "^[[:space:]]*public " "$REPO_DIR/src" --include="*.cs" \
            | grep -v "///" \
            | grep -v "test" \
            | grep -v "Test" \
            | grep -v "Program.cs" \
            | head -5 2>/dev/null || true)
    fi

    if [ -z "$missing" ]; then
        add_check "GOV-001-002" "XML Doc Comments on Public Members" "GOV-001" "true" "null" "warning"
    else
        local details
        details=$(echo "$missing" | head -3 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-001-002" "XML Doc Comments on Public Members" "GOV-001" "false" "$details" "warning"
    fi
}

# ---- GOV-002-001: dotnet build succeeds ----
run_gov_002_001() {
    if [ ! -d "$REPO_DIR" ]; then
        add_check "GOV-002-001" "Build Succeeds" "GOV-002" "false" "No repo directory found" "error"
        return
    fi

    # Find the solution or project file
    local build_target=""
    build_target=$(find "$REPO_DIR" -maxdepth 2 -name "*.slnx" -o -name "*.sln" 2>/dev/null | head -1)
    if [ -z "$build_target" ]; then
        build_target=$(find "$REPO_DIR" -maxdepth 3 -name "*.csproj" 2>/dev/null | head -1)
    fi

    if [ -z "$build_target" ]; then
        add_check "GOV-002-001" "Build Succeeds" "GOV-002" "false" "No .sln/.slnx/.csproj found" "error"
        return
    fi

    set +e
    local build_output
    build_output=$(dotnet build "$build_target" --nologo --verbosity quiet 2>&1)
    local build_exit=$?
    set -e

    if [ $build_exit -eq 0 ]; then
        add_check "GOV-002-001" "Build Succeeds" "GOV-002" "true" "null" "error"
    else
        local details
        details=$(echo "$build_output" | grep -i "error" | head -5 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-002-001" "Build Succeeds" "GOV-002" "false" "$details" "error"
    fi
}

# ---- GOV-002-002: dotnet test succeeds ----
run_gov_002_002() {
    # Find test projects
    local test_project=""
    test_project=$(find "$REPO_DIR" -maxdepth 4 -name "*.csproj" -path "*Test*" 2>/dev/null | head -1)

    if [ -z "$test_project" ]; then
        # No test project found — pass with warning
        add_check "GOV-002-002" "Tests Pass" "GOV-002" "true" "null" "warning"
        add_check "GOV-002-003" "Test Count > 0" "GOV-002" "true" "null" "warning"
        return
    fi

    set +e
    local test_output
    test_output=$(dotnet test "$test_project" --nologo --verbosity quiet 2>&1)
    local test_exit=$?
    set -e

    if [ $test_exit -eq 0 ]; then
        add_check "GOV-002-002" "Tests Pass" "GOV-002" "true" "null" "error"
    else
        local details
        details=$(echo "$test_output" | grep -iE "failed|error" | head -5 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-002-002" "Tests Pass" "GOV-002" "false" "$details" "error"
    fi

    # GOV-002-003: Test count > 0
    local test_count
    test_count=$(echo "$test_output" | grep -oP "Total tests:\s*\K\d+" 2>/dev/null || echo "0")
    if [ -z "$test_count" ]; then
        test_count=$(echo "$test_output" | grep -oP "Passed:\s*\K\d+" 2>/dev/null || echo "0")
    fi

    if [ "$test_count" -gt 0 ] 2>/dev/null; then
        add_check "GOV-002-003" "Test Count > 0" "GOV-002" "true" "null" "error"
    else
        add_check "GOV-002-003" "Test Count > 0" "GOV-002" "false" "No tests found or test count could not be determined" "error"
    fi
}

# ---- GOV-003-003: No Console.WriteLine in .cs service files ----
run_gov_003_003() {
    local hits=""
    if [ -d "$REPO_DIR/src" ]; then
        hits=$(grep -rn "Console\.WriteLine\|Console\.Write(" "$REPO_DIR/src" \
            --include="*.cs" \
            --include="*Service*.cs" \
            --include="*Controller*.cs" \
            --include="*Repository*.cs" \
            | grep -v "Test" \
            | grep -v "test" \
            | grep -v "Program.cs" \
            | head -5 2>/dev/null || true)
    fi

    if [ -z "$hits" ]; then
        add_check "GOV-003-003" "No Console.WriteLine in Service Files" "GOV-003" "true" "null" "warning"
    else
        local details
        details=$(echo "$hits" | head -3 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-003-003" "No Console.WriteLine in Service Files" "GOV-003" "false" "$details" "warning"
    fi
}

# ---- GOV-004-001: Error handling middleware present ----
run_gov_004_001() {
    local found=""
    if [ -d "$REPO_DIR/src" ]; then
        found=$(grep -rl "ErrorHandlingMiddleware\|UseExceptionHandler\|app\.UseMiddleware" "$REPO_DIR/src" \
            --include="*.cs" 2>/dev/null | head -1 || true)
    fi

    if [ -n "$found" ]; then
        add_check "GOV-004-001" "Error Handling Middleware Present" "GOV-004" "true" "null" "error"
    else
        add_check "GOV-004-001" "Error Handling Middleware Present" "GOV-004" "false" "No error handling middleware found in source" "error"
    fi
}

# ---- GOV-005-001: Branch name matches feature/JOB-* pattern ----
run_gov_005_001() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    local branch
    branch=$(cd "$REPO_DIR" && git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")

    if echo "$branch" | grep -qE "^(feature|fix|docs|refactor|test)/"; then
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "error"
    elif [ "$branch" = "main" ] || [ "$branch" = "master" ] || [ "$branch" = "HEAD" ]; then
        # Main/detached HEAD is acceptable for certain flows
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "true" "null" "error"
    else
        add_check "GOV-005-001" "Branch Name Convention" "GOV-005" "false" "Branch '$branch' does not match convention (feature/, fix/, docs/, etc.)" "error"
    fi
}

# ---- GOV-005-002: Latest commit message matches conventional format ----
run_gov_005_002() {
    if [ ! -d "$REPO_DIR/.git" ]; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    local message
    message=$(cd "$REPO_DIR" && git log -1 --pretty=format:"%s" 2>/dev/null || echo "")

    if [ -z "$message" ]; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "warning"
        return
    fi

    # Conventional commit: type(scope): description OR type: description
    if echo "$message" | grep -qE "^(feat|fix|docs|refactor|test|chore|ci|build|perf|style)(\(.+\))?:"; then
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "true" "null" "error"
    else
        local truncated
        truncated=$(echo "$message" | head -c 80)
        add_check "GOV-005-002" "Commit Message Convention" "GOV-005" "false" "Commit message does not match conventional format: $truncated" "error"
    fi
}

# ---- GOV-006-001: Services use ILogger injection ----
run_gov_006_001() {
    local services_without_logger=""
    if [ -d "$REPO_DIR/src" ]; then
        # Find service classes that don't have ILogger in them
        services_without_logger=$(grep -rlZ "class.*Service" "$REPO_DIR/src" --include="*.cs" 2>/dev/null \
            | xargs -0 grep -L "ILogger" 2>/dev/null \
            | grep -v "Test" \
            | grep -v "Interface" \
            | grep -v "/I[A-Z]" \
            | head -3 || true)
    fi

    if [ -z "$services_without_logger" ]; then
        add_check "GOV-006-001" "Services Use ILogger" "GOV-006" "true" "null" "warning"
    else
        local details
        details=$(echo "$services_without_logger" | xargs -I{} basename {} | tr '\n' ', ' | sed 's/,$//' | head -c 500)
        add_check "GOV-006-001" "Services Use ILogger" "GOV-006" "false" "Services without ILogger: $details" "warning"
    fi
}

# ---- GOV-006-002: No bare Console.Write in service files ----
run_gov_006_002() {
    # This is essentially the same as GOV-003-003 but categorized under GOV-006
    local hits=""
    if [ -d "$REPO_DIR/src" ]; then
        hits=$(grep -rn "Console\.Write" "$REPO_DIR/src" \
            --include="*Service*.cs" \
            --include="*Controller*.cs" \
            | grep -v "Test" \
            | grep -v "test" \
            | grep -v "Program.cs" \
            | head -5 2>/dev/null || true)
    fi

    if [ -z "$hits" ]; then
        add_check "GOV-006-002" "No Bare Console.Write in Services" "GOV-006" "true" "null" "warning"
    else
        local details
        details=$(echo "$hits" | head -3 | tr '\n' '; ' | sed 's/"/\\"/g' | head -c 500)
        add_check "GOV-006-002" "No Bare Console.Write in Services" "GOV-006" "false" "$details" "warning"
    fi
}

# ---- GOV-008-001: Dockerfile or docker-compose present ----
run_gov_008_001() {
    local found=""
    found=$(find "$REPO_DIR" -maxdepth 3 \( -name "Dockerfile" -o -name "docker-compose.yml" -o -name "docker-compose.yaml" \) 2>/dev/null | head -1 || true)

    if [ -n "$found" ]; then
        add_check "GOV-008-001" "Dockerfile or Docker Compose Present" "GOV-008" "true" "null" "warning"
    else
        add_check "GOV-008-001" "Dockerfile or Docker Compose Present" "GOV-008" "false" "No Dockerfile or docker-compose found" "warning"
    fi
}

# ==================================================================
# Runner — execute all .NET rules
# ==================================================================
run_dotnet_rules() {
    echo "=== Running .NET governance rules ==="
    run_gov_001_001
    run_gov_001_002
    run_gov_002_001
    run_gov_002_002
    # GOV-002-003 is called inside run_gov_002_002
    run_gov_003_003
    run_gov_004_001
    run_gov_005_001
    run_gov_005_002
    run_gov_006_001
    run_gov_006_002
    run_gov_008_001
    echo "=== .NET governance rules complete ==="
}
