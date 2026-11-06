import React, { useEffect, useState } from 'react';
import { 
  AlertTriangle, 
  CheckCircle2, 
  Info, 
  AlertCircle, 
  RefreshCw, 
  X, 
  WifiOff, 
  ArrowRight 
} from 'lucide-react';
import { EdithLogo, EdithLoader } from './EdithLogo';
import {
  AppNotice,
  ErrorCategory,
  dismissNotice,
  dismissOfflineFallback,
  generateErrorReference,
  subscribeErrorEngine,
  toGeneralError,
} from '../services/errorEngine';

interface ErrorBoundaryState {
  hasError: boolean;
  referenceCode: string;
}

export class EdithErrorBoundary extends React.Component<
  { children: React.ReactNode },
  ErrorBoundaryState
> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false, referenceCode: '' };
  }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return {
      hasError: true,
      referenceCode: generateErrorReference(),
    };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error(`[${this.state.referenceCode}] Unhandled UI Error:`, error, info);
  }

  handleReload = () => {
    this.setState({ hasError: false, referenceCode: '' });
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen w-screen bg-[#070b14] text-white flex items-center justify-center p-6 font-sans select-none">
          <div className="max-w-md w-full bg-[#0d1527] border border-slate-800 rounded-2xl p-8 text-center space-y-5 shadow-2xl">
            <div className="mx-auto w-16 h-16 rounded-2xl bg-[#121858] border border-primary/40 flex items-center justify-center p-2">
              <EdithLogo variant="dark" animated className="w-12 h-12" />
            </div>
            <div className="space-y-2">
              <h2 className="text-lg font-bold tracking-tight">
                Workspace Display Interrupted
              </h2>
              <p className="text-xs text-slate-300 leading-relaxed">
                The application encountered an unexpected interface error. Your compliance records and active session remain secure.
              </p>
            </div>
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-md bg-slate-900 border border-slate-700 text-[11px] font-mono text-amber-300">
              <span>Incident Reference: {this.state.referenceCode}</span>
            </div>
            <button
              type="button"
              onClick={this.handleReload}
              className="w-full py-2.5 px-4 rounded-xl bg-primary hover:bg-primary-hover text-white font-mono font-bold text-xs flex items-center justify-center gap-2 transition cursor-pointer shadow-lg shadow-primary/20"
            >
              <RefreshCw className="w-4 h-4" />
              <span>Restore Workspace</span>
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export interface GeneralErrorBannerProps {
  error: unknown;
  category?: ErrorCategory;
  onRetry?: () => void | Promise<void>;
  onDismiss?: () => void;
  dark?: boolean;
}

export const GeneralErrorBanner: React.FC<GeneralErrorBannerProps> = ({
  error,
  category = 'VALIDATION',
  onRetry,
  onDismiss,
  dark = false,
}) => {
  const [retrying, setRetrying] = useState(false);
  const [resolved, setResolved] = useState(() => toGeneralError(error, category));

  useEffect(() => {
    if (error) {
      setResolved(toGeneralError(error, category));
    }
  }, [error, category]);

  if (!error) return null;

  const handleRetry = async () => {
    if (!onRetry || retrying) return;
    setRetrying(true);
    try {
      await onRetry();
    } finally {
      setRetrying(false);
    }
  };

  return (
    <div
      role="alert"
      className={`p-3.5 rounded-xl border text-xs font-mono flex items-start justify-between gap-3 animate-in fade-in duration-150 ${
        dark
          ? 'bg-critical/15 border-critical/40 text-red-200'
          : 'bg-critical/10 border-critical/30 text-critical'
      }`}
    >
      <div className="flex items-start gap-2.5 min-w-0 flex-1">
        <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5 text-critical" />
        <div className="space-y-1 min-w-0 flex-1">
          <div className="font-bold flex items-center gap-2 flex-wrap">
            <span>{resolved.title}</span>
            <span
              className={`px-1.5 py-0.5 rounded text-[10px] font-mono ${
                dark
                  ? 'bg-slate-900/80 text-slate-300 border border-slate-700'
                  : 'bg-white/80 text-muted border border-border'
              }`}
            >
              {resolved.referenceCode}
            </span>
          </div>
          <div className="text-[11px] leading-relaxed">{resolved.message}</div>
        </div>
      </div>

      <div className="flex items-center gap-1.5 shrink-0">
        {onRetry && (
          <button
            type="button"
            onClick={handleRetry}
            disabled={retrying}
            className="px-2.5 py-1 rounded-lg bg-critical text-white text-[11px] font-bold hover:opacity-90 transition flex items-center gap-1 cursor-pointer disabled:opacity-50"
          >
            <RefreshCw className={`w-3 h-3 ${retrying ? 'animate-spin' : ''}`} />
            <span>{retrying ? 'Retrying...' : 'Retry'}</span>
          </button>
        )}
        {onDismiss && (
          <button
            type="button"
            onClick={onDismiss}
            className="p-1 rounded hover:bg-black/10 transition cursor-pointer"
            title="Dismiss notice"
          >
            <X className="w-4 h-4" />
          </button>
        )}
      </div>
    </div>
  );
};

export const GlobalErrorHost: React.FC = () => {
  const [notices, setNotices] = useState<AppNotice[]>([]);
  const [offlineMode, setOfflineMode] = useState(false);
  const [busyActionId, setBusyActionId] = useState<string | null>(null);

  useEffect(() => {
    return subscribeErrorEngine((updatedNotices, isOffline) => {
      setNotices(updatedNotices);
      setOfflineMode(isOffline);
    });
  }, []);

  const handleAction = async (notice: AppNotice) => {
    if (busyActionId) return;
    const action = notice.onAction || notice.onRetry;
    if (!action) return;

    setBusyActionId(notice.id);
    try {
      dismissNotice(notice.id);
      await action();
    } finally {
      setBusyActionId(null);
    }
  };

  if (notices.length === 0 && !offlineMode) return null;

  return (
    <>
      {busyActionId && (
        <EdithLoader
          overlay
          size="sm"
          label="RETRYING REQUEST..."
          sublabel="Reconnecting and re-submitting your action"
        />
      )}

      <div className="fixed bottom-12 right-5 z-[9995] flex flex-col gap-2.5 max-w-md w-full pointer-events-none px-4 sm:px-0">
        {/* Offline Reference Fallback Indicator */}
        {offlineMode && (
          <div className="pointer-events-auto px-3.5 py-2.5 rounded-xl bg-[#0d1333]/95 text-white border border-amber-400/40 shadow-xl flex items-center justify-between gap-3 text-xs font-mono animate-in fade-in slide-in-from-bottom-2 duration-200">
            <div className="flex items-center gap-2.5 min-w-0">
              <WifiOff className="w-4 h-4 text-amber-400 shrink-0" />
              <div className="min-w-0">
                <div className="font-bold text-[11px] text-amber-300">
                  Local Simulation Active
                </div>
                <div className="text-[10px] text-slate-300 truncate">
                  Gateway offline — displaying local reference data.
                </div>
              </div>
            </div>
            <button
              type="button"
              onClick={dismissOfflineFallback}
              className="p-1 text-slate-400 hover:text-white transition cursor-pointer shrink-0"
              title="Dismiss notice"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* Dynamic Toast Feedback Stack */}
        {notices.map(notice => {
          const isError = notice.type === 'error';
          const isSuccess = notice.type === 'success';
          const isWarning = notice.type === 'warning';
          const isInfo = notice.type === 'info';

          const containerStyles = isSuccess
            ? 'bg-white border-[#10b981]/50 text-ink shadow-lg shadow-[#10b981]/5'
            : isWarning
            ? 'bg-white border-[#f59e0b]/50 text-ink shadow-lg shadow-[#f59e0b]/5'
            : isInfo
            ? 'bg-white border-[#3b82f6]/50 text-ink shadow-lg shadow-[#3b82f6]/5'
            : 'bg-white border-critical/50 text-ink shadow-xl shadow-critical/5';

          const icon = isSuccess ? (
            <CheckCircle2 className="w-4 h-4 text-[#10b981] shrink-0 mt-0.5" />
          ) : isWarning ? (
            <AlertCircle className="w-4 h-4 text-[#f59e0b] shrink-0 mt-0.5" />
          ) : isInfo ? (
            <Info className="w-4 h-4 text-[#3b82f6] shrink-0 mt-0.5" />
          ) : (
            <AlertTriangle className="w-4 h-4 text-critical shrink-0 mt-0.5" />
          );

          const titleColor = isSuccess
            ? 'text-[#059669]'
            : isWarning
            ? 'text-[#d97706]'
            : isInfo
            ? 'text-[#2563eb]'
            : 'text-critical';

          const hasAction = Boolean(notice.actionLabel && (notice.onAction || notice.onRetry));

          return (
            <div
              key={notice.id}
              role={isError ? 'alert' : 'status'}
              className={`pointer-events-auto p-3.5 rounded-xl border flex items-start justify-between gap-3 text-xs font-mono transition-all animate-in fade-in slide-in-from-bottom-2 duration-200 ${containerStyles}`}
            >
              <div className="flex items-start gap-2.5 min-w-0 flex-1">
                {icon}
                <div className="space-y-0.5 min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className={`font-bold ${titleColor}`}>{notice.title}</span>
                    {notice.referenceCode && (
                      <span className="px-1.5 py-0.5 rounded bg-surface-muted border border-border text-[9px] text-muted font-mono">
                        {notice.referenceCode}
                      </span>
                    )}
                  </div>
                  <p className="text-[11px] text-ink/90 leading-relaxed">
                    {notice.message}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-1.5 shrink-0 self-center">
                {hasAction && (
                  <button
                    type="button"
                    onClick={() => handleAction(notice)}
                    className="px-2.5 py-1 rounded-lg bg-primary hover:bg-primary-hover text-white text-[10px] font-bold transition flex items-center gap-1 cursor-pointer shadow-xs"
                  >
                    <span>{notice.actionLabel}</span>
                    <ArrowRight className="w-3 h-3" />
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => dismissNotice(notice.id)}
                  className="p-1 text-muted hover:text-ink transition cursor-pointer rounded-md hover:bg-slate-100"
                  title="Dismiss"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
};
