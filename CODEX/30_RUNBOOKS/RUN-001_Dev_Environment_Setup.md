---
id: RUN-001
title: "Stewie Development Environment Setup"
type: how-to
status: APPROVED
owner: architect
version: "1.0.0"
created: "2026-04-08"
updated: "2026-04-08"
tags: [setup, workflow]
agents: [coder, architect]
related: []
---

> **BLUF:** Stand up the full Stewie development environment from a clean clone: SQL Server via Docker, .NET API, React frontend, and the dummy worker image.

# Stewie Development Environment Setup

## 1. Prerequisites

- **Docker Desktop** — running and accessible via CLI (`docker --version`)
- **.NET 10 SDK** — (`dotnet --version`)
- **Node.js 20+** — (`node --version`)
- **npm** — (`npm --version`)

## 2. Start SQL Server

```bash
docker compose up -d
```

Verify it's accepting connections (wait ~5 seconds after first start):

```bash
MSYS_NO_PATHCONV=1 docker exec stewie-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "Stewie_Dev_P@ss1" -Q "SELECT 1" -C
```

## 3. Build the Solution

```bash
dotnet build src/Stewie.slnx
```

All five projects should build with 0 errors:
`Stewie.Domain` → `Stewie.Application` → `Stewie.Infrastructure` → `Stewie.Api` → `Stewie.Web`

## 4. Build the Dummy Worker Image

```bash
docker build -t stewie-dummy-worker workers/dummy-worker/
```

## 5. Start the API

```bash
dotnet run --project src/Stewie.Api/Stewie.Api.csproj
```

On first run this will:
1. Create the `Stewie` database if it doesn't exist
2. Run all FluentMigrator migrations (Runs, Tasks, Artifacts tables)
3. Start listening on `http://localhost:5275`

## 6. Start the React Frontend

```bash
cd src/Stewie.Web/ClientApp
npm install   # first time only
npm run dev
```

Frontend available at `http://localhost:5173/`. API calls to `/api` are proxied to the backend.

## 7. Verify End-to-End

```bash
curl -X POST http://localhost:5275/runs/test
```

Expected: JSON response with `status: "Completed"`, a `resultPayload`, and stored artifact data.

## 8. Troubleshooting

### Port already in use
```bash
# Find the process holding the port (Windows)
netstat -ano | findstr :5275
# Kill it
taskkill /F /PID <pid>
```

### SQL Server container won't start
```bash
docker compose down
docker compose up -d
# Check logs
docker logs stewie-sqlserver
```

### NuGet restore failures
```bash
dotnet nuget locals all --clear
dotnet restore src/Stewie.slnx
```

### npm install fails
```bash
cd src/Stewie.Web/ClientApp
rm -rf node_modules package-lock.json
npm install
```
