/**
 * RegisterPage — Invite-code registration form with Stewie branding.
 * Calls POST /api/auth/register via AuthContext.
 * Validates password match and minimum length client-side.
 * Auto-logs in and redirects to dashboard on success.
 *
 * REF: CON-002 §4.0
 */
import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { useTheme } from "../hooks/useTheme";

/** Minimum password length */
const MIN_PASSWORD_LENGTH = 8;

/** Registration page with invite code, password confirmation */
export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  useTheme(); // Sets data-theme on <html> so CSS variables respect light/dark
  const [inviteCode, setInviteCode] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  /** Client-side validation */
  function validate(): string | null {
    if (!inviteCode.trim()) return "Invite code is required.";
    if (!username.trim()) return "Username is required.";
    if (password.length < MIN_PASSWORD_LENGTH) {
      return `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`;
    }
    if (password !== confirmPassword) return "Passwords do not match.";
    return null;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await register({
        inviteCode: inviteCode.trim(),
        username: username.trim(),
        password,
      });
      void navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-page" id="register-page">
      <div className="auth-card">
        <div className="auth-brand">
          <img src="/stewie-logo.png" alt="Stewie" className="auth-logo" />
        </div>

        <h2 className="auth-heading">Create account</h2>

        <form onSubmit={(e) => { void handleSubmit(e); }} id="register-form">
          <div className="form-group">
            <label className="form-label" htmlFor="register-invite">Invite Code</label>
            <input
              id="register-invite"
              className="form-input"
              type="text"
              placeholder="Enter your invite code"
              value={inviteCode}
              onChange={(e) => setInviteCode(e.target.value)}
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="register-username">Username</label>
            <input
              id="register-username"
              className="form-input"
              type="text"
              autoComplete="username"
              placeholder="Choose a username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="register-password">Password</label>
            <input
              id="register-password"
              className="form-input"
              type="password"
              autoComplete="new-password"
              placeholder={`Minimum ${MIN_PASSWORD_LENGTH} characters`}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="register-confirm">Confirm Password</label>
            <input
              id="register-confirm"
              className="form-input"
              type="password"
              autoComplete="new-password"
              placeholder="Repeat your password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
            {confirmPassword && password !== confirmPassword && (
              <div className="form-error">Passwords do not match.</div>
            )}
          </div>

          {error && <div className="form-error">{error}</div>}

          <button
            type="submit"
            className="btn btn-primary btn-full"
            disabled={submitting}
            id="register-submit-btn"
          >
            {submitting ? "Creating account…" : "Create Account"}
          </button>
        </form>

        <div className="auth-footer">
          <span>Already have an account?</span>{" "}
          <Link to="/login" className="auth-link">Sign in</Link>
        </div>
      </div>
    </div>
  );
}
