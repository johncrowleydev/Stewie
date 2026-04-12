/**
 * Modal — accessible overlay dialog with focus trap, backdrop, and size variants.
 *
 * Uses the `overlay-fade-in` and `dropdown-appear` CSS keyframes from `app.css`
 * for entry animations and design system tokens for theming.
 *
 * **Design decisions:**
 * - Focus trap cycles Tab through focusable elements within the modal,
 *   preventing focus from escaping to the page behind it (WCAG 2.1 AA,
 *   GOV-003 §8.3).
 * - Body scroll lock sets `overflow: hidden` on `<body>` while the modal is
 *   open, then restores the original value on close. This prevents background
 *   scroll-through on mobile.
 * - Escape key closes the modal via a `keydown` listener on `document`.
 * - Backdrop click closes via `onMouseDown` on the overlay (not `onClick`)
 *   to avoid false closes from drag-started-inside-modal gestures.
 * - Uses React Portal (`createPortal`) to render at the document root,
 *   avoiding z-index stacking context issues with nested components.
 * - `data-testid` on overlay, dialog, title, and close button (GOV-003 §8.4).
 *
 * Used by: (forward-looking) JOB-029 delete confirmations, JOB-033 admin panels
 * Related: app.css (overlay-fade-in, dropdown-appear keyframes), GOV-003 §8
 *
 * REF: JOB-028 T-506
 *
 * @example
 * ```tsx
 * <Modal isOpen={showDelete} onClose={() => setShowDelete(false)} title="Confirm Delete">
 *   <p>Are you sure you want to delete this job?</p>
 *   <Modal footer={<Button variant="danger" onClick={handleDelete}>Delete</Button>} />
 * </Modal>
 *
 * <Modal isOpen={showSettings} onClose={closeSettings} title="Settings" size="lg">
 *   <SettingsForm />
 * </Modal>
 * ```
 */

import { useCallback, useEffect, useRef } from "react";
import { createPortal } from "react-dom";

/**
 * Size → max-width class mapping.
 *
 * DECISION: Fixed max-widths rather than percentage-based so modal sizing
 * is predictable regardless of viewport. On mobile, all sizes collapse to
 * near-full-width via the `w-[calc(100%-2rem)]` base class.
 */
const SIZE_CLASSES = {
  sm: "max-w-sm",
  md: "max-w-lg",
  lg: "max-w-2xl",
} as const;

/**
 * Props for the {@link Modal} component.
 */
export interface ModalProps {
  /** Controls modal visibility. */
  isOpen: boolean;
  /** Callback fired when the modal should close (escape, backdrop click, close button). */
  onClose: () => void;
  /** Title displayed in the modal header. */
  title: string;
  /** Modal body content. */
  children: React.ReactNode;
  /** Optional footer content (e.g. action buttons). */
  footer?: React.ReactNode;
  /** Width preset. Defaults to `"md"`. */
  size?: "sm" | "md" | "lg";
}

/**
 * Collects all focusable elements within a container.
 *
 * PRECONDITION: `container` must be a mounted DOM element.
 * POSTCONDITION: Returns an array of focusable elements in DOM order.
 */
function getFocusableElements(container: HTMLElement): HTMLElement[] {
  const selector = [
    "a[href]",
    "button:not([disabled])",
    "textarea:not([disabled])",
    "input:not([disabled])",
    "select:not([disabled])",
    '[tabindex]:not([tabindex="-1"])',
  ].join(", ");

  return Array.from(container.querySelectorAll<HTMLElement>(selector));
}

/**
 * Renders an accessible modal dialog with backdrop, focus trap, and scroll lock.
 *
 * Uses `createPortal` to render at the document root.
 *
 * @returns A React portal containing the modal overlay and dialog, or `null` when closed.
 */
