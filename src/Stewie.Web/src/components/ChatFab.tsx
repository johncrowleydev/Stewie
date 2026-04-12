/**
 * ChatFab — Floating action button for opening the project chat.
 *
 * Renders a circular button fixed to the bottom-right corner. Only visible
 * on project-scoped pages (when ProjectContext is available). Clicking
 * toggles the ChatSlideover open/closed.
 *
 * DECISION: FAB manages its own ChatSlideover instance rather than lifting
 * state to Layout, because the chat is only relevant on project pages and
 * keeping the state localized avoids unnecessary re-renders of the full layout.
 *
 * DECISION: Uses ds-primary background with white icon to match the design
 * system's call-to-action pattern. shadow-ds-lg provides depth separation
 * from underlying content.
 *
 * FAILURE MODE: If ProjectContext is null (global/admin pages), the FAB
 * renders nothing. No error boundary needed.
 *
 * Used by: Layout.tsx (inside main content area)
 * REF: JOB-031 T-533
 *
 * @example
 * ```tsx
 * <ChatFab />
 * ```
 */
import { useState, useContext } from "react";
import { ProjectContext } from "../contexts/ProjectContext";
import { ChatSlideover } from "./ChatSlideover";

/**
 * Floating chat button — bottom-right corner, project pages only.
 *
 * PRECONDITION: Must be inside a Router (ChatSlideover uses it).
 * POSTCONDITION: Clicking opens/closes the ChatSlideover panel.
 */
export function ChatFab() {
  const projectCtx = useContext(ProjectContext);
  const [isChatOpen, setIsChatOpen] = useState(false);

  // Guard: only render on project-scoped pages
  if (!projectCtx) return null;

  return (
    <>
      {/* Floating action button */}
      <button
        className="fixed bottom-6 right-6 z-[150]
                   w-14 h-14 rounded-full
                   bg-ds-primary text-white
                   shadow-ds-lg border-none cursor-pointer
                   flex items-center justify-center
                   transition-all duration-200
                   hover:scale-110 hover:shadow-[0_8px_30px_rgba(111,172,80,0.4)]
                   active:scale-95
                   focus:outline-none focus:ring-2 focus:ring-ds-primary focus:ring-offset-2 focus:ring-offset-ds-bg"
        onClick={() => setIsChatOpen(!isChatOpen)}
        aria-label="Open chat"
        id="chat-fab"
        data-testid="chat-fab"
      >
        {isChatOpen ? (
          /* X icon when chat is open */
          <svg
            className="w-6 h-6"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        ) : (
          /* Chat bubble icon when closed */
          <svg
            className="w-6 h-6"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
        )}
      </button>

      {/* Chat slideover panel */}
      <ChatSlideover
        projectId={projectCtx.projectId}
        isOpen={isChatOpen}
        onClose={() => setIsChatOpen(false)}
      />
    </>
  );
}
