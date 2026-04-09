/**
 * ProjectsPage — Lists projects and provides a create form with link/create toggle.
 * Fetches from GET /api/projects and POST /api/projects (CON-002 §4.1 v1.4.0).
 *
 * Two creation modes:
 *   - "Link Existing Repository" (default): requires name + repoUrl
 *   - "Create New Repository": requires name + repoName, optionally description + isPrivate
 */
import { useEffect, useState } from "react";
import { fetchProjects, createProject } from "../api/client";
import type { Project, CreateProjectRequest } from "../types";

/** Possible creation modes */
type CreationMode = "link" | "create";

/** SVG icon for GitHub provider badge */
function GitHubIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
    </svg>
  );
}

/** Returns a provider icon/badge for the given provider string */
function ProviderBadge({ provider }: { provider: string | null }) {
  if (!provider) return null;

  const label = provider.charAt(0).toUpperCase() + provider.slice(1);
  const isGitHub = provider.toLowerCase() === "github";

  return (
    <span className="provider-badge" title={`Hosted on ${label}`}>
      {isGitHub && <GitHubIcon />}
      {label}
    </span>
  );
}

/** Projects page with list and inline create form */
export function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [showForm, setShowForm] = useState(false);
  const [creationMode, setCreationMode] = useState<CreationMode>("link");
  const [formName, setFormName] = useState("");
  const [formRepoUrl, setFormRepoUrl] = useState("");
  const [formRepoName, setFormRepoName] = useState("");
  const [formDescription, setFormDescription] = useState("");
  const [formIsPrivate, setFormIsPrivate] = useState(true);
  const [formError, setFormError] = useState<string | null>(null);
  const [formSuccess, setFormSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  /** Load all projects */
  async function loadProjects() {
    try {
      const data = await fetchProjects();
      setProjects(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load projects");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadProjects();
  }, []);

  /** Reset form fields when toggling modes or closing */
  function resetForm() {
    setFormName("");
    setFormRepoUrl("");
    setFormRepoName("");
    setFormDescription("");
    setFormIsPrivate(true);
    setFormError(null);
    setFormSuccess(null);
    setCreationMode("link");
  }

  /** Handle form submission */
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setFormError(null);
    setFormSuccess(null);

    // Common validation
    if (!formName.trim()) {
      setFormError("Project name is required.");
      return;
    }

    // Mode-specific validation
    if (creationMode === "link") {
      if (!formRepoUrl.trim()) {
        setFormError("Repository URL is required when linking an existing repo.");
        return;
      }
    } else {
      if (!formRepoName.trim()) {
        setFormError("Repository name is required when creating a new repo.");
        return;
      }
    }

    setSubmitting(true);
    try {
      const payload: CreateProjectRequest =
        creationMode === "link"
          ? {
              name: formName.trim(),
              repoUrl: formRepoUrl.trim(),
            }
          : {
              name: formName.trim(),
              createRepo: true,
              repoName: formRepoName.trim(),
              description: formDescription.trim() || undefined,
              isPrivate: formIsPrivate,
            };

      const created = await createProject(payload);
      setFormSuccess(`Project "${created.name}" created successfully.`);
      resetForm();
      setShowForm(false);
      // Reload projects list
      await loadProjects();
    } catch (err) {
      const message = err instanceof Error ? err.message : "Failed to create project";
      setFormError(message);
    } finally {
      setSubmitting(false);
    }
  }

  /** Format an ISO date string to a readable date */
  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  }

  if (loading) {
    return (
      <div>
        <div className="page-title-row">
          <h1>Projects</h1>
        </div>
        <div className="projects-grid">
          {[1, 2, 3].map((i) => (
            <div key={i} className="skeleton skeleton-card" />
          ))}
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div>
        <div className="page-title-row">
          <h1>Projects</h1>
        </div>
        <div className="error-state">
          <h3>Failed to load projects</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="projects-page">
      <div className="page-title-row">
        <h1>Projects</h1>
        <button
          className="btn btn-primary"
          onClick={() => { setShowForm(!showForm); if (showForm) resetForm(); }}
          id="toggle-create-form"
        >
          {showForm ? "Cancel" : "+ New Project"}
        </button>
      </div>

      {formSuccess && (
        <div className="form-success" style={{ marginBottom: 16 }}>
          ✓ {formSuccess}
        </div>
      )}

      {showForm && (
        <form className="create-form" onSubmit={(e) => { void handleSubmit(e); }} id="create-project-form">
          <h3>Create New Project</h3>

          {/* Mode Toggle */}
          <div className="mode-toggle" id="creation-mode-toggle">
            <button
              type="button"
              className={`mode-toggle-btn ${creationMode === "link" ? "active" : ""}`}
              onClick={() => { setCreationMode("link"); setFormError(null); }}
              id="mode-link"
            >
              <span className="mode-icon">🔗</span>
              Link Existing Repository
            </button>
            <button
              type="button"
              className={`mode-toggle-btn ${creationMode === "create" ? "active" : ""}`}
              onClick={() => { setCreationMode("create"); setFormError(null); }}
              id="mode-create"
            >
              <span className="mode-icon">✨</span>
              Create New Repository
            </button>
          </div>

          {/* Common: Project Name */}
          <div className="form-group">
            <label className="form-label" htmlFor="project-name">Project Name</label>
            <input
              className="form-input"
              id="project-name"
              type="text"
              placeholder="My Project"
              value={formName}
              onChange={(e) => setFormName(e.target.value)}
              disabled={submitting}
            />
          </div>

          {/* Link mode fields */}
          {creationMode === "link" && (
            <div className="form-group">
              <label className="form-label" htmlFor="project-repo-url">Repository URL</label>
              <input
                className="form-input"
                id="project-repo-url"
                type="text"
                placeholder="https://github.com/org/repo"
                value={formRepoUrl}
                onChange={(e) => setFormRepoUrl(e.target.value)}
                disabled={submitting}
              />
              <div className="form-hint">Paste the full URL of an existing repository.</div>
            </div>
          )}

          {/* Create mode fields */}
          {creationMode === "create" && (
            <>
              <div className="form-group">
                <label className="form-label" htmlFor="project-repo-name">Repository Name</label>
                <input
                  className="form-input"
                  id="project-repo-name"
                  type="text"
                  placeholder="my-new-repo"
                  value={formRepoName}
                  onChange={(e) => setFormRepoName(e.target.value)}
                  disabled={submitting}
                />
                <div className="form-hint">A new GitHub repository will be created with this name.</div>
              </div>

              <div className="form-group">
                <label className="form-label" htmlFor="project-description">Description</label>
                <textarea
                  className="form-input"
                  id="project-description"
                  placeholder="Optional repository description"
                  value={formDescription}
                  onChange={(e) => setFormDescription(e.target.value)}
                  disabled={submitting}
                  rows={2}
                />
              </div>

              <div className="form-group">
                <label className="form-checkbox-label" htmlFor="project-is-private">
                  <input
                    type="checkbox"
                    id="project-is-private"
                    checked={formIsPrivate}
                    onChange={(e) => setFormIsPrivate(e.target.checked)}
                    disabled={submitting}
                    className="form-checkbox"
                  />
                  <span className="checkbox-visual" />
                  Private repository
                </label>
                <div className="form-hint">Private repos are only visible to you and your collaborators.</div>
              </div>
            </>
          )}

          {formError && <div className="form-error" id="form-error-message">✕ {formError}</div>}
          <div className="form-actions">
            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting}
              id="submit-create-project"
            >
              {submitting
                ? "Creating…"
                : creationMode === "link"
                  ? "Link Repository"
                  : "Create Repository"}
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => { setShowForm(false); resetForm(); }}
              disabled={submitting}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {projects.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📁</div>
          <h3>No projects yet</h3>
          <p>Create a project to organize your orchestration runs.</p>
        </div>
      ) : (
        <div className="projects-grid">
          {projects.map((project) => (
            <div className="project-card" key={project.id} id={`project-${project.id}`}>
              <div className="project-card-header">
                <h3>{project.name}</h3>
                <ProviderBadge provider={project.repoProvider} />
              </div>
              <div className="repo-url">{project.repoUrl}</div>
              <div className="project-date">
                Created {formatDate(project.createdAt)}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
