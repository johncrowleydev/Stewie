/**
 * RepoCombobox — Search-as-you-type GitHub repository picker.
 * REF: JOB-025 T-303, JOB-027 T-407
 */
import { useState, useEffect, useRef, useCallback } from "react";
import { fetchGitHubRepos } from "../api/client";
import { formInput } from "../tw";
import type { GitHubRepo } from "../types";

interface RepoComboboxProps {
  onSelect: (repoUrl: string) => void;
  disabled?: boolean;
}

const SEARCH_DEBOUNCE_MS = 400;

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

  const searchRepos = useCallback(async (searchTerm: string) => {
    if (abortRef.current) abortRef.current.abort();
    abortRef.current = new AbortController();
    setLoading(true); setError(null);
    try {
      const trimmed = searchTerm.trim();
      setRepos(await fetchGitHubRepos(trimmed || undefined));
      setIsOpen(true);
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") return;
      setError(err instanceof Error ? err.message : "Failed to search repositories");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setIsOpen(false);
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => () => {
    if (abortRef.current) abortRef.current.abort();
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  const handleInputChange = useCallback((value: string) => {
    setQuery(value); setSelectedName("");
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => { void searchRepos(value); }, SEARCH_DEBOUNCE_MS);
  }, [searchRepos]);

  const handleFocus = useCallback(() => {
    if (repos.length === 0 && !loading) void searchRepos("");
    else setIsOpen(true);
  }, [repos.length, loading, searchRepos]);

  const handleSelect = useCallback((repo: GitHubRepo) => {
    setSelectedName(repo.fullName); setQuery(repo.fullName); setIsOpen(false); onSelect(repo.htmlUrl);
  }, [onSelect]);

  if (error) {
    return (
      <div id="repo-combobox-error">
        <div className="text-xs text-ds-failed italic mt-xs">Could not search repositories. Enter URL manually.</div>
      </div>
    );
  }

  return (
    <div className="relative" ref={containerRef} id="repo-combobox">
      <div className="relative">
        <input
          className={formInput}
          id="repo-combobox-input"
          type="text"
          placeholder="Search your repositories…"
          value={query}
          onChange={(e) => handleInputChange(e.target.value)}
          onFocus={handleFocus}
          disabled={disabled}
          autoComplete="off"
        />
        {loading && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 border-2 border-ds-border border-t-ds-primary rounded-full animate-spin" />
        )}
      </div>

      {isOpen && !loading && repos.length > 0 && (
        <ul className="absolute z-50 w-full mt-xs bg-ds-surface border border-ds-border rounded-md shadow-ds-lg max-h-[260px] overflow-y-auto list-none p-0 m-0 animate-[dropdown-appear_150ms_ease]" id="repo-combobox-dropdown">
          {repos.map((repo) => (
            <li
              key={repo.fullName}
              className={`px-md py-sm cursor-pointer transition-colors duration-100 hover:bg-ds-surface-hover ${selectedName === repo.fullName ? "bg-ds-primary-muted" : ""}`}
              onClick={() => handleSelect(repo)}
            >
              <div className="flex items-center gap-sm">
                <span className="font-medium text-s text-ds-text">{repo.name}</span>
                {repo.isPrivate && (
                  <span className="text-[10px] py-px px-1.5 rounded-full bg-[rgba(245,166,35,0.12)] text-ds-warning font-medium">Private</span>
                )}
              </div>
              <div className="text-xs text-ds-text-muted mt-px">{repo.fullName}</div>
            </li>
          ))}
        </ul>
      )}

      {isOpen && loading && (
        <div className="absolute z-50 w-full mt-xs bg-ds-surface border border-ds-border rounded-md shadow-ds-lg p-md text-center text-s text-ds-text-muted">Searching…</div>
      )}

      {isOpen && !loading && repos.length === 0 && query && (
        <div className="absolute z-50 w-full mt-xs bg-ds-surface border border-ds-border rounded-md shadow-ds-lg p-md text-center text-s text-ds-text-muted">No repositories match &quot;{query}&quot;</div>
      )}
    </div>
  );
}
