import React from 'react';
import { Shield, Activity, Bell, RefreshCw, Terminal, Layers } from 'lucide-react';

interface HeaderProps {
  onOpenCommandPalette: () => void;
  activeLatencyMs?: number;
  shiftStatus?: 'ON_DUTY' | 'OFF_DUTY';
}

export const Header: React.FC<HeaderProps> = ({ 
  onOpenCommandPalette, 
  activeLatencyMs = 8.4,
  shiftStatus = 'ON_DUTY'
}) => {
  return (
    <header className="h-[52px] border-b border-[#283593] bg-primary text-white px-4 flex items-center justify-between sticky top-0 z-40 shadow-md select-none">
      {/* Brand & Global Search */}
      <div className="flex items-center gap-5">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded bg-[#283593] border border-[#7986cb]/40 flex items-center justify-center text-[#ffd54f] shadow-xs">
            <Shield className="w-4 h-4 fill-[#ffd54f]/20 text-[#ffd54f]" />
          </div>
          <div className="flex items-baseline gap-2">
            <span className="font-bold text-sm tracking-widest uppercase text-white font-mono">EDITH // AML-OS</span>
            <span className="text-[10px] font-mono px-1.5 py-0.2 rounded bg-[#283593] text-[#c5cae9] border border-[#3949ab] font-bold">
              v3.0-LTS
            </span>
          </div>
        </div>

        <div className="h-4 w-[1px] bg-white/20 hidden md:block" />

        {/* Global Terminal Search Trigger */}
        <div 
          onClick={onOpenCommandPalette}
          className="relative hidden sm:flex items-center cursor-pointer group"
        >
          <Terminal className="w-3.5 h-3.5 absolute left-2.5 text-[#9fa8da] group-hover:text-white transition-colors" />
          <input 
            type="text" 
            readOnly
            placeholder="Search screens, entities, SWIFT wires... (Ctrl+K)" 
            className="bg-[#121858] text-white text-xs pl-8 pr-12 py-1 h-[30px] w-[300px] lg:w-[380px] rounded border border-[#3949ab] group-hover:border-[#9fa8da] cursor-pointer placeholder:text-[#9fa8da]/60 font-mono transition-colors"
          />
          <kbd className="absolute right-2 px-1.5 py-0.5 text-[10px] font-mono font-semibold bg-[#1a237e] border border-[#3949ab] text-[#c5cae9] rounded">
            ⌘K
          </kbd>
        </div>
      </div>

      {/* System Telemetry & Global Hub */}
      <div className="flex items-center gap-3">
        {/* SignalR 202 Background Job Pill */}
        <div className="hidden xl:flex items-center gap-2 px-2.5 py-1 rounded bg-[#121858] border border-[#3949ab] text-[11px] font-mono text-[#c5cae9]">
          <Layers className="w-3.5 h-3.5 text-[#ffd54f]" />
          <span className="text-white font-semibold">Job #8820:</span>
          <span className="text-[#a7f3d0]">Bulk_Wire_20261014.csv (68%)</span>
        </div>

        {/* System Health Status */}
        <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#121858] border border-[#3949ab] text-[11px] font-mono text-[#a7f3d0]">
          <Activity className="w-3.5 h-3.5 text-accent animate-pulse" />
          <span className="text-white font-medium">SLA: {activeLatencyMs.toFixed(1)}ms</span>
          <span className="text-[#9fa8da] hidden md:inline">(P99 &lt; 20ms)</span>
        </div>

        {/* Shift Presence Indicator */}
        <div className="hidden md:flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#121858] border border-[#3949ab] text-[11px] font-mono text-[#c5cae9]">
          <span className={`w-2 h-2 rounded-full ${shiftStatus === 'ON_DUTY' ? 'bg-accent' : 'bg-slate-400'}`} />
          <span className="text-white font-medium">{shiftStatus === 'ON_DUTY' ? 'On-Shift' : 'Standby'}</span>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-1">
          <button 
            title="System Sync Status"
            className="w-7 h-7 flex items-center justify-center text-[#c5cae9] hover:text-white hover:bg-[#283593] rounded transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5" />
          </button>

          <button 
            title="Alert Feed"
            className="relative w-7 h-7 flex items-center justify-center text-[#c5cae9] hover:text-white hover:bg-[#283593] rounded transition-colors"
          >
            <Bell className="w-3.5 h-3.5" />
            <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-critical animate-ping" />
            <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-critical" />
          </button>
        </div>
      </div>
    </header>
  );
};
