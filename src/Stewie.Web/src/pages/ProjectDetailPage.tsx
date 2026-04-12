/**
 * ProjectDetailPage — Project overview with Architect controls and chat slideover.
 * REF: JOB-013 T-138, JOB-018 T-178, JOB-025 T-301, JOB-027 T-405
 */
import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router-dom";
import { fetchProject } from "../api/client";
import { ArchitectControls } from "../components/ArchitectControls";
import { ChatSlideover } from "../components/ChatSlideover";
import { btnGhost, card, pageTitleRow, skeleton } from "../tw";
import type { Project } from "../types";

function ChatTriggerIcon() {
  return (
    <svg width={20} height={20} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M2 2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2v2.5a.5.5 0 0 0 .82.384L8.28 13H14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H2zm1 3.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5z" />
    </svg>
  );
}

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
      try { setProject(await fetchProject(id!)); setError(null); }
      catch (err) { setError(err instanceof Error ? err.message : "Failed to load project"); }
      finally { setLoading(false); }
    }
    void loadProject();
  }, [id]);

  const handleArchitectStatusChange = useCallback((active: boolean) => { setArchitectActive(active); }, []);
  const handlePinnedWidthChange = useCallback((width: number | null) => { setPinnedWidth(width); }, []);

  if (loading) {
    return (
      <div id="project-detail-page">
        <div className={pageTitleRow}>
          <Link to="/projects" className={btnGhost}>← Projects</Link>
        </div>
        <div className={`${skeleton} h-[80px]`} />
        <div className={`${skeleton} h-[120px] mt-md`} />
        <div className={`${skeleton} h-[400px] mt-md`} />
      </div>
    );
  }

  if (error || !project) {
    return (
      <div id="project-detail-page">
        <div className={pageTitleRow}>
          <Link to="/projects" className={btnGhost}>← Projects</Link>
        </div>
        <div className="text-center p-2xl text-ds-text-muted">
          <h3 className="text-base font-semibold text-ds-text mb-sm">Project not found</h3>
          <p className="text-md">{error || "This project doesn't exist or you don't have access."}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="project-detail-page" style={pinnedWidth ? { marginRight: pinnedWidth } : undefined}>
      <div className={pageTitleRow}>
        <div className="flex items-center gap-md">
          <Link to="/projects" className={btnGhost}>← Projects</Link>
          <h1 className="m-0 text-xl">{project.name}</h1>
        </div>
      </div>

      {/* Project info card */}
      <div className={`${card} mb-lg`}>
        <div className="flex items-center justify-between pb-sm border-b border-ds-border mb-md">
          <span className="text-md font-semibold text-ds-text">Project Details</span>
          <span className="font-mono text-xs text-ds-text-secondary">{project.id.slice(0, 8)}…</span>
        </div>
        <div className="flex gap-xl flex-wrap">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs">Repository</div>
            <div className="font-mono text-s">{project.repoUrl || "—"}</div>
          </div>
          {project.repoProvider && (
            <div>
              <div className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs">Provider</div>
              <div className="text-s">{project.repoProvider.charAt(0).toUpperCase() + project.repoProvider.slice(1)}</div>
            </div>
          )}
          <div>
            <div className="text-xs font-semibold uppercase tracking-wider text-ds-text-muted mb-xs">Created</div>
            <div className="text-s">{new Date(project.createdAt).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" })}</div>
          </div>
        </div>
      </div>

      <ArchitectControls projectId={project.id} onStatusChange={handleArchitectStatusChange} />

      {!chatOpen && (
        <button
          className="fixed bottom-xl right-xl z-[300] flex items-center gap-sm py-sm px-md rounded-full bg-ds-primary text-white border border-ds-primary shadow-ds-lg cursor-pointer transition-all duration-200 hover:bg-ds-primary-hover hover:shadow-[0_8px_32px_rgba(111,172,80,0.3)] hover:-translate-y-0.5 animate-[fab-appear_300ms_ease] dark:bg-transparent dark:text-ds-primary dark:border-ds-primary dark:hover:bg-[rgba(111,172,80,0.15)]"
          onClick={() => setChatOpen(true)}
          title="Open project chat"
          id="chat-trigger-fab"
        >
          <ChatTriggerIcon />
          <span className="text-s font-medium">Chat</span>
          {architectActive && <span className="w-2 h-2 rounded-full bg-white animate-[pulse_1.5s_ease-in-out_infinite]" />}
        </button>
      )}

      <ChatSlideover projectId={project.id} architectActive={architectActive} isOpen={chatOpen} onClose={() => setChatOpen(false)} onPinnedWidthChange={handlePinnedWidthChange} />
    </div>
  );
}
