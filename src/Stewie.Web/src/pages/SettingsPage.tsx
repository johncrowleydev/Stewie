/**
 * SettingsPage — GitHub integration, LLM credentials, invite codes, user management.
 * REF: JOB-025 T-302, T-201, T-310, T-311, JOB-027 T-406
 */
import { useEffect, useState, useCallback } from "react";
import { useAuth } from "../contexts/AuthContext";
import { IconKey, IconShield, IconUsers } from "../components/Icons";
import {
  getGitHubStatus, saveGitHubToken, removeGitHubToken,
  fetchCredentials, addCredential, deleteCredential,
  fetchInviteCodes, generateInviteCode, revokeInviteCode,
  fetchUsers, deleteUser,
} from "../api/client";
import { btnPrimary, btnGhost, btnDanger, formInput, formLabel, formGroup, formHint, card, pageTitleRow, skeleton, th, td, dataTable } from "../tw";
import type { GitHubStatus, Credential, InviteCode, UserInfo } from "../types";

const LLM_PROVIDERS = [
  { name: "Anthropic (Claude)", credentialType: "AnthropicApiKey", placeholder: "sk-ant-…" },
  { name: "OpenAI (GPT)", credentialType: "OpenAiApiKey", placeholder: "sk-…" },
  { name: "Google (Gemini)", credentialType: "GoogleApiKey", placeholder: "AIza…" },
];

/** Feedback message component */
function FeedbackMessage({ msg }: { msg: { type: "success" | "error"; text: string } | null }) {
  if (!msg) return null;
  const color = msg.type === "success" ? "text-ds-completed" : "text-ds-failed";
  return <div className={`${color} text-s mt-md`}>{msg.text}</div>;
}

/** Card wrapper with header */
function SettingsCard({ title, icon, actions, maxWidth = 600, id, children }: {
  title: string; icon?: React.ReactNode; actions?: React.ReactNode;
  maxWidth?: number; id?: string; children: React.ReactNode;
}) {
  return (
    <div className={`${card} mt-lg`} style={{ maxWidth }} id={id}>
      <div className="flex items-center justify-between pb-sm border-b border-ds-border mb-md">
        <span className="text-md font-semibold text-ds-text flex items-center gap-sm [&_svg]:w-3.5 [&_svg]:h-3.5 [&_svg]:opacity-60">{icon}{title}</span>
        {actions}
      </div>
      {children}
    </div>
  );
}

