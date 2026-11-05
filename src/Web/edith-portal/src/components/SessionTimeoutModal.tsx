import React from 'react';
import { createPortal } from 'react-dom';
import { ShieldAlert, Clock, LogOut, CheckCircle2 } from 'lucide-react';

interface SessionTimeoutModalProps {
  isOpen: boolean;
  remainingSeconds: number;
  onExtend: () => void;
  onLogout: () => void;
}

export const SessionTimeoutModal: React.FC<SessionTimeoutModalProps> = ({
  isOpen,
  remainingSeconds,
  onExtend,
  onLogout
}) => {
  if (!isOpen) return null;

  const minutes = Math.floor(remainingSeconds / 60);
  const seconds = remainingSeconds % 60;
  const formattedTime = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

  return createPortal(
    <div className="fixed inset-0 z-[99999] flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in duration-200 pointer-events-auto">
      <div 
        className="w-full max-w-md bg-surface border border-amber-500/40 rounded-2xl p-6 shadow-2xl relative overflow-hidden"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="session-timeout-title"
      >
        {/* Amber glowing accent line */}
        <div className="absolute top-0 left-0 right-0 h-1.5 bg-gradient-to-r from-amber-500 via-orange-500 to-amber-500 animate-pulse" />

        <div className="flex items-start gap-4">
          <div className="p-3 bg-amber-500/10 border border-amber-500/20 rounded-xl text-amber-500 shrink-0">
            <Clock className="w-6 h-6 animate-spin text-amber-500" style={{ animationDuration: '6s' }} />
          </div>
          <div>
            <h3 id="session-timeout-title" className="text-base font-bold text-ink tracking-tight flex items-center gap-2">
              Inactivity Security Notice
              <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-500/15 text-amber-600 border border-amber-500/30">
                COMPLIANCE
              </span>
            </h3>
            <p className="text-xs text-muted mt-1 leading-relaxed">
              To safeguard financial crime intelligence and customer KYC dossiers, your session will automatically lock due to inactivity.
            </p>
          </div>
        </div>

        {/* Big countdown badge */}
        <div className="my-6 p-4 rounded-xl bg-surface-muted border border-border flex items-center justify-between">
          <div className="flex items-center gap-2 text-xs font-mono text-muted">
            <ShieldAlert className="w-4 h-4 text-amber-500" />
            <span>Session expires in:</span>
          </div>
          <span className="font-mono text-2xl font-black text-amber-600 tracking-wider">
            {formattedTime}
          </span>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-end gap-3 pt-2">
          <button
            type="button"
            onClick={onLogout}
            className="px-4 py-2 text-xs font-mono font-medium text-muted hover:text-ink hover:bg-surface-muted rounded-lg transition-colors flex items-center gap-1.5 cursor-pointer"
          >
            <LogOut className="w-3.5 h-3.5" />
            Sign Out Now
          </button>

          <button
            type="button"
            onClick={onExtend}
            className="px-5 py-2 text-xs font-mono font-bold text-white bg-primary hover:bg-primary/90 active:scale-[0.98] rounded-lg shadow-sm transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <CheckCircle2 className="w-3.5 h-3.5" />
            Stay Signed In
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
};
