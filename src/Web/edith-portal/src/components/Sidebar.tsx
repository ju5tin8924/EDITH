import React, { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { 
  ShieldAlert, 
  Briefcase, 
  Layers, 
  Sliders, 
  FileText, 
  BarChart3, 
  Database,
  Lock,
  RefreshCw,
  ShieldCheck,
  UserCheck,
  FlaskConical,
  FileSpreadsheet,
  BellRing,
  Settings,
  Users,
  ChevronUp,
  BadgeCheck,
  Search
} from 'lucide-react';
import { MenuItemEntitlement, UserRole } from '../types';
import { UserProfileDrawer } from './UserProfileDrawer';

interface SidebarProps {
  menuItems: MenuItemEntitlement[];
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  onShiftStatusToggle: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ 
  menuItems,
  currentRole,
  onRoleChange,
  shiftStatus,
  onShiftStatusToggle
}) => {
  const [isProfileDrawerOpen, setIsProfileDrawerOpen] = useState(false);

  const getIcon = (iconName: string) => {
    switch (iconName) {
      case 'BarChart3': return <BarChart3 className="w-4 h-4" />;
      case 'Search': return <Search className="w-4 h-4" />;
      case 'ShieldAlert': return <ShieldAlert className="w-4 h-4" />;
      case 'Layers': return <Layers className="w-4 h-4" />;
      case 'RefreshCw': return <RefreshCw className="w-4 h-4" />;
      case 'ShieldCheck': return <ShieldCheck className="w-4 h-4" />;
      case 'Briefcase': return <Briefcase className="w-4 h-4" />;
      case 'UserCheck': return <UserCheck className="w-4 h-4" />;
      case 'FileText': return <FileText className="w-4 h-4" />;
      case 'Sliders': return <Sliders className="w-4 h-4" />;
      case 'FlaskConical': return <FlaskConical className="w-4 h-4" />;
      case 'FileSpreadsheet': return <FileSpreadsheet className="w-4 h-4" />;
      case 'BellRing': return <BellRing className="w-4 h-4" />;
      case 'Settings': return <Settings className="w-4 h-4" />;
      case 'Users': return <Users className="w-4 h-4" />;
      default: return <Database className="w-4 h-4" />;
    }
  };

  // Group definitions matching EDITH Master Architecture Outline (Section 2.3)
  const menuGroups = [
    {
      title: 'OPERATIONAL SCREENING',
      codes: ['DASHBOARD', 'NAME_SEARCH', 'SCREENING', 'BULK', 'DELTA', 'WHITELIST']
    },
    {
      title: 'CASE INVESTIGATION',
      codes: ['CASES', 'KYC360', 'GOAML']
    },
    {
      title: 'RULES & GOVERNANCE',
      codes: ['RULES', 'SANDBOX', 'REPORTS', 'NOTIFICATIONS', 'SETTINGS', 'IAM']
    }
  ];

  return (
    <aside className="w-64 border-r border-border bg-[#faf8ff] flex flex-col justify-between shrink-0 select-none overflow-y-auto">
      <div className="p-3 space-y-4">
        {/* Taskforce Unit Banner */}
        <div className="p-2.5 rounded-xl bg-white border border-border shadow-xs flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-primary/10 border border-primary/20 flex items-center justify-center text-primary font-mono text-xs font-bold shrink-0">
            U4
          </div>
          <div className="min-w-0">
            <div className="text-[10px] font-mono uppercase text-ink tracking-wider font-bold truncate">
              FINCRIM TASKFORCE
            </div>
            <div className="text-[10px] font-mono text-muted truncate">
              Unit 04 • High-Risk Desk
            </div>
          </div>
        </div>

        {/* Grouped Navigation Sections */}
        <div className="space-y-4">
          {menuGroups.map(group => {
            const groupItems = menuItems.filter(item => group.codes.includes(item.code));
            if (groupItems.length === 0) return null;

            return (
              <div key={group.title} className="space-y-1">
                <div className="px-2 mb-1 text-[9px] font-mono uppercase tracking-wider text-muted font-bold">
                  {group.title}
                </div>
                <nav className="space-y-0.5">
                  {groupItems.map(item => {
                    const targetPath = item.path || `/${item.code.toLowerCase()}`;

                    if (!item.enabled) {
                      return (
                        <div
                          key={item.code}
                          className="w-full flex items-center justify-between px-2.5 py-1.5 rounded text-xs font-medium text-disabled opacity-50 cursor-not-allowed bg-transparent"
                        >
                          <div className="flex items-center gap-2.5">
                            {getIcon(item.icon)}
                            <span className="text-[11px]">{item.label}</span>
                          </div>
                          <Lock className="w-3 h-3 text-disabled" />
                        </div>
                      );
                    }

                    return (
                      <NavLink
                        key={item.code}
                        to={targetPath}
                        className={({ isActive }) =>
                          `w-full flex items-center justify-between px-2.5 py-1.5 text-xs font-medium transition-colors ${
                            isActive
                              ? 'bg-[#e8eaf6] text-primary border-l-4 border-primary font-bold shadow-xs'
                              : 'text-ink hover:text-primary hover:bg-[#eef1fb] rounded'
                          }`
                        }
                      >
                        {({ isActive }) => (
                          <>
                            <div className="flex items-center gap-2.5 min-w-0">
                              <div className={isActive ? 'text-primary' : 'text-muted'}>
                                {getIcon(item.icon)}
                              </div>
                              <span className="text-[11px] truncate">{item.label}</span>
                            </div>

                            {/* Contextual Badges */}
                            {item.code === 'NAME_SEARCH' && (
                              <span className="px-1.5 py-0.2 text-[9px] font-mono bg-accent/15 text-accent rounded font-bold border border-accent/30 shrink-0">
                                ALL ROLES
                              </span>
                            )}
                            {item.code === 'SCREENING' && (
                              <span className="px-1.5 py-0.2 text-[9px] font-mono bg-primary text-white rounded font-bold shrink-0">
                                LIVE
                              </span>
                            )}
                            {item.code === 'CASES' && (
                              <span className="px-1.5 py-0.2 text-[9px] font-mono bg-warning/20 text-warning rounded font-bold border border-warning/40 shrink-0">
                                42
                              </span>
                            )}
                            {item.code === 'DELTA' && (
                              <span className="px-1.5 py-0.2 text-[9px] font-mono bg-critical/20 text-critical rounded font-bold shrink-0">
                                4 NEW
                              </span>
                            )}
                          </>
                        )}
                      </NavLink>
                    );
                  })}
                </nav>
              </div>
            );
          })}
        </div>

        {/* Live System Stack Status */}
        <div className="p-3 bg-white rounded-xl border border-border space-y-2 shadow-xs">
          <div className="text-[10px] font-mono uppercase tracking-wider text-ink font-bold flex items-center justify-between">
            <span>Stack Matrix</span>
            <span className="w-2 h-2 rounded-full bg-accent animate-pulse" />
          </div>
          <div className="space-y-1 text-[10px] font-mono text-ink">
            <div className="flex justify-between items-center">
              <span className="text-muted">PostgreSQL 17</span>
              <span className="text-accent font-semibold">33 Tables (OK)</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-muted">Kafka 3.9 KRaft</span>
              <span className="text-accent font-semibold">3 Replicas (OK)</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-muted">OpenSearch 2.x</span>
              <span className="text-accent font-semibold">Index Green</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-muted">Redis Dep</span>
              <span className="text-muted">Zero (Disabled)</span>
            </div>
          </div>
        </div>
      </div>

      {/* User Profile Drawer Anchor Trigger (Docked at the bottom of the sidebar) */}
      <div className="p-3 border-t border-border bg-[#faf8ff] sticky bottom-0">
        <button
          onClick={() => setIsProfileDrawerOpen(true)}
          className="w-full p-2.5 rounded-xl bg-white border border-border hover:border-primary/40 hover:bg-[#eef1fb] transition-all flex items-center justify-between shadow-xs group text-left"
        >
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="relative shrink-0">
              <div className="w-8 h-8 rounded-lg bg-primary text-white flex items-center justify-center font-mono text-xs font-bold shadow-xs">
                FA
              </div>
              <span className={`absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full border-2 border-white ${
                shiftStatus === 'ON_DUTY' ? 'bg-accent' : 'bg-slate-400'
              }`} />
            </div>

            <div className="min-w-0">
              <div className="text-xs font-bold text-ink truncate flex items-center gap-1">
                <span>F. Al-Nuaimi</span>
                <BadgeCheck className="w-3.5 h-3.5 text-accent" />
              </div>
              <div className="text-[10px] font-mono text-muted flex items-center gap-1.5">
                <span className="px-1 py-0.2 rounded bg-primary/10 text-primary text-[9px] font-bold">
                  {currentRole}
                </span>
                <span className="truncate">CCO // U04</span>
              </div>
            </div>
          </div>

          <ChevronUp className="w-4 h-4 text-muted group-hover:text-primary transition-transform group-hover:-translate-y-0.5 shrink-0" />
        </button>

        {/* User Profile Drawer Modal/Panel */}
        <UserProfileDrawer
          isOpen={isProfileDrawerOpen}
          onClose={() => setIsProfileDrawerOpen(false)}
          currentRole={currentRole}
          onRoleChange={onRoleChange}
          shiftStatus={shiftStatus}
          onShiftStatusToggle={onShiftStatusToggle}
        />
      </div>
    </aside>
  );
};
