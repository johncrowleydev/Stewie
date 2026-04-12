/**
 * Badge — generic variant-based status pill component.
 *
 * Refactored from the original `StatusBadge` component to support a typed
 * variant system with additional `info` and `warning` variants, size control,
 * and optional animated dot indicator.
 *
 * **Design decisions:**
 * - Variant-based API replaces the previous string-status→color lookup.
 *   This gives TypeScript narrowing on valid variants at compile time
 *   instead of runtime string matching (GOV-003 §8.5).
 * - Backwards-compatible: the `variant` prop accepts the same status strings
 *   that the old `StatusBadge` used (pending, running, completed, failed),
 *   plus new `warning` and `info` variants.
 * - Dot indicator defaults to `true` for consistency with the old StatusBadge.
 *   Running variant keeps the pulse animation.
 * - Uses `as const` for the style map for maximum type safety (GOV-003 §8.5).
 * - `data-testid` ensures deterministic test selectors (GOV-003 §8.4).
 *
 * Used by: DashboardPage, JobsPage, JobDetailPage, JobProgressPanel, TaskDagView
 * Related: StatusBadge (replaced), GOV-003 §8
 *
 * REF: JOB-028 T-503
 *
 * @example
 * ```tsx
 * <Badge variant="completed">Completed</Badge>
 * <Badge variant="running" size="sm">Running</Badge>
 * <Badge variant="info" dot={false}>New</Badge>
 * <Badge variant="warning">Attention</Badge>
 * ```
 */

/** Valid badge variant names. */
export type BadgeVariant =
  | "pending"
  | "running"
  | "completed"
  | "failed"
  | "warning"
  | "info";

/**
 * Per-variant style configuration.
 *
 * DECISION: Using inline CSS `style` for bg/text color rather than Tailwind
 * utility classes because the color values reference CSS custom properties
 * that vary by theme. Tailwind's `bg-ds-*` tokens only provide the solid
 * color — we need the semi-transparent background variant (e.g. rgba(…, 0.15)).
 * TRADEOFF: Slightly less "pure Tailwind" but guarantees correct theming.
 */
const VARIANT_STYLES = {
  pending: {
    bg: "rgba(139,141,147,0.15)",
    text: "var(--color-pending)",
  },
  running: {
    bg: "rgba(59,130,246,0.15)",
    text: "var(--color-running)",
  },
  completed: {
    bg: "rgba(111,172,80,0.15)",
    text: "var(--color-completed)",
  },
  failed: {
    bg: "rgba(229,72,77,0.15)",
    text: "var(--color-failed)",
  },
  warning: {
    bg: "rgba(245,166,35,0.15)",
    text: "var(--color-warning)",
  },
  info: {
    bg: "rgba(59,130,246,0.15)",
    text: "var(--color-info, #3b82f6)",
  },
} as const;

/**
 * Size → class modifiers.
 * sm uses smaller padding and text; md matches the original StatusBadge.
 */
const SIZE_CLASSES = {
  sm: "py-px px-1.5 text-[10px]",
  md: "py-[3px] px-2.5 text-xs",
} as const;

/**
 * Props for the {@link Badge} component.
 */
export interface BadgeProps {
  /** Visual variant controlling the badge color scheme. */
  variant: BadgeVariant;
  /** Size preset. Defaults to `"md"`. */
  size?: "sm" | "md";
  /** Whether to show the dot indicator. Defaults to `true`. */
  dot?: boolean;
  /** Badge text content. */
  children: React.ReactNode;
}

/**
 * Renders a color-coded status pill with optional animated dot indicator.
 *
 * @returns A styled `<span>` element.
 */
export function Badge({
  variant,
  size = "md",
  dot = true,
  children,
}: BadgeProps): React.JSX.Element {
  const style = VARIANT_STYLES[variant];
  const sizeClass = SIZE_CLASSES[size];
  const isRunning = variant === "running";

  return (
    <span
      className={`inline-flex items-center gap-1.5 ${sizeClass}
                  rounded-full font-semibold uppercase tracking-wide`}
      style={{ background: style.bg, color: style.text }}
      data-testid={`badge-${variant}`}
    >
      {dot && (
        <span
          className={`w-1.5 h-1.5 rounded-full ${
            isRunning ? "animate-[pulse_1.5s_ease-in-out_infinite]" : ""
          }`}
          style={{ background: "currentColor" }}
          aria-hidden="true"
        />
      )}
      {children}
    </span>
  );
}
