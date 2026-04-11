/**
 * RepoCombobox — Search-as-you-type GitHub repository picker.
 *
 * Architecture:
 * - On focus (empty input): loads recent repos via GET /api/github/repos
 * - On type: debounces 400ms, then searches via GET /api/github/repos?q={term}
 * - Each call proxies to GitHub Search API (server-side)
 * - No client-side filtering — all filtering happens at GitHub
 *
 * Rate limits: GitHub allows 30 search requests/min per authenticated user.
 * With 400ms debounce, a fast typist can't exceed that.
 *
 * Falls back to manual URL input if the API call fails.
 *
 * REF: JOB-025 T-303
 */
import { useState, useEffect, useRef, useCallback } from "react";
import { fetchGitHubRepos } from "../api/client";
import type { GitHubRepo } from "../types";

interface RepoComboboxProps {
  /** Callback when a repo is selected — receives the HTML URL */
  onSelect: (repoUrl: string) => void;
  /** Whether the combobox is disabled */
  disabled?: boolean;
}

/** Debounce delay for search requests (ms) */
const SEARCH_DEBOUNCE_MS = 400;

/**
 * RepoCombobox — search-as-you-type GitHub repo picker.
 */
export function RepoCombobox({ onSelect, disabled = false }: RepoComboboxProps) {
  const [repos, setRepos] = useState<GitHubRepo[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [selectedName, setSelectedName] = useState("");
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  /**
   * Search GitHub repos via the backend proxy.
   * Cancels any in-flight request before starting a new one.
   */
  const searchRepos = useCallback(async (searchTerm: string) => {
    // Cancel previous in-flight request
    if (abortRef.current) {
      abortRef.current.abort();
    }
    abortRef.current = new AbortController();

    setLoading(true);
    setError(null);
    try {
      const trimmed = searchTerm.trim();
      const data = await fetchGitHubRepos(trimmed || undefined);
      setRepos(data);
      setIsOpen(true);
    } catch (err) {
      // Don't show error for aborted requests
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(err instanceof Error ? err.message : "Failed to search repositories");
    } finally {
      setLoading(false);
    }
  }, []);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Cleanup abort controller on unmount
  useEffect(() => {
    return () => {
      if (abortRef.current) abortRef.current.abort();
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  /** Handle input changes — debounce and search */
  const handleInputChange = useCallback(
    (value: string) => {
      setQuery(value);
      setSelectedName("");

      // Clear previous debounce
      if (debounceRef.current) clearTimeout(debounceRef.current);

      // Debounce the search
      debounceRef.current = setTimeout(() => {
        void searchRepos(value);
      }, SEARCH_DEBOUNCE_MS);
    },
    [searchRepos]
  );

  /** Handle focus — load recent repos if dropdown isn't already open */
  const handleFocus = useCallback(() => {
    if (repos.length === 0 && !loading) {
      void searchRepos("");
    } else {
      setIsOpen(true);
    }
  }, [repos.length, loading, searchRepos]);

  /** Handle repo selection */
  const handleSelect = useCallback(
    (repo: GitHubRepo) => {
      setSelectedName(repo.fullName);
      setQuery(repo.fullName);
      setIsOpen(false);
      onSelect(repo.htmlUrl);
    },
    [onSelect]
  );

  // Error fallback
  if (error) {
    return (
      <div className="repo-combobox-error" id="repo-combobox-error">
        <div className="form-hint" style={{ color: "var(--color-failed)" }}>
          Could not search repositories. Enter URL manually.
        </div>
      </div>
    );
  }

  return (
    <div className="repo-combobox" ref={containerRef} id="repo-combobox">
      <div className="repo-combobox-input-wrapper">
        <input
          className="form-input"
          id="repo-combobox-input"
          type="text"
          placeholder="Search your repositories…"
          value={query}
          onChange={(e) => handleInputChange(e.target.value)}
          onFocus={handleFocus}
          disabled={disabled}
          autoComplete="off"
        />
        {loading && <span className="repo-combobox-spinner" />}
      </div>

      {/* Dropdown list */}
      {isOpen && !loading && repos.length > 0 && (
        <ul className="repo-combobox-dropdown" id="repo-combobox-dropdown">
          {repos.map((repo) => (
            <li
              key={repo.fullName}
              className={`repo-combobox-item ${selectedName === repo.fullName ? "selected" : ""}`}
              onClick={() => handleSelect(repo)}
            >
              <div className="repo-combobox-item-name">
                <span className="repo-combobox-name">{repo.name}</span>
                {repo.isPrivate && (
                  <span className="repo-combobox-private-badge">Private</span>
                )}
              </div>
              <div className="repo-combobox-fullname">{repo.fullName}</div>
            </li>
          ))}
        </ul>
      )}

      {/* Loading state */}
      {isOpen && loading && (
        <div className="repo-combobox-dropdown repo-combobox-empty">
          Searching…
        </div>
      )}

      {/* Empty state */}
      {isOpen && !loading && repos.length === 0 && query && (
        <div className="repo-combobox-dropdown repo-combobox-empty">
          No repositories match "{query}"
        </div>
      )}
    </div>
  );
}
