export type ErrorCategory =
  | 'AUTH'
  | 'VALIDATION'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'NETWORK'
  | 'SERVER';

export type NoticeType = 'error' | 'success' | 'warning' | 'info';

export interface AppNotice {
  id: string;
  type: NoticeType;
  referenceCode?: string;
  category?: ErrorCategory;
  title: string;
  message: string;
  canRetry: boolean;
  onRetry?: () => void | Promise<void>;
  actionLabel?: string;
  onAction?: () => void | Promise<void>;
  durationMs: number; // 0 = persistent until dismissed
  timestampUtc: string;
}

// Backward-compatibility alias
export type AppErrorNotice = AppNotice;

const GENERAL_MESSAGES: Record<
  ErrorCategory,
  { title: string; message: string; actionLabel?: string }
> = {
  AUTH: {
    title: 'Access or Sign-In Required',
    message: 'Your credentials, session, or role permissions could not be validated. Please verify your details or sign in again.',
    actionLabel: 'Sign In',
  },
  VALIDATION: {
    title: 'Review Input Data',
    message: 'Some of the submitted information is incomplete or does not meet compliance requirements. Please verify the highlighted fields.',
  },
  NOT_FOUND: {
    title: 'Record Not Found',
    message: 'The requested entity, watchlist profile, or case record could not be located in the repository.',
  },
  CONFLICT: {
    title: 'Resource Currently Locked',
    message: 'This record is currently in active review by another compliance officer. Please wait or request lock handover.',
    actionLabel: 'Refresh',
  },
  NETWORK: {
    title: 'Gateway Connection Issue',
    message: 'Unable to connect to the EDITH compliance core. Please check your network connection and retry.',
    actionLabel: 'Retry',
  },
  SERVER: {
    title: 'Service Temporarily Busy',
    message: 'The screening core encountered a transient operational issue. Your data is secure—please retry in a moment.',
    actionLabel: 'Retry',
  },
};

// Patterns for technical infrastructure that should never be shown raw to end users
const TECHNICAL_LEAK_PATTERN =
  /smtp|postgres|npgsql|opensearch|kafka|sql|exception|stack trace|at edith\.|at system\.|gateway|jwt|bearer|guid|tenant_id|localhost|127\.0\.0\.1|500|502|503|504|fetch|networkerror|typeerror|syntaxerror|cors|econnrefused/i;

export function generateErrorReference(): string {
  const token = Math.random().toString(36).substring(2, 8).toUpperCase();
  return `EDITH-ERR-${token}`;
}

export function inferCategoryFromStatus(status?: number): ErrorCategory {
  if (!status) return 'NETWORK';
  if (status === 401 || status === 403) return 'AUTH';
  if (status === 404) return 'NOT_FOUND';
  if (status === 409 || status === 423) return 'CONFLICT';
  if (status === 400 || status === 422) return 'VALIDATION';
  if (status >= 500) return 'SERVER';
  return 'SERVER';
}

export function toGeneralError(
  rawError: unknown,
  fallbackCategory: ErrorCategory = 'SERVER'
): {
  category: ErrorCategory;
  title: string;
  message: string;
  referenceCode: string;
} {
  let category: ErrorCategory = fallbackCategory;
  let candidateMessage = '';
  let candidateTitle = '';
  let candidateRef = '';

  if (typeof rawError === 'string') {
    candidateMessage = rawError.trim();
  } else if (rawError && typeof rawError === 'object') {
    const errObj = rawError as Record<string, any>;
    if (typeof errObj.status === 'number') {
      category = inferCategoryFromStatus(errObj.status);
    }
    candidateMessage =
      typeof errObj.detail === 'string'
        ? errObj.detail
        : typeof errObj.userMessage === 'string'
        ? errObj.userMessage
        : typeof errObj.message === 'string'
        ? errObj.message
        : typeof errObj.error === 'string'
        ? errObj.error
        : '';
    candidateTitle = typeof errObj.title === 'string' ? errObj.title : '';
    candidateRef =
      typeof errObj.referenceCode === 'string'
        ? errObj.referenceCode
        : typeof errObj.correlationId === 'string'
        ? errObj.correlationId
        : '';
  }

  const referenceCode = candidateRef && candidateRef.startsWith('EDITH-ERR-')
    ? candidateRef
    : generateErrorReference();

  const lower = candidateMessage.toLowerCase();
  if (lower.includes('network') || lower.includes('connect') || lower.includes('offline') || lower.includes('reach')) {
    category = 'NETWORK';
  } else if (lower.includes('invalid') || lower.includes('password') || lower.includes('credential') || lower.includes('code') || lower.includes('expired')) {
    if (fallbackCategory === 'AUTH' || lower.includes('credential') || lower.includes('session') || lower.includes('auth')) {
      category = 'AUTH';
    } else if (fallbackCategory === 'VALIDATION') {
      category = 'VALIDATION';
    }
  } else if (lower.includes('not found')) {
    category = 'NOT_FOUND';
  } else if (lower.includes('lock') || lower.includes('conflict')) {
    category = 'CONFLICT';
  }

  const template = GENERAL_MESSAGES[category];

  // Whitelist safe, non-leaking business messages under 160 characters
  const isSafeCustomMessage =
    candidateMessage.length > 0 &&
    candidateMessage.length <= 160 &&
    !TECHNICAL_LEAK_PATTERN.test(candidateMessage) &&
    (category === 'VALIDATION' || category === 'AUTH' || category === 'CONFLICT' || category === 'NOT_FOUND');

  const finalTitle = candidateTitle && !TECHNICAL_LEAK_PATTERN.test(candidateTitle)
    ? candidateTitle
    : template.title;

  const finalMessage = isSafeCustomMessage ? candidateMessage : template.message;

  // Log diagnostic context for compliance audit and support correlation
  console.warn(`[${referenceCode}] (${category})`, rawError);

  return {
    category,
    title: finalTitle,
    message: finalMessage,
    referenceCode,
  };
}

