/**
 * Icons — Shared SVG icon components for the Stewie dashboard.
 *
 * All icons are flat, monochrome, 16×16 by default. They inherit
 * currentColor for fill/stroke so they adapt to theme automatically.
 * No icon library dependency — raw SVG for full control.
 *
 * REF: JOB-024
 */

interface IconProps {
  /** Width and height in pixels */
  size?: number;
  /** CSS class name */
  className?: string;
}

/** Robot/circuit icon — represents the Architect agent */
export function IconBot({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M8 1a1 1 0 0 1 1 1v1h2a2 2 0 0 1 2 2v1h1a1 1 0 1 1 0 2h-1v2a2 2 0 0 1-2 2h-1v2a1 1 0 1 1-2 0v-2H6v2a1 1 0 1 1-2 0v-2H3a2 2 0 0 1-2-2V8H0a1 1 0 0 1 0-2h1V5a2 2 0 0 1 2-2h2V2a1 1 0 0 1 1-1zm2 4H6a1 1 0 0 0-1 1v4a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1zM6.5 7a.75.75 0 1 1 0 1.5.75.75 0 0 1 0-1.5zm3 0a.75.75 0 1 1 0 1.5.75.75 0 0 1 0-1.5z" />
    </svg>
  );
}

/** Chat bubble icon */
export function IconChat({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M2 2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2v2.5a.5.5 0 0 0 .82.384L8.28 13H14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H2zm1 3.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5z" />
    </svg>
  );
}

/** User/person icon — represents a human user */
export function IconUser({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm4.735 2.735A7.97 7.97 0 0 0 8 9a7.97 7.97 0 0 0-4.735 1.735A6.96 6.96 0 0 0 1 15h14a6.96 6.96 0 0 0-2.265-4.265z" />
    </svg>
  );
}

/** Gear/cog icon — represents system messages or settings */
export function IconGear({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M7.068.727c.243-.97 1.62-.97 1.864 0l.071.286a.96.96 0 0 0 1.622.434l.205-.211c.695-.719 1.888-.03 1.613.93l-.084.303a.96.96 0 0 0 1.187 1.187l.303-.084c.96-.275 1.65.918.93 1.613l-.211.205a.96.96 0 0 0 .434 1.622l.286.071c.97.243.97 1.62 0 1.864l-.286.071a.96.96 0 0 0-.434 1.622l.211.205c.719.695.03 1.888-.93 1.613l-.303-.084a.96.96 0 0 0-1.187 1.187l.084.303c.275.96-.918 1.65-1.613.93l-.205-.211a.96.96 0 0 0-1.622.434l-.071.286c-.243.97-1.62.97-1.864 0l-.071-.286a.96.96 0 0 0-1.622-.434l-.205.211c-.695.719-1.888.03-1.613-.93l.084-.303a.96.96 0 0 0-1.187-1.187l-.303.084c-.96.275-1.65-.918-.93-1.613l.211-.205a.96.96 0 0 0-.434-1.622l-.286-.071c-.97-.243-.97-1.62 0-1.864l.286-.071a.96.96 0 0 0 .434-1.622L.149 4.17c-.719-.695-.03-1.888.93-1.613l.303.084A.96.96 0 0 0 2.57 1.454L2.486 1.15c-.275-.96.918-1.65 1.613-.93l.205.211a.96.96 0 0 0 1.622-.434l.071-.286zM8 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" />
    </svg>
  );
}

/** Bar chart icon — represents analytics/metrics */
export function IconBarChart({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M1 15V1h2v14H1zm4 0V5h2v10H5zm4 0V8h2v7H9zm4 0V3h2v12h-2z" />
    </svg>
  );
}

/** Key icon — represents API keys / credentials */
export function IconKey({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M10.5 0a5.5 5.5 0 0 0-4.764 8.248L0 14v2h3v-2h2v-2h2l1.248-1.248A5.5 5.5 0 1 0 10.5 0zm1.5 5a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3z" />
    </svg>
  );
}

/** People group icon — represents users list. REF: JOB-026 */
export function IconUsers({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M6 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm-4.735 2.735A7.97 7.97 0 0 1 6 9c1.8 0 3.45.6 4.735 1.735A6.96 6.96 0 0 1 13 15H-1a6.96 6.96 0 0 1 2.265-4.265z" />
      <path d="M11 4a2 2 0 1 1 4 0 2 2 0 0 1-4 0zm.332 5.024A8.032 8.032 0 0 1 14 9c.94 0 1.84.163 2.676.46A5.97 5.97 0 0 1 19 14h-3.5a7.95 7.95 0 0 0-1.168-2.976 5.98 5.98 0 0 0-3-2z" opacity="0.6" />
    </svg>
  );
}

