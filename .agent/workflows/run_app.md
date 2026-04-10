---
description: Sets up persistent terminals for the backend API and frontend Vite server, explicitly handling the database admin user seed.
---

# /run_app

> Use this workflow when the human asks to "run the app" or test the environment locally.
> It sets up persistent terminals for the backend API and frontend Vite server, explicitly handling the database admin user seed.

## Step 1: Verify Infrastructure

// turbo
1. Check that the SQL Server Docker container is running:
```bash
docker ps | grep stewie-sqlserver || echo "❌ SQL Server not running. Please start it using 'docker-compose up -d sqlserver' or refer to RUN-001."
```

## Step 2: Launch Backend (.NET API)

1. Start the API in a **persistent terminal** (use `run_command` with `RunPersistent: true`).
2. You **MUST** provide `Stewie__AdminPassword=admin` alongside the 32-byte JWT and Encryption keys so the seeder catches it on boot (this only seeds if no users exist).

```bash
cd src/Stewie.Api
Stewie__AdminPassword=admin \
Stewie__JwtSecret="super-secret-jwt-key-that-is-at-least-32-bytes-long" \
Stewie__EncryptionKey="GkLUVANEsbyw1TnDCFvj5ZJ9BFmi3AlX9zMKvp5vHM4=" \
dotnet run
```

3. View logs to confirm you see: `Seeded admin user: admin` (if it is a fresh DB) and `Now listening on: http://localhost:5275`.

## Step 3: Launch Frontend (Vite)

1. Start the React UI in a **separate persistent terminal**.

```bash
cd src/Stewie.Web
npm run dev
```

2. Confirm it starts listening on `http://localhost:5173`.

## Step 4: Handoff to Human

1. Inform the user that the backend is running on `5275` and the frontend is successfully proxying from `5173`.
2. Provide the login credentials explicitly: **Username:** `admin` | **Password:** `admin`. 

---

## Quick Reference

| What | Command |
|:-----|:--------|
| Run the full stack | `/run_app` |
| Stop | Terminate the persistent tasks |
