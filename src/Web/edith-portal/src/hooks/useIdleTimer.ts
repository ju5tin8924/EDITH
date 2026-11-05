import { useState, useEffect, useRef, useCallback } from 'react';

interface UseIdleTimerOptions {
  timeoutMs?: number;       // Total idle timeout before auto-logout (default: 15 minutes)
  warningMs?: number;       // Warning dialog shown before timeout (default: 2 minutes)
  onTimeout: () => void;
  onRefresh?: () => Promise<boolean>;
  enabled?: boolean;
}

export function useIdleTimer({
  timeoutMs = 15 * 60 * 1000,
  warningMs = 2 * 60 * 1000,
  onTimeout,
  onRefresh,
  enabled = true
}: UseIdleTimerOptions) {
  const [isWarning, setIsWarning] = useState(false);
  const [remainingSeconds, setRemainingSeconds] = useState(Math.floor(warningMs / 1000));
  
  const lastActivityRef = useRef<number>(Date.now());
  const timerCheckRef = useRef<NodeJS.Timeout | null>(null);

  const resetTimer = useCallback(() => {
    lastActivityRef.current = Date.now();
    setIsWarning(false);
    setRemainingSeconds(Math.floor(warningMs / 1000));
  }, [warningMs]);

  const extendSession = useCallback(async () => {
    if (onRefresh) {
      await onRefresh();
    }
    resetTimer();
  }, [onRefresh, resetTimer]);

  useEffect(() => {
    if (!enabled) {
      setIsWarning(false);
      return;
    }

    const activityEvents = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'click'];

    const handleUserActivity = () => {
      // Only reset if we are NOT in the warning state (modal requires explicit interaction)
      if (!isWarning) {
        lastActivityRef.current = Date.now();
      }
    };

    activityEvents.forEach(evt => window.addEventListener(evt, handleUserActivity, { passive: true }));

    // Periodic check every 1 second
    timerCheckRef.current = setInterval(() => {
      const now = Date.now();
      const idleTime = now - lastActivityRef.current;
      const warningThreshold = timeoutMs - warningMs;

      if (idleTime >= timeoutMs) {
        // Timed out completely!
        if (timerCheckRef.current) clearInterval(timerCheckRef.current);
        setIsWarning(false);
        onTimeout();
      } else if (idleTime >= warningThreshold) {
        // In warning window
        setIsWarning(true);
        const timeLeftSec = Math.max(0, Math.ceil((timeoutMs - idleTime) / 1000));
        setRemainingSeconds(timeLeftSec);
      } else {
        setIsWarning(false);
      }
    }, 1000);

    return () => {
      activityEvents.forEach(evt => window.removeEventListener(evt, handleUserActivity));
      if (timerCheckRef.current) {
        clearInterval(timerCheckRef.current);
      }
    };
  }, [enabled, timeoutMs, warningMs, isWarning, onTimeout]);

  return {
    isWarning,
    remainingSeconds,
    extendSession,
    resetTimer
  };
}
