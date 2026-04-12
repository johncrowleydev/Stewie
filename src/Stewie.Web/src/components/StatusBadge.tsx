/**
 * StatusBadge — color-coded status pill component.
 * Maps RunStatus/TaskStatus values to visual states.
 * Running state includes an animated pulse dot.
 * REF: JOB-027 T-407
 */

interface StatusBadgeProps {
  status: string;
}

/** Status-specific styles: [bg, textColor] */
const statusStyles: Record<string, { bg: string; text: string; extra?: string }> = {
  pending:            { bg: "rgba(139,141,147,0.15)", text: "var(--color-pending)" },
  running:            { bg: "rgba(59,130,246,0.15)",  text: "var(--color-running)" },
  completed:          { bg: "rgba(111,172,80,0.15)",  text: "var(--color-completed)" },
  failed:             { bg: "rgba(229,72,77,0.15)",   text: "var(--color-failed)" },
  partiallycompleted: { bg: "rgba(245,166,35,0.15)",  text: "var(--color-warning)" },
  blocked:            { bg: "rgba(139,141,147,0.12)", text: "var(--color-text-muted)" },
  cancelled:          { bg: "rgba(139,141,147,0.08)", text: "var(--color-text-muted)", extra: "line-through opacity-70" },
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const normalized = status.toLowerCase();
  const s = statusStyles[normalized] ?? statusStyles.pending;
  const isRunning = normalized === "running";
  const isBlocked = normalized === "blocked";

  return (
    <span
      className={`inline-flex items-center gap-1.5 py-[3px] px-2.5 rounded-full text-xs font-semibold uppercase tracking-wide ${s.extra ?? ""}`}
      style={{ background: s.bg, color: s.text }}
    >
      <span
        className={`w-1.5 h-1.5 rounded-full ${isRunning ? "animate-[pulse_1.5s_ease-in-out_infinite]" : ""}`}
        style={{
          background: isBlocked ? "transparent" : "currentColor",
          border: isBlocked ? "1px solid currentColor" : undefined,
        }}
      />
      {status}
    </span>
  );
}
