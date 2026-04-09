/**
 * ProjectsPage — Lists projects and provides a create form.
 * Fetches from GET /api/projects and POST /api/projects (CON-002 §4.1).
 */
import { useEffect, useState } from "react";
import { fetchProjects, createProject } from "../api/client";
import type { Project } from "../types";

/** Projects page with list and inline create form */
export function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [showForm, setShowForm] = useState(false);
  const [formName, setFormName] = useState("");
  const [formRepoUrl, setFormRepoUrl] = useState("");
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

  /** Handle form submission */
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setFormError(null);
    setFormSuccess(null);

    if (!formName.trim()) {
      setFormError("Project name is required.");
      return;
    }
    if (!formRepoUrl.trim()) {
      setFormError("Repository URL is required.");
      return;
    }

    setSubmitting(true);
    try {
      const created = await createProject({
        name: formName.trim(),
        repoUrl: formRepoUrl.trim(),
      });
      setFormSuccess(`Project "${created.name}" created successfully.`);
      setFormName("");
      setFormRepoUrl("");
      setShowForm(false);
      // Reload projects list
      await loadProjects();
    } catch (err) {
      setFormError(err instanceof Error ? err.message : "Failed to create project");
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
          onClick={() => { setShowForm(!showForm); setFormError(null); setFormSuccess(null); }}
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
          <div className="form-row">
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
            </div>
          </div>
          {formError && <div className="form-error">✕ {formError}</div>}
          <div className="form-actions">
            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting}
              id="submit-create-project"
            >
              {submitting ? "Creating…" : "Create Project"}
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setShowForm(false)}
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
              <h3>{project.name}</h3>
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
