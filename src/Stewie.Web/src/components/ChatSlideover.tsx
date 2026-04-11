/**
 * ChatSlideover — Right-side chat panel with slideover/pinned sidebar modes.
 *
 * Wraps the existing ChatPanel component. Two display modes:
 * - **Slideover**: overlays content from right edge, 200ms ease-out transition,
 *   backdrop click-to-close, Escape key handler.
 * - **Pinned**: fixed sidebar that shrinks the content area, resizable via
 *   drag handle on left edge (min 320px, max 600px, default 440px).
 *
 * Mode and width persist to localStorage:
 * - `stewie:chatMode` — "slideover" | "pinned"
 * - `stewie:chatWidth` — number (px)
 *
 * Mobile (≤768px): always fullscreen slideover, no pin option.
 *
 * REF: JOB-025 T-300
 */
import { useState, useEffect, useCallback, useRef } from "react";
import { ChatPanel } from "./ChatPanel";

/** localStorage keys for persistence */
const LS_MODE_KEY = "stewie:chatMode";
const LS_WIDTH_KEY = "stewie:chatWidth";

/** Chat display mode */
type ChatMode = "slideover" | "pinned";

/** Default pinned width in pixels */
const DEFAULT_WIDTH = 440;
const MIN_WIDTH = 320;
const MAX_WIDTH = 600;

interface ChatSlideoverProps {
  /** The project this chat belongs to */
  projectId: string;
  /** Whether the Architect Agent is currently online */
  architectActive?: boolean;
  /** Whether the chat is currently open */
  isOpen: boolean;
  /** Callback when the chat should close (slideover mode only) */
  onClose: () => void;
  /** Callback to report the current pinned width for layout adjustments */
  onPinnedWidthChange?: (width: number | null) => void;
}

/**
 * Read persisted mode from localStorage, defaulting to "slideover".
 */
function getPersistedMode(): ChatMode {
  try {
    const stored = localStorage.getItem(LS_MODE_KEY);
    if (stored === "slideover" || stored === "pinned") return stored;
  } catch {
    // localStorage unavailable — use default
  }
  return "slideover";
}

/**
 * Read persisted width from localStorage, defaulting to DEFAULT_WIDTH.
 */
function getPersistedWidth(): number {
  try {
    const stored = localStorage.getItem(LS_WIDTH_KEY);
    if (stored) {
      const parsed = parseInt(stored, 10);
      if (!isNaN(parsed) && parsed >= MIN_WIDTH && parsed <= MAX_WIDTH) return parsed;
    }
  } catch {
    // localStorage unavailable — use default
  }
  return DEFAULT_WIDTH;
}

/**
 * ChatSlideover — right-side panel wrapping ChatPanel.
 */
export function ChatSlideover({
  projectId,
  architectActive = false,
  isOpen,
  onClose,
  onPinnedWidthChange,
}: ChatSlideoverProps) {
  const [mode, setMode] = useState<ChatMode>(getPersistedMode);
  const [width, setWidth] = useState<number>(getPersistedWidth);
  const [isMobile, setIsMobile] = useState(false);
  const [isResizing, setIsResizing] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const resizeStartX = useRef(0);
  const resizeStartWidth = useRef(0);

  // Detect mobile breakpoint
  useEffect(() => {
    function checkMobile() {
      setIsMobile(window.innerWidth <= 768);
    }
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  // Persist mode changes
  useEffect(() => {
    try {
      localStorage.setItem(LS_MODE_KEY, mode);
    } catch {
      // localStorage unavailable
    }
  }, [mode]);

  // Persist width changes
  useEffect(() => {
    try {
      localStorage.setItem(LS_WIDTH_KEY, String(width));
    } catch {
      // localStorage unavailable
    }
  }, [width]);

  // Report pinned width to parent for layout
  useEffect(() => {
    if (onPinnedWidthChange) {
      if (isOpen && mode === "pinned" && !isMobile) {
        onPinnedWidthChange(width);
      } else {
        onPinnedWidthChange(null);
      }
    }
  }, [isOpen, mode, width, isMobile, onPinnedWidthChange]);

  // Escape key handler
  useEffect(() => {
    if (!isOpen) return;

    function handleEscape(e: KeyboardEvent) {
      if (e.key === "Escape") {
        // In slideover mode, close. In pinned mode, do nothing.
        if (actualMode === "slideover") {
          onClose();
        }
      }
    }

    document.addEventListener("keydown", handleEscape);
    return () => document.removeEventListener("keydown", handleEscape);
  });

  // Resize handlers for pinned mode
  const handleResizeStart = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      setIsResizing(true);
      resizeStartX.current = e.clientX;
      resizeStartWidth.current = width;

      function handleMouseMove(ev: MouseEvent) {
        // Dragging left edge left = wider, dragging right = narrower
        const delta = resizeStartX.current - ev.clientX;
        const newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, resizeStartWidth.current + delta));
        setWidth(newWidth);
      }

      function handleMouseUp() {
        setIsResizing(false);
        document.removeEventListener("mousemove", handleMouseMove);
        document.removeEventListener("mouseup", handleMouseUp);
        document.body.style.cursor = "";
        document.body.style.userSelect = "";
      }

      document.addEventListener("mousemove", handleMouseMove);
      document.addEventListener("mouseup", handleMouseUp);
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";
    },
    [width]
  );

  // Toggle between modes
  const toggleMode = useCallback(() => {
    setMode((prev) => (prev === "slideover" ? "pinned" : "slideover"));
  }, []);

  // Actual mode: mobile always forces slideover
  const actualMode = isMobile ? "slideover" : mode;

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop for slideover mode */}
      {actualMode === "slideover" && (
        <div
          className="chat-slideover-backdrop"
          onClick={onClose}
          aria-hidden="true"
          id="chat-slideover-backdrop"
        />
      )}

      {/* Panel */}
      <div
        ref={panelRef}
        className={`chat-slideover chat-slideover--${actualMode} ${isResizing ? "chat-slideover--resizing" : ""}`}
        style={{ width: actualMode === "pinned" ? width : undefined }}
        id="chat-slideover"
      >
        {/* Resize handle (pinned mode only) */}
        {actualMode === "pinned" && (
          <div
            className="chat-slideover-resize"
            onMouseDown={handleResizeStart}
            id="chat-slideover-resize"
          />
        )}

        {/* Header bar with pin/close controls */}
        <div className="chat-slideover-header">
          <span className="chat-slideover-title">
            <ChatIcon />
            Project Chat
            {architectActive && (
              <span className="chat-architect-indicator" title="Architect is online">
                <span className="chat-architect-dot" />
              </span>
            )}
          </span>
          <div className="chat-slideover-actions">
            {/* Pin / Unpin button — hidden on mobile */}
            {!isMobile && (
              <button
                className={`chat-slideover-btn ${actualMode === "pinned" ? "active" : ""}`}
                onClick={toggleMode}
                title={actualMode === "pinned" ? "Unpin to slideover" : "Pin to sidebar"}
                id="chat-pin-toggle"
              >
                <PinIcon pinned={actualMode === "pinned"} />
              </button>
            )}
            {/* Close button */}
            <button
              className="chat-slideover-btn"
              onClick={onClose}
              title="Close chat"
              id="chat-close-btn"
            >
              <CloseIcon />
            </button>
          </div>
        </div>

        {/* Wrapped ChatPanel */}
        <div className="chat-slideover-body">
          <ChatPanel projectId={projectId} architectActive={architectActive} />
        </div>
      </div>
    </>
  );
}

