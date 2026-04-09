/**
 * Layout — App shell component with sidebar navigation and main content area.
 * Provides consistent structure across all pages with Stewie branding.
 * Includes theme toggle, user display, logout button, and Settings nav link.
 */
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useTheme } from "../hooks/useTheme";
import { useAuth } from "../contexts/AuthContext";

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

function JobsIcon() {
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

function EventsIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  );
}

/** Settings gear icon */
function SettingsIcon() {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

/** Maps route paths to page titles for the header bar */
function getPageTitle(pathname: string): string {
  if (pathname === "/") return "Dashboard";
  if (pathname === "/jobs/new") return "Create Job";
  if (pathname === "/jobs") return "Jobs";
  if (pathname.startsWith("/jobs/")) return "Job Details";
  if (pathname === "/projects") return "Projects";
  if (pathname === "/events") return "Events";
  if (pathname === "/settings") return "Settings";
  return "Stewie";
}

/** App shell — sidebar + header + main content outlet */
export function Layout() {
  const location = useLocation();
  const pageTitle = getPageTitle(location.pathname);
  const { theme, toggleTheme } = useTheme();
  const { user, logout } = useAuth();

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
          <NavLink to="/jobs" className={({ isActive }) => isActive ? "active" : ""}>
            <JobsIcon />
            Jobs
          </NavLink>
          <NavLink to="/projects" className={({ isActive }) => isActive ? "active" : ""}>
            <ProjectsIcon />
            Projects
          </NavLink>
          <NavLink to="/events" className={({ isActive }) => isActive ? "active" : ""}>
            <EventsIcon />
            Events
          </NavLink>
          <NavLink to="/settings" className={({ isActive }) => isActive ? "active" : ""}>
            <SettingsIcon />
            Settings
          </NavLink>
        </nav>

        <div className="sidebar-footer">
          {user && (
            <div className="sidebar-user" id="sidebar-user">
              <span className="sidebar-user-name">{user.username}</span>
              <span className="sidebar-user-role">{user.role}</span>
            </div>
          )}
          <button
            className="theme-toggle"
            onClick={toggleTheme}
            id="theme-toggle-btn"
            aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
          >
            <span className="theme-icon">{theme === "dark" ? "☀️" : "🌙"}</span>
            {theme === "dark" ? "Light mode" : "Dark mode"}
          </button>
          <button
            className="theme-toggle"
            onClick={logout}
            id="logout-btn"
            style={{ marginTop: "var(--space-xs)" }}
          >
            <span className="theme-icon">🚪</span>
            Sign out
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
