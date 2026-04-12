/**
 * AdminRoute — Route guard that restricts access to admin-only pages.
 * Redirects non-admin users to `/projects`. Shows nothing while auth
 * state is initializing (prevents content flash).
 *
 * Pattern matches ProtectedRoute.tsx — same loading/guard structure.
 *
 * REF: JOB-030 T-521
 */
import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { skeleton } from "../tw";
import type { ReactNode } from "react";

/** Props for the AdminRoute guard component */
interface AdminRouteProps {
  /** Child elements to render if user is an admin */
  children: ReactNode;
}

/**
 * Route guard — redirects to `/projects` if the user is not an admin.
 * Renders a loading skeleton while auth state initializes.
 */
export function AdminRoute({ children }: AdminRouteProps) {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <div className="p-xl">
        <div className={`${skeleton} h-[200px]`} />
      </div>
    );
  }

  if (!user || user.role !== "admin") {
    return <Navigate to="/projects" replace />;
  }

  return <>{children}</>;
}
