/**
 * CreateRunPage — Form for creating a new run with task definition.
 * Submits to POST /api/runs per CON-002 §4.2 (v1.2.0).
 * Project selector populated from GET /api/projects.
 *
 * Fields: project (required), objective (required), scope (optional),
 * script commands (optional, multi-line), acceptance criteria (optional, multi-line).
 */
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { fetchProjects, createRun } from "../api/client";
import type { Project } from "../types";

/** Create Run form page */
export function CreateRunPage() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form fields
  const [projectId, setProjectId] = useState("");
  const [objective, setObjective] = useState("");
  const [scope, setScope] = useState("");
  const [scriptText, setScriptText] = useState("");
  const [criteriaText, setCriteriaText] = useState("");

  // Validation
  const [touched, setTouched] = useState<Record<string, boolean>>({});

  useEffect(() => {
    let cancelled = false;
    async function loadProjects() {
      try {
        const data = await fetchProjects();
        if (!cancelled) {
          setProjects(data);
          if (data.length > 0) setProjectId(data[0].id);
        }
      } catch {
        // Projects may not load — form still usable with manual ID
      } finally {
        if (!cancelled) setLoadingProjects(false);
      }
    }
    void loadProjects();
    return () => { cancelled = true; };
  }, []);

  /** Parse multi-line text into string array, filtering empty lines */
  function parseLines(text: string): string[] | null {
    const lines = text.split("\n").map((l) => l.trim()).filter(Boolean);
    return lines.length > 0 ? lines : null;
  }

  /** Validate form and return error message or null */
  function validate(): string | null {
    if (!projectId) return "Please select a project.";
    if (!objective.trim()) return "Objective is required.";
    return null;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setTouched({ projectId: true, objective: true });

    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const run = await createRun({
        projectId,
        objective: objective.trim(),
        scope: scope.trim() || null,
        script: parseLines(scriptText),
        acceptanceCriteria: parseLines(criteriaText),
      });
      void navigate(`/runs/${run.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create run");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div id="create-run-page">
      <div className="page-title-row">
        <h1>Create Run</h1>
      </div>

      <form className="create-form" onSubmit={(e) => { void handleSubmit(e); }} id="create-run-form">
        <div className="form-group">
          <label className="form-label" htmlFor="project-select">
            Project *
          </label>
          {loadingProjects ? (
            <div className="skeleton skeleton-row" style={{ height: 38 }} />
          ) : (
            <select
              id="project-select"
              className="form-input"
              value={projectId}
              onChange={(e) => setProjectId(e.target.value)}
              onBlur={() => setTouched((p) => ({ ...p, projectId: true }))}
            >
              <option value="">Select a project…</option>
              {projects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.repoUrl})
                </option>
              ))}
            </select>
          )}
          {touched.projectId && !projectId && (
            <div className="form-error">Project is required.</div>
          )}
        </div>

        <div className="form-group">
          <label className="form-label" htmlFor="objective-input">
            Objective *
          </label>
          <textarea
            id="objective-input"
            className="form-input"
            rows={3}
            placeholder="What should the worker accomplish?"
            value={objective}
            onChange={(e) => setObjective(e.target.value)}
            onBlur={() => setTouched((p) => ({ ...p, objective: true }))}
          />
          {touched.objective && !objective.trim() && (
            <div className="form-error">Objective is required.</div>
          )}
        </div>

        <div className="form-group">
          <label className="form-label" htmlFor="scope-input">
            Scope
          </label>
          <input
            id="scope-input"
            className="form-input"
            type="text"
            placeholder="Boundaries of the work (optional)"
            value={scope}
            onChange={(e) => setScope(e.target.value)}
          />
        </div>

        <div className="form-group">
          <label className="form-label" htmlFor="script-input">
            Script Commands
          </label>
          <textarea
            id="script-input"
            className="form-input"
            rows={4}
            placeholder={"One command per line (optional)\ne.g. npm install\nnpm run build"}
            value={scriptText}
            onChange={(e) => setScriptText(e.target.value)}
          />
          <div className="form-hint">
            Each line is a bash command executed sequentially in the workspace.
          </div>
        </div>

        <div className="form-group">
          <label className="form-label" htmlFor="criteria-input">
            Acceptance Criteria
          </label>
          <textarea
            id="criteria-input"
            className="form-input"
            rows={3}
            placeholder={"One criterion per line (optional)\ne.g. All tests pass\nNo lint errors"}
            value={criteriaText}
            onChange={(e) => setCriteriaText(e.target.value)}
          />
        </div>

        {error && <div className="form-error">{error}</div>}

        <div className="form-actions">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={submitting}
            id="submit-run-btn"
          >
            {submitting ? "Creating…" : "Create Run"}
          </button>
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => { void navigate("/runs"); }}
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
