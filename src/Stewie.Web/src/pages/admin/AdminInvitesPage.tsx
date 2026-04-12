/**
 * AdminInvitesPage — admin-only page for managing invite codes.
 *
 * Extracted from SettingsPage (JOB-033 T-550) into a dedicated admin page
 * accessible via `/admin/invites`. Uses the ui/ component library for
 * consistent styling across the admin panel.
 *
 * **Design decisions:**
 * - Uses Card for page layout, DataTable for the invite code list, Button
 *   for actions, and Badge for status indicators (GOV-003 §8.2).
 * - Newly generated codes are shown in a highlight banner with a copy-to-
 *   clipboard button that falls back to `document.execCommand("copy")`.
 * - Revoke uses inline confirm/cancel button pair within the table row,
 *   keeping the flow contextual without needing a full modal overlay.
 * - DataTable columns: Code, Status, Used By, Created, Actions.
 * - `data-testid` on all interactive elements (GOV-003 §8.4).
 * - Responsive via DataTable's built-in horizontal scroll (GOV-003 §8.10).
 *
 * Used by: App.tsx (admin routes)
 * Related: api/client.ts (fetchInviteCodes, generateInviteCode, revokeInviteCode)
 *
 * REF: JOB-033 T-550
 *
 * @example
 * ```tsx
 * <Route path="invites" element={<AdminInvitesPage />} />
 * ```
 */

import { useCallback, useEffect, useState } from "react";
import { fetchInviteCodes, generateInviteCode, revokeInviteCode } from "../../api/client";
import { Card, Button, DataTable, Badge } from "../../components/ui";
import type { Column } from "../../components/ui";
import type { InviteCode } from "../../types";
import { pageTitleRow } from "../../tw";

/**
 * Row type for DataTable — intersects InviteCode with Record to satisfy
 * DataTable's generic constraint while keeping full type safety.
 * DECISION: Intersection over explicit index signature to avoid modifying
 * the shared InviteCode type (GOV-003 §5.1).
 */
type InviteRow = InviteCode & Record<string, unknown>;

/** Format ISO date string to human-readable locale format. */
function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

/**
 * Feedback message component — displays success/error text below actions.
 *
 * PRECONDITION: `msg` must have `type` and `text` when non-null.
 */
function FeedbackMessage({ msg }: { msg: { type: "success" | "error"; text: string } | null }) {
  if (!msg) return null;
  const color = msg.type === "success" ? "text-ds-completed" : "text-ds-failed";
  return <div className={`${color} text-s mt-md`} data-testid="invite-feedback">{msg.text}</div>;
}

