/**
 * ProtectedRoute — Route guard that redirects unauthenticated users to /login.
 * Shows a loading skeleton while auth state initializes.
 *
 * Usage: Wrap protected routes with <ProtectedRoute><Component /></ProtectedRoute>
 */
import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import type { ReactNode } from "react";

/** Route guard — redirects to /login if not authenticated */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, loading } = useAuth();

  if (loading) {
    return (
      <div style={{ padding: "var(--space-xl)" }}>
        <div className="skeleton skeleton-card" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}
