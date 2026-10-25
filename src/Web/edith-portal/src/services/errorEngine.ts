export type ErrorCategory =
  | 'AUTH'
  | 'VALIDATION'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'NETWORK'
  | 'SERVER';

export interface AppErrorNotice {
  id: string;
  referenceCode: string;
  category: ErrorCategory;
  title: string;
  message: string;
  canRetry: boolean;
  onRetry?: () => void | Promise<void>;
  timestampUtc: string;
}

const GENERAL_MESSAGES: Record<ErrorCategory, { title: string; message: string }> = {
  AUTH: {
    title: 'Access or Sign-In Issue',
    message: 'Your credentials or permissions could not be verified. Please check your details and try again.',
  },
  VALIDATION: {
    title: 'Invalid Information',
    message: 'Some of the submitted information is incomplete or invalid. Please review your input and try again.',
  },
  NOT_FOUND: {
    title: 'Record Not Found',
    message: 'The requested item could not be found or may have been moved.',
  },
  CONFLICT: {
    title: 'Action Could Not Be Completed',
    message: 'This record is currently in use or was recently updated. Please refresh and try again.',
  },
  NETWORK: {
    title: 'Connection Issue',
    message: 'Unable to complete the request right now. Please check your connection and try again.',
  },
  SERVER: {
    title: 'Service Temporarily Unavailable',
    message: 'Something went wrong while processing your request. Please try again in a moment.',
  },
};

// Matches internal technical terms that should never be shown verbatim to end users
const TECHNICAL_LEAK_PATTERN =
  /smtp|postgres|npgsql|opensearch|kafka|sql|exception|stack|gateway|jwt|bearer|guid|tenant_id|localhost|500|502|503|504|fetch|networkerror|typeerror|syntaxerror|cors|econnrefused/i;

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
  const referenceCode = generateErrorReference();
  let category: ErrorCategory = fallbackCategory;
  let candidateMessage = '';

  if (typeof rawError === 'string') {
    candidateMessage = rawError.trim();
  } else if (rawError && typeof rawError === 'object') {
    const errObj = rawError as Record<string, any>;
    if (typeof errObj.status === 'number') {
      category = inferCategoryFromStatus(errObj.status);
    }
    candidateMessage =
      typeof errObj.userMessage === 'string'
        ? errObj.userMessage
        : typeof errObj.message === 'string'
        ? errObj.message
        : typeof errObj.error === 'string'
        ? errObj.error
        : '';
  }

  const lower = candidateMessage.toLowerCase();
  if (lower.includes('network') || lower.includes('connect') || lower.includes('offline') || lower.includes('reach')) {
    category = 'NETWORK';
  } else if (lower.includes('invalid') || lower.includes('password') || lower.includes('credential') || lower.includes('code') || lower.includes('expired')) {
    if (fallbackCategory === 'AUTH' || lower.includes('credential') || lower.includes('session')) {
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

  // Keep safe, plain-language validation/auth hints if they don't expose internal details
  const isSafeCustomMessage =
    candidateMessage.length > 0 &&
    candidateMessage.length <= 140 &&
    !TECHNICAL_LEAK_PATTERN.test(candidateMessage) &&
    (category === 'VALIDATION' || category === 'AUTH');

  const finalMessage = isSafeCustomMessage ? candidateMessage : template.message;

  // Log technical detail with correlation code for support diagnostics
  console.warn(`[${referenceCode}] (${category})`, rawError);

  return {
    category,
    title: template.title,
    message: finalMessage,
    referenceCode,
  };
}

type ErrorStoreListener = (notices: AppErrorNotice[], offlineMode: boolean) => void;

let activeNotices: AppErrorNotice[] = [];
let isOfflineFallbackActive = false;
const listeners = new Set<ErrorStoreListener>();

function notifyListeners() {
  const snapshot = [...activeNotices];
  listeners.forEach(fn => fn(snapshot, isOfflineFallbackActive));
}

export function subscribeErrorEngine(listener: ErrorStoreListener): () => void {
  listeners.add(listener);
  listener([...activeNotices], isOfflineFallbackActive);
  return () => {
    listeners.delete(listener);
  };
}

export function reportError(
  rawError: unknown,
  options?: {
    category?: ErrorCategory;
    onRetry?: () => void | Promise<void>;
  }
): AppErrorNotice {
  const resolved = toGeneralError(rawError, options?.category ?? 'SERVER');
  const canRetry =
    Boolean(options?.onRetry) ||
    resolved.category === 'NETWORK' ||
    resolved.category === 'SERVER';

  const notice: AppErrorNotice = {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    referenceCode: resolved.referenceCode,
    category: resolved.category,
    title: resolved.title,
    message: resolved.message,
    canRetry: Boolean(options?.onRetry) && canRetry,
    onRetry: options?.onRetry,
    timestampUtc: new Date().toISOString(),
  };

  // Deduplicate identical messages currently visible
  activeNotices = [
    notice,
    ...activeNotices.filter(n => n.message !== notice.message),
  ].slice(0, 3);

  notifyListeners();
  return notice;
}

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

export function dismissError(id: string): void {
  activeNotices = activeNotices.filter(n => n.id !== id);
  notifyListeners();
}

export function clearAllErrors(): void {
  activeNotices = [];
  notifyListeners();
}
