/**
 * Layout — App shell component with sidebar navigation and main content area.
 * Provides consistent structure across all pages with Stewie branding.
 * Responsive: hamburger menu on mobile, overlay sidebar.
 * User menu with theme toggle and logout in the top-right header.
 *
 * DECISION: Sidebar nav is data-driven via navConfig.ts. Items are filtered
 * by scope (project/global/admin) and user role at render time. Section
 * headers visually group items by scope.
 *
 * READING GUIDE FOR INCIDENT RESPONDERS:
 * 1. If sidebar links are missing → check filterNavItems() and user role
 * 2. If wrong project in sidebar → check ProjectContext read
 * 3. If header title is wrong → check getPageTitle()
 * 4. If user menu doesn't close → check handleClickOutside effect
 *
 * REF: JOB-030 T-523, JOB-031 T-531, JOB-031 T-532
 */
import { useState, useRef, useEffect, useContext, useMemo } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useTheme } from "../hooks/useTheme";
import { useAuth } from "../contexts/AuthContext";
import { ProjectContext } from "../contexts/ProjectContext";
import { useSignalR } from "../hooks/useSignalR";
import {
  NAV_ITEMS,
  filterNavItems,
  resolveNavPath,
} from "./sidebar/navConfig";
import type { NavItem, NavScope } from "./sidebar/navConfig";
import { ProjectSwitcher } from "./sidebar/ProjectSwitcher";

/** Maps route paths to page titles for the header bar */
function getPageTitle(pathname: string): string {
  if (pathname === "/" || pathname === "/projects") return "Projects";
  if (pathname === "/settings") return "Settings";

  // Project-scoped routes: /p/:projectId/*
  const projectMatch = pathname.match(/^\/p\/[^/]+(\/.*)?$/);
  if (projectMatch) {
    const sub = projectMatch[1] ?? "";
    if (sub === "" || sub === "/") return "Dashboard";
    if (sub === "/jobs") return "Jobs";
    if (sub.startsWith("/jobs/")) return "Job Details";
    if (sub === "/events") return "Events";
    return "Dashboard";
  }

  // Admin routes: /admin/*
  if (pathname.startsWith("/admin")) {
    if (pathname === "/admin/users") return "User Management";
    if (pathname === "/admin/invites") return "Invite Codes";
    if (pathname === "/admin/system") return "System Dashboard";
    return "Admin";
  }

  return "Stewie";
}

/** Active nav link style helper */
function navLinkClass(isActive: boolean): string {
  const base = "flex items-center gap-md py-sm px-md rounded-md text-s font-medium transition-all duration-150 relative";
  return isActive
    ? `${base} bg-ds-primary-muted text-ds-primary before:content-[''] before:absolute before:-left-md before:top-1/2 before:-translate-y-1/2 before:w-[3px] before:h-5 before:bg-ds-primary before:rounded-r-sm [&_svg]:opacity-100`
    : `${base} text-ds-text-muted hover:bg-ds-surface-hover hover:text-ds-text`;
}

/**
 * Section header labels for each nav scope.
 * Rendered as small uppercase dividers between scope groups.
 */
const SCOPE_LABELS: Record<NavScope, string> = {
  project: "Project",
  global: "General",
  admin: "Admin",
} as const;

/**
 * Renders a group of nav items for a single scope with an optional header.
 *
 * @param items - Nav items belonging to this scope (already filtered)
 * @param scope - The scope being rendered (for section header label)
 * @param projectId - Active project ID for path resolution (null if none)
 * @param showHeader - Whether to render the section header
 */
function NavSection({
  items,
  scope,
  projectId,
  showHeader,
}: {
  items: NavItem[];
  scope: NavScope;
  projectId: string | null;
  showHeader: boolean;
}) {
  if (items.length === 0) return null;

  return (
    <>
      {showHeader && (
        <span
          className="text-[10px] font-semibold uppercase tracking-wider text-ds-text-muted mt-md mb-xs px-md select-none"
          aria-hidden="true"
        >
          {SCOPE_LABELS[scope]}
        </span>
      )}
      {items.map((item) => {
        const Icon = item.icon;
        const resolvedPath = resolveNavPath(item.path, projectId);
        return (
          <NavLink
            key={item.id}
            to={resolvedPath}
            end={item.end}
            className={({ isActive }) => navLinkClass(isActive)}
            data-testid={`nav-${item.id}`}
          >
            <Icon />
            {item.label}
          </NavLink>
        );
      })}
    </>
  );
}

