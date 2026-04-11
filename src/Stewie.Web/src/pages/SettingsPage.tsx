/**
 * SettingsPage — GitHub PAT management, LLM provider key management, connection status,
 * and admin-only invite code + user management panels.
 * Allows users to connect/disconnect GitHub, manage LLM API keys
 * for Google AI, Anthropic, and OpenAI providers, and admins to manage invite codes and users.
 *
 * REF: CON-002 §4.0.1, §4.0.2, JOB-023 T-201, JOB-026 T-310/T-311
 */
import { useEffect, useState, useCallback } from "react";
import {
  getGitHubStatus,
  saveGitHubToken,
  removeGitHubToken,
  fetchCredentials,
  addCredential,
  deleteCredential,
  generateInviteCode,
  fetchInviteCodes,
  revokeInviteCode,
  fetchUsers,
  deleteUser,
} from "../api/client";
import { useAuth } from "../contexts/AuthContext";
import { IconKey, IconUsers, IconShield } from "../components/Icons";
import type { GitHubStatus, Credential, InviteCode, UserInfo } from "../types";

/** LLM provider definitions — maps credential types to display info */
const LLM_PROVIDERS = [
  {
    credentialType: "GoogleAiApiKey",
    name: "Google AI (Gemini)",
    placeholder: "AIza...",
  },
  {
    credentialType: "AnthropicApiKey",
    name: "Anthropic (Claude)",
    placeholder: "sk-ant-...",
  },
  {
    credentialType: "OpenAiApiKey",
    name: "OpenAI (GPT)",
    placeholder: "sk-...",
  },
] as const;