// ------------------------------------------------------------------
// Inline SVG icons
// ------------------------------------------------------------------

/** Chat bubble icon for the header */
function ChatIcon() {
  return (
    <svg width={16} height={16} viewBox="0 0 16 16" fill="currentColor" className="chat-header-icon" aria-hidden="true">
      <path d="M2 2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2v2.5a.5.5 0 0 0 .82.384L8.28 13H14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H2zm1 3.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5z" />
    </svg>
  );
}

/** Pin/unpin toggle icon */
function PinIcon({ pinned }: { pinned: boolean }) {
  if (pinned) {
    // Filled pin — currently pinned
    return (
      <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
        <path d="M4.146.146A.5.5 0 0 1 4.5 0h7a.5.5 0 0 1 .5.5c0 .68-.342 1.174-.646 1.479-.126.125-.25.224-.354.298v4.431l.078.048c.203.127.476.314.751.555C12.366 7.794 13 8.545 13 9.5a.5.5 0 0 1-.5.5h-4v4.5a.5.5 0 0 1-1 0V10h-4A.5.5 0 0 1 3 9.5c0-.955.634-1.706 1.17-2.189.276-.241.548-.428.752-.555l.078-.048V2.277a2.4 2.4 0 0 1-.354-.298C4.342 1.674 4 1.18 4 .5a.5.5 0 0 1 .146-.354z" />
      </svg>
    );
  }
  // Outline pin — currently slideover
  return (
    <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M4.146.146A.5.5 0 0 1 4.5 0h7a.5.5 0 0 1 .5.5c0 .68-.342 1.174-.646 1.479-.126.125-.25.224-.354.298v4.431l.078.048c.203.127.476.314.751.555C12.366 7.794 13 8.545 13 9.5a.5.5 0 0 1-.5.5h-4v4.5a.5.5 0 0 1-1 0V10h-4A.5.5 0 0 1 3 9.5c0-.955.634-1.706 1.17-2.189.276-.241.548-.428.752-.555l.078-.048V2.277a2.4 2.4 0 0 1-.354-.298C4.342 1.674 4 1.18 4 .5a.5.5 0 0 1 .146-.354zM5.098 1c.049.136.138.302.293.458.18.18.43.342.705.458.13.056.27.1.404.13V7a.5.5 0 0 1-.25.433l-.148.092a7 7 0 0 0-.641.472A3.2 3.2 0 0 0 4.581 9H11.42a3.2 3.2 0 0 0-.879-1.445 7 7 0 0 0-.641-.472l-.149-.092A.5.5 0 0 1 9.5 7V2.046a2.7 2.7 0 0 0 .404-.13c.274-.116.525-.278.705-.458.155-.156.244-.322.293-.458z" />
    </svg>
  );
}

/** X close icon */
function CloseIcon() {
  return (
    <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
      <path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8z" />
    </svg>
  );
}
