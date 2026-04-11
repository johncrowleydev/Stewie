/**
 * ProjectDetailPage — Project overview with Architect controls and chat slideover.
 *
 * Displays project metadata (name, repo, provider), the Architect Agent
 * lifecycle controls, and a right-side ChatSlideover for the Human↔Architect
 * conversation. Chat can be slideover (overlay) or pinned sidebar.
 *
 * Route: /projects/:id
 *
 * REF: JOB-013 T-138, JOB-018 T-178, JOB-025 T-301
 */
import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchProject } from "../api/client";
import { ArchitectControls } from "../components/ArchitectControls";
import { ChatSlideover } from "../components/ChatSlideover";
import type { Project } from "../types";

/** Floating chat trigger button icon */
function ChatTriggerIcon() {
  return (
    <svg width={20} height={20} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M2 2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2v2.5a.5.5 0 0 0 .82.384L8.28 13H14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H2zm1 3.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5z" />
    </svg>
  );
}

/** ProjectDetailPage — project info + architect controls + chat slideover */
export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [project, setProject] = useState<Project | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [architectActive, setArchitectActive] = useState(false);
  const [chatOpen, setChatOpen] = useState(false);
  const [pinnedWidth, setPinnedWidth] = useState<number | null>(null);

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

  /** Called by ArchitectControls when status changes */
  const handleArchitectStatusChange = useCallback((active: boolean) => {
    setArchitectActive(active);
  }, []);

  /** Handle pinned width changes for layout adjustment */
  const handlePinnedWidthChange = useCallback((width: number | null) => {
    setPinnedWidth(width);
  }, []);

  if (loading) {
    return (
      <div id="project-detail-page">
        <div className="page-title-row">
          <Link to="/projects" className="btn btn-ghost">← Projects</Link>
        </div>
        <div className="skeleton skeleton-card" style={{ height: 80 }} />
        <div className="skeleton skeleton-card" style={{ height: 120, marginTop: 16 }} />
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
    <div
      id="project-detail-page"
      style={pinnedWidth ? { marginRight: pinnedWidth } : undefined}
    >
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

      {/* Architect controls */}
      <ArchitectControls
        projectId={project.id}
        onStatusChange={handleArchitectStatusChange}
      />

      {/* Floating chat trigger button — visible when chat is closed */}
      {!chatOpen && (
        <button
          className="chat-trigger-fab"
          onClick={() => setChatOpen(true)}
          title="Open project chat"
          id="chat-trigger-fab"
        >
          <ChatTriggerIcon />
          <span className="chat-trigger-label">Chat</span>
          {architectActive && <span className="chat-trigger-dot" />}
        </button>
      )}

      {/* Chat slideover */}
      <ChatSlideover
        projectId={project.id}
        architectActive={architectActive}
        isOpen={chatOpen}
        onClose={() => setChatOpen(false)}
        onPinnedWidthChange={handlePinnedWidthChange}
      />
    </div>
  );
}
