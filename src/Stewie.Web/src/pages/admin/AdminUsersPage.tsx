/**
 * AdminUsersPage — admin-only page for managing user accounts.
 *
 * Extracted from SettingsPage (JOB-033 T-551) into a dedicated admin page
 * accessible via `/admin/users`. Uses the ui/ component library for
 * consistent styling across the admin panel.
 *
 * **Design decisions:**
 * - Uses Card for page layout, DataTable for the user list, Button for
 *   actions, Badge for role indicators, and Modal for delete confirmation
 *   (GOV-003 §8.2).
 * - Delete confirmation uses Modal instead of inline `confirm()` for
 *   better UX and accessibility (GOV-003 §8.3).
 * - Admin users and the current user are protected from deletion — the
 *   delete button is replaced with a disabled placeholder and tooltip.
 * - DataTable columns: Username, Role (Badge), Created, Actions.
 * - `data-testid` on all interactive elements (GOV-003 §8.4).
 * - Responsive via DataTable's built-in horizontal scroll (GOV-003 §8.10).
 *
 * Used by: App.tsx (admin routes)
 * Related: api/client.ts (fetchUsers, deleteUser)
 *
 * REF: JOB-033 T-551
 *
 * @example
 * ```tsx
 * <Route path="users" element={<AdminUsersPage />} />
 * ```
 */

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../contexts/AuthContext";
import { fetchUsers, deleteUser } from "../../api/client";
import { Card, Button, DataTable, Badge, Modal } from "../../components/ui";
import type { Column } from "../../components/ui";
import type { UserInfo } from "../../types";
import { pageTitleRow } from "../../tw";

/**
 * Row type for DataTable — intersects UserInfo with Record to satisfy
 * DataTable's generic constraint while keeping full type safety.
 * DECISION: Intersection over explicit index signature to avoid modifying
 * the shared UserInfo type (GOV-003 §5.1).
 */
type UserRow = UserInfo & Record<string, unknown>;

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
  return <div className={`${color} text-s mt-md`} data-testid="users-feedback">{msg.text}</div>;
}

export function AdminUsersPage() {
  const { user: authUser } = useAuth();
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<UserInfo | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadUsers = useCallback(async () => {
    try {
      setUsers(await fetchUsers());
    } catch {
      setUsers([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  /** Open delete confirmation modal for a specific user. */
  function handleOpenDeleteModal(userToDelete: UserInfo) {
    setDeleteTarget(userToDelete);
    setMessage(null);
  }

  /** Close the delete confirmation modal without action. */
  function handleCloseDeleteModal() {
    setDeleteTarget(null);
  }

  /** Execute user deletion after modal confirmation. */
  async function handleConfirmDelete() {
    if (!deleteTarget) return;

    setDeleting(true);
    setMessage(null);
    try {
      await deleteUser(deleteTarget.id);
      await loadUsers();
      setMessage({ type: "success", text: `User "${deleteTarget.username}" deleted.` });
    } catch (err) {
      setMessage({ type: "error", text: err instanceof Error ? err.message : "Failed to delete user" });
    } finally {
      setDeleting(false);
      setDeleteTarget(null);
    }
  }

  /** Total user count for the page header. */
  const userCount = users.length;

  /** DataTable column definitions for users. */
  const columns: Column<UserRow>[] = [
    {
      key: "username",
      header: "Username",
      render: (row) => {
        const isSelf = row.id === authUser?.id;
        return (
          <span className="flex items-center gap-sm" data-testid={`user-row-${row.id}`}>
            {row.username}
            {isSelf && (
              <Badge variant="info" size="sm" dot={false}>you</Badge>
            )}
          </span>
        );
      },
    },
    {
      key: "role",
      header: "Role",
      render: (row) => (
        <Badge variant={row.role === "admin" ? "warning" : "running"} dot={false}>
          {row.role}
        </Badge>
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
      width: "100px",
      render: (row) => {
        const isSelf = row.id === authUser?.id;
        const isTargetAdmin = row.role === "admin";
        const canDelete = !isSelf && !isTargetAdmin;

        if (!canDelete) {
          return (
            <span
              className="text-ds-text-muted opacity-50 cursor-not-allowed"
              title={isSelf ? "Cannot delete your own account" : "Cannot delete admin users"}
              data-testid={`delete-user-disabled-${row.id}`}
            >
              —
            </span>
          );
        }

        return (
          <Button
            variant="danger"
            size="sm"
            onClick={() => handleOpenDeleteModal(row)}
            data-testid={`delete-user-${row.id}`}
          >
            Delete
          </Button>
        );
      },
    },
  ];

  return (
    <article id="admin-users-page" data-testid="admin-users-page" aria-label="User Management">
      {/* Page header */}
      <div className={pageTitleRow}>
        <div>
          <h1 className="text-xl font-bold text-ds-text">Users</h1>
          {!loading && (
            <p className="text-s text-ds-text-muted mt-xs" data-testid="user-count">
              {userCount} {userCount === 1 ? "user" : "users"} registered
            </p>
          )}
        </div>
      </div>

      {/* Users table */}
      <Card>
        <DataTable<UserRow>
          columns={columns}
          data={users as UserRow[]}
          loading={loading}
          skeletonRows={4}
          emptyMessage="No users found."
        />
        <FeedbackMessage msg={message} />
      </Card>

      {/* Delete confirmation modal */}
      <Modal
        isOpen={Boolean(deleteTarget)}
        onClose={handleCloseDeleteModal}
        title="Delete User"
        size="sm"
        footer={
          <>
            <Button
              variant="ghost"
              onClick={handleCloseDeleteModal}
              disabled={deleting}
            >
              Cancel
            </Button>
            <Button
              variant="danger"
              onClick={() => { void handleConfirmDelete(); }}
              loading={deleting}
              disabled={deleting}
              data-testid="confirm-delete-user-btn"
            >
              {deleting ? "Deleting…" : "Delete User"}
            </Button>
          </>
        }
      >
        <p>
          Are you sure you want to delete user{" "}
          <strong className="text-ds-text">{deleteTarget?.username}</strong>?
          This action cannot be undone.
        </p>
      </Modal>
    </article>
  );
}
