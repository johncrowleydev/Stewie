/**
 * ChatSlideover — Right-side chat panel with slideover/pinned sidebar modes.
 * REF: JOB-025 T-300, JOB-027 T-407
 */
import { useState, useEffect, useCallback, useRef } from "react";
import { ChatPanel } from "./ChatPanel";

const LS_MODE_KEY = "stewie:chatMode";
const LS_WIDTH_KEY = "stewie:chatWidth";
type ChatMode = "slideover" | "pinned";
const DEFAULT_WIDTH = 440;
const MIN_WIDTH = 320;
const MAX_WIDTH = 600;

interface ChatSlideoverProps {
  projectId: string;
  architectActive?: boolean;
  isOpen: boolean;
  onClose: () => void;
  onPinnedWidthChange?: (width: number | null) => void;
}

function getPersistedMode(): ChatMode {
  try { const s = localStorage.getItem(LS_MODE_KEY); if (s === "slideover" || s === "pinned") return s; } catch { /* noop */ }
  return "slideover";
}

function getPersistedWidth(): number {
  try { const s = localStorage.getItem(LS_WIDTH_KEY); if (s) { const n = parseInt(s, 10); if (!isNaN(n) && n >= MIN_WIDTH && n <= MAX_WIDTH) return n; } } catch { /* noop */ }
  return DEFAULT_WIDTH;
}

export function ChatSlideover({ projectId, architectActive = false, isOpen, onClose, onPinnedWidthChange }: ChatSlideoverProps) {
  const [mode, setMode] = useState<ChatMode>(getPersistedMode);
  const [width, setWidth] = useState<number>(getPersistedWidth);
  const [isMobile, setIsMobile] = useState(false);
  const [isResizing, setIsResizing] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const resizeStartX = useRef(0);
  const resizeStartWidth = useRef(0);

  useEffect(() => { function check() { setIsMobile(window.innerWidth <= 768); } check(); window.addEventListener("resize", check); return () => window.removeEventListener("resize", check); }, []);
  useEffect(() => { try { localStorage.setItem(LS_MODE_KEY, mode); } catch { /* noop */ } }, [mode]);
  useEffect(() => { try { localStorage.setItem(LS_WIDTH_KEY, String(width)); } catch { /* noop */ } }, [width]);
  useEffect(() => { if (onPinnedWidthChange) onPinnedWidthChange(isOpen && mode === "pinned" && !isMobile ? width : null); }, [isOpen, mode, width, isMobile, onPinnedWidthChange]);

  useEffect(() => {
    if (!isOpen) return;
    function handleEscape(e: KeyboardEvent) { if (e.key === "Escape" && actualMode === "slideover") onClose(); }
    document.addEventListener("keydown", handleEscape);
    return () => document.removeEventListener("keydown", handleEscape);
  });

  const actualMode = isMobile ? "slideover" : mode;

  const handleResizeStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault(); setIsResizing(true); resizeStartX.current = e.clientX; resizeStartWidth.current = width;
    function onMove(ev: MouseEvent) { setWidth(Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, resizeStartWidth.current + (resizeStartX.current - ev.clientX)))); }
    function onUp() { setIsResizing(false); document.removeEventListener("mousemove", onMove); document.removeEventListener("mouseup", onUp); document.body.style.cursor = ""; document.body.style.userSelect = ""; }
    document.addEventListener("mousemove", onMove); document.addEventListener("mouseup", onUp);
    document.body.style.cursor = "col-resize"; document.body.style.userSelect = "none";
  }, [width]);

  const toggleMode = useCallback(() => setMode(p => p === "slideover" ? "pinned" : "slideover"), []);

  if (!isOpen) return null;

  const isPinned = actualMode === "pinned";
  const panelBase = `fixed top-0 right-0 h-full z-[200] flex flex-col bg-ds-surface border-l border-ds-border shadow-ds-lg`;
  const panelSlideover = `${panelBase} w-[440px] max-w-[90vw] animate-[slide-in-right_200ms_ease-out]`;
  const panelPinned = `${panelBase}`;

  return (
    <>
      {actualMode === "slideover" && (
        <div
          className="fixed inset-0 z-[199] bg-black/40 animate-[fade-in_200ms_ease]"
          onClick={onClose} aria-hidden="true" id="chat-slideover-backdrop"
        />
      )}
      <div
        ref={panelRef}
        className={`${isPinned ? panelPinned : panelSlideover} ${isResizing ? "select-none" : ""}`}
        style={isPinned ? { width } : undefined}
        id="chat-slideover"
      >
        {isPinned && (
          <div
            className="absolute left-0 top-0 bottom-0 w-1 cursor-col-resize hover:bg-ds-primary transition-colors duration-150 z-10"
            onMouseDown={handleResizeStart} id="chat-slideover-resize"
          />
        )}
        <div className="flex items-center justify-between px-md py-sm border-b border-ds-border bg-ds-surface-elevated shrink-0">
          <span className="flex items-center gap-sm text-s font-semibold text-ds-text [&_svg]:w-4 [&_svg]:h-4 [&_svg]:text-ds-primary">
            <ChatIcon /> Project Chat
            {architectActive && <span className="w-2 h-2 rounded-full bg-ds-completed animate-[pulse_1.5s_ease-in-out_infinite]" title="Architect is online" />}
          </span>
          <div className="flex items-center gap-xs">
            {!isMobile && (
              <button
                className={`w-7 h-7 flex items-center justify-center rounded-sm border-none cursor-pointer transition-all duration-150 ${isPinned ? "bg-ds-primary-muted text-ds-primary" : "bg-transparent text-ds-text-muted hover:text-ds-text hover:bg-ds-surface-hover"}`}
                onClick={toggleMode} title={isPinned ? "Unpin to slideover" : "Pin to sidebar"} id="chat-pin-toggle"
              ><PinIcon pinned={isPinned} /></button>
            )}
            <button className="w-7 h-7 flex items-center justify-center rounded-sm bg-transparent border-none text-ds-text-muted cursor-pointer transition-all duration-150 hover:text-ds-text hover:bg-ds-surface-hover" onClick={onClose} title="Close chat" id="chat-close-btn"><CloseIcon /></button>
          </div>
        </div>
        <div className="flex-1 overflow-hidden">
          <ChatPanel projectId={projectId} architectActive={architectActive} />
        </div>
      </div>
    </>
  );
}