export function AdminInvitesPage() {
  const [inviteCodes, setInviteCodes] = useState<InviteCode[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [generating, setGenerating] = useState(false);
  const [newCode, setNewCode] = useState<string | null>(null);
  const [copiedCode, setCopiedCode] = useState(false);
  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [confirmRevokeId, setConfirmRevokeId] = useState<string | null>(null);

  const loadInviteCodes = useCallback(async () => {
    try {
      setInviteCodes(await fetchInviteCodes());
    } catch {
      setInviteCodes([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadInviteCodes();
  }, [loadInviteCodes]);

  /** Generate a new invite code and refresh the list. */
  async function handleGenerate() {
    setGenerating(true);
    setMessage(null);
    setNewCode(null);
    setCopiedCode(false);
    try {
      const invite = await generateInviteCode();
      setNewCode(invite.code);
      await loadInviteCodes();
      setMessage({ type: "success", text: "Invite code generated." });
    } catch (err) {
      setMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to generate invite code" });
    } finally {
      setGenerating(false);
    }
  }

  /** Copy a code to clipboard with fallback for older browsers. */
  async function handleCopyCode(code: string) {
    try {
      await navigator.clipboard.writeText(code);
    } catch {
      /* Fallback: temporary input element + execCommand */
      const input = document.createElement("input");
      input.value = code;
      document.body.appendChild(input);
      input.select();
      document.execCommand("copy"); // GOV-NNN-exempt: legacy fallback
      document.body.removeChild(input);
    }
    setCopiedCode(true);
    setTimeout(() => setCopiedCode(false), 2000);
  }

  /**
   * Revoke an invite code with two-step confirmation.
   * First click sets the confirm state; second click executes.
   */
  async function handleRevoke(id: string) {
    if (confirmRevokeId !== id) {
      setConfirmRevokeId(id);
      return;
    }
    setRevokingId(id);
    setConfirmRevokeId(null);
    setMessage(null);
    try {
      await revokeInviteCode(id);
      await loadInviteCodes();
      setMessage({ type: "success", text: "Invite code revoked." });
    } catch (err) {
      setMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to revoke invite code" });
    } finally {
      setRevokingId(null);
    }
  }

  /** DataTable column definitions for invite codes. */
  const columns: Column<InviteRow>[] = [
    {
      key: "code",
      header: "Code",
      render: (row) => <span className="font-mono" data-testid={`invite-code-${row.id}`}>{row.code}</span>,
    },
    {
      key: "status",
      header: "Status",
      render: (row) => {
        const isUsed = Boolean(row.usedByUserId);
        return (
          <Badge variant={isUsed ? "pending" : "completed"} dot={false}>
            {isUsed ? "Used" : "Available"}
          </Badge>
        );
      },
    },
    {
      key: "usedByUserId",
      header: "Used By",
      render: (row) => (
        <span className="text-ds-text-muted text-s">
          {row.usedByUserId ?? "—"}
        </span>
      ),
    },
    {
      key: "createdAt",
      header: "Created",
      render: (row) => <span className="text-ds-text-muted">{formatDate(row.createdAt)}</span>,
    },
    {
      key: "actions",
      header: "",
      width: "140px",
      render: (row) => {
        const isUsed = Boolean(row.usedByUserId);
        if (isUsed) return null;

        if (confirmRevokeId === row.id) {
          return (
            <div className="flex gap-xs" data-testid={`revoke-confirm-${row.id}`}>
              <Button
                variant="danger"
                size="sm"
                onClick={() => { void handleRevoke(row.id); }}
                disabled={revokingId === row.id}
                loading={revokingId === row.id}
              >
                Confirm
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setConfirmRevokeId(null)}
                disabled={revokingId === row.id}
              >
                Cancel
              </Button>
            </div>
          );
        }

        return (
          <Button
            variant="danger"
            size="sm"
            onClick={() => { void handleRevoke(row.id); }}
            disabled={revokingId === row.id}
            data-testid={`revoke-invite-${row.id}`}
          >
            Revoke
          </Button>
        );
      },
    },
  ];

  return (
    <article id="admin-invites-page" data-testid="admin-invites-page" aria-label="Invite Code Management">
      {/* Page header */}
      <div className={pageTitleRow}>
        <h1 className="text-xl font-bold text-ds-text">Invite Codes</h1>
        <Button
          variant="primary"
          onClick={() => { void handleGenerate(); }}
          loading={generating}
          disabled={generating}
          data-testid="generate-invite-btn"
        >
          {generating ? "Generating…" : "Generate Code"}
        </Button>
      </div>

      {/* Newly generated code banner */}
      {newCode && (
        <div
          className="flex items-center gap-md p-md bg-ds-primary-muted rounded-md mb-lg"
          data-testid="generated-invite-code"
        >
          <span className="font-mono font-bold text-md text-ds-primary flex-1">{newCode}</span>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => { void handleCopyCode(newCode); }}
            data-testid="copy-invite-btn"
          >
            {copiedCode ? "Copied!" : "Copy"}
          </Button>
        </div>
      )}

      {/* Invite codes table */}
      <Card>
        <DataTable<InviteRow>
          columns={columns}
          data={inviteCodes as InviteRow[]}
          loading={loading}
          skeletonRows={4}
          emptyMessage="No invite codes generated yet."
        />
        <FeedbackMessage msg={message} />
      </Card>
    </article>
  );
}
