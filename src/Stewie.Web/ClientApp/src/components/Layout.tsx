/**
 * Layout — App shell component with sidebar navigation and main content area.
 * Provides consistent structure across all pages with Stewie branding.
 * Includes theme toggle (DEF-001) and Events nav link (T-025).
 */
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useTheme } from "../hooks/useTheme";

/** SVG icon components for sidebar navigation */
function DashboardIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </svg>
  );
}

function RunsIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
    </svg>
  );
}

function ProjectsIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
    </svg>
  );
}

/** Events timeline icon */
function EventsIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  );
}

/** Maps route paths to page titles for the header bar */
function getPageTitle(pathname: string): string {
  if (pathname === "/") return "Dashboard";
  if (pathname === "/runs") return "Runs";
  if (pathname.startsWith("/runs/")) return "Run Details";
  if (pathname === "/projects") return "Projects";
  if (pathname === "/events") return "Events";
  return "Stewie";
}

/** App shell — sidebar + header + main content outlet */
export function Layout() {
  const location = useLocation();
  const pageTitle = getPageTitle(location.pathname);
  const { theme, toggleTheme } = useTheme();

  return (
    <div className="app-layout">
      <aside className="sidebar" id="main-sidebar">
        <div className="sidebar-brand">
          <img src="/stewie-logo.png" alt="Stewie logo" />
          <h1>stewie</h1>
        </div>

        <nav className="sidebar-nav" id="main-nav">
          <NavLink to="/" end className={({ isActive }) => isActive ? "active" : ""}>
            <DashboardIcon />
            Dashboard
          </NavLink>
          <NavLink to="/runs" className={({ isActive }) => isActive ? "active" : ""}>
            <RunsIcon />
            Runs
          </NavLink>
          <NavLink to="/projects" className={({ isActive }) => isActive ? "active" : ""}>
            <ProjectsIcon />
            Projects
          </NavLink>
          <NavLink to="/events" className={({ isActive }) => isActive ? "active" : ""}>
            <EventsIcon />
            Events
          </NavLink>
        </nav>

        <div className="sidebar-footer">
          <button
            className="theme-toggle"
            onClick={toggleTheme}
            id="theme-toggle-btn"
            aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
          >
            <span className="theme-icon">{theme === "dark" ? "☀️" : "🌙"}</span>
            {theme === "dark" ? "Light mode" : "Dark mode"}
          </button>
        </div>
      </aside>

      <main className="main-content">
        <header className="main-header">
          <h2>{pageTitle}</h2>
        </header>
        <div className="page-content">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
