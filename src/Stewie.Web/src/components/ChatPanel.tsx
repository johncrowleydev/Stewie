/**
 * ChatPanel — Project-scoped chat interface with real-time SignalR updates.
 *
 * Features:
 * - Loads chat history on mount via fetchChatMessages
 * - Appends messages pushed by SignalR ChatMessageReceived events
 * - Human messages aligned right (green), Architect/System left (gray/muted)
 * - Auto-scrolls to latest message
 * - Textarea input with send button, validation (non-empty, ≤10000 chars)
 * - Send button disabled while submitting
 *
 * REF: JOB-013 T-136, T-137
 */
import { useState, useEffect, useRef, useCallback } from "react";
import { fetchChatMessages, sendChatMessage } from "../api/client";
import { useSignalR } from "../hooks/useSignalR";
import type { ChatMessage } from "../types";

/** Max message length (matches backend validation) */
const MAX_CONTENT_LENGTH = 10000;

/** Role display config */
const ROLE_CONFIG: Record<string, { icon: string; label: string }> = {
  Human: { icon: "👤", label: "Human" },
  Architect: { icon: "🤖", label: "Architect" },
  System: { icon: "⚙️", label: "System" },
};

interface ChatPanelProps {
  /** The project this chat belongs to */
  projectId: string;
}

/**
 * ChatPanel — renders a scrollable chat with real-time updates.
 */
export function ChatPanel({ projectId }: ChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);

  // SignalR connection
  const { state: signalRState, joinGroup, leaveGroup, on } = useSignalR();
  const isLive = signalRState === "connected";

  // Load chat history
  const loadHistory = useCallback(async () => {
    try {
      const response = await fetchChatMessages(projectId);
      setMessages(response.messages);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load chat history");
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  // Initial load
  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  // Auto-scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Join/leave project SignalR group for chat messages
  const joinedRef = useRef(false);
  useEffect(() => {
    if (isLive && !joinedRef.current) {
      void joinGroup("project", projectId);
      joinedRef.current = true;
    }

    return () => {
      if (joinedRef.current) {
        void leaveGroup("project", projectId);
        joinedRef.current = false;
      }
    };
  }, [isLive, projectId, joinGroup, leaveGroup]);

  // Listen for ChatMessageReceived SignalR events
  useEffect(() => {
    if (!isLive) return;

    const cleanup = on(
      "ChatMessageReceived",
      (
        incomingProjectId: unknown,
        messageId: unknown,
        senderRole: unknown,
        senderName: unknown,
        content: unknown,
        createdAt: unknown
      ) => {
        // Only append if this message belongs to our project
        if (String(incomingProjectId) !== projectId) return;

        const newMessage: ChatMessage = {
          id: String(messageId),
          projectId: String(incomingProjectId),
          senderRole: String(senderRole) as ChatMessage["senderRole"],
          senderName: String(senderName),
          content: String(content),
          createdAt: String(createdAt),
        };

        // Deduplicate — avoid showing the same message twice
        // (can happen if we sent it ourselves and also receive it via SignalR)
        setMessages((prev) => {
          if (prev.some((m) => m.id === newMessage.id)) return prev;
          return [...prev, newMessage];
        });
      }
    );

    return cleanup;
  }, [isLive, on, projectId]);

  // Send message handler
  const handleSend = useCallback(async () => {
    const trimmed = input.trim();
    if (!trimmed || sending) return;

    if (trimmed.length > MAX_CONTENT_LENGTH) {
      setSendError(`Message too long (${trimmed.length}/${MAX_CONTENT_LENGTH} chars)`);
      return;
    }

    setSending(true);
    setSendError(null);

    try {
      const sent = await sendChatMessage(projectId, trimmed);
      // Optimistically append if not already in list (SignalR might beat us)
      setMessages((prev) => {
        if (prev.some((m) => m.id === sent.id)) return prev;
        return [...prev, sent];
      });
      setInput("");
    } catch (err) {
      setSendError(err instanceof Error ? err.message : "Failed to send message");
    } finally {
      setSending(false);
    }
  }, [input, sending, projectId]);

  // Handle Enter key (Shift+Enter for newline)
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        void handleSend();
      }
    },
    [handleSend]
  );

  /** Format a timestamp to a short readable time */
  function formatTime(dateStr: string): string {
    const d = new Date(dateStr);
    const now = new Date();
    const isToday = d.toDateString() === now.toDateString();
    if (isToday) {
      return d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString(undefined, { month: "short", day: "numeric" }) +
      " " +
      d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  }

  if (loading) {
    return (
      <div className="chat-panel" id="chat-panel">
        <div className="chat-messages">
          <div className="skeleton skeleton-row" />
          <div className="skeleton skeleton-row" />
          <div className="skeleton skeleton-row" />
        </div>
      </div>
    );
  }

  return (
    <div className="chat-panel" id="chat-panel">
      {/* Header */}
      <div className="chat-header">
        <span className="chat-header-title">💬 Project Chat</span>
        {isLive ? (
          <span className="live-indicator live-indicator--ws">
            <span className="live-dot" />
            Live
          </span>
        ) : (
          <span className="chat-header-hint">Messages may be delayed</span>
        )}
      </div>

      {/* Error */}
      {error && (
        <div className="chat-error">
          <p>{error}</p>
          <button className="btn btn-ghost" onClick={() => void loadHistory()}>Retry</button>
        </div>
      )}

      {/* Messages area */}
      <div className="chat-messages" ref={messagesContainerRef} id="chat-messages">
        {messages.length === 0 && !error && (
          <div className="chat-empty">
            <div className="empty-icon">💬</div>
            <h3>Start a conversation</h3>
            <p>Send a message to begin chatting with the Architect.</p>
          </div>
        )}

        {messages.map((msg) => {
          const roleKey = msg.senderRole;
          const config = ROLE_CONFIG[roleKey] || ROLE_CONFIG.System;

          return (
            <div
              key={msg.id}
              className={`chat-bubble chat-bubble--${roleKey.toLowerCase()}`}
              id={`chat-msg-${msg.id}`}
            >
              <div className="chat-sender">
                <span className="chat-sender-icon">{config.icon}</span>
                <span className="chat-sender-name">{msg.senderName}</span>
                <span className={`chat-role-badge chat-role-badge--${roleKey.toLowerCase()}`}>
                  {config.label}
                </span>
              </div>
              <div className="chat-content">{msg.content}</div>
              <div className="chat-time">{formatTime(msg.createdAt)}</div>
            </div>
          );
        })}
        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <div className="chat-input" id="chat-input">
        {sendError && <div className="chat-send-error">{sendError}</div>}
        <div className="chat-input-row">
          <textarea
            className="chat-textarea"
            id="chat-textarea"
            placeholder="Type a message… (Enter to send, Shift+Enter for newline)"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={sending}
            rows={1}
            maxLength={MAX_CONTENT_LENGTH}
          />
          <button
            className="btn btn-primary chat-send-btn"
            id="chat-send-btn"
            onClick={() => void handleSend()}
            disabled={sending || !input.trim()}
          >
            {sending ? "…" : "Send"}
          </button>
        </div>
        <div className="chat-input-hint">
          {input.length > 0 && (
            <span className={input.length > MAX_CONTENT_LENGTH * 0.9 ? "chat-char-warn" : ""}>
              {input.length}/{MAX_CONTENT_LENGTH}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
