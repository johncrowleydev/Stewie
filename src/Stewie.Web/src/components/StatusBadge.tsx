/**
 * StatusBadge — backwards-compatible wrapper around the new Badge component.
 *
 * Maps the legacy string-based `status` prop to the typed `Badge` variant API.
 * Consumers should migrate to `<Badge>` from `components/ui/Badge` directly.
 *
 * DECISION: Keep this file as a thin re-export rather than deleting it now.
 * Deleting would require updating all consumers in this same task, which
 * conflicts with JOB-029's scope (consumer migration). This wrapper ensures
 * zero breakage during the transition.
 *
 * REF: JOB-028 T-503 (Badge refactor), JOB-027 T-407 (original)
 */

import { Badge } from "./ui/Badge";
import type { BadgeVariant } from "./ui/Badge";

interface StatusBadgeProps {
  status: string;
}

/**
 * Maps legacy status strings to Badge variant names.
 * Statuses not in this map fall back to "pending".
 */
const STATUS_TO_VARIANT: Record<string, BadgeVariant> = {
  pending: "pending",
  running: "running",
  completed: "completed",
  failed: "failed",
  partiallycompleted: "warning",
  blocked: "pending",
  cancelled: "pending",
} as const;

/**
 * Legacy StatusBadge wrapper. Delegates to `<Badge>` internally.
 *
 * @deprecated Use `<Badge>` from `components/ui/Badge` directly.
 */
export function StatusBadge({ status }: StatusBadgeProps): React.JSX.Element {
  const normalized = status.toLowerCase();
  const variant = STATUS_TO_VARIANT[normalized] ?? "pending";

  return (
    <Badge variant={variant}>
      {status}
    </Badge>
  );
}
