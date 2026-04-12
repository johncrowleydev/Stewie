/**
 * App — Root component with hierarchical route definitions and auth protection.
 *
 * Route structure:
 * - Public: /login, /register
 * - Global: /projects, /settings (protected, no project scope)
 * - Project-scoped: /p/:projectId/* (protected, wrapped in ProjectProvider)
 * - Admin: /admin/* (protected, admin-only — users and invites live,
 *   system dashboard placeholder for JOB-032)
 *
 * REF: JOB-030 T-522, JOB-033 T-553
 */
import { Routes, Route, Navigate, Outlet } from "react-router-dom";
import { AuthProvider } from "./contexts/AuthContext";
import { ProjectProvider } from "./contexts/ProjectContext";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { AdminRoute } from "./components/AdminRoute";
import { Layout } from "./components/Layout";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { DashboardPage } from "./pages/DashboardPage";
import { JobsPage } from "./pages/JobsPage";
import { JobDetailPage } from "./pages/JobDetailPage";
import { ProjectsPage } from "./pages/ProjectsPage";
import { EventsPage } from "./pages/EventsPage";
import { SettingsPage } from "./pages/SettingsPage";
import { AdminInvitesPage } from "./pages/admin/AdminInvitesPage";
import { AdminUsersPage } from "./pages/admin/AdminUsersPage";
import { Card } from "./components/ui";

/** localStorage key matching ProjectContext */
const LAST_PROJECT_KEY = "stewie:lastProjectId";

/**
 * RootRedirect — handles the `/` route.
 * Redirects to the last-used project dashboard if one exists in localStorage,
 * otherwise redirects to `/projects` (the project picker).
 */
function RootRedirect() {
  const lastProjectId = localStorage.getItem(LAST_PROJECT_KEY);
  if (lastProjectId) {
    return <Navigate to={`/p/${lastProjectId}/`} replace />;
  }
  return <Navigate to="/projects" replace />;
}

/**
 * AdminPlaceholder — renders a Card with the section title and "Coming soon" text.
 * Used as a stub for admin pages not yet built. JOB-033 pages (users, invites)
 * have been replaced with live components. Only System Dashboard (JOB-032) remains.
 */
function AdminPlaceholder({ title }: { title: string }) {
  return (
    <Card>
      <Card.Header>{title}</Card.Header>
      <p className="text-ds-text-muted text-md">
        Coming soon — this page is planned for a future sprint.
      </p>
    </Card>
  );
}

function App() {
  return (
    <AuthProvider>
      <Routes>
        {/* Public routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />

        {/* Protected routes — all wrapped in ProtectedRoute + Layout */}
        <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
          <Route path="/" element={<RootRedirect />} />
          <Route path="/projects" element={<ProjectsPage />} />
          <Route path="/settings" element={<SettingsPage />} />

          {/* Project-scoped routes */}
          <Route path="/p/:projectId" element={<ProjectProvider><Outlet /></ProjectProvider>}>
            <Route index element={<DashboardPage />} />
            <Route path="jobs" element={<JobsPage />} />
            <Route path="jobs/:id" element={<JobDetailPage />} />
            <Route path="events" element={<EventsPage />} />
          </Route>

          {/* Admin routes — users and invites are live (JOB-033), system dashboard placeholder (JOB-032) */}
          <Route path="/admin" element={<AdminRoute><Outlet /></AdminRoute>}>
            <Route path="users" element={<AdminUsersPage />} />
            <Route path="invites" element={<AdminInvitesPage />} />
            <Route path="system" element={<AdminPlaceholder title="System Dashboard" />} />
          </Route>
        </Route>
      </Routes>
    </AuthProvider>
  );
}

export default App;