/** Shield/security icon — represents admin / invite codes. REF: JOB-026 */
export function IconShield({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M8 0L1.5 3v4.5c0 3.83 2.77 7.41 6.5 8.5 3.73-1.09 6.5-4.67 6.5-8.5V3L8 0zm-.5 11.32L4.76 8.58l1.06-1.06L7.5 9.2l2.68-2.68 1.06 1.06L7.5 11.32z" />
    </svg>
  );
}

// ── Additional icons for replacing all emoji usage ──

/** Heart/pulse icon — represents health status */
export function IconHeart({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M1.35 5.44C1.35 3.01 3.36 1 5.79 1c1.27 0 2.42.54 3.21 1.39A4.44 4.44 0 0 1 12.21 1c2.43 0 4.44 2.01 4.44 4.44 0 1.26-.52 2.4-1.37 3.2L8 16 .72 8.64C.34 7.84 1.35 6.7 1.35 5.44z" />
    </svg>
  );
}

/** Tag/label icon — represents version */
export function IconTag({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M2 1a1 1 0 0 0-1 1v4.586a1 1 0 0 0 .293.707l7.414 7.414a1 1 0 0 0 1.414 0l4.586-4.586a1 1 0 0 0 0-1.414L7.293 1.293A1 1 0 0 0 6.586 1H2zm2 2a1 1 0 1 1 0 2 1 1 0 0 1 0-2z" />
    </svg>
  );
}

/** Folder icon — represents projects */
export function IconFolder({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M1 3.5A1.5 1.5 0 0 1 2.5 2h3.879a1.5 1.5 0 0 1 1.06.44l1.122 1.12A1.5 1.5 0 0 0 9.62 4H13.5A1.5 1.5 0 0 1 15 5.5v7a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 1 12.5v-9z" />
    </svg>
  );
}

/** Lightning bolt icon — represents jobs / activity */
export function IconBolt({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M9.585 0L4 9h4l-1.585 7L13 7H9l.585-7z" />
    </svg>
  );
}

/** Wrench icon — represents system details / configuration */
export function IconWrench({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M11.5 1a4.5 4.5 0 0 0-4.28 5.88L1.29 12.8a1 1 0 0 0 0 1.41l.5.5a1 1 0 0 0 1.41 0l5.92-5.93A4.5 4.5 0 1 0 11.5 1zm0 2a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5z" />
    </svg>
  );
}

/** Clipboard/list icon — represents activity feed */
export function IconClipboard({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M6.5 0A1.5 1.5 0 0 0 5 1.5H3.5A1.5 1.5 0 0 0 2 3v11a1.5 1.5 0 0 0 1.5 1.5h9A1.5 1.5 0 0 0 14 14V3a1.5 1.5 0 0 0-1.5-1.5H11A1.5 1.5 0 0 0 9.5 0h-3zM8 1a.5.5 0 1 1 0 1 .5.5 0 0 1 0-1zM5 6h6v1H5V6zm0 2.5h6v1H5v-1zm0 2.5h4v1H5v-1z" />
    </svg>
  );
}

/** Warning triangle icon — represents alerts / errors */
export function IconAlertTriangle({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M6.457 1.047c.659-1.234 2.427-1.234 3.086 0l5.97 11.186C16.14 13.399 15.266 15 13.97 15H2.03C.734 15-.14 13.399.487 12.233L6.457 1.047zM8 5a.75.75 0 0 0-.75.75v2.5a.75.75 0 0 0 1.5 0v-2.5A.75.75 0 0 0 8 5zm0 6.25a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5z" />
    </svg>
  );
}

/** Checkmark icon — represents success / pass */
export function IconCheck({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M13.78 4.22a.75.75 0 0 1 0 1.06l-7.25 7.25a.75.75 0 0 1-1.06 0L2.22 9.28a.75.75 0 0 1 1.06-1.06L6 10.94l6.72-6.72a.75.75 0 0 1 1.06 0z" />
    </svg>
  );
}

/** X mark icon — represents failure / close / remove */
export function IconX({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M3.72 3.72a.75.75 0 0 1 1.06 0L8 6.94l3.22-3.22a.75.75 0 1 1 1.06 1.06L9.06 8l3.22 3.22a.75.75 0 1 1-1.06 1.06L8 9.06l-3.22 3.22a.75.75 0 0 1-1.06-1.06L6.94 8 3.72 4.78a.75.75 0 0 1 0-1.06z" />
    </svg>
  );
}