type NoticeStoreListener = (notices: AppNotice[], offlineMode: boolean) => void;

let activeNotices: AppNotice[] = [];
let isOfflineFallbackActive = false;
const listeners = new Set<NoticeStoreListener>();

function notifyListeners() {
  const snapshot = [...activeNotices];
  listeners.forEach(fn => fn(snapshot, isOfflineFallbackActive));
}

export function subscribeErrorEngine(listener: NoticeStoreListener): () => void {
  listeners.add(listener);
  listener([...activeNotices], isOfflineFallbackActive);
  return () => {
    listeners.delete(listener);
  };
}

export function reportNotice(notice: Omit<AppNotice, 'id' | 'timestampUtc'>): AppNotice {
  const fullNotice: AppNotice = {
    ...notice,
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    timestampUtc: new Date().toISOString(),
  };

  // Stack up to 4 distinct notices, deduplicating identical messages
  activeNotices = [
    fullNotice,
    ...activeNotices.filter(n => n.message !== fullNotice.message),
  ].slice(0, 4);

  notifyListeners();

  // Auto-dismiss if duration specified
  if (fullNotice.durationMs > 0) {
    setTimeout(() => {
      dismissNotice(fullNotice.id);
    }, fullNotice.durationMs);
  }

  return fullNotice;
}

export function reportError(
  rawError: unknown,
  options?: {
    category?: ErrorCategory;
    title?: string;
    onRetry?: () => void | Promise<void>;
    durationMs?: number;
  }
): AppNotice {
  const resolved = toGeneralError(rawError, options?.category ?? 'SERVER');
  const canRetry =
    Boolean(options?.onRetry) ||
    resolved.category === 'NETWORK' ||
    resolved.category === 'SERVER';

  return reportNotice({
    type: 'error',
    referenceCode: resolved.referenceCode,
    category: resolved.category,
    title: options?.title || resolved.title,
    message: resolved.message,
    canRetry: Boolean(options?.onRetry) && canRetry,
    onRetry: options?.onRetry,
    actionLabel: options?.onRetry ? 'Retry' : undefined,
    durationMs: options?.durationMs ?? 0, // Errors stay until dismissed or retried by default
  });
}

// User-friendly messaging system entry points
export const notify = {
  success(message: string, options?: { title?: string; durationMs?: number; actionLabel?: string; onAction?: () => void }) {
    return reportNotice({
      type: 'success',
      title: options?.title || 'Action Completed',
      message,
      canRetry: false,
      actionLabel: options?.actionLabel,
      onAction: options?.onAction,
      durationMs: options?.durationMs ?? 4000,
    });
  },

  info(message: string, options?: { title?: string; durationMs?: number; actionLabel?: string; onAction?: () => void }) {
    return reportNotice({
      type: 'info',
      title: options?.title || 'Notice',
      message,
      canRetry: false,
      actionLabel: options?.actionLabel,
      onAction: options?.onAction,
      durationMs: options?.durationMs ?? 4500,
    });
  },

  warning(message: string, options?: { title?: string; durationMs?: number; actionLabel?: string; onAction?: () => void }) {
    return reportNotice({
      type: 'warning',
      title: options?.title || 'Attention Needed',
      message,
      canRetry: false,
      actionLabel: options?.actionLabel,
      onAction: options?.onAction,
      durationMs: options?.durationMs ?? 6000,
    });
  },

  error(rawError: unknown, options?: { category?: ErrorCategory; title?: string; onRetry?: () => void | Promise<void>; durationMs?: number }) {
    return reportError(rawError, options);
  },

  dismiss(id: string) {
    dismissNotice(id);
  },

  clearAll() {
    clearAllNotices();
  },
};

export function reportOfflineFallback(context?: string): void {
  if (!isOfflineFallbackActive) {
    isOfflineFallbackActive = true;
    console.info(`[EDITH-OFFLINE-MODE] Using local fallback data${context ? ` (${context})` : ''}.`);
    notifyListeners();
  }
}

export function dismissOfflineFallback(): void {
  if (isOfflineFallbackActive) {
    isOfflineFallbackActive = false;
    notifyListeners();
  }
}

export function dismissNotice(id: string): void {
  activeNotices = activeNotices.filter(n => n.id !== id);
  notifyListeners();
}

export const dismissError = dismissNotice;

export function clearAllNotices(): void {
  activeNotices = [];
  notifyListeners();
}

export const clearAllErrors = clearAllNotices;
