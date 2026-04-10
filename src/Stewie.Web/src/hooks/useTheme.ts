/**
 * useTheme — Custom hook for managing light/dark theme state.
 *
 * Behavior:
 * 1. On mount, reads saved preference from localStorage
 * 2. If no saved preference, respects `prefers-color-scheme` media query
 * 3. Falls back to dark if media query unavailable
 * 4. Persists changes to localStorage on toggle
 * 5. Sets `data-theme` attribute on `<html>` for CSS variable switching
 *
 * @returns Current theme string and toggle function
 */
import { useState, useEffect, useCallback } from "react";

/** Valid theme values */
export type Theme = "light" | "dark";

/** localStorage key for persisting preference */
const STORAGE_KEY = "stewie-theme";

/**
 * Detects the user's OS-level color scheme preference.
 * Falls back to "dark" if the media query is unavailable.
 */
function getSystemPreference(): Theme {
  if (typeof window !== "undefined" && window.matchMedia) {
    return window.matchMedia("(prefers-color-scheme: light)").matches
      ? "light"
      : "dark";
  }
  return "dark";
}

/**
 * Reads the initial theme: localStorage → OS preference → dark fallback.
 */
function getInitialTheme(): Theme {
  if (typeof window !== "undefined") {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "light" || stored === "dark") {
      return stored;
    }
  }
  return getSystemPreference();
}

/**
 * Hook that manages theme state with localStorage persistence
 * and `data-theme` DOM attribute synchronization.
 */
export function useTheme(): { theme: Theme; toggleTheme: () => void } {
  const [theme, setTheme] = useState<Theme>(getInitialTheme);

  // Sync data-theme attribute on <html> whenever theme changes
  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  const toggleTheme = useCallback(() => {
    setTheme((prev) => (prev === "dark" ? "light" : "dark"));
  }, []);

  return { theme, toggleTheme };
}
