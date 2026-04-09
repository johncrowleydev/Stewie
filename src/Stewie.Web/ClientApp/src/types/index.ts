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

/** Run entity — CON-002 §5.2 */
export interface Run {
  id: string;
  projectId: string | null;
  status: RunStatus;
  createdAt: string;
  completedAt: string | null;
  tasks: WorkTask[];
}

/** Task entity — CON-002 §5.3 */
export interface WorkTask {
  id: string;
  runId: string;
  role: "developer" | "tester" | "researcher";
  status: TaskStatus;
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

/** Status enum values for Runs */
export type RunStatus = "Pending" | "Running" | "Completed" | "Failed";

/** Status enum values for Tasks */
export type TaskStatus = "Pending" | "Running" | "Completed" | "Failed";
