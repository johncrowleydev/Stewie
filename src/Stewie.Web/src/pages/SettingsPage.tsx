/**
 * SettingsPage — personal preferences: GitHub integration and LLM credentials.
 *
 * Admin-only sections (invite codes, user management) have been extracted to
 * dedicated admin pages: AdminInvitesPage and AdminUsersPage (JOB-033 T-552).
 *
 * REF: JOB-025 T-302, T-201, JOB-027 T-406, JOB-033 T-552
 */
import { useEffect, useState, useCallback } from "react";
import { IconKey } from "../components/Icons";
import {
  getGitHubStatus, saveGitHubToken, removeGitHubToken,
  fetchCredentials, addCredential, deleteCredential,
} from "../api/client";
import { btnPrimary, btnGhost, btnDanger, formInput, formLabel, formGroup, formHint, card, pageTitleRow, skeleton } from "../tw";
import type { GitHubStatus, Credential } from "../types";

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
    </div>
  );
}
