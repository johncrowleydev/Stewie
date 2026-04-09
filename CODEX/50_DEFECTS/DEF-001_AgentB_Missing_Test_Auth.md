---
id: DEF-001
title: "Agent B failed to update existing integration tests for Auth"
type: reference
status: OPEN
owner: coder
agents: [coder, tester]
tags: [defect, testing]
related: [SPR-004, VER-005]
created: 2026-04-09
updated: 2026-04-09
version: 1.0.0
---

> **BLUF:** 13 Integration tests fail with a 401 Unauthorized status because the auth headers were not added to existing tests and the requested `GetAuthToken()` helper method was not created in `StewieWebApplicationFactory`, violating T-047 requirements.

# Defect Report: Agent B failed to update existing integration tests for Auth

## 1. Summary

| Field | Value |
|:------|:------|
| **Priority** | P0 |
| **Severity** | 3-MAJOR |
| **Status** | OPEN |
| **Discovered By** | architect |
| **Discovered During** | Sprint Audit VER-005 |
| **Component** | `Stewie.Tests` |
| **Branch** | `feature/SPR-004-frontend-tests` |

## 2. Description
In SPR-004, Dev Agent B was tasked with implementing T-047 which includes:
- Update `StewieWebApplicationFactory` to support auth:
  - Auto-seed a test user + invite code
  - Provide helper to generate test JWT: `factory.GetAuthToken()`
  - Update all existing tests to include auth header

During the sprint audit `/audit_sprint`, `dotnet test` returned 13 failures across `ProjectsControllerTests`, `RunsControllerTests`, and `RunCreationTests`. The logs demonstrate that these endpoints are returning 401 Unauthorized instead of their expected 200/201/404 HTTP payloads.

Agent B skipped refactoring the old tests. 

## 3. Reproduction Steps
1. Run `dotnet test src/Stewie.Tests/Stewie.Tests.csproj`
2. Observer 13 test failures on all previously existing controller calls.

## 4. Remediation Plan
Agent B must checkout `feature/SPR-004-frontend-tests` and:
1. Implement `public string GetAuthToken()` in `StewieWebApplicationFactory.cs` returning a signed JWT using the `Stewie__JwtSecret` value (`"test-jwt-secret-minimum-32-characters-long!!"`) and admin test credentials.
2. In all tests that test endpoints which are now protected by `[Authorize]` (essentially all endpoint hits inside `ProjectsControllerTests`, `RunsControllerTests`, and `RunCreationTests`), manually add the generated JWT as a Bearer token in the request headers on `_client`.
3. Verify that all 40 tests execute successfully with zero failures.
