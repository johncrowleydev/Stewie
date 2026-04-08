<p align="center">
  <img src="docs/assets/stewie-logo.png" alt="Stewie" width="160" />
</p>

# Stewie

Governance-first, multi-agent software development orchestration system.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Quick Start

### 1. Start SQL Server

```bash
docker-compose up -d
```

This starts a SQL Server 2022 container on port 1433.

### 2. Build the Dummy Worker Image

```bash
docker build -t stewie-dummy-worker workers/dummy-worker/
```

### 3. Run the API

```bash
dotnet run --project src/Stewie.Web
```

On first start, FluentMigrator will create the `Stewie` database and run all migrations (Runs, Tasks, Artifacts tables).

### 4. Trigger a Test Run

```bash
curl -X POST http://localhost:5275/runs/test
```

Expected response:

```json
{
  "runId": "...",
  "taskId": "...",
  "artifactId": "...",
  "status": "Completed",
  "summary": "Dummy worker executed successfully. Runtime contract verified.",
  "resultPayload": { ... }
}
```

### What Happens

1. Stewie creates a **Run** (persisted to SQL Server)
2. Stewie creates a **Task** with role `developer`
3. A workspace is created on disk: `workspaces/{taskId}/input/`, `output/`, `repo/`
4. `task.json` is written to the workspace input directory
5. A Docker container is launched using the dummy worker image
6. The container reads `task.json` and writes `result.json`
7. Stewie ingests `result.json`, stores it as an **Artifact**
8. Run and Task statuses are updated to `Completed`
9. The result is returned via the API

## Project Structure

```
src/
  Stewie.Domain/         # Entities, enums, contracts (DTOs)
  Stewie.Application/    # Service interfaces, orchestration use cases
  Stewie.Infrastructure/ # NHibernate, FluentMigrator, Docker, filesystem
  Stewie.Web/            # ASP.NET Core API host
workers/
  dummy-worker/          # Dummy worker runtime (proves container contract)
workspaces/              # Runtime-created per-task directories (ephemeral)
```

## Configuration

Configuration is in `src/Stewie.Web/appsettings.json`:

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Stewie` | `Server=localhost,1433;...` | SQL Server connection string |
| `Stewie:WorkspaceRoot` | `./workspaces` | Base directory for task workspaces |
| `Stewie:DockerImageName` | `stewie-dummy-worker` | Docker image name for worker containers |

## Runtime Contract

### task.json (input)

```json
{
  "taskId": "guid",
  "runId": "guid",
  "role": "developer",
  "objective": "...",
  "scope": "...",
  "allowedPaths": [],
  "forbiddenPaths": [],
  "acceptanceCriteria": ["..."]
}
```

### result.json (output)

```json
{
  "taskId": "guid",
  "status": "success",
  "summary": "...",
  "filesChanged": [],
  "testsPassed": false,
  "errors": [],
  "notes": "...",
  "nextAction": "review"
}
```
