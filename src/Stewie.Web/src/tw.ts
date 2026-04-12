/**
 * tw.ts — Shared Tailwind class strings for common UI patterns.
 * These replace the CSS classes from index.css (.btn-primary, .form-input, etc.)
 * with equivalent Tailwind utilities using ds-* @theme tokens.
 *
 * REF: JOB-027 T-407
 */

/** Primary button — solid green (light) / outlined green (dark) */
export const btnPrimary = [
  "inline-flex items-center justify-center gap-sm py-sm px-md rounded-md",
  "text-md font-medium font-sans cursor-pointer border leading-tight whitespace-nowrap no-underline",
  "transition-all duration-150",
  /* Light mode: solid fill */
  "bg-ds-primary text-white border-ds-primary",
  "hover:bg-ds-primary-hover hover:border-ds-primary-hover hover:shadow-ds-sm",
  "active:translate-y-px",
  "disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none",
  /* Dark mode: outline variant */
  "dark:bg-transparent dark:text-ds-primary dark:border-ds-primary",
  "dark:hover:bg-[rgba(111,172,80,0.15)] dark:hover:text-[#8ecf6e] dark:hover:border-[#8ecf6e]",
  "dark:hover:shadow-[0_0_12px_rgba(111,172,80,0.1)]",
].join(" ");

/** Ghost button — subtle bordered */
export const btnGhost = [
  "inline-flex items-center justify-center gap-sm py-sm px-md rounded-md",
  "text-md font-medium font-sans cursor-pointer leading-tight whitespace-nowrap no-underline",
  "transition-all duration-150",
  "bg-transparent text-ds-text-muted border border-ds-border",
  "hover:bg-ds-surface-hover hover:text-ds-text hover:border-ds-border-hover",
].join(" ");

/** Danger button — destructive actions */
export const btnDanger = [
  "inline-flex items-center justify-center gap-sm py-sm px-md rounded-md",
  "text-md font-medium font-sans cursor-pointer leading-tight whitespace-nowrap no-underline",
  "transition-all duration-150",
  "bg-transparent text-ds-failed border border-ds-failed",
  "hover:bg-[rgba(220,60,60,0.1)] hover:text-[#ff6b6b] hover:border-[#ff6b6b]",
].join(" ");

/** Standard form input */
export const formInput = [
  "w-full py-sm px-md bg-ds-bg border border-ds-border rounded-md",
  "text-ds-text text-md font-sans",
  "transition-[border-color] duration-150",
  "focus:outline-none focus:border-ds-primary focus:shadow-[0_0_0_3px_var(--color-primary-muted)]",
  "placeholder:text-ds-text-muted",
].join(" ");

/** Form label */
export const formLabel = "block text-s font-medium text-ds-text-muted mb-xs";

/** Form group wrapper */
export const formGroup = "mb-md";

/** Form hint text */
export const formHint = "text-xs text-ds-text-muted mt-xs italic";

/** Card container */
export const card = [
  "bg-ds-surface border border-ds-border rounded-lg p-lg",
  "transition-all duration-150",
].join(" ");

/** Data table */
export const dataTable = "w-full border-collapse";

/** Table header cell */
export const th = "text-left py-sm px-md text-xs font-semibold uppercase tracking-wider text-ds-text-muted border-b border-ds-border";

/** Table body cell */
export const td = "p-md text-s";

/** Table row (interactive) */
export const trClickable = "border-b border-ds-border last:border-b-0 cursor-pointer hover:bg-ds-surface-hover transition-colors duration-150";

/** Empty state wrapper */
export const emptyState = "text-center p-2xl text-ds-text-muted";

/** Skeleton loading shimmer */
export const skeleton = "bg-ds-surface rounded-md animate-[shimmer_1.5s_infinite] bg-[length:200%_100%] bg-[linear-gradient(90deg,var(--color-surface)_25%,var(--color-surface-hover)_50%,var(--color-surface)_75%)]";

/** Back button */
export const backButton = [
  "inline-flex items-center gap-xs text-s text-ds-text-muted no-underline font-medium mb-lg",
  "hover:text-ds-primary",
  "transition-colors duration-150",
].join(" ");

/** Section heading */
export const sectionHeading = "text-base font-semibold text-ds-text mb-md flex items-center gap-sm";

/** Page title row */
export const pageTitleRow = "flex items-center justify-between mb-xl";
