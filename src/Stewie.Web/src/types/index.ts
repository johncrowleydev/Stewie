/**
 * TypeScript interfaces matching CON-002 API schemas.
 * These types are the frontend's contract with the Stewie API.
 */

/** Project entity — CON-002 §5.1 (v1.4.0) */
export interface Project {
  id: string;
  name: string;
  repoUrl: string;
  repoProvider: string | null;
  createdAt: string;
}

/** Job entity — CON-002 §5.2 (v1.5.0) */
export interface Job {
  id: string;
  projectId: string | null;
  status: JobStatus;
  branch: string | null;
  diffSummary: string | null;
  commitSha: string | null;
  pullRequestUrl: string | null;
  createdAt: string;
  completedAt: string | null;
  tasks: WorkTask[];
}

/** Task entity — CON-002 §5.3 (v1.5.0) */
export interface WorkTask {
  id: string;
  jobId: string;
  parentTaskId: string | null;
  attemptNumber: number;
  role: "developer" | "tester" | "researcher";
  status: TaskStatus;
  objective: string;
  scope: string | null;
  governanceViolationsJson: string | null;
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

/** Create project request body — CON-002 §4.1 (v1.4.0) */
export interface CreateProjectRequest {
  name: string;
  repoUrl?: string | null;
  createRepo?: boolean;
  repoName?: string | null;
  isPrivate?: boolean;
  description?: string | null;
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

/** Status enum values for Jobs — includes Phase 4 PartiallyCompleted */
export type JobStatus = "Pending" | "Running" | "Completed" | "Failed" | "PartiallyCompleted";

/** Status enum values for Tasks — includes Phase 4 Blocked/Cancelled */
export type TaskStatus = "Pending" | "Running" | "Completed" | "Failed" | "Blocked" | "Cancelled";

/** Event entity — CON-002 §5.5 */
export interface Event {
  id: string;
  entityType: "Job" | "Task";
  entityId: string;
  eventType: EventType;
  payload: string;
  timestamp: string;
}

/** Event type classification — mirrors Stewie.Domain.Enums.EventType */
export type EventType =
  | "JobCreated"
  | "JobStarted"
  | "JobCompleted"
  | "JobFailed"
  | "TaskCreated"
  | "TaskStarted"
  | "TaskCompleted"
  | "TaskFailed"
  | "GovernanceStarted"
  | "GovernancePassed"
  | "GovernanceFailed"
  | "GovernanceRetry";

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

/** Governance report — CON-002 §4.6 (v1.6.0) */
export interface GovernanceReport {
  id: string;
  taskId: string;
  passed: boolean;
  totalChecks: number;
  passedChecks: number;
  failedChecks: number;
  checks: GovernanceCheckResult[];
  createdAt: string;
}

/** Individual governance check result — CON-002 §4.6 */
export interface GovernanceCheckResult {
  ruleId: string;
  ruleName: string;
  category: string;
  passed: boolean;
  details: string | null;
  severity: "error" | "warning" | "info";
}

/** Governance analytics response — CON-002 v1.8.0 */
export interface GovernanceAnalytics {
  totalJobs: number;
  totalGovernanceRuns: number;
  passRate: number;
  topFailingRules: FailingRule[];
  suggestedGovUpdates: GovUpdateSuggestion[];
}

/** Top failing governance rule — part of analytics response */
export interface FailingRule {
  ruleId: string;
  ruleName: string;
  failCount: number;
  trend: "increasing" | "decreasing" | "stable";
}

/** Suggested GOV document update — part of analytics response */
export interface GovUpdateSuggestion {
  govDoc: string;
  reason: string;
}

/** Chat message entity — CON-002 v2.0.0 */
export interface ChatMessage {
  id: string;
  projectId: string;
  senderRole: "Human" | "Architect" | "System";
  senderName: string;
  content: string;
  createdAt: string;
}

/** Paginated chat messages response — CON-002 v2.0.0 */
export interface ChatMessagesResponse {
  messages: ChatMessage[];
  total: number;
  limit: number;
  offset: number;
}

/** Container output response — CON-002 v2.0.0 (JOB-014) */
export interface ContainerOutputResponse {
  taskId: string;
  lines: string[];
  lineCount: number;
}

/** Agent session entity — CON-002 v2.2.0 (JOB-017/JOB-018) */
export interface AgentSession {
  id: string;
  projectId: string;
  taskId: string | null;
  containerId: string;
  runtimeName: string;
  role: string;
  status: string;
  startedAt: string;
  stoppedAt: string | null;
}

/** Architect agent status response — CON-002 v2.2.0 (JOB-018) */
export interface ArchitectStatus {
  active: boolean;
  session: AgentSession | null;
}

/** Stored credential (masked) — CON-002 v1.9.0 (JOB-023 T-201) */
export interface Credential {
  id: string;
  credentialType: string;
  maskedValue: string;
  createdAt: string;
}

/** Request body for adding a credential — CON-002 v1.9.0 (JOB-023 T-201) */
export interface AddCredentialRequest {
  credentialType: string;
  value: string;
}

/** GitHub repository from GET /api/github/repos — CON-002 v1.10.0 (JOB-025 T-304) */
export interface GitHubRepo {
  name: string;
  fullName: string;
  htmlUrl: string;
  isPrivate: boolean;
}

/** Invite code entity — CON-002 §4.0.2 (JOB-026 T-312) */
export interface InviteCode {
  id: string;
  code: string;
  usedByUserId: string | null;
  usedAt: string | null;
  expiresAt: string | null;
  createdAt: string;
}

/** User info for admin management — CON-002 §4.0.1 (JOB-026 T-312) */
export interface UserInfo {
  id: string;
  username: string;
  role: "admin" | "user";
  createdAt: string;
}

