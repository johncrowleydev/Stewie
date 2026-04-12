/**
 * ProtectedRoute — Route guard that redirects unauthenticated users to /login.
 * Shows a loading skeleton while auth state initializes.
 */
import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { skeleton } from "../tw";
import type { ReactNode } from "react";

/** Route guard — redirects to /login if not authenticated */
export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, loading } = useAuth();

  if (loading) {
    return (
      <div className="p-xl">
        <div className={`${skeleton} h-[200px]`} />
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}
