/**
 * usePolling — Reusable hook for periodic data fetching with auto-cleanup.
 *
 * Behavior:
 * 1. Calls fetchFn on mount and then every intervalMs
 * 2. Skips if enabled=false
 * 3. Cleans up interval on unmount or when enabled changes to false
 * 4. Returns the latest data, loading state, polling active flag, and error
 *
 * @param fetchFn - Async function that returns data of type T
 * @param intervalMs - Polling interval in milliseconds
 * @param enabled - Whether polling is active (false stops the interval)
 */
import { useEffect, useState, useRef, useCallback } from "react";

/** Result object returned by the usePolling hook */
export interface UsePollingResult<T> {
  /** Latest fetched data */
  data: T | null;
  /** True during the initial load only */
  loading: boolean;
  /** True when the polling interval is actively running */
  polling: boolean;
  /** Error message from the most recent failed fetch, or null */
  error: string | null;
  /** Manually trigger a refresh */
  refresh: () => void;
}

/**
 * Polls an async fetch function at a configurable interval.
 * Stops when enabled=false. Cleans up on unmount.
 */
export function usePolling<T>(
  fetchFn: () => Promise<T>,
  intervalMs: number,
  enabled: boolean = true
): UsePollingResult<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [polling, setPolling] = useState(false);
  const mountedRef = useRef(true);

  const doFetch = useCallback(async () => {
    try {
      const result = await fetchFn();
      if (mountedRef.current) {
        setData(result);
        setError(null);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : "Fetch failed");
      }
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, [fetchFn]);

  // Initial fetch
  useEffect(() => {
    mountedRef.current = true;
    void doFetch();
    return () => { mountedRef.current = false; };
  }, [doFetch]);

  // Polling interval
  useEffect(() => {
    if (!enabled) {
      setPolling(false);
      return;
    }

    setPolling(true);
    const intervalId = setInterval(() => {
      void doFetch();
    }, intervalMs);

    return () => {
      clearInterval(intervalId);
      setPolling(false);
    };
  }, [enabled, intervalMs, doFetch]);

  const refresh = useCallback(() => {
    void doFetch();
  }, [doFetch]);

  return { data, loading, polling, error, refresh };
}
