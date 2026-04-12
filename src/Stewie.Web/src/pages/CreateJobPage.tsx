/**
 * CreateJobPage — Form for creating a new job with task definition.
 * REF: CON-002 §4.2 (v1.5.0), JOB-027 T-404
 */
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { fetchProjects, createJob } from "../api/client";
import { btnPrimary, btnGhost, formInput, formLabel, formGroup, formHint, card, pageTitleRow, skeleton } from "../tw";
import type { Project } from "../types";

export function CreateJobPage() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [projectId, setProjectId] = useState("");
  const [objective, setObjective] = useState("");
  const [scope, setScope] = useState("");
  const [scriptText, setScriptText] = useState("");
  const [criteriaText, setCriteriaText] = useState("");
  const [touched, setTouched] = useState<Record<string, boolean>>({});

  useEffect(() => {
    let cancelled = false;
    async function loadProjects() {
      try {
        const data = await fetchProjects();
        if (!cancelled) { setProjects(data); if (data.length > 0) setProjectId(data[0].id); }
      } catch { /* noop */ }
      finally { if (!cancelled) setLoadingProjects(false); }
    }
    void loadProjects();
    return () => { cancelled = true; };
  }, []);

  function parseLines(text: string): string[] | null {
    const lines = text.split("\n").map((l) => l.trim()).filter(Boolean);
    return lines.length > 0 ? lines : null;
  }

  function validate(): string | null {
    if (!projectId) return "Please select a project.";
    if (!objective.trim()) return "Objective is required.";
    return null;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setTouched({ projectId: true, objective: true });
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setSubmitting(true);
    setError(null);
    try {
      const job = await createJob({
        projectId,
        objective: objective.trim(),
        scope: scope.trim() || null,
        script: parseLines(scriptText),
        acceptanceCriteria: parseLines(criteriaText),
      });
      void navigate(`/jobs/${job.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create job");
    } finally {
      setSubmitting(false);
    }
  }

  /* Custom select styling: arrow icon, pointer */
  const selectInput = `${formInput} appearance-none bg-[url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%238b8d93' d='M2 4l4 4 4-4'/%3E%3C/svg%3E")] bg-no-repeat bg-[position:right_12px_center] pr-8 cursor-pointer`;

  return (
    <div id="create-job-page">
      <div className={pageTitleRow} />

      <form className={`${card} mb-xl`} onSubmit={(e) => { void handleSubmit(e); }} id="create-job-form">
        <div className={formGroup}>
          <label className={formLabel} htmlFor="project-select">Project *</label>
          {loadingProjects ? (
            <div className={`${skeleton} h-[38px]`} />
          ) : (
            <select
              id="project-select"
              className={selectInput}
              value={projectId}
              onChange={(e) => setProjectId(e.target.value)}
              onBlur={() => setTouched((p) => ({ ...p, projectId: true }))}
            >
              <option value="">Select a project…</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>{p.name} ({p.repoUrl})</option>
              ))}
            </select>
          )}
          {touched.projectId && !projectId && (
            <div className="text-ds-failed text-s mt-sm">Project is required.</div>
          )}
        </div>

        <div className={formGroup}>
          <label className={formLabel} htmlFor="objective-input">Objective *</label>
          <textarea
            id="objective-input"
            className={`${formInput} resize-y min-h-[60px]`}
            rows={3}
            placeholder="What should the worker accomplish?"
            value={objective}
            onChange={(e) => setObjective(e.target.value)}
            onBlur={() => setTouched((p) => ({ ...p, objective: true }))}
          />
          {touched.objective && !objective.trim() && (
            <div className="text-ds-failed text-s mt-sm">Objective is required.</div>
          )}
        </div>

        <div className={formGroup}>
          <label className={formLabel} htmlFor="scope-input">Scope</label>
          <input id="scope-input" className={formInput} type="text" placeholder="Boundaries of the work (optional)" value={scope} onChange={(e) => setScope(e.target.value)} />
        </div>

        <div className={formGroup}>
          <label className={formLabel} htmlFor="script-input">Script Commands</label>
          <textarea
            id="script-input"
            className={`${formInput} resize-y min-h-[60px]`}
            rows={4}
            placeholder={"One command per line (optional)\ne.g. npm install\nnpm run build"}
            value={scriptText}
            onChange={(e) => setScriptText(e.target.value)}
          />
          <div className={formHint}>Each line is a bash command executed sequentially in the workspace.</div>
        </div>

        <div className={formGroup}>
          <label className={formLabel} htmlFor="criteria-input">Acceptance Criteria</label>
          <textarea
            id="criteria-input"
            className={`${formInput} resize-y min-h-[60px]`}
            rows={3}
            placeholder={"One criterion per line (optional)\ne.g. All tests pass\nNo lint errors"}
            value={criteriaText}
            onChange={(e) => setCriteriaText(e.target.value)}
          />
        </div>

        {error && <div className="text-ds-failed text-s mt-sm">{error}</div>}

        <div className="flex gap-sm mt-md">
          <button type="submit" className={btnPrimary} disabled={submitting} id="submit-job-btn">
            {submitting ? "Creating…" : "Create Job"}
          </button>
          <button type="button" className={btnGhost} onClick={() => { void navigate("/jobs"); }}>Cancel</button>
        </div>
      </form>
    </div>
  );
}
