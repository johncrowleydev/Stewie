/**
 * App — Root component with hierarchical route definitions and auth protection.
 *
 * Route structure:
 * - Public: /login, /register
 * - Global: /projects, /settings (protected, no project scope)
 * - Project-scoped: /p/:projectId/* (protected, wrapped in ProjectProvider)
 * - Admin: /admin/* (protected, admin-only — placeholder pages for now)
 *
 * REF: JOB-030 T-522
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
 * Used as a stub for admin pages that will be built in JOB-032 and JOB-033.
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

          {/* Admin routes (placeholder pages — JOB-032 and JOB-033) */}
          <Route path="/admin" element={<AdminRoute><Outlet /></AdminRoute>}>
            <Route path="users" element={<AdminPlaceholder title="User Management" />} />
            <Route path="invites" element={<AdminPlaceholder title="Invite Codes" />} />
            <Route path="system" element={<AdminPlaceholder title="System Dashboard" />} />
          </Route>
        </Route>
      </Routes>
    </AuthProvider>
  );
}

export default App;
