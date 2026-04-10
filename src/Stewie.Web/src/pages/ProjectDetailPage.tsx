/**
 * ProjectDetailPage — Project overview with embedded chat panel.
 *
 * Displays project metadata (name, repo, provider) and an inline ChatPanel
 * for the Human↔Architect conversation. This is the primary project interaction page.
 *
 * Route: /projects/:id
 *
 * REF: JOB-013 T-138
 */
import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchProject } from "../api/client";
import { ChatPanel } from "../components/ChatPanel";
import type { Project } from "../types";

/** ProjectDetailPage — project info + integrated chat */
export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;

    async function loadProject() {
      try {
        const data = await fetchProject(id!);
        setProject(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load project");
      } finally {
        setLoading(false);
      }
    }

    void loadProject();
  }, [id]);

  if (loading) {
    return (
      <div id="project-detail-page">
        <div className="page-title-row">
          <Link to="/projects" className="btn btn-ghost">← Projects</Link>
        </div>
        <div className="skeleton skeleton-card" style={{ height: 80 }} />
        <div className="skeleton skeleton-card" style={{ height: 400, marginTop: 16 }} />
      </div>
    );
  }

  if (error || !project) {
    return (
      <div id="project-detail-page">
        <div className="page-title-row">
          <Link to="/projects" className="btn btn-ghost">← Projects</Link>
        </div>
        <div className="error-state">
          <h3>Project not found</h3>
          <p>{error || "This project doesn't exist or you don't have access."}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="project-detail-page">
      {/* Breadcrumb */}
      <div className="page-title-row">
        <div style={{ display: "flex", alignItems: "center", gap: "var(--space-md)" }}>
          <Link to="/projects" className="btn btn-ghost">← Projects</Link>
          <h1 style={{ margin: 0, fontSize: "var(--font-size-xl)" }}>{project.name}</h1>
        </div>
      </div>

      {/* Project info card */}
      <div className="card" style={{ marginBottom: "var(--space-lg)" }}>
        <div className="card-header">
          <span className="card-title">Project Details</span>
          <span className="mono" style={{ fontSize: "var(--font-size-xs)", color: "var(--text-secondary)" }}>
            {project.id.slice(0, 8)}…
          </span>
        </div>
        <div style={{ padding: "var(--space-md) var(--space-lg)" }}>
          <div style={{ display: "flex", gap: "var(--space-xl)", flexWrap: "wrap" }}>
            <div>
              <div className="card-label">Repository</div>
              <div className="mono" style={{ fontSize: "var(--font-size-sm)" }}>
                {project.repoUrl || "—"}
              </div>
            </div>
            {project.repoProvider && (
              <div>
                <div className="card-label">Provider</div>
                <div style={{ fontSize: "var(--font-size-sm)" }}>
                  {project.repoProvider.charAt(0).toUpperCase() + project.repoProvider.slice(1)}
                </div>
              </div>
            )}
            <div>
              <div className="card-label">Created</div>
              <div style={{ fontSize: "var(--font-size-sm)" }}>
                {new Date(project.createdAt).toLocaleDateString(undefined, {
                  year: "numeric",
                  month: "short",
                  day: "numeric",
                })}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Chat panel */}
      <ChatPanel projectId={project.id} />
    </div>
  );
}
