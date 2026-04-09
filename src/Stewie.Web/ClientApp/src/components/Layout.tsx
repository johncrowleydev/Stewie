/**
 * Layout — App shell component with sidebar navigation and main content area.
 * Provides consistent structure across all pages with Stewie branding.
 */
import { NavLink, Outlet, useLocation } from "react-router-dom";

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

/** Maps route paths to page titles for the header bar */
function getPageTitle(pathname: string): string {
  if (pathname === "/") return "Dashboard";
  if (pathname === "/runs") return "Runs";
  if (pathname.startsWith("/runs/")) return "Run Details";
  if (pathname === "/projects") return "Projects";
  return "Stewie";
}

/** App shell — sidebar + header + main content outlet */
export function Layout() {
  const location = useLocation();
  const pageTitle = getPageTitle(location.pathname);

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
        </nav>

        <div className="sidebar-footer">
          Stewie v0.1.0 — Governance-first orchestration
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