/** GitHub settings page */
export function SettingsPage() {
  const { user: authUser } = useAuth();
  const isAdmin = authUser?.role === "admin";

  // GitHub state
  const [status, setStatus] = useState<GitHubStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [patInput, setPatInput] = useState("");
  const [saving, setSaving] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  // LLM credentials state
  const [credentials, setCredentials] = useState<Credential[]>([]);
  const [credLoading, setCredLoading] = useState(true);
  const [credMessage, setCredMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [addingKey, setAddingKey] = useState<string | null>(null); // credentialType being added
  const [keyInput, setKeyInput] = useState("");
  const [savingKey, setSavingKey] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Invite code state (admin only) — T-310
  const [inviteCodes, setInviteCodes] = useState<InviteCode[]>([]);
  const [inviteLoading, setInviteLoading] = useState(true);
  const [inviteMessage, setInviteMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [generatingInvite, setGeneratingInvite] = useState(false);
  const [newInviteCode, setNewInviteCode] = useState<string | null>(null);
  const [copiedCode, setCopiedCode] = useState(false);
  const [revokingInviteId, setRevokingInviteId] = useState<string | null>(null);
  const [confirmRevokeId, setConfirmRevokeId] = useState<string | null>(null);

  // User management state (admin only) — T-311
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [usersLoading, setUsersLoading] = useState(true);
  const [usersMessage, setUsersMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [confirmDeleteUserId, setConfirmDeleteUserId] = useState<string | null>(null);
  const [deletingUserId, setDeletingUserId] = useState<string | null>(null);

  // Load GitHub status
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

  // Load LLM credentials
  const loadCredentials = useCallback(async () => {
    try {
      const creds = await fetchCredentials();
      setCredentials(creds);
    } catch {
      // Endpoint may not exist yet — show empty
      setCredentials([]);
    } finally {
      setCredLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadCredentials();
  }, [loadCredentials]);

  // Load invite codes (admin only) — T-310
  const loadInviteCodes = useCallback(async () => {
    if (!isAdmin) return;
    try {
      const codes = await fetchInviteCodes();
      setInviteCodes(codes);
    } catch {
      setInviteCodes([]);
    } finally {
      setInviteLoading(false);
    }
  }, [isAdmin]);

  useEffect(() => {
    void loadInviteCodes();
  }, [loadInviteCodes]);

  // Load users (admin only) — T-311
  const loadUsers = useCallback(async () => {
    if (!isAdmin) return;
    try {
      const u = await fetchUsers();
      setUsers(u);
    } catch {
      setUsers([]);
    } finally {
      setUsersLoading(false);
    }
  }, [isAdmin]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  // --- GitHub handlers ---

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

  // --- LLM credential handlers ---

  /** Start adding a key for a specific provider */
  function handleStartAddKey(credentialType: string) {
    setAddingKey(credentialType);
    setKeyInput("");
    setCredMessage(null);
  }

  /** Cancel adding a key */
  function handleCancelAddKey() {
    setAddingKey(null);
    setKeyInput("");
  }

  /** Save a new credential */
  async function handleSaveKey() {
    if (!addingKey || !keyInput.trim()) {
      setCredMessage({ type: "error", text: "Please enter an API key." });
      return;
    }

    setSavingKey(true);
    setCredMessage(null);

    try {
      await addCredential(addingKey, keyInput.trim());
      await loadCredentials();
      setAddingKey(null);
      setKeyInput("");
      setCredMessage({ type: "success", text: "API key saved successfully." });
    } catch (err) {
      setCredMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to save key",
      });
    } finally {
      setSavingKey(false);
    }
  }

  /** Delete a credential */
  async function handleDeleteKey(id: string) {
    if (confirmDeleteId !== id) {
      setConfirmDeleteId(id);
      return;
    }

    setDeletingId(id);
    setConfirmDeleteId(null);
    setCredMessage(null);

    try {
      await deleteCredential(id);
      await loadCredentials();
      setCredMessage({ type: "success", text: "API key removed." });
    } catch (err) {
      setCredMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to remove key",
      });
    } finally {
      setDeletingId(null);
    }
  }

  /** Find a stored credential by type */
  function getCredentialByType(credentialType: string): Credential | undefined {
    return credentials.find((c) => c.credentialType === credentialType);
  }

  // --- Invite code handlers (T-310) ---

  /** Generate a new invite code */
  async function handleGenerateInvite() {
    setGeneratingInvite(true);
    setInviteMessage(null);
    setNewInviteCode(null);
    setCopiedCode(false);

    try {
      const invite = await generateInviteCode();
      setNewInviteCode(invite.code);
      await loadInviteCodes();
      setInviteMessage({ type: "success", text: "Invite code generated." });
    } catch (err) {
      setInviteMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to generate invite code",
      });
    } finally {
      setGeneratingInvite(false);
    }
  }

  /** Copy invite code to clipboard */
  async function handleCopyCode(code: string) {
    try {
      await navigator.clipboard.writeText(code);
      setCopiedCode(true);
      setTimeout(() => setCopiedCode(false), 2000);
    } catch {
      // Fallback for non-HTTPS
      const input = document.createElement("input");
      input.value = code;
      document.body.appendChild(input);
      input.select();
      document.execCommand("copy");
      document.body.removeChild(input);
      setCopiedCode(true);
      setTimeout(() => setCopiedCode(false), 2000);
    }
  }

  /** Revoke an invite code */
  async function handleRevokeInvite(id: string) {
    if (confirmRevokeId !== id) {
      setConfirmRevokeId(id);
      return;
    }

    setRevokingInviteId(id);
    setConfirmRevokeId(null);
    setInviteMessage(null);

    try {
      await revokeInviteCode(id);
      await loadInviteCodes();
      setInviteMessage({ type: "success", text: "Invite code revoked." });
    } catch (err) {
      setInviteMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to revoke invite code",
      });
    } finally {
      setRevokingInviteId(null);
    }
  }

  // --- User management handlers (T-311) ---

  /** Delete a user */
  async function handleDeleteUser(id: string) {
    if (confirmDeleteUserId !== id) {
      setConfirmDeleteUserId(id);
      return;
    }

    setDeletingUserId(id);
    setConfirmDeleteUserId(null);
    setUsersMessage(null);

    try {
      await deleteUser(id);
      await loadUsers();
      setUsersMessage({ type: "success", text: "User deleted." });
    } catch (err) {
      setUsersMessage({
        type: "error",
        text: err instanceof Error ? err.message : "Failed to delete user",
      });
    } finally {
      setDeletingUserId(null);
    }
  }

  /** Format a date string for display */
  function formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
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

      {/* GitHub Integration */}
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

      {/* LLM Provider Keys — T-201 */}
      <div className="card credential-card" style={{ maxWidth: 600, marginTop: "var(--space-lg)" }} id="llm-credentials">
        <div className="card-header">
          <span className="card-title"><IconKey size={14} className="card-title-icon" /> LLM Provider Keys</span>
        </div>

        <div style={{ padding: "var(--space-md)" }}>
          {credLoading ? (
            <div className="skeleton skeleton-row" style={{ height: 60 }} />
          ) : (
            <div className="credential-list">
              {LLM_PROVIDERS.map((provider) => {
                const cred = getCredentialByType(provider.credentialType);
                const isAddingThis = addingKey === provider.credentialType;

                return (
                  <div
                    key={provider.credentialType}
                    className="credential-provider"
                    id={`credential-${provider.credentialType}`}
                  >
                    <div className="credential-provider-header">
                      <IconKey size={14} className="credential-provider-icon" />
                      <span className="credential-provider-name">{provider.name}</span>
                    </div>

                    {cred ? (
                      /* Key is configured — show masked value + remove */
                      <div className="credential-configured">
                        <span className="credential-masked-value">{cred.maskedValue}</span>
                        <div className="credential-actions">
                          {confirmDeleteId === cred.id ? (
                            <>
                              <button
                                className="btn btn-ghost credential-delete-btn"
                                onClick={() => { void handleDeleteKey(cred.id); }}
                                disabled={deletingId === cred.id}
                              >
                                {deletingId === cred.id ? "Removing…" : "Confirm"}
                              </button>
                              <button
                                className="btn btn-ghost"
                                onClick={() => setConfirmDeleteId(null)}
                                disabled={deletingId === cred.id}
                              >
                                Cancel
                              </button>
                            </>
                          ) : (
                            <button
                              className="btn btn-ghost credential-delete-btn"
                              onClick={() => { void handleDeleteKey(cred.id); }}
                              disabled={deletingId === cred.id}
                            >
                              ✕ Remove
                            </button>
                          )}
                        </div>
                      </div>
                    ) : isAddingThis ? (
                      /* Adding a key — inline input */
                      <div className="credential-add-form">
                        <input
                          className="form-input"
                          type="password"
                          placeholder={provider.placeholder}
                          value={keyInput}
                          onChange={(e) => setKeyInput(e.target.value)}
                          autoFocus
                          onKeyDown={(e) => {
                            if (e.key === "Enter") void handleSaveKey();
                            if (e.key === "Escape") handleCancelAddKey();
                          }}
                        />
                        <div className="credential-add-actions">
                          <button
                            className="btn btn-primary"
                            onClick={() => { void handleSaveKey(); }}
                            disabled={savingKey}
                          >
                            {savingKey ? "Saving…" : "Save"}
                          </button>
                          <button
                            className="btn btn-ghost"
                            onClick={handleCancelAddKey}
                            disabled={savingKey}
                          >
                            Cancel
                          </button>
                        </div>
                      </div>
                    ) : (
                      /* Not configured — show add button */
                      <div className="credential-not-configured">
                        <span className="credential-empty-text">Not configured</span>
                        <button
                          className="btn btn-ghost credential-add-btn"
                          onClick={() => handleStartAddKey(provider.credentialType)}
                        >
                          + Add Key
                        </button>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}

          {/* Feedback Message */}
          {credMessage && (
            <div
              className={`form-message ${credMessage.type}`}
              style={{ marginTop: "var(--space-md)" }}
            >
              {credMessage.text}
            </div>
          )}
        </div>
      </div>

      {/* ── Admin-Only: Invite Code Management ── T-310 */}
      {isAdmin && (
        <div
          className="card admin-panel"
          style={{ maxWidth: 600, marginTop: "var(--space-lg)" }}
          id="invite-management"
        >
          <div className="card-header">
            <span className="card-title">
              <IconShield size={14} className="card-title-icon" /> Invite Codes
            </span>
            <button
              className="btn btn-primary"
              onClick={() => { void handleGenerateInvite(); }}
              disabled={generatingInvite}
              id="generate-invite-btn"
            >
              {generatingInvite ? "Generating…" : "Generate Code"}
            </button>
          </div>

          <div style={{ padding: "var(--space-md)" }}>
            {/* Newly generated code — inline display with copy */}
            {newInviteCode && (
              <div className="admin-generated-code" id="generated-invite-code">
                <span className="admin-code-value">{newInviteCode}</span>
                <button
                  className="btn btn-ghost admin-copy-btn"
                  onClick={() => { void handleCopyCode(newInviteCode); }}
                  id="copy-invite-btn"
                >
                  {copiedCode ? "Copied!" : "Copy"}
                </button>
              </div>
            )}

            {/* Invite code list */}
            {inviteLoading ? (
              <div className="skeleton skeleton-row" style={{ height: 60 }} />
            ) : inviteCodes.length === 0 ? (
              <div className="admin-empty">No invite codes generated yet.</div>
            ) : (
              <table className="data-table admin-table" id="invite-codes-table">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {inviteCodes.map((invite) => {
                    const isUsed = !!invite.usedByUserId;
                    return (
                      <tr key={invite.id}>
                        <td>
                          <span className="mono admin-code-cell">{invite.code}</span>
                        </td>
                        <td>
                          <span className={`admin-invite-status ${isUsed ? "used" : "available"}`}>
                            {isUsed ? "Used" : "Available"}
                          </span>
                        </td>
                        <td className="admin-date-cell">{formatDate(invite.createdAt)}</td>
                        <td>
                          {!isUsed && (
                            confirmRevokeId === invite.id ? (
                              <div className="admin-inline-confirm">
                                <button
                                  className="btn btn-ghost credential-delete-btn"
                                  onClick={() => { void handleRevokeInvite(invite.id); }}
                                  disabled={revokingInviteId === invite.id}
                                >
                                  {revokingInviteId === invite.id ? "Revoking…" : "Confirm"}
                                </button>
                                <button
                                  className="btn btn-ghost"
                                  onClick={() => setConfirmRevokeId(null)}
                                  disabled={revokingInviteId === invite.id}
                                >
                                  Cancel
                                </button>
                              </div>
                            ) : (
                              <button
                                className="btn btn-ghost credential-delete-btn"
                                onClick={() => { void handleRevokeInvite(invite.id); }}
                                disabled={revokingInviteId === invite.id}
                                id={`revoke-invite-${invite.id}`}
                              >
                                Revoke
                              </button>
                            )
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}

            {/* Feedback Message */}
            {inviteMessage && (
              <div
                className={`form-message ${inviteMessage.type}`}
                style={{ marginTop: "var(--space-md)" }}
              >
                {inviteMessage.text}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Admin-Only: User Management ── T-311 */}
      {isAdmin && (
        <div
          className="card admin-panel"
          style={{ maxWidth: 600, marginTop: "var(--space-lg)" }}
          id="user-management"
        >
          <div className="card-header">
            <span className="card-title">
              <IconUsers size={14} className="card-title-icon" /> Users
            </span>
          </div>

          <div style={{ padding: "var(--space-md)" }}>
            {usersLoading ? (
              <div className="skeleton skeleton-row" style={{ height: 60 }} />
            ) : users.length === 0 ? (
              <div className="admin-empty">No users found.</div>
            ) : (
              <table className="data-table admin-table" id="users-table">
                <thead>
                  <tr>
                    <th>Username</th>
                    <th>Role</th>
                    <th>Created</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((u) => {
                    const isSelf = u.id === authUser?.id;
                    const isTargetAdmin = u.role === "admin";
                    const canDelete = !isSelf && !isTargetAdmin;

                    return (
                      <tr key={u.id}>
                        <td>
                          <span className="admin-username">
                            {u.username}
                            {isSelf && <span className="admin-you-badge">you</span>}
                          </span>
                        </td>
                        <td>
                          <span className={`admin-role-badge ${u.role}`}>
                            {u.role}
                          </span>
                        </td>
                        <td className="admin-date-cell">{formatDate(u.createdAt)}</td>
                        <td>
                          {canDelete ? (
                            confirmDeleteUserId === u.id ? (
                              <div className="admin-inline-confirm">
                                <button
                                  className="btn btn-ghost credential-delete-btn"
                                  onClick={() => { void handleDeleteUser(u.id); }}
                                  disabled={deletingUserId === u.id}
                                >
                                  {deletingUserId === u.id ? "Deleting…" : "Confirm"}
                                </button>
                                <button
                                  className="btn btn-ghost"
                                  onClick={() => setConfirmDeleteUserId(null)}
                                  disabled={deletingUserId === u.id}
                                >
                                  Cancel
                                </button>
                              </div>
                            ) : (
                              <button
                                className="btn btn-ghost credential-delete-btn"
                                onClick={() => { void handleDeleteUser(u.id); }}
                                disabled={deletingUserId === u.id}
                                id={`delete-user-${u.id}`}
                              >
                                Delete
                              </button>
                            )
                          ) : (
                            <span
                              className="admin-delete-disabled"
                              title={isSelf ? "Cannot delete your own account" : "Cannot delete admin users"}
                            >
                              —
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}

            {/* Feedback Message */}
            {usersMessage && (
              <div
                className={`form-message ${usersMessage.type}`}
                style={{ marginTop: "var(--space-md)" }}
              >
                {usersMessage.text}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
