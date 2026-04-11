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

/** Gear/cog icon — represents system messages */
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
