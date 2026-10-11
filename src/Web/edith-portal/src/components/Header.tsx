import React from 'react';
import { Shield, Activity, User, Bell, RefreshCw } from 'lucide-react';
import { UserRole } from '../types';

interface HeaderProps {
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  activeLatencyMs?: number;
}

export const Header: React.FC<HeaderProps> = ({ currentRole, onRoleChange, activeLatencyMs = 8.4 }) => {
  return (
    <header className="h-16 border-b border-border bg-surface px-6 flex items-center justify-between sticky top-0 z-50">
      {/* Brand & System Status */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2.5">
          <div className="h-9 w-9 rounded-lg bg-primary/10 border border-primary/30 flex items-center justify-center text-primary shadow-[0_0_12px_rgba(6,182,212,0.25)]">
            <Shield className="w-5 h-5 text-primary" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="font-mono font-bold text-lg tracking-wider text-white">EDITH</span>
              <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-primary/20 text-primary-light border border-primary/30 uppercase tracking-widest font-semibold">
                GCC v3.0
              </span>
            </div>
            <p className="text-[11px] text-slate-400 font-medium">Enterprise AML Screening & Intelligence Platform</p>
          </div>
        </div>

        <div className="hidden md:flex items-center gap-2 pl-6 border-l border-border/80">
          <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-slate-900 border border-border text-[11px] font-mono">
            <Activity className="w-3.5 h-3.5 text-accent animate-pulse" />
            <span className="text-slate-400">P99 Latency:</span>
            <span className="text-accent font-semibold">{activeLatencyMs.toFixed(1)}ms</span>
            <span className="text-slate-500 text-[10px]">(Target: &lt;20ms)</span>
          </div>

          <div className="px-2.5 py-1 rounded bg-slate-900/80 border border-border text-[11px] font-mono text-slate-400">
            Tenant: <span className="text-slate-200 font-medium">Abu Dhabi Commercial Bank (AE)</span>
          </div>
        </div>
      </div>

      {/* Quick Actions & Role Switcher */}
      <div className="flex items-center gap-3">
        {/* Role Preview Switcher for Development & Demonstration */}
        <div className="flex items-center gap-1.5 bg-slate-900/90 border border-border rounded-lg p-1 text-xs">
          <span className="text-slate-400 pl-2 text-[11px] font-medium hidden sm:inline">Role:</span>
          {(['ANALYST', 'MLRO', 'SUPER_ADMIN', 'AUDITOR'] as UserRole[]).map((role) => (
            <button
              key={role}
              onClick={() => onRoleChange(role)}
              className={`px-2.5 py-1 rounded text-[11px] font-semibold transition-all ${
                currentRole === role
                  ? 'bg-primary text-slate-950 shadow-[0_0_8px_rgba(6,182,212,0.4)]'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-surface'
              }`}
            >
              {role}
            </button>
          ))}
        </div>

        <button 
          title="System Sync Status"
          className="p-2 rounded-lg text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition border border-transparent hover:border-border"
        >
          <RefreshCw className="w-4 h-4" />
        </button>

        <button 
          title="Critical Alerts"
          className="relative p-2 rounded-lg text-slate-400 hover:text-slate-200 hover:bg-slate-800 transition border border-transparent hover:border-border"
        >
          <Bell className="w-4 h-4" />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-critical animate-ping" />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-critical" />
        </button>

        {/* User Identity */}
        <div className="flex items-center gap-2 pl-3 border-l border-border">
          <div className="w-8 h-8 rounded-full bg-slate-800 border border-border flex items-center justify-center text-slate-300">
            <User className="w-4 h-4" />
          </div>
          <div className="hidden lg:block text-left text-xs leading-tight">
            <div className="font-semibold text-slate-200">Fatima Al-Nuaimi</div>
            <div className="text-[10px] text-slate-400 font-mono">ID: AE-ADCB-8092</div>
          </div>
        </div>
      </div>
    </header>
  );
};
