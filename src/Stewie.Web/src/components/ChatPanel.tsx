/**
 * ChatPanel — Project-scoped chat interface with real-time SignalR updates.
 * REF: JOB-013 T-136, T-137, JOB-027 T-407
 */
import { useState, useEffect, useRef, useCallback } from "react";
import { fetchChatMessages, sendChatMessage } from "../api/client";
import { useSignalR } from "../hooks/useSignalR";
import { IconUser, IconBot, IconGear, IconChat } from "./Icons";
import { btnPrimary, btnGhost, skeleton } from "../tw";
import type { ChatMessage } from "../types";

const MAX_CONTENT_LENGTH = 10000;

const ROLE_CONFIG: Record<string, { icon: React.ReactNode; label: string }> = {
  Human: { icon: <IconUser size={14} />, label: "Human" },
  Architect: { icon: <IconBot size={14} />, label: "Architect" },
  System: { icon: <IconGear size={14} />, label: "System" },
};

const ROLE_BUBBLE_STYLES: Record<string, string> = {
  human: "ml-auto bg-ds-primary text-white rounded-bl-lg rounded-tl-lg rounded-tr-lg",
  architect: "mr-auto bg-ds-surface-elevated border border-ds-border rounded-br-lg rounded-tl-lg rounded-tr-lg",
  system: "mr-auto bg-ds-bg border border-ds-border rounded-br-lg rounded-tl-lg rounded-tr-lg opacity-70",
};

const ROLE_BADGE_STYLES: Record<string, string> = {
  human: "bg-ds-primary-muted text-ds-primary",
  architect: "bg-[rgba(59,130,246,0.1)] text-ds-running",
  system: "bg-[rgba(139,141,147,0.1)] text-ds-text-muted",
};

interface ChatPanelProps { projectId: string; architectActive?: boolean; }

