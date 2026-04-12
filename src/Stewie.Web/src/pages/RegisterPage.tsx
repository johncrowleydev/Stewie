/**
 * RegisterPage — Invite-code registration form with Stewie branding.
 * REF: CON-002 §4.0, JOB-027 T-403
 */
import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { useTheme } from "../hooks/useTheme";
import { btnPrimary } from "../tw";

const MIN_PASSWORD_LENGTH = 8;

const inputClass = "w-full py-sm px-md bg-ds-bg border border-ds-border rounded-md text-ds-text text-md font-sans transition-[border-color] duration-150 focus:outline-none focus:border-ds-primary focus:shadow-[0_0_0_3px_var(--color-primary-muted)] placeholder:text-ds-text-muted";

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  useTheme();
  const [inviteCode, setInviteCode] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function validate(): string | null {
    if (!inviteCode.trim()) return "Invite code is required.";
    if (!username.trim()) return "Username is required.";
    if (password.length < MIN_PASSWORD_LENGTH) return `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`;
    if (password !== confirmPassword) return "Passwords do not match.";
    return null;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setSubmitting(true);
    setError(null);
    try {
      await register({ inviteCode: inviteCode.trim(), username: username.trim(), password });
      void navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-ds-bg p-lg" id="register-page">
      <div className="w-full max-w-[400px] bg-ds-surface border border-ds-border rounded-lg py-xl px-lg">
        <div className="flex items-center justify-center gap-sm mb-xl flex-col">
          <img src="/stewie-logo.png" alt="Stewie" className="w-[228px] h-auto" />
          <span className="font-sans text-2xl font-bold tracking-wide text-ds-primary mt-xs">stewie</span>
        </div>

        <form onSubmit={(e) => { void handleSubmit(e); }} id="register-form">
          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="register-invite">Invite Code</label>
            <input id="register-invite" className={inputClass} type="text" placeholder="Enter your invite code" value={inviteCode} onChange={(e) => setInviteCode(e.target.value)} autoFocus />
          </div>
          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="register-username">Username</label>
            <input id="register-username" className={inputClass} type="text" autoComplete="username" placeholder="Choose a username" value={username} onChange={(e) => setUsername(e.target.value)} />
          </div>
          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="register-password">Password</label>
            <input id="register-password" className={inputClass} type="password" autoComplete="new-password" placeholder={`Minimum ${MIN_PASSWORD_LENGTH} characters`} value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          <div className="mb-md">
            <label className="block text-s font-medium text-ds-text-muted mb-xs" htmlFor="register-confirm">Confirm Password</label>
            <input id="register-confirm" className={inputClass} type="password" autoComplete="new-password" placeholder="Repeat your password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
            {confirmPassword && password !== confirmPassword && (
              <div className="text-ds-failed text-s mt-sm">Passwords do not match.</div>
            )}
          </div>

          {error && <div className="text-ds-failed text-s mt-sm">{error}</div>}

          <button
            type="submit"
            className={`${btnPrimary} w-full mt-md`}
            disabled={submitting}
            id="register-submit-btn"
          >
            {submitting ? "Creating account…" : "Create Account"}
          </button>
        </form>

        <div className="mt-lg text-center text-s text-ds-text-muted">
          <span>Already have an account?</span>{" "}
          <Link to="/login" className="text-ds-primary no-underline font-medium hover:underline">Sign in</Link>
        </div>
      </div>
    </div>
  );
}
