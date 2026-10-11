import React from 'react';
import { 
  ShieldAlert, 
  Briefcase, 
  Layers, 
  Sliders, 
  FileText, 
  BarChart3, 
  Database,
  Lock
} from 'lucide-react';
import { MenuItemEntitlement } from '../types';

interface SidebarProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
  menuItems: MenuItemEntitlement[];
}

export const Sidebar: React.FC<SidebarProps> = ({ activeTab, onTabChange, menuItems }) => {
  const getIcon = (iconName: string) => {
    switch (iconName) {
      case 'ShieldAlert': return <ShieldAlert className="w-5 h-5" />;
      case 'Briefcase': return <Briefcase className="w-5 h-5" />;
      case 'Layers': return <Layers className="w-5 h-5" />;
      case 'Sliders': return <Sliders className="w-5 h-5" />;
      case 'FileText': return <FileText className="w-5 h-5" />;
      case 'BarChart3': return <BarChart3 className="w-5 h-5" />;
      default: return <Database className="w-5 h-5" />;
    }
  };

  return (
    <aside className="w-64 border-r border-border bg-surface flex flex-col justify-between shrink-0 select-none">
      <div className="p-4 space-y-6">
        <div>
          <div className="px-3 mb-2 text-[10px] font-mono uppercase tracking-wider text-slate-500 font-semibold">
            Compliance Operations
          </div>
          <nav className="space-y-1">
            {menuItems.map((item) => {
              const isActive = activeTab === item.code;
              return (
                <button
                  key={item.code}
                  disabled={!item.enabled}
                  onClick={() => onTabChange(item.code)}
                  className={`w-full flex items-center justify-between px-3.5 py-2.5 rounded-lg text-xs font-medium transition-all ${
                    isActive
                      ? 'bg-primary/10 text-primary border-l-2 border-primary shadow-[inset_0_1px_0_rgba(255,255,255,0.05)]'
                      : item.enabled
                      ? 'text-slate-400 hover:text-slate-100 hover:bg-surface-hover'
                      : 'text-slate-600 opacity-50 cursor-not-allowed'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className={isActive ? 'text-primary' : 'text-slate-400'}>
                      {getIcon(item.icon)}
                    </div>
                    <span>{item.label}</span>
                  </div>
                  {!item.enabled && (
                    <Lock className="w-3.5 h-3.5 text-slate-600" />
                  )}
                  {isActive && (
                    <span className="w-1.5 h-1.5 rounded-full bg-primary shadow-[0_0_6px_rgba(6,182,212,0.8)]" />
                  )}
                </button>
              );
            })}
          </nav>
        </div>

        {/* Live System Health Matrix */}
        <div className="p-3 bg-slate-900/80 rounded-lg border border-border space-y-2">
          <div className="text-[10px] font-mono uppercase tracking-wider text-slate-400 font-semibold flex items-center justify-between">
            <span>Stack Status</span>
            <span className="w-2 h-2 rounded-full bg-accent" />
          </div>
          <div className="space-y-1.5 text-[11px] font-mono text-slate-300">
            <div className="flex justify-between items-center">
              <span className="text-slate-500">PostgreSQL 17</span>
              <span className="text-accent font-medium">33 Tables (OK)</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-slate-500">Kafka 3.9 KRaft</span>
              <span className="text-accent font-medium">3 Replicas (OK)</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-slate-500">OpenSearch 2.x</span>
              <span className="text-accent font-medium">Index Green</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-slate-500">Redis Dependency</span>
              <span className="text-slate-400">Zero (Disabled)</span>
            </div>
          </div>
        </div>
      </div>

      {/* Footer Version Details */}
      <div className="p-4 border-t border-border bg-surface-muted text-[10px] font-mono text-slate-500 space-y-1">
        <div className="flex justify-between">
          <span>Engine Build:</span>
          <span className="text-slate-400">v3.0.400-LTS</span>
        </div>
        <div className="flex justify-between">
          <span>Region:</span>
          <span className="text-slate-400">me-central-1 (UAE)</span>
        </div>
        <div className="flex justify-between">
          <span>Security:</span>
          <span className="text-accent">mTLS Istio Ambient</span>
        </div>
      </div>
    </aside>
  );
};
