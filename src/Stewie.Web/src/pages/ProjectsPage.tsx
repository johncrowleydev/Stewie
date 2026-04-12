/**
 * ProjectsPage — Lists projects and provides a create form with link/create toggle.
 * REF: JOB-025 T-302, T-303, JOB-027 T-405
 */
import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { fetchProjects, createProject, getGitHubStatus } from "../api/client";
import { RepoCombobox } from "../components/RepoCombobox";
import { IconCheck, IconX } from "../components/Icons";
import { btnPrimary, btnGhost, formInput, formLabel, formGroup, formHint, card, pageTitleRow, skeleton } from "../tw";
import type { Project, CreateProjectRequest, GitHubStatus } from "../types";

type CreationMode = "link" | "create";

function GitHubIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
    </svg>
  );
}

function ProviderBadge({ provider }: { provider: string | null }) {
  if (!provider) return null;
  const label = provider.charAt(0).toUpperCase() + provider.slice(1);
  const isGitHub = provider.toLowerCase() === "github";
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-ds-primary-muted text-ds-primary whitespace-nowrap [&_svg]:w-3 [&_svg]:h-3" title={`Hosted on ${label}`}>
      {isGitHub && <GitHubIcon />}
      {label}
    </span>
  );
}

export function ProjectsPage() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [gitHubStatus, setGitHubStatus] = useState<GitHubStatus | null>(null);
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

  async function loadProjects() {
    try { const data = await fetchProjects(); setProjects(data); setError(null); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load projects"); }
    finally { setLoading(false); }
  }

  const loadGitHubStatus = useCallback(async () => {
    try { setGitHubStatus(await getGitHubStatus()); }
    catch { setGitHubStatus({ connected: false, username: null }); }
  }, []);

  useEffect(() => { void loadProjects(); void loadGitHubStatus(); }, [loadGitHubStatus]);

  const isGitHubConnected = gitHubStatus?.connected ?? false;

  function resetForm() {
    setFormName(""); setFormRepoUrl(""); setFormRepoName(""); setFormDescription("");
    setFormIsPrivate(true); setFormError(null); setFormSuccess(null); setCreationMode("link");
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setFormError(null); setFormSuccess(null);
    if (!formName.trim()) { setFormError("Project name is required."); return; }
    if (creationMode === "link" && !formRepoUrl.trim()) { setFormError("Repository URL is required when linking an existing repo."); return; }
    if (creationMode === "create" && !formRepoName.trim()) { setFormError("Repository name is required when creating a new repo."); return; }
    setSubmitting(true);
    try {
      const payload: CreateProjectRequest = creationMode === "link"
        ? { name: formName.trim(), repoUrl: formRepoUrl.trim() }
        : { name: formName.trim(), createRepo: true, repoName: formRepoName.trim(), description: formDescription.trim() || undefined, isPrivate: formIsPrivate };
      const created = await createProject(payload);
      setFormSuccess(`Project "${created.name}" created successfully.`);
      resetForm(); setShowForm(false); await loadProjects();
    } catch (err) { setFormError(err instanceof Error ? err.message : "Failed to create project"); }
    finally { setSubmitting(false); }
  }

  const handleRepoSelect = useCallback((repoUrl: string) => { setFormRepoUrl(repoUrl); }, []);

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  }

  /* Mode toggle button */
  const modeBtn = (active: boolean, disabled = false) => [
    "flex items-center justify-center gap-sm py-sm px-md border rounded-sm text-s font-medium font-sans cursor-pointer transition-all duration-150",
    active ? "bg-ds-surface text-ds-primary border-ds-primary shadow-ds-sm" : "bg-transparent text-ds-text-muted border-transparent hover:text-ds-text hover:bg-ds-surface-hover",
    disabled ? "opacity-50 cursor-not-allowed" : "",
  ].join(" ");

  if (loading) {
    return (
      <div>
        <div className={pageTitleRow} />
        <div className="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-lg">
          {[1, 2, 3].map((i) => <div key={i} className={`${skeleton} h-[120px] rounded-lg`} />)}
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div>
        <div className={pageTitleRow} />
        <div className="text-center p-2xl text-ds-text-muted">
          <h3 className="text-base font-semibold text-ds-text mb-sm">Failed to load projects</h3>
          <p className="text-md">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div id="projects-page">
      <div className={pageTitleRow}>
        <div />
        <button className={btnPrimary} onClick={() => { setShowForm(!showForm); if (showForm) resetForm(); }} id="toggle-create-form">
          {showForm ? "Cancel" : "+ New Project"}
        </button>
      </div>

      {formSuccess && <div className="text-ds-completed text-s mt-sm mb-md flex items-center gap-1"><IconCheck size={14} /> {formSuccess}</div>}

      {showForm && (
        <form className={`${card} mb-xl`} onSubmit={(e) => { void handleSubmit(e); }} id="create-project-form">
          <h3 className="text-base font-semibold mb-md">Create New Project</h3>

          {/* Mode Toggle */}
          <div className="grid grid-cols-2 gap-sm mb-lg bg-ds-bg rounded-md p-xs" id="creation-mode-toggle">
            <button type="button" className={modeBtn(creationMode === "link")} onClick={() => { setCreationMode("link"); setFormError(null); }} id="mode-link">
              Link Existing Repository
            </button>
            <button
              type="button"
              className={modeBtn(creationMode === "create", !isGitHubConnected)}
              onClick={() => { if (!isGitHubConnected) return; setCreationMode("create"); setFormError(null); }}
              disabled={!isGitHubConnected}
              id="mode-create"
              title={!isGitHubConnected ? "Connect GitHub in Settings to create repos" : ""}
            >
              Create New Repository
              {!isGitHubConnected && <span className="text-[10px] font-medium text-ds-warning ml-xs">No GitHub</span>}
            </button>
          </div>

          {/* Project Name */}
          <div className={formGroup}>
            <label className={formLabel} htmlFor="project-name">Project Name</label>
            <input className={formInput} id="project-name" type="text" placeholder="My Project" value={formName} onChange={(e) => setFormName(e.target.value)} disabled={submitting} />
          </div>

          {/* Link mode */}
          {creationMode === "link" && (
            <div className={formGroup}>
              <label className={formLabel} htmlFor="project-repo-url">Repository URL</label>
              {isGitHubConnected ? (
                <>
                  <RepoCombobox onSelect={handleRepoSelect} disabled={submitting} />
                  {formRepoUrl && <div className={`${formHint} not-italic`} style={{ color: "var(--color-completed)" }}>Selected: {formRepoUrl}</div>}
                  <div className={formHint}>Select from your repos or paste a URL below.</div>
                  <input className={`${formInput} mt-sm`} id="project-repo-url" type="text" placeholder="Or paste URL manually…" value={formRepoUrl} onChange={(e) => setFormRepoUrl(e.target.value)} disabled={submitting} />
                </>
              ) : (
                <>
                  <input className={formInput} id="project-repo-url" type="text" placeholder="https://github.com/org/repo" value={formRepoUrl} onChange={(e) => setFormRepoUrl(e.target.value)} disabled={submitting} />
                  <div className={formHint}>Paste the full URL of an existing repository.</div>
                </>
              )}
            </div>
          )}

          {/* Create mode */}
          {creationMode === "create" && (
            <>
              <div className={formGroup}>
                <label className={formLabel} htmlFor="project-repo-name">Repository Name</label>
                <input className={formInput} id="project-repo-name" type="text" placeholder="my-new-repo" value={formRepoName} onChange={(e) => setFormRepoName(e.target.value)} disabled={submitting} />
                <div className={formHint}>A new GitHub repository will be created with this name.</div>
              </div>
              <div className={formGroup}>
                <label className={formLabel} htmlFor="project-description">Description</label>
                <textarea className={`${formInput} resize-y min-h-[60px]`} id="project-description" placeholder="Optional repository description" value={formDescription} onChange={(e) => setFormDescription(e.target.value)} disabled={submitting} rows={2} />
              </div>
              <div className={formGroup}>
                <label className="flex items-center gap-sm text-md text-ds-text cursor-pointer select-none" htmlFor="project-is-private">
                  <input type="checkbox" id="project-is-private" checked={formIsPrivate} onChange={(e) => setFormIsPrivate(e.target.checked)} disabled={submitting}
                    className="sr-only peer" />
                  <span className="w-4 h-4 rounded-[3px] border border-ds-border bg-ds-bg transition-all duration-150 peer-checked:bg-ds-primary peer-checked:border-ds-primary flex items-center justify-center [&>svg]:hidden peer-checked:[&>svg]:block">
                    <svg className="w-2.5 h-2.5 text-white" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><polyline points="2 7 5.5 10.5 12 4" /></svg>
                  </span>
                  Private repository
                </label>
                <div className={formHint}>Private repos are only visible to you and your collaborators.</div>
              </div>
            </>
          )}

          {formError && <div className="text-ds-failed text-s mt-sm flex items-center gap-1" id="form-error-message"><IconX size={14} /> {formError}</div>}
          <div className="flex gap-sm mt-md">
            <button type="submit" className={btnPrimary} disabled={submitting} id="submit-create-project">
              {submitting ? "Creating…" : creationMode === "link" ? "Link Repository" : "Create Repository"}
            </button>
            <button type="button" className={btnGhost} onClick={() => { setShowForm(false); resetForm(); }} disabled={submitting}>Cancel</button>
          </div>
        </form>
      )}

      {projects.length === 0 ? (
        <div className="text-center p-2xl text-ds-text-muted">
          <div className="text-[3rem] mb-md opacity-30">--</div>
          <h3 className="text-base font-semibold text-ds-text mb-sm">No projects yet</h3>
          <p className="text-md max-w-[400px] mx-auto">Create a project to organize your orchestration runs.</p>
        </div>
      ) : (
        <div className="grid grid-cols-[repeat(auto-fill,minmax(280px,1fr))] gap-lg">
          {projects.map((project) => (
            <div
              className="bg-ds-surface border border-ds-border rounded-lg p-lg transition-all duration-150 cursor-pointer hover:border-ds-primary hover:shadow-ds-md hover:-translate-y-0.5"
              key={project.id}
              id={`project-${project.id}`}
              onClick={() => { void navigate(`/p/${project.id}/`); }}
            >
              <div className="flex items-center justify-between gap-sm mb-sm">
                <h3 className="text-base font-semibold m-0">{project.name}</h3>
                <ProviderBadge provider={project.repoProvider} />
              </div>
              <div className="text-s text-ds-text-muted break-all mb-md">{project.repoUrl}</div>
              <div className="text-xs text-ds-text-muted">Created {formatDate(project.createdAt)}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