function ChatIcon() {
  return <svg width={16} height={16} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M2 2a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h2v2.5a.5.5 0 0 0 .82.384L8.28 13H14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H2zm1 3.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5zm0 3a.5.5 0 0 1 .5-.5h5a.5.5 0 0 1 0 1h-5a.5.5 0 0 1-.5-.5z" /></svg>;
}

function PinIcon({ pinned }: { pinned: boolean }) {
  if (pinned) return <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M4.146.146A.5.5 0 0 1 4.5 0h7a.5.5 0 0 1 .5.5c0 .68-.342 1.174-.646 1.479-.126.125-.25.224-.354.298v4.431l.078.048c.203.127.476.314.751.555C12.366 7.794 13 8.545 13 9.5a.5.5 0 0 1-.5.5h-4v4.5a.5.5 0 0 1-1 0V10h-4A.5.5 0 0 1 3 9.5c0-.955.634-1.706 1.17-2.189.276-.241.548-.428.752-.555l.078-.048V2.277a2.4 2.4 0 0 1-.354-.298C4.342 1.674 4 1.18 4 .5a.5.5 0 0 1 .146-.354z" /></svg>;
  return <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M4.146.146A.5.5 0 0 1 4.5 0h7a.5.5 0 0 1 .5.5c0 .68-.342 1.174-.646 1.479-.126.125-.25.224-.354.298v4.431l.078.048c.203.127.476.314.751.555C12.366 7.794 13 8.545 13 9.5a.5.5 0 0 1-.5.5h-4v4.5a.5.5 0 0 1-1 0V10h-4A.5.5 0 0 1 3 9.5c0-.955.634-1.706 1.17-2.189.276-.241.548-.428.752-.555l.078-.048V2.277a2.4 2.4 0 0 1-.354-.298C4.342 1.674 4 1.18 4 .5a.5.5 0 0 1 .146-.354zM5.098 1c.049.136.138.302.293.458.18.18.43.342.705.458.13.056.27.1.404.13V7a.5.5 0 0 1-.25.433l-.148.092a7 7 0 0 0-.641.472A3.2 3.2 0 0 0 4.581 9H11.42a3.2 3.2 0 0 0-.879-1.445 7 7 0 0 0-.641-.472l-.149-.092A.5.5 0 0 1 9.5 7V2.046a2.7 2.7 0 0 0 .404-.13c.274-.116.525-.278.705-.458.155-.156.244-.322.293-.458z" /></svg>;
}

function CloseIcon() {
  return <svg width={14} height={14} viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8z" /></svg>;
}
