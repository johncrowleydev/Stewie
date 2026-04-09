/**
 * TypeScript interfaces matching CON-002 API schemas.
 * These types are the frontend's contract with the Stewie API.
 */

/** Project entity — CON-002 §5.1 */
export interface Project {
  id: string;
  name: string;
  repoUrl: string;
  createdAt: string;
}

/** Run entity — CON-002 §5.2 (v1.3.0) */
export interface Run {
  id: string;
  projectId: string | null;
  status: RunStatus;
  branch: string | null;
  diffSummary: string | null;
  commitSha: string | null;
  pullRequestUrl: string | null;
  createdAt: string;
  completedAt: string | null;
  tasks: WorkTask[];
}

/** Task entity — CON-002 §5.3 (v1.2.0) */
export interface WorkTask {
  id: string;
  runId: string;
  role: "developer" | "tester" | "researcher";
  status: TaskStatus;
  objective: string;
  scope: string | null;
  workspacePath: string;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

/** Health response — CON-002 §5.4 */
export interface HealthResponse {
  status: string;
  version: string;
  timestamp: string;
}

/** Standardized error response — CON-002 §6 */
export interface ApiError {
  error: {
    code: string;
    message: string;
    details: Record<string, unknown>;
  };
}

/** Create project request body */
export interface CreateProjectRequest {
  name: string;
  repoUrl: string;
}

/** Create run request body — CON-002 §4.2 (v1.2.0) */
export interface CreateRunRequest {
  projectId: string;
  objective: string;
  scope?: string | null;
  script?: string[] | null;
  acceptanceCriteria?: string[] | null;
}

/** Artifact entity — CON-002 §5.6 */
export interface Artifact {
  id: string;
  taskId: string;
  type: string;
  contentJson?: string;
  createdAt: string;
}

/** Diff artifact content — CON-002 §5.6 */
export interface DiffContent {
  diffStat: string;
  diffPatch: string;
}

/** Status enum values for Runs */
export type RunStatus = "Pending" | "Running" | "Completed" | "Failed";

/** Status enum values for Tasks */
export type TaskStatus = "Pending" | "Running" | "Completed" | "Failed";

/** Event entity — CON-002 §5.5 */
export interface Event {
  id: string;
  entityType: "Run" | "Task";
  entityId: string;
  eventType: EventType;
  payload: string;
  timestamp: string;
}

/** Event type classification — mirrors Stewie.Domain.Enums.EventType */
export type EventType =
  | "RunCreated"
  | "RunStarted"
  | "RunCompleted"
  | "RunFailed"
  | "TaskCreated"
  | "TaskStarted"
  | "TaskCompleted"
  | "TaskFailed";

/** Authenticated user from JWT token — CON-002 §4.0 */
export interface AuthUser {
  id: string;
  username: string;
  role: "admin" | "user";
}

/** Login request body — CON-002 §4.0 */
export interface LoginRequest {
  username: string;
  password: string;
}

/** Registration request body — CON-002 §4.0 */
export interface RegisterRequest {
  username: string;
  password: string;
  inviteCode: string;
}

/** Auth response — CON-002 §4.0 */
export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

/** GitHub connection status — CON-002 §4.0.1 */
export interface GitHubStatus {
  connected: boolean;
  username: string | null;
}
