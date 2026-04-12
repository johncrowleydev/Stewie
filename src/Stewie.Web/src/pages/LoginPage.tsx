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

/** Login page with branded centered form */
export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  useTheme(); // Sets data-theme on <html> so CSS variables respect light/dark
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
    <div
      className="flex items-center justify-center min-h-screen bg-[var(--color-bg)] p-[var(--space-lg)]"
      id="login-page"
    >
      <div className="w-full max-w-[400px] bg-[var(--color-surface)] border border-[var(--color-border)] rounded-[var(--radius-lg)] py-[var(--space-xl)] px-[var(--space-lg)]">
        <div className="flex items-center justify-center gap-[var(--space-sm)] mb-[var(--space-xl)] flex-col">
          <img src="/stewie-logo.png" alt="Stewie" className="w-[228px] h-auto" />
          <span className="brand-wordmark">stewie</span>
        </div>


        <form onSubmit={(e) => { void handleSubmit(e); }} id="login-form">
          <div className="form-group">
            <label className="form-label" htmlFor="login-username">Username</label>
            <input
              id="login-username"
              className="form-input"
              type="text"
              autoComplete="username"
              placeholder="Enter your username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="login-password">Password</label>
            <input
              id="login-password"
              className="form-input"
              type="password"
              autoComplete="current-password"
              placeholder="Enter your password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {error && <div className="text-[var(--color-failed)] text-[var(--font-size-sm)] mt-[var(--space-sm)]">{error}</div>}

          <button
            type="submit"
            className="btn btn-primary btn-full"
            disabled={submitting}
            id="login-submit-btn"
          >
            {submitting ? "Signing in…" : "Sign In"}
          </button>
        </form>

        <div className="mt-[var(--space-lg)] text-center text-[var(--font-size-sm)] text-[var(--color-text-muted)]">
          <span>Don't have an account?</span>{" "}
          <Link
            to="/register"
            className="text-[var(--color-primary)] no-underline font-medium hover:underline"
          >
            Register
          </Link>
        </div>
      </div>
    </div>
  );
}
