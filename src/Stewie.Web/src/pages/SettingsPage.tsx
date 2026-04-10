/**
 * SettingsPage — GitHub PAT management and connection status.
 * Allows users to connect/disconnect their GitHub account.
 * Shows green/gray connection indicator.
 *
 * REF: CON-002 §4.0.1
 */
import { useEffect, useState } from "react";
import { getGitHubStatus, saveGitHubToken, removeGitHubToken } from "../api/client";
import type { GitHubStatus } from "../types";

/** GitHub settings page */
export function SettingsPage() {
  const [status, setStatus] = useState<GitHubStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [patInput, setPatInput] = useState("");
  const [saving, setSaving] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function loadStatus() {
      try {
        const s = await getGitHubStatus();
        if (!cancelled) setStatus(s);
      } catch {
        // API may not exist yet — show disconnected
        if (!cancelled) setStatus({ connected: false, username: null });
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void loadStatus();
    return () => { cancelled = true; };
  }, []);

  async function handleConnect() {
    if (!patInput.trim()) {
      setMessage({ type: "error", text: "Please enter a GitHub Personal Access Token." });
      return;
    }

    setSaving(true);
    setMessage(null);

    try {
      await saveGitHubToken(patInput.trim());
      const s = await getGitHubStatus();
      setStatus(s);
      setPatInput("");
      setMessage({ type: "success", text: "GitHub connected successfully." });
    } catch (err) {
      setMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to save token",
      });
    } finally {
      setSaving(false);
    }
  }

  async function handleDisconnect() {
    setDisconnecting(true);
    setMessage(null);

    try {
      await removeGitHubToken();
      setStatus({ connected: false, username: null });
      setMessage({ type: "success", text: "GitHub disconnected." });
    } catch (err) {
      setMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to disconnect",
      });
    } finally {
      setDisconnecting(false);
    }
  }

  if (loading) {
    return (
      <div>
        <div className="page-title-row"></div>
        <div className="skeleton skeleton-card" />
      </div>
    );
  }

  return (
    <div id="settings-page">
      <div className="page-title-row">
        
      </div>

      <div className="card" style={{ maxWidth: 600 }}>
        <div className="card-header">
          <span className="card-title">GitHub Integration</span>
        </div>

        <div style={{ padding: "var(--space-md)" }}>
          {/* Connection Status */}
          <div className="github-status" id="github-status">
            <span
              className={`status-dot-lg ${status?.connected ? "connected" : "disconnected"}`}
            />
            <div>
              <div style={{ fontWeight: 600 }}>
                {status?.connected ? "Connected" : "Not connected"}
              </div>
              {status?.connected && status.username && (
                <div className="mono" style={{ fontSize: "var(--font-size-sm)", color: "var(--color-text-muted)" }}>
                  {status.username}
                </div>
              )}
            </div>
          </div>

          {/* Connect Form */}
          {!status?.connected && (
            <div style={{ marginTop: "var(--space-lg)" }}>
              <div className="form-group">
                <label className="form-label" htmlFor="github-pat-input">
                  Personal Access Token
                </label>
                <input
                  id="github-pat-input"
                  className="form-input"
                  type="password"
                  placeholder="ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                  value={patInput}
                  onChange={(e) => setPatInput(e.target.value)}
                />
                <div className="form-hint">
                  Generate a token at GitHub → Settings → Developer settings → Personal Access Tokens.
                  Requires repo scope.
                </div>
              </div>

              <button
                className="btn btn-primary"
                onClick={() => { void handleConnect(); }}
                disabled={saving}
                id="github-connect-btn"
              >
                {saving ? "Connecting…" : "Connect GitHub"}
              </button>
            </div>
          )}

          {/* Disconnect Button */}
          {status?.connected && (
            <div style={{ marginTop: "var(--space-lg)" }}>
              <button
                className="btn btn-ghost"
                onClick={() => { void handleDisconnect(); }}
                disabled={disconnecting}
                id="github-disconnect-btn"
                style={{ color: "var(--color-failed)" }}
              >
                {disconnecting ? "Disconnecting…" : "Disconnect GitHub"}
              </button>
            </div>
          )}

          {/* Feedback Message */}
          {message && (
            <div
              className={`form-message ${message.type}`}
              style={{ marginTop: "var(--space-md)" }}
            >
              {message.text}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
