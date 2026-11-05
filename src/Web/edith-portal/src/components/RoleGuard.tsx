import React from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldX, ArrowLeft, Lock } from 'lucide-react';
import { UserRole } from '../types';

interface RoleGuardProps {
  allowedRoles: UserRole[];
  currentRole: UserRole;
  moduleName?: string;
  children: React.ReactNode;
}

export const RoleGuard: React.FC<RoleGuardProps> = ({
  allowedRoles,
  currentRole,
  moduleName,
  children
}) => {
  const navigate = useNavigate();

  const isAllowed = allowedRoles.includes(currentRole);

  if (isAllowed) {
    return <>{children}</>;
  }

  return (
    <div className="min-h-[70vh] flex items-center justify-center p-6">
      <div className="max-w-md w-full bg-surface border border-red-500/20 rounded-2xl p-6 sm:p-8 shadow-xl text-center relative overflow-hidden">
        {/* Red accent glow */}
        <div className="absolute top-0 left-0 right-0 h-1 bg-red-500" />

        <div className="w-14 h-14 mx-auto mb-4 bg-red-500/10 border border-red-500/20 rounded-2xl flex items-center justify-center text-red-500">
          <ShieldX className="w-7 h-7" />
        </div>

        <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-mono font-bold bg-red-500/10 text-red-600 border border-red-500/20 mb-3">
          <Lock className="w-3 h-3" />
          403 ACCESS FORBIDDEN
        </div>

        <h2 className="text-lg font-bold text-ink tracking-tight mb-2">
          Compliance Entitlement Required
        </h2>

        <p className="text-xs text-muted leading-relaxed mb-6">
          Your current assigned role (<span className="font-mono font-bold text-ink">{currentRole}</span>) does not have entitlement to view or manage the {moduleName ? <strong className="text-ink">{moduleName}</strong> : 'requested'} workspace.
        </p>

        <div className="p-3 bg-surface-muted rounded-xl border border-border text-left mb-6 font-mono text-[11px] space-y-1">
          <div className="flex justify-between text-muted">
            <span>Current Role:</span>
            <span className="text-ink font-bold">{currentRole}</span>
          </div>
          <div className="flex justify-between text-muted">
            <span>Permitted Roles:</span>
            <span className="text-primary font-bold">{allowedRoles.join(', ')}</span>
          </div>
        </div>

        <button
          type="button"
          onClick={() => navigate('/dashboard')}
          className="w-full py-2.5 px-4 bg-primary text-white text-xs font-mono font-bold rounded-xl hover:bg-primary/90 active:scale-[0.98] transition-all flex items-center justify-center gap-2 cursor-pointer shadow-sm"
        >
          <ArrowLeft className="w-4 h-4" />
          Return to Dashboard
        </button>
      </div>
    </div>
  );
};