/** App shell — sidebar + header + main content outlet */
export function Layout() {
  const location = useLocation();
  const pageTitle = getPageTitle(location.pathname);
  const { theme, toggleTheme } = useTheme();
  const { user, logout } = useAuth();
  const { state: signalRState } = useSignalR();
  const isLive = signalRState === "connected";

  // Optional project context — null on global pages (/projects, /settings)
  const projectCtx = useContext(ProjectContext);
  const [menuOpen, setMenuOpen] = useState(false);
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  const userRole = user?.role ?? "user";
  const hasProject = projectCtx !== null;
  const projectId = projectCtx?.projectId ?? null;

  // Filter nav items by current context — memoized to avoid re-filtering on every render
  const visibleItems = useMemo(
    () => filterNavItems(NAV_ITEMS, hasProject, userRole),
    [hasProject, userRole],
  );

  // Group visible items by scope for sectioned rendering
  const projectItems = useMemo(() => visibleItems.filter((i) => i.scope === "project"), [visibleItems]);
  const globalItems = useMemo(() => visibleItems.filter((i) => i.scope === "global"), [visibleItems]);
  const adminItems = useMemo(() => visibleItems.filter((i) => i.scope === "admin"), [visibleItems]);

  // Close user menu on outside click
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Close mobile sidebar on route change
  useEffect(() => {
    setMobileSidebarOpen(false);
  }, [location.pathname]);

  return (
    <div className="flex min-h-screen">
      {/* Mobile overlay — click to dismiss sidebar */}
      {mobileSidebarOpen && (
        <div
          className="fixed inset-0 bg-black/50 backdrop-blur-[2px] z-[199] animate-[overlay-fade-in_200ms_ease] md:hidden"
          onClick={() => setMobileSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        className={`w-sidebar bg-ds-surface border-r border-ds-border flex flex-col fixed top-0 left-0 bottom-0 z-[100] transition-transform duration-250 ease-in-out max-md:-translate-x-full max-md:shadow-ds-lg ${mobileSidebarOpen ? "max-md:translate-x-0" : ""}`}
        id="main-sidebar"
      >
        <div className="flex flex-col items-center justify-center p-lg border-b border-ds-border">
          <img src="/stewie-logo.png" alt="Stewie" className="w-full px-lg rounded-sm" />
          <span className="font-sans text-2xl font-bold tracking-wide text-ds-primary mt-xs">stewie</span>
        </div>

        {/* Project switcher dropdown — between logo and nav */}
        <ProjectSwitcher projectId={projectId} />

        <nav className="flex-1 p-md flex flex-col gap-xs overflow-y-auto" id="main-nav">
          {/* Global links — always visible at top */}
          <NavSection items={globalItems} scope="global" projectId={projectId} showHeader={false} />

          {/* Project-scoped links — only when a project is active */}
          <NavSection items={projectItems} scope="project" projectId={projectId} showHeader={hasProject} />

          {/* Admin links — only for admin users */}
          <NavSection items={adminItems} scope="admin" projectId={projectId} showHeader={adminItems.length > 0} />
        </nav>
      </aside>

      <main className="flex-1 ml-sidebar min-h-screen max-md:ml-0">
        <header className="h-header border-b border-ds-border flex items-center justify-between px-xl bg-ds-surface sticky top-0 z-50 max-md:px-md">
          <div className="flex items-center gap-md">
            {/* Hamburger — visible only on mobile */}
            <button
              className="hidden max-md:flex items-center justify-center w-9 h-9 p-0 bg-transparent border border-ds-border rounded-md text-ds-text-muted cursor-pointer shrink-0 transition-all duration-150 hover:bg-ds-surface-hover hover:text-ds-text hover:border-ds-border-hover [&_svg]:w-5 [&_svg]:h-5"
              onClick={() => setMobileSidebarOpen(!mobileSidebarOpen)}
              aria-label="Toggle navigation menu"
              id="mobile-menu-btn"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="3" y1="6" x2="21" y2="6" />
                <line x1="3" y1="12" x2="21" y2="12" />
                <line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            </button>
            <h2 className="text-base font-semibold text-ds-text max-md:text-md">{pageTitle}</h2>
            {isLive ? (
              <span className="inline-flex items-center gap-1.5 text-xs font-medium text-ds-completed" id="global-live" title="Connected to live data feed">
                <span className="w-2 h-2 rounded-full bg-ds-completed animate-[pulse_1.5s_ease-in-out_infinite]" />
                Live
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 text-xs font-medium text-ds-running" id="global-polling" title="Disconnected from live feed, polling for updates">
                <span className="w-2 h-2 rounded-full bg-ds-running animate-[pulse_1.5s_ease-in-out_infinite]" />
                Polling
              </span>
            )}
          </div>
          <div className="relative" ref={menuRef}>
            <button
              className="flex items-center gap-sm bg-transparent border border-ds-border rounded-md py-1.5 px-3 cursor-pointer text-ds-text-muted text-s font-sans transition-all duration-150 hover:bg-ds-surface-hover hover:text-ds-text hover:border-ds-border-hover"
              onClick={() => setMenuOpen(!menuOpen)}
              id="user-menu-btn"
              aria-label="User menu"
            >
              <svg className="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
              {user && <span className="font-medium max-md:hidden">{user.username}</span>}
              <svg className={`w-3.5 h-3.5 transition-transform duration-150 ${menuOpen ? "rotate-180" : ""}`} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </button>
            {menuOpen && (
              <div className="absolute top-[calc(100%+6px)] right-0 min-w-[200px] bg-ds-surface border border-ds-border rounded-lg shadow-ds-lg z-[200] overflow-hidden animate-[dropdown-appear_150ms_ease]" id="user-dropdown">
                {user && (
                  <div className="p-md flex flex-col gap-0.5">
                    <span className="text-s font-semibold text-ds-text">{user.username}</span>
                    <span className="text-xs text-ds-text-muted capitalize">{user.role}</span>
                  </div>
                )}
                <div className="h-px bg-ds-border" />
                <button
                  className="flex items-center gap-sm w-full py-sm px-md bg-transparent border-none cursor-pointer text-ds-text-muted text-s font-sans transition-all duration-150 hover:bg-ds-surface-hover hover:text-ds-text"
                  onClick={() => { toggleTheme(); setMenuOpen(false); }}
                  id="theme-toggle-btn"
                >
                  {theme === "dark" ? (
                    <svg className="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="12" r="5" /><line x1="12" y1="1" x2="12" y2="3" /><line x1="12" y1="21" x2="12" y2="23" /><line x1="4.22" y1="4.22" x2="5.64" y2="5.64" /><line x1="18.36" y1="18.36" x2="19.78" y2="19.78" /><line x1="1" y1="12" x2="3" y2="12" /><line x1="21" y1="12" x2="23" y2="12" /><line x1="4.22" y1="19.78" x2="5.64" y2="18.36" /><line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
                    </svg>
                  ) : (
                    <svg className="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
                    </svg>
                  )}
                  {theme === "dark" ? "Light mode" : "Dark mode"}
                </button>
                <button
                  className="flex items-center gap-sm w-full py-sm px-md bg-transparent border-none cursor-pointer text-ds-text-muted text-s font-sans transition-all duration-150 hover:bg-ds-surface-hover hover:text-ds-failed"
                  onClick={() => { logout(); setMenuOpen(false); }}
                  id="logout-btn"
                >
                  <svg className="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" /><polyline points="16 17 21 12 16 7" /><line x1="21" y1="12" x2="9" y2="12" />
                  </svg>
                  Sign out
                </button>
              </div>
            )}
          </div>
        </header>
        <div className="p-xl max-w-[1200px] max-md:p-md">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
