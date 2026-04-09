/**
 * Stewie API client — typed fetch wrapper for all CON-002 endpoints.
 * Handles error responses and provides typed return values.
 */
import type { Run, Project, CreateProjectRequest, ApiError, Event } from "../types";

/** Base URL is proxied via Vite config — no absolute URL needed. */
const BASE = "";

/**
 * Generic fetch wrapper with error handling.
 * Throws structured errors matching CON-002 §6.
 */
async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    headers: { "Content-Type": "application/json", ...options?.headers },
    ...options,
  });

  if (!response.ok) {
    let errorBody: ApiError | null = null;
    try {
      errorBody = (await response.json()) as ApiError;
    } catch {
      // Response wasn't JSON — fall through to generic error
    }

    const message =
      errorBody?.error?.message ?? `Request failed: ${response.status} ${response.statusText}`;
    throw new Error(message);
  }

  return (await response.json()) as T;
}

/** Fetch all runs — GET /api/runs */
export async function fetchRuns(): Promise<Run[]> {
  return request<Run[]>("/api/runs");
}

/** Fetch a single run by ID — GET /api/runs/{id} */
export async function fetchRun(id: string): Promise<Run> {
  return request<Run>(`/api/runs/${encodeURIComponent(id)}`);
}

/** Fetch all projects — GET /api/projects */
export async function fetchProjects(): Promise<Project[]> {
  return request<Project[]>("/api/projects");
}

/** Fetch a single project by ID — GET /api/projects/{id} */
export async function fetchProject(id: string): Promise<Project> {
  return request<Project>(`/api/projects/${encodeURIComponent(id)}`);
}

/** Create a new project — POST /api/projects */
export async function createProject(data: CreateProjectRequest): Promise<Project> {
  return request<Project>("/api/projects", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

/** Fetch recent events — GET /api/events */
export async function fetchEvents(limit?: number): Promise<Event[]> {
  const params = limit ? `?limit=${limit}` : "";
  return request<Event[]>(`/api/events${params}`);
}

/** Fetch events filtered by entity — GET /api/events?entityType=X&entityId=Y */
export async function fetchEventsByEntity(
  entityType: string,
  entityId: string
): Promise<Event[]> {
  return request<Event[]>(
    `/api/events?entityType=${encodeURIComponent(entityType)}&entityId=${encodeURIComponent(entityId)}`
  );
}
