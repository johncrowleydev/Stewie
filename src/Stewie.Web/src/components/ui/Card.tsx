/**
 * Card — composable card container with optional header and footer slots.
 *
 * Wraps the `card` class string from `tw.ts` into a typed React component
 * with padding variants, hover effect, and compound sub-components.
 *
 * **Design decisions:**
 * - Compound pattern (`Card.Header`, `Card.Footer`) gives consumers flexible
 *   slot composition without prop-drilling title/footer content (GOV-003 §8.2).
 * - `hoverable` adds shadow-on-hover via Tailwind's hover: modifier, which
 *   already respects the ds-* shadow tokens for theme-awareness.
 * - Padding variants map to the existing ds spacing tokens (sm/md/lg) so the
 *   component stays consistent with the design system.
 * - `className` is merged (not replaced) so consumers can layer additional
 *   styles without losing the base card appearance.
 * - `data-testid` ensures deterministic test selectors (GOV-003 §8.4).
 *
 * Used by: DashboardPage, JobDetailPage, ProjectDetailPage, SettingsPage
 * Related: tw.ts (base `card` style), GOV-003 §8
 *
 * REF: JOB-028 T-501
 *
 * @example
 * ```tsx
 * <Card>Simple content</Card>
 *
 * <Card hoverable padding="md">
 *   <Card.Header>Section Title</Card.Header>
 *   <p>Body content here</p>
 *   <Card.Footer>Footer actions</Card.Footer>
 * </Card>
 * ```
 */

import { card } from "../../tw";

/* ── Padding variant → Tailwind class mapping ──
 * DECISION: Override the base `p-lg` from tw.ts with explicit padding.
 * lg is the default and matches what tw.ts already ships, so it's a no-op
 * at that size. sm and md provide denser variants.
 */
const PADDING_CLASSES = {
  sm: "p-sm",
  md: "p-md",
  lg: "p-lg",
} as const;

/**
 * Props for the {@link Card} component.
 */
export interface CardProps {
  /** Card body content. */
  children: React.ReactNode;
  /** Additional CSS classes merged with the base card style. */
  className?: string;
  /** When true, adds a shadow lift effect on hover. */
  hoverable?: boolean;
  /** Padding preset. Defaults to `"lg"` (matches tw.ts base). */
  padding?: "sm" | "md" | "lg";
}

/**
 * Props for {@link CardHeader} and {@link CardFooter} sub-components.
 */
export interface CardSectionProps {
  /** Section content. */
  children: React.ReactNode;
  /** Additional CSS classes. */
  className?: string;
}

/**
 * Renders a themed card container with optional hover shadow and padding variants.
 *
 * Supports compound sub-components `Card.Header` and `Card.Footer` for
 * structured layouts.
 *
 * @returns A styled `<section>` element.
 */
function CardRoot({
  children,
  className,
  hoverable = false,
  padding = "lg",
}: CardProps): React.JSX.Element {
  const hoverClass = hoverable ? "hover:shadow-ds-md" : "";
  const paddingClass = PADDING_CLASSES[padding];

  const composedClassName = [card, paddingClass, hoverClass, className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <section className={composedClassName} data-testid="card">
      {children}
    </section>
  );
}

/**
 * Card header slot — renders a top section with a bottom border separator.
 *
 * @example
 * ```tsx
 * <Card.Header>Dashboard Overview</Card.Header>
 * ```
 */
function CardHeader({
  children,
  className,
}: CardSectionProps): React.JSX.Element {
  const baseClass =
    "border-b border-ds-border pb-md mb-md text-base font-semibold text-ds-text";
  const composedClassName = [baseClass, className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={composedClassName} data-testid="card-header">
      {children}
    </div>
  );
}

/**
 * Card footer slot — renders a bottom section with a top border separator.
 *
 * @example
 * ```tsx
 * <Card.Footer>
 *   <Button variant="primary">Save</Button>
 * </Card.Footer>
 * ```
 */
function CardFooter({
  children,
  className,
}: CardSectionProps): React.JSX.Element {
  const baseClass =
    "border-t border-ds-border pt-md mt-md flex items-center gap-sm";
  const composedClassName = [baseClass, className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={composedClassName} data-testid="card-footer">
      {children}
    </div>
  );
}

/**
 * Compound Card component with `Card.Header` and `Card.Footer` sub-components.
 *
 * DECISION: Using the Object.assign compound pattern rather than React context
 * for sub-components. This avoids unnecessary re-renders and keeps the API
 * simple — sub-components are just children, not context consumers.
 * TRADEOFF: No enforcement that Header/Footer are used inside a Card, but
 * the visual context makes misuse obvious.
 */
export const Card = Object.assign(CardRoot, {
  Header: CardHeader,
  Footer: CardFooter,
});