export function SettingsPage() {
  const { user: authUser } = useAuth();
  const isAdmin = authUser?.role === "admin";
  const [status, setStatus] = useState<GitHubStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [patInput, setPatInput] = useState("");
  const [saving, setSaving] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [credentials, setCredentials] = useState<Credential[]>([]);
  const [credLoading, setCredLoading] = useState(true);
  const [credMessage, setCredMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [addingKey, setAddingKey] = useState<string | null>(null);
  const [keyInput, setKeyInput] = useState("");
  const [savingKey, setSavingKey] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [inviteCodes, setInviteCodes] = useState<InviteCode[]>([]);
  const [inviteLoading, setInviteLoading] = useState(true);
  const [inviteMessage, setInviteMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [generatingInvite, setGeneratingInvite] = useState(false);
  const [newInviteCode, setNewInviteCode] = useState<string | null>(null);
  const [copiedCode, setCopiedCode] = useState(false);
  const [revokingInviteId, setRevokingInviteId] = useState<string | null>(null);
  const [confirmRevokeId, setConfirmRevokeId] = useState<string | null>(null);
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [usersLoading, setUsersLoading] = useState(true);
  const [usersMessage, setUsersMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [confirmDeleteUserId, setConfirmDeleteUserId] = useState<string | null>(null);
  const [deletingUserId, setDeletingUserId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function loadStatus() {
      try { const s = await getGitHubStatus(); if (!cancelled) setStatus(s); }
      catch { if (!cancelled) setStatus({ connected: false, username: null }); }
      finally { if (!cancelled) setLoading(false); }
    }
    void loadStatus();
    return () => { cancelled = true; };
  }, []);

  const loadCredentials = useCallback(async () => {
    try { setCredentials(await fetchCredentials()); } catch { setCredentials([]); }
    finally { setCredLoading(false); }
  }, []);
  useEffect(() => { void loadCredentials(); }, [loadCredentials]);

  const loadInviteCodes = useCallback(async () => {
    if (!isAdmin) return;
    try { setInviteCodes(await fetchInviteCodes()); } catch { setInviteCodes([]); }
    finally { setInviteLoading(false); }
  }, [isAdmin]);
  useEffect(() => { void loadInviteCodes(); }, [loadInviteCodes]);

  const loadUsers = useCallback(async () => {
    if (!isAdmin) return;
    try { setUsers(await fetchUsers()); } catch { setUsers([]); }
    finally { setUsersLoading(false); }
  }, [isAdmin]);
  useEffect(() => { void loadUsers(); }, [loadUsers]);

  async function handleConnect() {
    if (!patInput.trim()) { setMessage({ type: "error", text: "Please enter a GitHub Personal Access Token." }); return; }
    setSaving(true); setMessage(null);
    try { await saveGitHubToken(patInput.trim()); setStatus(await getGitHubStatus()); setPatInput(""); setMessage({ type: "success", text: "GitHub connected successfully." }); }
    catch (err) { setMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to save token" }); }
    finally { setSaving(false); }
  }

  async function handleDisconnect() {
    setDisconnecting(true); setMessage(null);
    try { await removeGitHubToken(); setStatus({ connected: false, username: null }); setMessage({ type: "success", text: "GitHub disconnected." }); }
    catch (err) { setMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to disconnect" }); }
    finally { setDisconnecting(false); }
  }

  function handleStartAddKey(credentialType: string) { setAddingKey(credentialType); setKeyInput(""); setCredMessage(null); }
  function handleCancelAddKey() { setAddingKey(null); setKeyInput(""); }

  async function handleSaveKey() {
    if (!addingKey || !keyInput.trim()) { setCredMessage({ type: "error", text: "Please enter an API key." }); return; }
    setSavingKey(true); setCredMessage(null);
    try { await addCredential(addingKey, keyInput.trim()); await loadCredentials(); setAddingKey(null); setKeyInput(""); setCredMessage({ type: "success", text: "API key saved successfully." }); }
    catch (err) { setCredMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to save key" }); }
    finally { setSavingKey(false); }
  }

  async function handleDeleteKey(id: string) {
    if (confirmDeleteId !== id) { setConfirmDeleteId(id); return; }
    setDeletingId(id); setConfirmDeleteId(null); setCredMessage(null);
    try { await deleteCredential(id); await loadCredentials(); setCredMessage({ type: "success", text: "API key removed." }); }
    catch (err) { setCredMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to remove key" }); }
    finally { setDeletingId(null); }
  }

  function getCredentialByType(t: string) { return credentials.find((c) => c.credentialType === t); }

  async function handleGenerateInvite() {
    setGeneratingInvite(true); setInviteMessage(null); setNewInviteCode(null); setCopiedCode(false);
    try { const invite = await generateInviteCode(); setNewInviteCode(invite.code); await loadInviteCodes(); setInviteMessage({ type: "success", text: "Invite code generated." }); }
    catch (err) { setInviteMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to generate invite code" }); }
    finally { setGeneratingInvite(false); }
  }

  async function handleCopyCode(code: string) {
    try { await navigator.clipboard.writeText(code); } catch { const input = document.createElement("input"); input.value = code; document.body.appendChild(input); input.select(); document.execCommand("copy"); document.body.removeChild(input); }
    setCopiedCode(true); setTimeout(() => setCopiedCode(false), 2000);
  }

  async function handleRevokeInvite(id: string) {
    if (confirmRevokeId !== id) { setConfirmRevokeId(id); return; }
    setRevokingInviteId(id); setConfirmRevokeId(null); setInviteMessage(null);
    try { await revokeInviteCode(id); await loadInviteCodes(); setInviteMessage({ type: "success", text: "Invite code revoked." }); }
    catch (err) { setInviteMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to revoke invite code" }); }
    finally { setRevokingInviteId(null); }
  }

  async function handleDeleteUser(id: string) {
    if (confirmDeleteUserId !== id) { setConfirmDeleteUserId(id); return; }
    setDeletingUserId(id); setConfirmDeleteUserId(null); setUsersMessage(null);
    try { await deleteUser(id); await loadUsers(); setUsersMessage({ type: "success", text: "User deleted." }); }
    catch (err) { setUsersMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to delete user" }); }
    finally { setDeletingUserId(null); }
  }

  function formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
  }

  if (loading) {
    return (
      <div>
        <div className={pageTitleRow} />
        <div className={`${skeleton} h-[120px]`} />
      </div>
    );
  }

  return (
    <div id="settings-page">
      <div className={pageTitleRow} />

      {/* GitHub */}
      <SettingsCard title="GitHub Integration" id="github-settings">
        <div className="flex items-center gap-md mb-lg" id="github-status">
          <span className={`w-3 h-3 rounded-full shrink-0 ${status?.connected ? "bg-ds-completed" : "bg-ds-failed"}`} />
          <div>
            <div className="font-semibold">{status?.connected ? "Connected" : "Not connected"}</div>
            {status?.connected && status.username && <div className="font-mono text-s text-ds-text-muted">{status.username}</div>}
          </div>
        </div>

        {!status?.connected && (
          <div className="mt-lg">
            <div className={formGroup}>
              <label className={formLabel} htmlFor="github-pat-input">Personal Access Token</label>
              <input className={formInput} id="github-pat-input" type="password" placeholder="ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" value={patInput} onChange={(e) => setPatInput(e.target.value)} />
              <div className={formHint}>Generate a token at GitHub → Settings → Developer settings → Personal Access Tokens. Requires repo scope.</div>
            </div>
            <button className={btnPrimary} onClick={() => { void handleConnect(); }} disabled={saving} id="github-connect-btn">{saving ? "Connecting…" : "Connect GitHub"}</button>
          </div>
        )}

        {status?.connected && (
          <div className="mt-lg">
            <button className={`${btnGhost} text-ds-failed`} onClick={() => { void handleDisconnect(); }} disabled={disconnecting} id="github-disconnect-btn">{disconnecting ? "Disconnecting…" : "Disconnect GitHub"}</button>
          </div>
        )}
        <FeedbackMessage msg={message} />
      </SettingsCard>

      {/* LLM Keys */}
      <SettingsCard title="LLM Provider Keys" icon={<IconKey size={14} />} id="llm-credentials">
        {credLoading ? (
          <div className={`${skeleton} h-[60px]`} />
        ) : (
          <div className="flex flex-col gap-md">
            {LLM_PROVIDERS.map((provider) => {
              const cred = getCredentialByType(provider.credentialType);
              const isAddingThis = addingKey === provider.credentialType;
              return (
                <div key={provider.credentialType} className="border border-ds-border rounded-md p-md" id={`credential-${provider.credentialType}`}>
                  <div className="flex items-center gap-sm mb-sm">
                    <IconKey size={14} className="opacity-60" />
                    <span className="font-semibold text-s">{provider.name}</span>
                  </div>
                  {cred ? (
                    <div className="flex items-center justify-between">
                      <span className="font-mono text-s text-ds-text-muted">{cred.maskedValue}</span>
                      <div className="flex gap-xs">
                        {confirmDeleteId === cred.id ? (
                          <>
                            <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleDeleteKey(cred.id); }} disabled={deletingId === cred.id}>{deletingId === cred.id ? "Removing…" : "Confirm"}</button>
                            <button className={`${btnGhost} text-xs py-xs px-sm`} onClick={() => setConfirmDeleteId(null)} disabled={deletingId === cred.id}>Cancel</button>
                          </>
                        ) : (
                          <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleDeleteKey(cred.id); }} disabled={deletingId === cred.id}>✕ Remove</button>
                        )}
                      </div>
                    </div>
                  ) : isAddingThis ? (
                    <div>
                      <input className={formInput} type="password" placeholder={provider.placeholder} value={keyInput} onChange={(e) => setKeyInput(e.target.value)} autoFocus
                        onKeyDown={(e) => { if (e.key === "Enter") void handleSaveKey(); if (e.key === "Escape") handleCancelAddKey(); }} />
                      <div className="flex gap-xs mt-sm">
                        <button className={btnPrimary} onClick={() => { void handleSaveKey(); }} disabled={savingKey}>{savingKey ? "Saving…" : "Save"}</button>
                        <button className={btnGhost} onClick={handleCancelAddKey} disabled={savingKey}>Cancel</button>
                      </div>
                    </div>
                  ) : (
                    <div className="flex items-center justify-between">
                      <span className="text-s text-ds-text-muted italic">Not configured</span>
                      <button className={`${btnGhost} text-xs py-xs px-sm`} onClick={() => handleStartAddKey(provider.credentialType)}>+ Add Key</button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
        <FeedbackMessage msg={credMessage} />
      </SettingsCard>

      {/* Invite Codes (admin) */}
      {isAdmin && (
        <SettingsCard
          title="Invite Codes"
          icon={<IconShield size={14} />}
          id="invite-management"
          actions={
            <button className={btnPrimary} onClick={() => { void handleGenerateInvite(); }} disabled={generatingInvite} id="generate-invite-btn">
              {generatingInvite ? "Generating…" : "Generate Code"}
            </button>
          }
        >
          {newInviteCode && (
            <div className="flex items-center gap-md p-md bg-ds-primary-muted rounded-md mb-md" id="generated-invite-code">
              <span className="font-mono font-bold text-md text-ds-primary flex-1">{newInviteCode}</span>
              <button className={`${btnGhost} text-xs py-xs px-sm`} onClick={() => { void handleCopyCode(newInviteCode); }} id="copy-invite-btn">{copiedCode ? "Copied!" : "Copy"}</button>
            </div>
          )}

          {inviteLoading ? (
            <div className={`${skeleton} h-[60px]`} />
          ) : inviteCodes.length === 0 ? (
            <div className="text-center py-lg text-s text-ds-text-muted italic">No invite codes generated yet.</div>
          ) : (
            <div className="overflow-x-auto">
            <table className={dataTable} id="invite-codes-table">
              <thead>
                <tr>
                  {["Code", "Status", "Created", ""].map((h) => <th key={h} className={th}>{h}</th>)}
                </tr>
              </thead>
              <tbody>
                {inviteCodes.map((invite) => {
                  const isUsed = !!invite.usedByUserId;
                  return (
                    <tr key={invite.id} className="border-b border-ds-border last:border-b-0">
                      <td className={`${td} font-mono`}>{invite.code}</td>
                      <td className={td}>
                        <span className={`inline-flex items-center px-2 py-px rounded-full text-xs font-medium ${isUsed ? "bg-[rgba(139,141,147,0.1)] text-ds-text-muted" : "bg-ds-primary-muted text-ds-primary"}`}>
                          {isUsed ? "Used" : "Available"}
                        </span>
                      </td>
                      <td className={`${td} text-ds-text-muted`}>{formatDate(invite.createdAt)}</td>
                      <td className={td}>
                        {!isUsed && (
                          confirmRevokeId === invite.id ? (
                            <div className="flex gap-xs">
                              <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleRevokeInvite(invite.id); }} disabled={revokingInviteId === invite.id}>{revokingInviteId === invite.id ? "Revoking…" : "Confirm"}</button>
                              <button className={`${btnGhost} text-xs py-xs px-sm`} onClick={() => setConfirmRevokeId(null)} disabled={revokingInviteId === invite.id}>Cancel</button>
                            </div>
                          ) : (
                            <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleRevokeInvite(invite.id); }} disabled={revokingInviteId === invite.id} id={`revoke-invite-${invite.id}`}>Revoke</button>
                          )
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            </div>
          )}
          <FeedbackMessage msg={inviteMessage} />
        </SettingsCard>
      )}

      {/* Users (admin) */}
      {isAdmin && (
        <SettingsCard title="Users" icon={<IconUsers size={14} />} id="user-management">
          {usersLoading ? (
            <div className={`${skeleton} h-[60px]`} />
          ) : users.length === 0 ? (
            <div className="text-center py-lg text-s text-ds-text-muted italic">No users found.</div>
          ) : (
            <div className="overflow-x-auto">
            <table className={dataTable} id="users-table">
              <thead>
                <tr>
                  {["Username", "Role", "Created", ""].map((h) => <th key={h} className={th}>{h}</th>)}
                </tr>
              </thead>
              <tbody>
                {users.map((u) => {
                  const isSelf = u.id === authUser?.id;
                  const isTargetAdmin = u.role === "admin";
                  const canDelete = !isSelf && !isTargetAdmin;
                  return (
                    <tr key={u.id} className="border-b border-ds-border last:border-b-0">
                      <td className={td}>
                        <span className="flex items-center gap-sm">
                          {u.username}
                          {isSelf && <span className="text-[10px] py-px px-1.5 rounded-full bg-ds-primary-muted text-ds-primary font-medium">you</span>}
                        </span>
                      </td>
                      <td className={td}>
                        <span className={`inline-flex items-center px-2 py-px rounded-full text-xs font-medium ${u.role === "admin" ? "bg-[rgba(245,166,35,0.15)] text-ds-warning" : "bg-[rgba(59,130,246,0.1)] text-ds-running"}`}>
                          {u.role}
                        </span>
                      </td>
                      <td className={`${td} text-ds-text-muted`}>{formatDate(u.createdAt)}</td>
                      <td className={td}>
                        {canDelete ? (
                          confirmDeleteUserId === u.id ? (
                            <div className="flex gap-xs">
                              <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleDeleteUser(u.id); }} disabled={deletingUserId === u.id}>{deletingUserId === u.id ? "Deleting…" : "Confirm"}</button>
                              <button className={`${btnGhost} text-xs py-xs px-sm`} onClick={() => setConfirmDeleteUserId(null)} disabled={deletingUserId === u.id}>Cancel</button>
                            </div>
                          ) : (
                            <button className={`${btnDanger} text-xs py-xs px-sm`} onClick={() => { void handleDeleteUser(u.id); }} disabled={deletingUserId === u.id} id={`delete-user-${u.id}`}>Delete</button>
                          )
                        ) : (
                          <span className="text-ds-text-muted opacity-50 cursor-not-allowed" title={isSelf ? "Cannot delete your own account" : "Cannot delete admin users"}>—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            </div>
          )}
          <FeedbackMessage msg={usersMessage} />
        </SettingsCard>
      )}
    </div>
  );
}