export function ChatPanel({ projectId, architectActive = false }: ChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  const loadHistory = useCallback(async () => {
    try { setMessages((await fetchChatMessages(projectId)).messages); setError(null); }
    catch (err) { setError(err instanceof Error ? err.message : "Failed to load chat history"); }
    finally { setLoading(false); }
  }, [projectId]);

  useEffect(() => { void loadHistory(); }, [loadHistory]);
  useEffect(() => { messagesEndRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages]);

  const joinedRef = useRef(false);
  useEffect(() => {
    if (isLive && !joinedRef.current) { void joinGroup("project", projectId); joinedRef.current = true; }
    return () => { if (joinedRef.current) { void leaveGroup("project", projectId); joinedRef.current = false; } };
  }, [isLive, projectId, joinGroup, leaveGroup]);

  useEffect(() => {
    if (!isLive) return;
    return on("ChatMessageReceived", (incomingProjectId: unknown, messageId: unknown, senderRole: unknown, senderName: unknown, content: unknown, createdAt: unknown) => {
      if (String(incomingProjectId) !== projectId) return;
      const newMsg: ChatMessage = { id: String(messageId), projectId: String(incomingProjectId), senderRole: String(senderRole) as ChatMessage["senderRole"], senderName: String(senderName), content: String(content), createdAt: String(createdAt) };
      setMessages(prev => prev.some(m => m.id === newMsg.id) ? prev : [...prev, newMsg]);
    });
  }, [isLive, on, projectId]);

  const handleSend = useCallback(async () => {
    const trimmed = input.trim();
    if (!trimmed || sending) return;
    if (trimmed.length > MAX_CONTENT_LENGTH) { setSendError(`Message too long (${trimmed.length}/${MAX_CONTENT_LENGTH} chars)`); return; }
    setSending(true); setSendError(null);
    try { const sent = await sendChatMessage(projectId, trimmed); setMessages(prev => prev.some(m => m.id === sent.id) ? prev : [...prev, sent]); setInput(""); }
    catch (err) { setSendError(err instanceof Error ? err.message : "Failed to send message"); }
    finally { setSending(false); }
  }, [input, sending, projectId]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); void handleSend(); } }, [handleSend]);

  function formatTime(dateStr: string): string {
    const d = new Date(dateStr); const now = new Date();
    const isToday = d.toDateString() === now.toDateString();
    if (isToday) return d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    return `${d.toLocaleDateString(undefined, { month: "short", day: "numeric" })} ${d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })}`;
  }

  if (loading) {
    return (
      <div className="flex flex-col h-full" id="chat-panel">
        <div className="flex-1 p-md">
          {[1, 2, 3].map(i => <div key={i} className={`${skeleton} h-12 mb-sm`} />)}
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full" id="chat-panel">
      {/* Header */}
      <div className="flex items-center gap-sm px-md py-sm border-b border-ds-border bg-ds-surface-elevated">
        <span className="flex items-center gap-sm text-s font-semibold text-ds-text [&_svg]:w-4 [&_svg]:h-4 [&_svg]:text-ds-primary">
          <IconChat size={16} /> Project Chat
          {architectActive && (
            <span className="ml-xs" title="Architect is online">
              <span className="w-2 h-2 rounded-full bg-ds-completed inline-block animate-[pulse_1.5s_ease-in-out_infinite]" />
            </span>
          )}
        </span>
      </div>

      {error && (
        <div className="px-md py-sm bg-[rgba(229,72,77,0.08)] border-b border-[rgba(229,72,77,0.2)] text-ds-failed text-s">
          <p className="m-0">{error}</p>
          <button className={`${btnGhost} text-xs mt-xs`} onClick={() => void loadHistory()}>Retry</button>
        </div>
      )}

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-md flex flex-col gap-sm" ref={messagesContainerRef} id="chat-messages">
        {messages.length === 0 && !error && (
          <div className="flex-1 flex flex-col items-center justify-center text-ds-text-muted text-center">
            <div className="text-[2rem] mb-md opacity-30"><IconChat size={32} /></div>
            <h3 className="text-base font-semibold text-ds-text mb-xs">Start a conversation</h3>
            <p className="text-s">Send a message to begin chatting with the Architect.</p>
          </div>
        )}

        {messages.map((msg) => {
          const roleKey = msg.senderRole.toLowerCase();
          const config = ROLE_CONFIG[msg.senderRole] || ROLE_CONFIG.System;
          return (
            <div key={msg.id} className={`max-w-[80%] p-sm rounded-lg text-s ${ROLE_BUBBLE_STYLES[roleKey] ?? ROLE_BUBBLE_STYLES.system}`} id={`chat-msg-${msg.id}`}>
              <div className="flex items-center gap-xs mb-xs">
                <span className="[&_svg]:w-3.5 [&_svg]:h-3.5">{config.icon}</span>
                <span className="font-semibold text-xs">{msg.senderName}</span>
                <span className={`text-[10px] py-px px-1 rounded-full font-medium ${ROLE_BADGE_STYLES[roleKey] ?? ROLE_BADGE_STYLES.system}`}>{config.label}</span>
              </div>
              <div className="whitespace-pre-wrap break-words leading-relaxed">{msg.content}</div>
              <div className={`text-[10px] mt-xs ${roleKey === "human" ? "text-white/60" : "text-ds-text-muted"}`}>{formatTime(msg.createdAt)}</div>
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className={`border-t border-ds-border px-md py-sm ${!architectActive ? "opacity-60" : ""}`} id="chat-input">
        {!architectActive && (
          <div className="text-xs text-ds-warning text-center py-xs mb-xs italic" id="chat-offline-hint">Start the Architect to begin chatting</div>
        )}
        {sendError && <div className="text-ds-failed text-xs mb-xs">{sendError}</div>}
        <div className="flex gap-sm items-end">
          <textarea
            className="flex-1 resize-none bg-ds-bg border border-ds-border rounded-md px-sm py-xs text-s text-ds-text font-sans focus:outline-none focus:border-ds-primary transition-[border-color] duration-150 placeholder:text-ds-text-muted"
            id="chat-textarea"
            placeholder={architectActive ? "Type a message… (Enter to send, Shift+Enter for newline)" : "Architect is offline…"}
            value={input} onChange={(e) => setInput(e.target.value)} onKeyDown={handleKeyDown}
            disabled={sending || !architectActive} rows={1} maxLength={MAX_CONTENT_LENGTH}
          />
          <button className={btnPrimary} id="chat-send-btn" onClick={() => void handleSend()} disabled={sending || !input.trim() || !architectActive}>
            {sending ? "…" : "Send"}
          </button>
        </div>
        <div className="text-xs text-ds-text-muted mt-xs text-right">
          {input.length > 0 && (
            <span className={input.length > MAX_CONTENT_LENGTH * 0.9 ? "text-ds-warning" : ""}>{input.length}/{MAX_CONTENT_LENGTH}</span>
          )}
        </div>
      </div>
    </div>
  );
}
