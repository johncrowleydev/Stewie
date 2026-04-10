---
id: JOB-015
title: "Job 015 — Migrate Stewie.Web from csproj to esproj"
type: how-to
status: OPEN
owner: architect
agents: [coder]
tags: [project-management, job, workflow, phase-5a, tooling, infrastructure]
related: [PRJ-001, GOV-008]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** Modernize the frontend project file by converting `Stewie.Web.csproj` (which uses the legacy SpaProxy middleware) into a native `Stewie.Web.esproj` (JavaScript Project System). 

# Job 015 — Migrate Stewie.Web to esproj

---

## 1. Context

The frontend project currently uses `Stewie.Web.csproj` as a wrapper around the React/Vite app. This legacy pattern uses `Microsoft.AspNetCore.SpaProxy` to intercept HTTP requests and forward them to a background `npm run dev` task. 

Modern .NET tooling separates frontend and backend lifecycles using `.esproj` files, which correctly map to the JavaScript Project System in Visual Studio and natively integrate `npm install`/`npm build` into MSBuild without needing the SpaProxy shim.

---

## 2. Agent Assignment

| Agent | Territory | Branch |
|:------|:----------|:-------|
| Dev A | Solution file, csproj deletion, esproj creation | `feature/JOB-015-esproj` |

**Only one agent required for this configuration task.**

---

## 3. Dependencies

- Builds on `main`. Can be run at any time.

---

## 4. Tasks

### Dev A — Tooling Migration

#### T-151: Delete Legacy .csproj

**Delete** `src/Stewie.Web/Stewie.Web.csproj`.

*(Note: Keep all files in `src/Stewie.Web/ClientApp` untouched, as this is the actual frontend code).*

---

#### T-152: Create Stewie.Web.esproj

**Create** `src/Stewie.Web/Stewie.Web.esproj` with standard modern configuration:

```xml
<Project Sdk="Microsoft.VisualStudio.JavaScript.Sdk/1.0.1738743">
  <PropertyGroup>
    <StartupCommand>npm run dev</StartupCommand>
    <JavaScriptTestRoot>ClientApp\</JavaScriptTestRoot>
    <JavaScriptTestFramework>Jest</JavaScriptTestFramework>
    <!-- Allows the build (or compile) script located on package.json to run on Build -->
    <ShouldRunBuildScript>false</ShouldRunBuildScript>
    <!-- Folder where production build objects will be placed -->
    <BuildOutputFolder>$(MSBuildProjectDirectory)\ClientApp\dist</BuildOutputFolder>
  </PropertyGroup>
  <ItemGroup>
    <Script Include="**" Exclude="*.esproj;**\node_modules\**" />
  </ItemGroup>
</Project>
```

*(Note: `ShouldRunBuildScript` can be false because our CI/CD container handles the build, but setting up the shell is important so VS recognizes it).*

---

#### T-153: Move Frontend Files Up One Level (Optional, but Recommended)

Currently, the React app lives in `src/Stewie.Web/ClientApp/`. 
With `.esproj`, it's standard practice to put the `package.json` right next to the `.esproj` file.

**Action:** Move all files from `src/Stewie.Web/ClientApp/*` up directly into `src/Stewie.Web/`.
**Delete** the empty `ClientApp` directory.
*(If you do this, adjust the `<BuildOutputFolder>` in the previous step to just `$(MSBuildProjectDirectory)\dist`)*.

---

#### T-154: Update Stewie.sln

**Modify** `Stewie.sln` to reference the new `.esproj` file instead of the old `.csproj`.

*Note: The project type GUID for `.esproj` is `54A90642-561A-4BB1-A94E-469ADEE60C69`.*

```text
Project("{54A90642-561A-4BB1-A94E-469ADEE60C69}") = "Stewie.Web", "Stewie.Web\Stewie.Web.esproj", "{NEW-GUID}"
EndProject
```

Ensure the old `.csproj` reference is completely removed from the solution file's Project and GlobalConfiguration sections.

---

## 5. Contracts Affected

None.

---

## 6. Verification

```bash
# Verify sln still builds (the remaining API/Application/Infrastructure/Test projects)
dotnet build Stewie.sln
```

**Exit criteria:**
- The `.csproj` is gone.
- The `.esproj` is created and configured.
- The `.sln` file opens successfully and builds the .NET components.

---

## 7. Change Log

| Date | Change |
|:-----|:-------|
| 2026-04-10 | JOB-015 created for .esproj migration. |
