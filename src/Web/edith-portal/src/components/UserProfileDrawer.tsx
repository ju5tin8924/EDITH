import React from 'react';
import { 
  ShieldCheck, 
  Lock, 
  LogOut, 
  Building2, 
  Clock, 
  X,
  BadgeCheck
} from 'lucide-react';
import { UserRole } from '../types';

interface UserProfileDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  onShiftStatusToggle: () => void;
}

export const UserProfileDrawer: React.FC<UserProfileDrawerProps> = ({
  isOpen,
  onClose,
  currentRole,
  onRoleChange,
  shiftStatus,
  onShiftStatusToggle
}) => {
  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div 
        onClick={onClose} 
        className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs transition-opacity" 
      />

      {/* Drawer Container (Docked at bottom-left adjacent to sidebar or sliding from bottom) */}
      <div className="fixed bottom-0 left-0 w-80 md:w-96 z-50 bg-surface border-t-2 border-r border-primary shadow-2xl rounded-tr-2xl p-5 font-mono text-xs space-y-4 animate-in slide-in-from-bottom-5 duration-200">
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-border">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-primary text-white flex items-center justify-center font-bold text-sm shadow-xs border border-primary-light">
              FA
            </div>
            <div>
              <div className="font-bold text-ink text-sm flex items-center gap-1.5">
                <span>Fatima Al-Nuaimi</span>
                <BadgeCheck className="w-4 h-4 text-accent fill-accent/20" />
              </div>
              <div className="text-[10px] text-muted">CCO // FinCrime Taskforce Unit 04</div>
            </div>
          </div>
          <button 
            onClick={onClose} 
            className="p-1 rounded hover:bg-surface-hover text-muted hover:text-ink transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Officer Identity & Tenant */}
        <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1.5 text-[11px]">
          <div className="flex justify-between items-center">
            <span className="text-muted flex items-center gap-1">
              <Building2 className="w-3.5 h-3.5 text-primary" /> Tenant:
            </span>
            <span className="font-bold text-ink">ADCB Central Hub</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-muted">Username:</span>
            <span className="text-ink">analyst.fatima@adcb.ae</span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-muted">Security Clearance:</span>
            <span className="font-bold text-primary">Level 4 FIU Liaison</span>
          </div>
        </div>

        {/* Role Switcher */}
        <div className="space-y-1.5">
          <label className="text-[10px] uppercase tracking-wider text-muted font-bold block">
            Active Role Designation
          </label>
          <div className="grid grid-cols-2 gap-1.5">
            {(['ANALYST', 'MLRO', 'SUPER_ADMIN', 'AUDITOR'] as UserRole[]).map(role => (
              <button
                key={role}
                onClick={() => onRoleChange(role)}
                className={`py-1.5 px-2 rounded text-[10px] font-bold transition-all border ${
                  currentRole === role
                    ? 'bg-primary text-white border-primary shadow-xs'
                    : 'bg-surface text-ink border-border hover:bg-surface-hover'
                }`}
              >
                {role}
              </button>
            ))}
          </div>
        </div>

        {/* Shift Presence Status */}
        <div className="p-3 rounded-lg border border-border bg-surface flex items-center justify-between">
          <div>
            <div className="font-bold text-ink text-xs flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5 text-primary" />
              <span>Shift Routing Status</span>
            </div>
            <div className="text-[10px] text-muted mt-0.5">
              {shiftStatus === 'ON_DUTY' 
                ? 'Receiving P1 Statutory Case Routing' 
                : 'Bypassing Live Case Queue'}
            </div>
          </div>
          <button
            onClick={onShiftStatusToggle}
            className={`px-2.5 py-1 rounded text-[10px] font-bold border transition-colors ${
              shiftStatus === 'ON_DUTY'
                ? 'bg-accent/20 border-accent/40 text-accent'
                : 'bg-surface-muted border-border text-muted'
            }`}
          >
            {shiftStatus === 'ON_DUTY' ? '● ON-DUTY' : '○ OFF-DUTY'}
          </button>
        </div>

        {/* Security Credentials */}
        <div className="space-y-1 text-[10px] text-muted border-t border-border pt-3">
          <div className="flex justify-between items-center">
            <span>Hardware Token:</span>
            <span className="text-accent font-semibold flex items-center gap-1">
              <ShieldCheck className="w-3.5 h-3.5" /> FIPS 140-3 Active
            </span>
          </div>
          <div className="flex justify-between items-center">
            <span>Vault Session Token:</span>
            <span className="text-ink font-semibold">12h Lease (OK)</span>
          </div>
        </div>

        {/* Terminal Actions */}
        <div className="pt-2 border-t border-border flex gap-2">
          <button 
            onClick={onClose}
            className="flex-1 py-1.5 rounded border border-border bg-surface-muted hover:bg-surface-hover text-ink text-[11px] font-bold flex items-center justify-center gap-1.5"
          >
            <Lock className="w-3 h-3 text-muted" />
            <span>Lock Terminal</span>
          </button>
          <button 
            onClick={onClose}
            className="py-1.5 px-3 rounded border border-critical/30 bg-critical/10 hover:bg-critical hover:text-white text-critical text-[11px] font-bold flex items-center justify-center gap-1.5 transition-colors"
          >
            <LogOut className="w-3 h-3" />
            <span>Sign Out</span>
          </button>
        </div>
      </div>
    </>
  );
};
