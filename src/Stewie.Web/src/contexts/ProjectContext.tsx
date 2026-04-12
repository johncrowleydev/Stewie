/**
 * ProjectContext — React context for tracking the active project.
 * Reads `projectId` from the URL param (`:projectId` in `/p/:projectId/*`),
 * fetches the full project object from the API, and persists the last-used
 * project ID to localStorage for redirect logic.
 *
 * Usage: Wrap project-scoped routes in <ProjectProvider>, then call useProject().
 *
 * REF: JOB-030 T-520
 */
import { createContext, useContext, useState, useEffect } from "react";
import type { ReactNode } from "react";
import { useParams } from "react-router-dom";
import { fetchProject } from "../api/client";
import type { Project } from "../types";

/** localStorage key for persisting the last-used project ID */
const LAST_PROJECT_KEY = "stewie:lastProjectId";

/** Shape of the ProjectContext value */
export interface ProjectContextValue {
  /** The currently active project ID from the URL param */
  projectId: string;
  /** The full project object (loaded from API on mount) */
  project: Project | null;
  /** Whether the project is still loading */
  loading: boolean;
  /** Error message if project load failed */
  error: string | null;
}

/**
 * Raw context — exported so Layout can do an optional read via
 * `useContext(ProjectContext)` without throwing when outside a provider.
 */
export const ProjectContext = createContext<ProjectContextValue | null>(null);

/**
 * ProjectProvider — wraps project-scoped routes to provide project state.
 * Reads `:projectId` from the URL, fetches the project from the API,
 * and persists `projectId` to localStorage on every change.
 */
export function ProjectProvider({ children }: { children: ReactNode }) {
  const { projectId } = useParams<{ projectId: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Persist last-used projectId to localStorage
  useEffect(() => {
    if (projectId) {
      localStorage.setItem(LAST_PROJECT_KEY, projectId);
    }
  }, [projectId]);

  // Fetch the full project on mount / projectId change
  useEffect(() => {
    if (!projectId) {
      setError("No project ID in URL");
      setLoading(false);
      return;
    }

    let cancelled = false;

    async function loadProject() {
      setLoading(true);
      setError(null);
      try {
        const data = await fetchProject(projectId!);
        if (!cancelled) {
          setProject(data);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setProject(null);
          setError(
            err instanceof Error ? err.message : "Failed to load project",
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadProject();
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  // Guard: projectId must exist (route param is mandatory)
  if (!projectId) {
    return null;
  }

  return (
    <ProjectContext.Provider value={{ projectId, project, loading, error }}>
      {children}
    </ProjectContext.Provider>
  );
}

/**
 * useProject — Access the active project context.
 * Must be called inside a `<ProjectProvider>`.
 *
 * @throws Error if called outside ProjectProvider
 */
export function useProject(): ProjectContextValue {
  const ctx = useContext(ProjectContext);
  if (!ctx) {
    throw new Error("useProject must be used within a ProjectProvider");
  }
  return ctx;
}
