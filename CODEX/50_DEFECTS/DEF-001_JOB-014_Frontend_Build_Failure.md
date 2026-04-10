---
id: DEF-001
title: "JOB-014 Frontend Build Failure — Missing API Client + Implicit Any"
type: reference
status: CLOSED
owner: architect
agents: [coder]
tags: [defect, phase-5a, streaming, frontend, typescript]
related: [JOB-014, VER-014]
created: 2026-04-10
updated: 2026-04-10
version: 1.0.0
---

> **BLUF:** The JOB-014 frontend branch (`feature/JOB-014-frontend`) fails `npm run build` with 3 TypeScript errors. Dev B implemented T-147 (ContainerOutputPanel component) but did not complete T-146 (API client function) and left implicit `any` types violating GOV-003.

# DEF-001: JOB-014 Frontend Build Failure

**Discovered during:** VER-014 audit
**Branch:** `feature/JOB-014-frontend`
**Severity:** Blocking (branch cannot merge)

---

## 1. Errors

```
src/components/ContainerOutputPanel.tsx(17,10): error TS2305:
  Module '"../api/client"' has no exported member 'fetchContainerOutput'.

src/components/ContainerOutputPanel.tsx(112,46): error TS7006:
  Parameter 'raw' implicitly has an 'any' type.

src/components/ContainerOutputPanel.tsx(112,51): error TS7006:
  Parameter 'idx' implicitly has an 'any' type.
```

---

## 2. Root Cause

1. **T-146 not completed:** The `fetchContainerOutput` function was never added to `src/Stewie.Web/src/api/client.ts`. The `ContainerOutputResponse` type was never added to `src/Stewie.Web/src/types/index.ts`.
2. **GOV-003 violation:** Line 112 of `ContainerOutputPanel.tsx` uses `.map((raw, idx) => ...)` without type annotations, causing implicit `any` in strict mode.

---

## 3. Required Fixes

### Fix 1: Add API client function (T-146)

**File:** `src/Stewie.Web/src/api/client.ts`

Add:
```typescript
export async function fetchContainerOutput(taskId: string): Promise<ContainerOutputResponse> {
  const res = await fetch(`/api/tasks/${taskId}/output`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Failed to fetch container output: ${res.status}`);
  return res.json();
}
```

### Fix 2: Add response type

**File:** `src/Stewie.Web/src/types/index.ts`

Add:
```typescript
export interface ContainerOutputResponse {
  taskId: string;
  lines: string[];
  lineCount: number;
}
```

### Fix 3: Type the .map() parameters

**File:** `src/Stewie.Web/src/components/ContainerOutputPanel.tsx`, line 112

Change:
```typescript
const parsed = response.lines.map((raw, idx) => parseLine(raw, idx + 1));
```
To:
```typescript
const parsed = response.lines.map((raw: string, idx: number) => parseLine(raw, idx + 1));
```

---

## 4. Verification

After applying fixes, run:
```bash
cd src/Stewie.Web && npm run build && npx tsc --noEmit
```

Both must produce zero errors.

---

## 5. Assignment

Send back to JOB-014 Dev B (frontend agent) on branch `feature/JOB-014-frontend`.
