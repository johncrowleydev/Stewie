/**
 * LoginPage — Username/password login form with Stewie branding.
 * Calls POST /api/auth/login via AuthContext.
 * Redirects to dashboard on success. Shows error on failure.
 * Links to registration page.
 *
 * REF: CON-002 §4.0, JOB-027 T-403
 */
import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { useTheme } from "../hooks/useTheme";
import { btnPrimary } from "../tw";

/** Login page with branded centered form */
export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  useTheme();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!username.trim() || !password) {
      setError("Username and password are required.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await login({ username: username.trim(), password });
      void navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-ds-bg p-lg" id="login-page">
      <div className="w-full max-w-[400px] bg-ds-surface border border-ds-border rounded-lg py-xl px-lg">
        <div className="flex items-center justify-center gap-sm mb-xl flex-col">
          <img src="/stewie-logo.png" alt="Stewie" className="w-[228px] h-auto" />
          <span className="font-sans text-2xl font-bold tracking-wide text-ds-primary mt-xs">stewie</span>
        </div>

        <form onSubmit={(e) => { void handleSubmit(e); }} id="login-form">
          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="login-username">Username</label>
            <input
              id="login-username"
              className="w-full py-sm px-md bg-ds-bg border border-ds-border rounded-md text-ds-text text-md font-sans transition-[border-color] duration-150 focus:outline-none focus:border-ds-primary focus:shadow-[0_0_0_3px_var(--color-primary-muted)] placeholder:text-ds-text-muted"
              type="text"
              autoComplete="username"
              placeholder="Enter your username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
            />
          </div>

          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="login-password">Password</label>
            <input
              id="login-password"
              className="w-full py-sm px-md bg-ds-bg border border-ds-border rounded-md text-ds-text text-md font-sans transition-[border-color] duration-150 focus:outline-none focus:border-ds-primary focus:shadow-[0_0_0_3px_var(--color-primary-muted)] placeholder:text-ds-text-muted"
              type="password"
              autoComplete="current-password"
              placeholder="Enter your password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {error && <div className="text-ds-failed text-s mt-sm">{error}</div>}

          <button
            type="submit"
            className={`${btnPrimary} w-full mt-md`}
            disabled={submitting}
            id="login-submit-btn"
          >
            {submitting ? "Signing in…" : "Sign In"}
          </button>
        </form>

        <div className="mt-lg text-center text-s text-ds-text-muted">
          <span>Don't have an account?</span>{" "}
          <Link to="/register" className="text-ds-primary no-underline font-medium hover:underline">Register</Link>
        </div>
      </div>
    </div>
  );
}
