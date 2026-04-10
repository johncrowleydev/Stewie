/**
 * StatusBadge — color-coded status pill component.
 * Maps RunStatus/TaskStatus values to visual states.
 * Running state includes an animated pulse dot.
 */

interface StatusBadgeProps {
  /** The status value — must be a valid RunStatus or TaskStatus */
  status: string;
}

/**
 * Renders a color-coded status badge with a dot indicator.
 * Supports: Pending (gray), Running (blue, pulsing), Completed (green), Failed (red).
 */
export function StatusBadge({ status }: StatusBadgeProps) {
  const normalized = status.toLowerCase();

  return (
    <span className={`status-badge ${normalized}`}>
      <span className="status-dot" />
      {status}
    </span>
  );
}