/** Chevron right icon — represents collapsed / expand */
export function IconChevronRight({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M6.22 3.22a.75.75 0 0 1 1.06 0l4.25 4.25a.75.75 0 0 1 0 1.06l-4.25 4.25a.75.75 0 0 1-1.06-1.06L9.94 8 6.22 4.28a.75.75 0 0 1 0-1.06z" />
    </svg>
  );
}

/** Chevron down icon — represents expanded / collapse */
export function IconChevronDown({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M3.22 6.22a.75.75 0 0 1 1.06 0L8 9.94l3.72-3.72a.75.75 0 0 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L3.22 7.28a.75.75 0 0 1 0-1.06z" />
    </svg>
  );
}

/** Play icon — represents started / running */
export function IconPlay({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M4 2a1 1 0 0 1 1.537-.844l9 5.5a1 1 0 0 1 0 1.688l-9 5.5A1 1 0 0 1 4 13V2z" />
    </svg>
  );
}

/** Stop/square icon — represents terminated / stopped */
export function IconStop({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M3 3h10v10H3z" />
    </svg>
  );
}

/** Refresh/retry icon — represents retry / reload */
export function IconRefresh({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M13.987 6.5H16L13 2l-3 4.5h2.018A5.023 5.023 0 0 1 13 8c0 2.757-2.243 5-5 5a4.98 4.98 0 0 1-3.535-1.465l-1.06 1.06A6.476 6.476 0 0 0 8 14.5c3.584 0 6.5-2.916 6.5-6.5 0-.527-.069-1.037-.513-1.5zM3 8c0-2.757 2.243-5 5-5a4.98 4.98 0 0 1 3.535 1.465l1.06-1.06A6.476 6.476 0 0 0 8 1.5C4.416 1.5 1.5 4.416 1.5 8c0 .527.069 1.037.513 1.5H0L3 14l3-4.5H3.982A5.023 5.023 0 0 1 3 8z" />
    </svg>
  );
}

/** Plus icon — represents created / add */
export function IconPlus({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M8 2a.75.75 0 0 1 .75.75v4.5h4.5a.75.75 0 0 1 0 1.5h-4.5v4.5a.75.75 0 0 1-1.5 0v-4.5h-4.5a.75.75 0 0 1 0-1.5h4.5v-4.5A.75.75 0 0 1 8 2z" />
    </svg>
  );
}

/** Question mark icon — represents unknown / fallback */
export function IconQuestion({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M8 1a7 7 0 1 0 0 14A7 7 0 0 0 8 1zm0 2.5a2.5 2.5 0 0 1 2.42 3.13l-.17.55A1.99 1.99 0 0 0 8.75 9v.25a.75.75 0 0 1-1.5 0V9c0-.69.354-1.337.94-1.708l.17-.11A1 1 0 1 0 7 6.25a.75.75 0 0 1-1.5 0A2.5 2.5 0 0 1 8 3.5zM8 13a1 1 0 1 1 0-2 1 1 0 0 1 0 2z" />
    </svg>
  );
}

/** Git branch icon — represents branches */
export function IconBranch({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M9.5 3.25a2.25 2.25 0 1 1 3 2.122V6A2.5 2.5 0 0 1 10 8.5H6a1 1 0 0 0-1 1v1.128a2.251 2.251 0 1 1-1.5 0V5.372a2.25 2.25 0 1 1 1.5 0v1.836A2.492 2.492 0 0 1 6 7h4a1 1 0 0 0 1-1v-.628A2.25 2.25 0 0 1 9.5 3.25z" />
    </svg>
  );
}

/** Microscope/beaker icon — represents tester/researcher role */
export function IconBeaker({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <path d="M5 1v5.27L1.33 13A1.5 1.5 0 0 0 2.6 15h10.8a1.5 1.5 0 0 0 1.27-2L11 6.27V1h1a.5.5 0 0 0 0-1H4a.5.5 0 0 0 0 1h1zm1.5 0h3v5.5a.5.5 0 0 0 .07.25L12.79 13H3.21l3.22-6.25A.5.5 0 0 0 6.5 6.5V1z" />
    </svg>
  );
}

/** Circle/dot icon — represents in-progress / running indicator */
export function IconCircle({ size = 16, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" className={className} aria-hidden="true">
      <circle cx="8" cy="8" r="5" />
    </svg>
  );
}
