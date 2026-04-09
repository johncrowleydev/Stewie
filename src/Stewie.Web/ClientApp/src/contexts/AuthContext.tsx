/**
 * AuthContext — React context for authentication state management.
 * Stores JWT in localStorage, provides login/logout/register actions,
 * and exposes the authenticated user object.
 *
 * Usage: Wrap App in <AuthProvider>, then use useAuth() hook.
 *
 * REF: CON-002 §4.0
 */
import { createContext, useContext, useState, useCallback, useEffect } from "react";
import type { ReactNode } from "react";
import type { AuthUser, LoginRequest, RegisterRequest } from "../types";
import {
  login as apiLogin,
  register as apiRegister,
  getToken,
  setToken,
  clearToken,
} from "../api/client";

/** Auth context shape */
interface AuthContextValue {
  /** Currently authenticated user, or null */
  user: AuthUser | null;
  /** Whether a user is authenticated */
  isAuthenticated: boolean;
  /** Whether auth state is still being initialized */
  loading: boolean;
  /** Login with credentials — stores JWT and sets user */
  login: (data: LoginRequest) => Promise<void>;
  /** Register with invite code — stores JWT and sets user */
  register: (data: RegisterRequest) => Promise<void>;
  /** Logout — clears JWT and user */
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Decode the user payload from a JWT token (without verification — that's the server's job).
 * Returns null if token is missing or malformed.
 */
function decodeUser(token: string): AuthUser | null {
  try {
    const payload = token.split(".")[1];
    const decoded = JSON.parse(atob(payload)) as Record<string, string>;
    return {
      id: decoded.sub ?? decoded.id ?? "",
      username: decoded.username ?? decoded.unique_name ?? "",
      role: (decoded.role ?? "user") as "admin" | "user",
    };
  } catch {
    return null;
  }
}

/** AuthProvider — wraps children with auth state */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Initialize from stored token
  useEffect(() => {
    const token = getToken();
    if (token) {
      const decoded = decodeUser(token);
      setUser(decoded);
    }
    setLoading(false);
  }, []);

  const loginAction = useCallback(async (data: LoginRequest) => {
    const response = await apiLogin(data);
    setToken(response.token);
    setUser(response.user);
  }, []);

  const registerAction = useCallback(async (data: RegisterRequest) => {
    const response = await apiRegister(data);
    setToken(response.token);
    setUser(response.user);
  }, []);

  const logout = useCallback(() => {
    clearToken();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        loading,
        login: loginAction,
        register: registerAction,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

/**
 * useAuth — Access auth context from any component.
 * Must be used inside AuthProvider.
 */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return ctx;
}