export function Modal({
  isOpen,
  onClose,
  title,
  children,
  footer,
  size = "md",
}: ModalProps): React.JSX.Element | null {
  const dialogRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  /* ── Body scroll lock ──
   * SIDE EFFECTS: Mutates document.body.style.overflow.
   * Restores original value on cleanup. */
  useEffect(() => {
    if (!isOpen) return;

    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = originalOverflow;
    };
  }, [isOpen]);

  /* ── Focus management ──
   * Saves the previously-focused element, focuses the dialog on open,
   * and restores focus on close. */
  useEffect(() => {
    if (!isOpen) return;

    previousFocusRef.current = document.activeElement as HTMLElement | null;

    /* Delay focus to allow the portal to mount */
    const timer = setTimeout(() => {
      dialogRef.current?.focus();
    }, 0);

    return () => {
      clearTimeout(timer);
      previousFocusRef.current?.focus();
    };
  }, [isOpen]);

  /* ── Escape key handler ── */
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  /* ── Focus trap ──
   * Tab and Shift+Tab cycle through focusable elements within the dialog. */
  const handleFocusTrap = useCallback(
    (event: React.KeyboardEvent): void => {
      if (event.key !== "Tab" || !dialogRef.current) return;

      const focusable = getFocusableElements(dialogRef.current);
      if (focusable.length === 0) return;

      const firstFocusable = focusable[0];
      const lastFocusable = focusable[focusable.length - 1];

      if (event.shiftKey) {
        /* Shift+Tab: wrap to last element */
        if (document.activeElement === firstFocusable) {
          event.preventDefault();
          lastFocusable.focus();
        }
      } else {
        /* Tab: wrap to first element */
        if (document.activeElement === lastFocusable) {
          event.preventDefault();
          firstFocusable.focus();
        }
      }
    },
    [],
  );

  /* ── Backdrop click handler ──
   * Uses mousedown (not click) to avoid closing when user drags from
   * inside the dialog to the backdrop. */
  const handleBackdropMouseDown = useCallback(
    (event: React.MouseEvent): void => {
      if (event.target === event.currentTarget) {
        onClose();
      }
    },
    [onClose],
  );

  if (!isOpen) return null;

  const sizeClass = SIZE_CLASSES[size];

  return createPortal(
    <div
      className="fixed inset-0 z-[9999] flex items-center justify-center
                 bg-black/60 backdrop-blur-sm
                 animate-[overlay-fade-in_200ms_ease-out]"
      onMouseDown={handleBackdropMouseDown}
      data-testid="modal-overlay"
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        tabIndex={-1}
        className={`${sizeClass} w-[calc(100%-2rem)]
                    bg-ds-surface border border-ds-border rounded-lg shadow-ds-lg
                    animate-[dropdown-appear_200ms_ease-out]
                    focus:outline-none`}
        onKeyDown={handleFocusTrap}
        data-testid="modal-dialog"
      >
        {/* ── Header ── */}
        <div className="flex items-center justify-between px-lg pt-lg pb-md border-b border-ds-border">
          <h2
            id="modal-title"
            className="text-base font-semibold text-ds-text"
            data-testid="modal-title"
          >
            {title}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="p-xs rounded-md text-ds-text-muted
                       hover:bg-ds-surface-hover hover:text-ds-text
                       transition-colors duration-150 cursor-pointer"
            aria-label="Close modal"
            data-testid="modal-close"
          >
            <svg
              className="w-5 h-5"
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 20 20"
              fill="currentColor"
              aria-hidden="true"
            >
              <path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75
                       0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06
                       10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
            </svg>
          </button>
        </div>

        {/* ── Body ── */}
        <div className="px-lg py-md text-md text-ds-text" data-testid="modal-body">
          {children}
        </div>

        {/* ── Footer (optional) ── */}
        {footer && (
          <div
            className="px-lg pb-lg pt-md border-t border-ds-border
                       flex items-center justify-end gap-sm"
            data-testid="modal-footer"
          >
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}
