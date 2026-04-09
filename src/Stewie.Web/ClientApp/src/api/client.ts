/**
 * Stewie API client — typed fetch wrapper for all CON-002 endpoints.
 * Handles error responses, JWT auth headers, and 401 redirect.
 *
 * Auth token is stored in localStorage as 'stewie_token'.
 * All requests auto-attach the Authorization header when a token exists.
 * On 401, the token is cleared and user is redirected to /login.
 */
import type {
  Job, Project, CreateProjectRequest, CreateJobRequest,
  ApiError, Event, LoginRequest, RegisterRequest, AuthResponse, GitHubStatus
} from "../types";

/** Base URL is proxied via Vite config — no absolute URL needed. */
const BASE = "";

/** localStorage key for JWT token */
const TOKEN_KEY = "stewie_token";

/** Get stored JWT token */
export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

/** Store JWT token */
export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

/** Remove JWT token */
export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

/**
 * Generic fetch wrapper with error handling and auth.
 * Auto-attaches Bearer token. Redirects on 401.
 * Throws structured errors matching CON-002 §6.
 */
async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options?.headers as Record<string, string> | undefined),
  };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${BASE}${path}`, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    // Check if this is a login/register attempt (don't redirect)
    if (!path.startsWith("/api/auth/")) {
      clearToken();
      window.location.href = "/login";
    }
  }

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

// --- Auth endpoints ---

/** Login — POST /api/auth/login */
export async function login(data: LoginRequest): Promise<AuthResponse> {
  return request<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

/** Register — POST /api/auth/register */
export async function register(data: RegisterRequest): Promise<AuthResponse> {
  return request<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

// --- Job endpoints ---

/** Fetch all jobs — GET /api/jobs */
export async function fetchJobs(): Promise<Job[]> {
  return request<Job[]>("/api/jobs");
}

/** Create a new job — POST /api/jobs */
export async function createJob(data: CreateJobRequest): Promise<Job> {
  return request<Job>("/api/jobs", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

/** Fetch a single job by ID — GET /api/jobs/{id} */
export async function fetchJob(id: string): Promise<Job> {
  return request<Job>(`/api/jobs/${encodeURIComponent(id)}`);
}

// --- Project endpoints ---

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

// --- Event endpoints ---

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

// --- GitHub endpoints ---

/** Save GitHub PAT — PUT /api/users/me/github-token */
export async function saveGitHubToken(token: string): Promise<void> {
  await request<unknown>("/api/users/me/github-token", {
    method: "PUT",
    body: JSON.stringify({ token }),
  });
}

/** Remove GitHub PAT — DELETE /api/users/me/github-token */
export async function removeGitHubToken(): Promise<void> {
  await request<unknown>("/api/users/me/github-token", {
    method: "DELETE",
  });
}

/** Get GitHub connection status — GET /api/users/me/github-status */
export async function getGitHubStatus(): Promise<GitHubStatus> {
  return request<GitHubStatus>("/api/users/me/github-status");
}
