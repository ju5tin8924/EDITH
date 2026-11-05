import { useState, useEffect, useRef, useCallback } from 'react';

const STORAGE_LAST_ACTIVITY = 'edith_last_activity';
const STORAGE_TIMEOUT_SEC = 'edith_idle_timeout_seconds';

export const DEFAULT_TIMEOUT_SECONDS = 120; // 2 minutes default for fast, verifiable review
export const DEFAULT_WARNING_SECONDS = 30;  // 30 seconds warning modal

export function getStoredTimeoutSeconds(): number {
  try {
    const raw = localStorage.getItem(STORAGE_TIMEOUT_SEC);
    if (raw) {
      const val = parseInt(raw, 10);
      if (!isNaN(val) && val >= 30) return val;
    }
  } catch {}
  return DEFAULT_TIMEOUT_SECONDS;
}

export function setStoredTimeoutSeconds(sec: number): void {
  try {
    localStorage.setItem(STORAGE_TIMEOUT_SEC, String(sec));
  } catch {}
}

export function recordUserActivity(): void {
  try {
    localStorage.setItem(STORAGE_LAST_ACTIVITY, String(Date.now()));
  } catch {}
}

export function getStoredLastActivity(): number {
  try {
    const raw = localStorage.getItem(STORAGE_LAST_ACTIVITY);
    if (raw) {
      const val = parseInt(raw, 10);
      if (!isNaN(val)) return val;
    }
  } catch {}
  return Date.now();
}

interface UseIdleTimerOptions {
  onTimeout: () => void;
  onRefresh?: () => Promise<boolean>;
  enabled?: boolean;
}

export function useIdleTimer({
  onTimeout,
  onRefresh,
  enabled = true
}: UseIdleTimerOptions) {
  const [isWarning, setIsWarning] = useState(false);
  const [remainingSeconds, setRemainingSeconds] = useState(DEFAULT_WARNING_SECONDS);
  const [timeoutSeconds, setTimeoutSecondsState] = useState(getStoredTimeoutSeconds());

  // Stable callback refs so interval does not restart on re-render
  const onTimeoutRef = useRef(onTimeout);
  onTimeoutRef.current = onTimeout;

  const onRefreshRef = useRef(onRefresh);
  onRefreshRef.current = onRefresh;

  const isWarningRef = useRef(isWarning);
  isWarningRef.current = isWarning;

  const lastActivityTimeRef = useRef(getStoredLastActivity());
  const lastEventThrottleRef = useRef<number>(0);

  const resetTimer = useCallback(() => {
    const now = Date.now();
    lastActivityTimeRef.current = now;
    recordUserActivity();
    setIsWarning(false);
  }, []);

  const extendSession = useCallback(async () => {
    if (onRefreshRef.current) {
      try {
        await onRefreshRef.current();
      } catch {}
    }
    resetTimer();
  }, [resetTimer]);

  const updateTimeoutSeconds = useCallback((sec: number) => {
    setStoredTimeoutSeconds(sec);
    setTimeoutSecondsState(sec);
    resetTimer();
  }, [resetTimer]);

  // Manually trigger warning dialog (for immediate test verification)
  const triggerTestWarning = useCallback(() => {
    const now = Date.now();
    const currentTimeoutSec = getStoredTimeoutSeconds();
    const simulatedActivity = now - (currentTimeoutSec - 30) * 1000;
    lastActivityTimeRef.current = simulatedActivity;
    localStorage.setItem(STORAGE_LAST_ACTIVITY, String(simulatedActivity));
    setIsWarning(true);
    setRemainingSeconds(30);
  }, []);

  useEffect(() => {
    if (!enabled) {
      setIsWarning(false);
      return;
    }

    if (!localStorage.getItem(STORAGE_LAST_ACTIVITY)) {
      recordUserActivity();
      lastActivityTimeRef.current = Date.now();
    }

    const checkIdle = () => {
      const currentTimeoutSec = getStoredTimeoutSeconds();
      const currentWarningSec = Math.min(30, Math.floor(currentTimeoutSec / 4));
      const warningThresholdSec = currentTimeoutSec - currentWarningSec;

      const storedActivity = getStoredLastActivity();
      const lastAct = Math.max(lastActivityTimeRef.current, storedActivity);
      lastActivityTimeRef.current = lastAct;

      const now = Date.now();
      const idleElapsedSec = Math.floor((now - lastAct) / 1000);

      if (idleElapsedSec >= currentTimeoutSec) {
        setIsWarning(false);
        localStorage.removeItem(STORAGE_LAST_ACTIVITY);
        if (onTimeoutRef.current) {
          onTimeoutRef.current();
        }
      } else if (idleElapsedSec >= warningThresholdSec) {
        const left = Math.max(0, currentTimeoutSec - idleElapsedSec);
        setRemainingSeconds(left);
        setIsWarning(true);
      } else {
        if (isWarningRef.current) {
          setIsWarning(false);
        }
      }
    };

    const handleActivity = () => {
      // If warning modal is actively displayed, ignore passive mouse moves
      if (isWarningRef.current) return;

      const now = Date.now();
      if (now - lastEventThrottleRef.current < 1500) return; // 1.5s throttle

      lastEventThrottleRef.current = now;
      lastActivityTimeRef.current = now;
      recordUserActivity();
    };

    const events = ['mousedown', 'keydown', 'touchstart', 'click'];
    events.forEach(evt => window.addEventListener(evt, handleActivity, { passive: true }));
    window.addEventListener('mousemove', handleActivity, { passive: true });

    const handleVisibility = () => {
      if (document.visibilityState === 'visible') {
        checkIdle();
      }
    };
    document.addEventListener('visibilitychange', handleVisibility);
    window.addEventListener('focus', checkIdle);

    const interval = setInterval(checkIdle, 1000);
    checkIdle();

    return () => {
      clearInterval(interval);
      events.forEach(evt => window.removeEventListener(evt, handleActivity));
      window.removeEventListener('mousemove', handleActivity);
      document.removeEventListener('visibilitychange', handleVisibility);
      window.removeEventListener('focus', checkIdle);
    };
  }, [enabled]);

  return {
    isWarning,
    remainingSeconds,
    timeoutSeconds,
    extendSession,
    resetTimer,
    updateTimeoutSeconds,
    triggerTestWarning
  };
}
