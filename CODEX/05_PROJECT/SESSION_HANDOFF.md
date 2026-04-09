# Session Handoff: Stewie Project

> **Date:** 2026-04-09
> **Branch State:** `main` (Clean, tested, fully merged)
> **Prior Objective:** GitHub Integration + User System (`JOB-004`)

## 1. What Just Landed (`JOB-004`)
We successfully completed the **GitHub Integration + Auth sprint** with Dev Agents A & B.
- **Backend:** Integrated JWT-based authentication (Bcrypt hashing, 24-hr sessions) and AES-256-CBC encrypted local storage for GitHub Personal Access Tokens (PATs).
- **Frontend:** Built `AuthContext` with login and invite-only registration UI. Implemented a user Settings page to securely store the GitHub PAT, and updated the PR links in the Run Detail view.
- **Octokit.net:** Implemented `IGitHubService` for repository creation, PR creation, and automated branch pushes on Run completion.
- **Testing:** Fixed 13 failing integration tests by adding a `GetAuthToken()` helper to `StewieWebApplicationFactory` and injecting Auth headers. **40 out of 40 tests are currently passing.**

## 2. Critical Context for Next Session
When opening the project, be aware of the following newly implemented governance:
- **EVO-001 / Rule 9:** ALL agents MUST use the **File-Redirect workflow** for any heavy processes (`dotnet test`, `dotnet build`, `npm run build`), redirecting output to `/tmp/` and reading it with `tail`/`grep`. Do not use bash pipe chains for long-running processes or it will trigger a phantom terminal hang.
- **Test Factory:** The `StewieWebApplicationFactory` sets up in-memory SQLite and automatically injects test AES credentials and JWT signing keys. The test user's credentials are `admin` / `Admin@Stewie123!`.

## 3. Next Steps (Where We Left Off)
The user has requested that **Repository Automation / Repo Creation** be the highest priority for the next sprint, aligning with the "Real Repo Interaction" phase of `PRJ-001`.
- **Primary Goal:** Since `IGitHubService` now possesses `CreateRepositoryAsync(owner, name, ...)`, we need to wire this into the `ProjectsController` so that generating a Project in Stewie provisions the corresponding remote git repository.
- **Secondary Goal:** We need to update Stewie's worker pipeline to clone this repository, run tasks iteratively inside it, and invoke the automated PR pushing logic successfully.

**To resume work**, proceed immediately into planning `JOB-005: Repository Automation`.
