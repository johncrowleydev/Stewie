/**
 * Button — reusable button component with variant, size, and loading support.
 *
 * Wraps the `btnPrimary`, `btnGhost`, and `btnDanger` class strings from
 * `tw.ts` into a typed React component with props-driven variants.
 *
 * **Design decisions:**
 * - Imports base styles from `tw.ts` to stay DRY during the migration period
 *   (GOV-003 §1.3 — single responsibility, no duplication).
 * - `as="a"` renders a React Router `<Link>` for SPA navigation instead of a
 *   raw anchor, preserving client-side routing (GOV-003 §8.2).
 * - Loading state disables the button AND shows an inline spinner SVG to give
 *   users clear visual feedback without layout shift.
 * - Size variants use additive class strings rather than replacing base styles,
 *   so spacing/font tweaks layer on top of the existing tw.ts foundation.
 * - `data-testid` is always present for deterministic test selectors (GOV-003 §8.4).
 *
 * Used by: Pages that need action buttons (SettingsPage, ProjectsPage, etc.)
 * Related: tw.ts (base styles), GOV-003 §8
 *
 * REF: JOB-028 T-500
 *
 * @example
 * ```tsx
 * <Button variant="primary">Save</Button>
 * <Button variant="primary" loading>Saving…</Button>
 * <Button variant="ghost" size="sm">Cancel</Button>
 * <Button variant="danger" onClick={handleDelete}>Delete</Button>
 * <Button variant="primary" as="a" href="/projects">View Projects</Button>
 * ```
 */

import { Link } from "react-router-dom";
import { btnPrimary, btnGhost, btnDanger } from "../../tw";

/* ── Variant → tw.ts class mapping ── */
const VARIANT_CLASSES = {
  primary: btnPrimary,
  ghost: btnGhost,
  danger: btnDanger,
} as const;

/* ── Size modifier classes ──
 * DECISION: Additive modifiers rather than full replacements.
 * TRADEOFF: Slightly more class output, but avoids duplicating the entire
 * base style string for each size.
 */
const SIZE_CLASSES = {
  sm: "py-xs px-sm text-s",
  md: "", // md is the default size baked into tw.ts base strings
} as const;

/**
 * Props for the {@link Button} component.
 *
 * Extends native `<button>` attributes so consumers can pass `onClick`,
 * `disabled`, `type`, `aria-*`, etc. without wrapper boilerplate.
 */
export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual variant controlling color scheme and hover behavior. */
  variant: "primary" | "ghost" | "danger";
  /** Size preset. Defaults to `"md"`. */
  size?: "sm" | "md";
  /** When true, disables the button and shows an inline spinner. */
  loading?: boolean;
  /**
   * Polymorphic render tag.
   * - `"button"` (default): renders a native `<button>`.
   * - `"a"`: renders a React Router `<Link>` for client-side navigation.
   */
  as?: "button" | "a";
  /** Destination URL. Only used when `as="a"`. */
  href?: string;
}

/**
 * Renders a styled button (or link) with variant, size, and loading support.
 *
 * @returns A `<button>` element, or a React Router `<Link>` when `as="a"`.
 */
export function Button({
  variant,
  size = "md",
  loading = false,
  as: renderAs = "button",
  href,
  children,
  className,
  disabled,
  ...rest
}: ButtonProps): React.JSX.Element {
  const baseClasses = VARIANT_CLASSES[variant];
  const sizeClasses = SIZE_CLASSES[size];
  const composedClassName = [
    baseClasses,
    sizeClasses,
    loading ? "opacity-70 cursor-wait" : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");

  const isDisabled = disabled || loading;

  /* ── Link rendering path ── */
  if (renderAs === "a" && href) {
    return (
      <Link
        to={href}
        className={composedClassName}
        data-testid={`button-link-${variant}`}
        aria-disabled={isDisabled || undefined}
        tabIndex={isDisabled ? -1 : undefined}
        onClick={isDisabled ? (e) => e.preventDefault() : undefined}
      >
        {loading && <LoadingSpinner />}
        {children}
      </Link>
    );
  }

  /* ── Default button rendering path ── */
  return (
    <button
      className={composedClassName}
      disabled={isDisabled}
      data-testid={`button-${variant}`}
      {...rest}
    >
      {loading && <LoadingSpinner />}
      {children}
    </button>
  );
}

/**
 * Inline SVG spinner displayed inside the button during loading state.
 * Uses `currentColor` so it inherits the button's text color automatically.
 *
 * DECISION: Inline SVG rather than a CSS-only spinner.
 * TRADEOFF: Slightly more markup, but guarantees consistent sizing and
 * color inheritance across all variants without extra CSS.
 */
function LoadingSpinner(): React.JSX.Element {
  return (
    <svg
      className="animate-spin h-4 w-4 shrink-0"
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
      aria-hidden="true"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962
           7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}
