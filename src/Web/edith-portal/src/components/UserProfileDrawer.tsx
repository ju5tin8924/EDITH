import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { 
  LogOut, 
  Clock, 
  X,
  BadgeCheck,
  KeyRound
} from 'lucide-react';
import { UserRole } from '../types';

interface UserProfileDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  currentRole: UserRole;
  onRoleChange?: (role: UserRole) => void;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  onShiftStatusToggle: () => void;
  onLogout?: () => void;
  username?: string;
  timeoutSeconds?: number;
  onUpdateTimeout?: (sec: number) => void;
  onTestTimeout?: () => void;
}

export const UserProfileDrawer: React.FC<UserProfileDrawerProps> = ({
  isOpen,
  onClose,
  currentRole,
  shiftStatus,
  onShiftStatusToggle,
  onLogout,
  username = 'admin@edith.aml.cloud',
  timeoutSeconds = 120,
  onUpdateTimeout,
  onTestTimeout
}) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return createPortal(
    <div className="fixed inset-0 z-[9999] pointer-events-auto">
      {/* Backdrop */}
      <div 
        onClick={onClose} 
        className="fixed inset-0 bg-slate-950/50 backdrop-blur-xs transition-opacity duration-200" 
      />

      {/* Drawer Container */}
      <div 
        role="dialog"
        aria-modal="true"
        aria-label="User Profile"
        className="fixed bottom-0 left-0 w-80 sm:w-88 max-w-[calc(100vw-1rem)] bg-white border-t-2 border-r border-primary shadow-2xl rounded-tr-2xl p-5 font-mono text-xs space-y-4 animate-in slide-in-from-bottom-5 duration-200 z-[10000] overflow-hidden"
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-border">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-primary text-white flex items-center justify-center font-bold text-sm shadow-xs uppercase">
              {username ? username.substring(0, 2) : 'OP'}
            </div>
            <div>
              <div className="font-bold text-ink text-sm flex items-center gap-1.5">
                <span>{username.split('@')[0]}</span>
                <BadgeCheck className="w-4 h-4 text-accent fill-accent/20" />
              </div>
              <div className="text-[10px] text-muted">{currentRole}</div>
            </div>
          </div>
          <button 
            type="button"
            onClick={onClose} 
            className="p-1.5 rounded-lg hover:bg-slate-100 text-muted hover:text-ink transition-colors cursor-pointer"
            title="Close"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Account Info */}
        <div className="p-3 rounded-xl bg-[#faf8ff] border border-border flex justify-between items-center text-[11px]">
          <span className="text-muted flex items-center gap-1.5">
            <KeyRound className="w-3.5 h-3.5 text-primary" /> Account:
          </span>
          <span className="text-ink font-semibold truncate max-w-[190px]" title={username}>{username}</span>
        </div>

        {/* Assigned System Role */}
        <div className="p-3 rounded-xl bg-white border border-border flex justify-between items-center text-[11px]">
          <span className="text-muted font-bold uppercase tracking-wider text-[10px]">
            Assigned Role:
          </span>
          <span className="px-2.5 py-1 rounded-md bg-primary/10 text-primary font-mono font-bold text-[11px] border border-primary/20">
            {currentRole}
          </span>
        </div>

        {/* Shift Presence Status */}
        <div className="p-3 rounded-xl border border-border bg-white flex items-center justify-between">
          <div className="font-bold text-ink text-xs flex items-center gap-1.5">
            <Clock className="w-3.5 h-3.5 text-primary" />
            <span>Shift Status</span>
          </div>
          <button
            type="button"
            onClick={onShiftStatusToggle}
            className={`px-2.5 py-1 rounded-lg text-[10px] font-bold border transition-colors cursor-pointer ${
              shiftStatus === 'ON_DUTY'
                ? 'bg-accent/15 border-accent/40 text-accent font-bold'
                : 'bg-slate-100 border-border text-muted'
            }`}
          >
            {shiftStatus === 'ON_DUTY' ? '● ON-DUTY' : '○ OFF-DUTY'}
          </button>
        </div>

        {/* Inactivity Auto-Logout Timeout Setting */}
        <div className="p-3 rounded-xl border border-border bg-white space-y-2">
          <div className="flex items-center justify-between text-xs">
            <span className="font-bold text-ink flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5 text-primary" /> Auto-Logout
            </span>
            <span className="text-[10px] text-muted font-mono font-bold">
              {timeoutSeconds < 60 ? `${timeoutSeconds}s` : `${Math.floor(timeoutSeconds / 60)}m`} idle
            </span>
          </div>

          <div className="grid grid-cols-4 gap-1 pt-0.5">
            {[
              { label: '1m', sec: 60, sub: 'Test' },
              { label: '2m', sec: 120, sub: 'Review' },
              { label: '5m', sec: 300, sub: 'Standard' },
              { label: '15m', sec: 900, sub: 'CBUAE' },
            ].map(opt => (
              <button
                key={opt.sec}
                type="button"
                onClick={() => onUpdateTimeout && onUpdateTimeout(opt.sec)}
                className={`py-1 px-1 rounded-lg text-[10px] font-bold border transition-all text-center cursor-pointer ${
                  timeoutSeconds === opt.sec
                    ? 'bg-primary text-white border-primary shadow-xs'
                    : 'bg-slate-50 border-border text-muted hover:text-ink hover:bg-slate-100'
                }`}
              >
                <div>{opt.label}</div>
                <div className={`text-[8px] font-normal ${timeoutSeconds === opt.sec ? 'text-white/80' : 'text-muted'}`}>{opt.sub}</div>
              </button>
            ))}
          </div>

          {onTestTimeout && (
            <button
              type="button"
              onClick={() => {
                onClose();
                onTestTimeout();
              }}
              className="w-full py-1.5 px-2 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-600 border border-amber-500/30 text-[10px] font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
            >
              <Clock className="w-3 h-3" />
              <span>Simulate Auto-Logout Warning</span>
            </button>
          )}
        </div>

        {/* Sign Out */}
        <div className="pt-2 border-t border-border">
          <button 
            type="button"
            onClick={() => {
              onClose();
              if (onLogout) onLogout();
            }}
            className="w-full py-2 px-3 rounded-lg border border-critical/30 bg-critical/10 hover:bg-critical hover:text-white text-critical text-[11px] font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
          >
            <LogOut className="w-3.5 h-3.5" />
            <span>Sign Out</span>
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
};
