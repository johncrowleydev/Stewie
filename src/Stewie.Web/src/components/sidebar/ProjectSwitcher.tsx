/**
 * ProjectSwitcher — Sidebar dropdown for switching the active project.
 *
 * Fetches the project list from the API on mount and renders a `Select`
 * dropdown. Changing the selection navigates to `/p/:newProjectId/`.
 * When no project is active, displays a "Select project…" placeholder.
 *
 * DECISION: Reads projectId directly from the URL pathname, not from
 * ProjectContext, because Layout (which renders this) sits above
 * ProjectProvider in the component tree.
 *
 * DECISION: Fetch projects directly via API rather than using a shared context
 * because the project list is lightweight and rarely changes. Avoids coupling
 * the sidebar to a global projects provider.
 *
 * FAILURE MODE: If the fetch fails, the dropdown shows nothing.
 * The user can still navigate via other means.
 *
 * Used by: Layout.tsx (sidebar, between logo and nav)
 * REF: JOB-031 T-532
 */
import { useState, useEffect, useCallback } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { Select } from "../ui";
import type { SelectOption } from "../ui";
import { fetchProjects } from "../../api/client";

/**
 * Renders a project dropdown in the sidebar for quick project switching.
 *
 * PRECONDITION: Must be inside a Router (uses useNavigate).
 * POSTCONDITION: Selecting a project navigates to /p/:id/ dashboard.
 */
export function ProjectSwitcher() {
  const navigate = useNavigate();
  const location = useLocation();
  const [options, setOptions] = useState<SelectOption[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Extract projectId from URL — Layout sits above ProjectProvider
  const projectMatch = location.pathname.match(/^\/p\/([^/]+)/);
  const projectId = projectMatch ? projectMatch[1] : null;

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

