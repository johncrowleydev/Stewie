/**
 * navConfig — Typed navigation configuration for the sidebar.
 *
 * Defines all sidebar navigation items as a data structure instead of
 * hardcoded JSX. Each item specifies its scope (project/global/admin),
 * required role, icon component, route path, and display properties.
 *
 * DECISION: Using a flat array with `scope` discriminator instead of nested
 * groups because the sidebar rendering loop handles grouping/sectioning.
 * The `:projectId` placeholder in paths is replaced at render time.
 *
 * Used by: Layout.tsx sidebar rendering
 * REF: JOB-031 T-530
 */
import type { ComponentType } from "react";
import {
  DashboardIcon,
  JobsIcon,
  ProjectsIcon,
  EventsIcon,
  SettingsIcon,
  SystemIcon,
  UsersIcon,
  InvitesIcon,
} from "./icons";

/**
 * Scope determines when a nav item is visible in the sidebar.
 * - `project`: only when a project is active (inside /p/:projectId/*)
 * - `global`: always visible regardless of active project
 * - `admin`: only visible to users with role === 'admin'
 */
export type NavScope = "project" | "global" | "admin";

/**
 * A single sidebar navigation item.
 *
 * PRECONDITION: If scope is 'admin', requiredRole must be 'admin'.
 * POSTCONDITION: Only items matching active scope + user role are rendered.
 */
export interface NavItem {
  /** Unique identifier for the nav item (used as React key) */
  id: string;
  /** Display label shown beside the icon */
  label: string;
  /** Route path — may include `:projectId` placeholder for project scope */
  path: string;
  /** Icon component rendered before the label */
  icon: ComponentType;
  /** Determines visibility context */
  scope: NavScope;
  /** If set, item is only shown to users with this role */
  requiredRole?: "admin";
  /** If true, NavLink uses `end` prop for exact path matching */
  end?: boolean;
}

/**
 * Canonical sidebar navigation items — single source of truth.
 *
 * Order within each scope determines render order in the sidebar.
 * The Layout component groups these by scope and adds section headers.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  // ── Project-scoped items ──
  {
    id: "dashboard",
    label: "Dashboard",
    path: "/p/:projectId/",
    icon: DashboardIcon,
    scope: "project",
    end: true,
  },
  {
    id: "jobs",
    label: "Jobs",
    path: "/p/:projectId/jobs",
    icon: JobsIcon,
    scope: "project",
  },
  {
    id: "events",
    label: "Events",
    path: "/p/:projectId/events",
    icon: EventsIcon,
    scope: "project",
  },

  // ── Global items ──
  {
    id: "projects",
    label: "Projects",
    path: "/projects",
    icon: ProjectsIcon,
    scope: "global",
  },
  {
    id: "settings",
    label: "Settings",
    path: "/settings",
    icon: SettingsIcon,
    scope: "global",
  },

  // ── Admin items ──
  {
    id: "admin-system",
    label: "System",
    path: "/admin/system",
    icon: SystemIcon,
    scope: "admin",
    requiredRole: "admin",
  },
  {
    id: "admin-users",
    label: "Users",
    path: "/admin/users",
    icon: UsersIcon,
    scope: "admin",
    requiredRole: "admin",
  },
  {
    id: "admin-invites",
    label: "Invites",
    path: "/admin/invites",
    icon: InvitesIcon,
    scope: "admin",
    requiredRole: "admin",
  },
] as const;

/**
 * Resolve a nav item path by replacing the `:projectId` placeholder.
 *
 * @param path - Raw path from NavItem (may contain `:projectId`)
 * @param projectId - Active project ID to substitute, or null if none
 * @returns Resolved path with placeholder replaced, or raw path if no projectId
 */
export function resolveNavPath(path: string, projectId: string | null): string {
  if (!projectId) return path;
  return path.replace(":projectId", projectId);
}

/**
 * Filter nav items based on current context (active project, user role).
 *
 * DECISION: Filter at render time rather than pre-computing to keep the
 * config array immutable and avoid stale closures over context values.
 *
 * @param items - Full nav item array
 * @param hasProject - Whether a project is currently active
 * @param userRole - Current user's role ('admin' | 'user')
 * @returns Filtered array of items visible in current context
 */
export function filterNavItems(
  items: readonly NavItem[],
  hasProject: boolean,
  userRole: string,
): NavItem[] {
  return items.filter((item) => {
    // Hide project-scoped items when no project is active
    if (item.scope === "project" && !hasProject) return false;

    // Hide admin items for non-admin users
    if (item.requiredRole === "admin" && userRole !== "admin") return false;

    return true;
  });
}
