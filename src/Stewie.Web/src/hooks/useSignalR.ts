/**
 * useSignalR — Manages a SignalR HubConnection lifecycle with auto-reconnect.
 *
 * Connects to /hubs/stewie when the user is authenticated. Provides:
 * - connection state tracking (disconnected, connecting, connected, reconnecting)
 * - group join/leave helpers for dashboard, project, and job groups
 * - on() listener registration that auto-cleans on unmount
 * - exponential backoff reconnection: [0, 2000, 5000, 10000, 30000]ms
 *
 * Auth token is read from getToken() in api/client.ts.
 * Connection trigger is isAuthenticated from useAuth().
 *
 * REF: JOB-012 T-125
 */
import { useEffect, useState, useRef, useCallback, useMemo } from "react";
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";
import { getToken } from "../api/client";
import { useAuth } from "../contexts/AuthContext";

/** Connection state exposed to consumers */
export type SignalRState = "disconnected" | "connecting" | "connected" | "reconnecting";

/** Group types supported by the StewieHub */
export type GroupType = "dashboard" | "project" | "job";

/** Options for the useSignalR hook */
export interface UseSignalROptions {
  /** Whether to auto-connect. Default: true when authenticated */
  enabled?: boolean;
}

/** Return value from useSignalR */
export interface UseSignalRResult {
  /** Current connection state */
  state: SignalRState;
  /** The underlying HubConnection (null before first connect attempt) */
  connection: HubConnection | null;
  /** Join a SignalR group */
  joinGroup: (type: GroupType, id?: string) => Promise<void>;
  /** Leave a SignalR group */
  leaveGroup: (type: GroupType, id?: string) => Promise<void>;
  /** Register a listener. Returns cleanup function. */
  on: (event: string, handler: (...args: unknown[]) => void) => () => void;
}

/**
 * Builds the hub URL. In dev, the Vite proxy routes /hubs to the API server
 * with WebSocket upgrade support. In production, the hub is on the same origin.
 */
function getHubUrl(): string {
  return "/hubs/stewie";
}


/**
 * Maps GroupType + optional ID to the server-side method name and argument.
 */
function groupMethodArgs(type: GroupType, id?: string): { join: string; leave: string; arg?: string } {
  switch (type) {
    case "dashboard":
      return { join: "JoinDashboard", leave: "LeaveDashboard" };
    case "project":
      return { join: "JoinProject", leave: "LeaveProject", arg: id };
    case "job":
      return { join: "JoinJob", leave: "LeaveJob", arg: id };
  }
}

/**
 * useSignalR — manages the HubConnection lifecycle.
 */
export function useSignalR(options?: UseSignalROptions): UseSignalRResult {
  const { isAuthenticated } = useAuth();
  const enabled = options?.enabled ?? true;
  const shouldConnect = isAuthenticated && enabled;

  const [state, setState] = useState<SignalRState>("disconnected");
  const connectionRef = useRef<HubConnection | null>(null);
  const mountedRef = useRef(true);

  // Build connection (memoized — only recreated when shouldConnect toggles)
  useEffect(() => {
    mountedRef.current = true;

    if (!shouldConnect) {
      // Tear down existing connection
      const existing = connectionRef.current;
      if (existing) {
        void existing.stop();
        connectionRef.current = null;
      }
      setState("disconnected");
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => getToken() ?? "",
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // State change listeners
    connection.onreconnecting(() => {
      if (mountedRef.current) setState("reconnecting");
    });

    connection.onreconnected(() => {
      if (mountedRef.current) setState("connected");
    });

    connection.onclose(() => {
      if (mountedRef.current) setState("disconnected");
    });

    // Start connection
    setState("connecting");
    connection
      .start()
      .then(() => {
        if (mountedRef.current) setState("connected");
      })
      .catch((err) => {
        console.warn("[useSignalR] Connection failed, will rely on polling fallback:", err);
        if (mountedRef.current) setState("disconnected");
      });

    return () => {
      mountedRef.current = false;
      void connection.stop();
      connectionRef.current = null;
    };
  }, [shouldConnect]);

  // Join a group
  const joinGroup = useCallback(async (type: GroupType, id?: string) => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;
    const { join, arg } = groupMethodArgs(type, id);
    try {
      if (arg) {
        await conn.invoke(join, arg);
      } else {
        await conn.invoke(join);
      }
    } catch (err) {
      console.warn(`[useSignalR] Failed to join ${type} group:`, err);
    }
  }, []);

  // Leave a group
  const leaveGroup = useCallback(async (type: GroupType, id?: string) => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;
    const { leave, arg } = groupMethodArgs(type, id);
    try {
      if (arg) {
        await conn.invoke(leave, arg);
      } else {
        await conn.invoke(leave);
      }
    } catch (err) {
      console.warn(`[useSignalR] Failed to leave ${type} group:`, err);
    }
  }, []);

  // Register event listener — returns cleanup function
  const on = useCallback((event: string, handler: (...args: unknown[]) => void): (() => void) => {
    const conn = connectionRef.current;
    if (!conn) return () => {};
    conn.on(event, handler);
    return () => conn.off(event, handler);
  }, []);

  const result = useMemo<UseSignalRResult>(
    () => ({
      state,
      connection: connectionRef.current,
      joinGroup,
      leaveGroup,
      on,
    }),
    [state, joinGroup, leaveGroup, on]
  );

  return result;
}
