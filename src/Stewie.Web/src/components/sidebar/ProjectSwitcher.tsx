/**
 * ProjectSwitcher — Sidebar dropdown for switching the active project.
 *
 * Fetches the project list from the API on mount and renders a `Select`
 * dropdown. Changing the selection navigates to `/p/:newProjectId/`.
 * When no project is active, displays a "Select project…" placeholder.
 *
 * DECISION: Fetch projects directly via API rather than using a shared context
 * because the project list is lightweight and rarely changes. Avoids coupling
 * the sidebar to a global projects provider.
 *
 * FAILURE MODE: If the fetch fails, the dropdown shows a single "…" option
 * to avoid a blank/broken sidebar. The user can still navigate via /projects.
 *
 * Used by: Layout.tsx (sidebar, between logo and nav)
 * REF: JOB-031 T-532
 *
 * @example
 * ```tsx
 * <ProjectSwitcher projectId={projectCtx?.projectId ?? null} />
 * ```
 */
import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Select } from "../ui";
import type { SelectOption } from "../ui";
import { fetchProjects } from "../../api/client";

/** Props for the ProjectSwitcher component */
interface ProjectSwitcherProps {
  /** Currently active project ID from context, or null if on a global page */
  projectId: string | null;
}

/**
 * Renders a project dropdown in the sidebar for quick project switching.
 *
 * PRECONDITION: Must be rendered inside a Router (uses useNavigate).
 * POSTCONDITION: Selecting a project navigates to /p/:id/ dashboard.
 */
export function ProjectSwitcher({ projectId }: ProjectSwitcherProps) {
  const navigate = useNavigate();
  const [options, setOptions] = useState<SelectOption[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Fetch project list on mount
  useEffect(() => {
    let cancelled = false;

    async function loadProjects() {
      setIsLoading(true);
      try {
        const projects = await fetchProjects();
        if (!cancelled) {
          setOptions(
            projects.map((project) => ({
              value: project.id,
              label: project.name,
            })),
          );
        }
      } catch {
        // FAILURE MODE: Show empty options — user can navigate via /projects page
        if (!cancelled) {
          setOptions([]);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void loadProjects();
    return () => { cancelled = true; };
  }, []);

  /** Navigate to the selected project's dashboard */
  const handleProjectChange = useCallback(
    (newProjectId: string) => {
      if (newProjectId && newProjectId !== projectId) {
        navigate(`/p/${newProjectId}/`);
      }
    },
    [navigate, projectId],
  );

  // Guard: don't render until options have loaded
  if (isLoading) {
    return (
      <div
        className="mx-md my-sm h-9 bg-ds-surface-hover rounded-md animate-pulse"
        role="status"
        aria-label="Loading project list"
        data-testid="project-switcher-skeleton"
      />
    );
  }

  // Guard: no projects available
  if (options.length === 0) {
    return null;
  }

  return (
    <div className="px-md py-sm" data-testid="project-switcher">
      <Select
        options={options}
        value={projectId ?? ""}
        onChange={handleProjectChange}
        placeholder="Select project…"
        label="Project"
      />
    </div>
  );
}
