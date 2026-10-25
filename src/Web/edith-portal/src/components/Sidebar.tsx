import React, { useState, useEffect } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
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
  ChevronDown,
  ChevronRight,
  PanelLeftClose,
  PanelLeftOpen,
  BadgeCheck,
  Search,
  X
} from 'lucide-react';
import { MenuItemEntitlement, UserRole } from '../types';
import { UserProfileDrawer } from './UserProfileDrawer';
import { REPORT_SUB_MENUS } from '../config/reportCategories';

interface SidebarProps {
  menuItems: MenuItemEntitlement[];
  currentRole: UserRole;
  onRoleChange: (role: UserRole) => void;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  onShiftStatusToggle: () => void;
  onLogout?: () => void;
  username?: string;
}

export const Sidebar: React.FC<SidebarProps> = ({ 
  menuItems,
  currentRole,
  onRoleChange,
  shiftStatus,
  onShiftStatusToggle,
  onLogout,
  username = 'admin@edith.aml.cloud'
}) => {
  const location = useLocation();
  const [isProfileDrawerOpen, setIsProfileDrawerOpen] = useState(false);
  const [isCollapsed, setIsCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem('edith_sidebar_collapsed') === 'true';
    } catch {
      return false;
    }
  });
  const [filterQuery, setFilterQuery] = useState('');
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({});
  const [expandedSubMenus, setExpandedSubMenus] = useState<Record<string, boolean>>({});

  useEffect(() => {
    try {
      localStorage.setItem('edith_sidebar_collapsed', String(isCollapsed));
    } catch {}
  }, [isCollapsed]);

  // Auto-expand ONLY the active parent menu and auto-collapse all others on route change
  useEffect(() => {
    const activeParent = menuItems.find(item => {
      const isReports = item.code === 'MENU_REPORTS' || item.code === 'REPORTS';
      const subs = isReports ? REPORT_SUB_MENUS : item.subMenus;
      return (
        item.path === location.pathname ||
        subs?.some(sub => location.pathname === sub.path || location.pathname.startsWith(sub.path + '/'))
      );
    });
    if (activeParent) {
      setExpandedSubMenus({ [activeParent.code]: true });
    } else {
      setExpandedSubMenus({});
    }
  }, [location.pathname, menuItems]);

  const toggleGroup = (title: string) => {
    setCollapsedGroups(prev => ({ ...prev, [title]: !prev[title] }));
  };

  // Single-open accordion: opening one menu automatically collapses any other open menu
  const toggleSubMenu = (code: string, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setExpandedSubMenus(prev => (prev[code] ? {} : { [code]: true }));
  };

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

  const menuGroups = [
    {
      title: 'Screening',
      codes: [
        'DASHBOARD', 'MENU_DASHBOARD', 
        'NAME_SEARCH', 
        'SCREENING', 'MENU_BLACKLIST', 
        'BULK', 
        'DELTA', 'MENU_DELTA', 
        'WHITELIST', 'MENU_WHITELIST'
      ]
    },
    {
      title: 'Investigation',
      codes: [
        'CASES', 'MENU_STR_PROCESS', 
        'KYC360', 'MENU_CUSTOMER', 
        'GOAML', 'MENU_AUTHORIZATION'
      ]
    },
    {
      title: 'Governance',
      codes: [
        'RULES', 'MENU_AML', 
        'SANDBOX', 
        'REPORTS', 'MENU_REPORTS', 
        'SETTINGS', 'MENU_SETTINGS', 
        'MASTER', 'MENU_MASTER', 
        'IAM'
      ]
    }
  ];

  const q = filterQuery.trim().toLowerCase();

  return (
    <aside 
      className={`${
        isCollapsed ? 'w-[68px]' : 'w-60'
      } border-r border-border bg-white flex flex-col justify-between shrink-0 select-none transition-all duration-200 ease-out`}
    >
      {/* Top Toolbar: Quick Filter + Collapse Toggle */}
      <div className="p-2.5 border-b border-border/70 flex items-center justify-between gap-2 bg-[#faf8ff]">
        {!isCollapsed && (
          <div className="relative flex-1 min-w-0">
            <Search className="w-3.5 h-3.5 text-muted absolute left-2.5 top-2 pointer-events-none" />
            <input
              type="text"
              value={filterQuery}
              onChange={e => setFilterQuery(e.target.value)}
              placeholder="Filter navigation..."
              className="w-full pl-8 pr-6 py-1 text-xs rounded-lg bg-white border border-border text-ink placeholder:text-muted/70 focus:outline-none focus:border-primary transition"
            />
            {filterQuery && (
              <button
                type="button"
                onClick={() => setFilterQuery('')}
                className="absolute right-2 top-2 text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-3 h-3" />
              </button>
            )}
          </div>
        )}

        <button
          type="button"
          onClick={() => setIsCollapsed(prev => !prev)}
          title={isCollapsed ? 'Expand Sidebar' : 'Collapse Sidebar'}
          className={`p-1.5 rounded-lg border border-border bg-white hover:bg-primary/5 hover:border-primary/30 text-muted hover:text-primary transition-colors cursor-pointer ${
            isCollapsed ? 'mx-auto' : 'shrink-0'
          }`}
        >
          {isCollapsed ? (
            <PanelLeftOpen className="w-4 h-4" />
          ) : (
            <PanelLeftClose className="w-4 h-4" />
          )}
        </button>
      </div>

      {/* Scrollable Navigation Rail */}
      <div className="flex-1 overflow-y-auto p-2.5 space-y-4">
        {menuGroups.map(group => {
          const groupItems = menuItems.map(item => {
            if (item.code === 'MENU_REPORTS' || item.code === 'REPORTS') {
              return { ...item, label: 'Reports', subMenus: REPORT_SUB_MENUS };
            }
            return item;
          }).filter(item => {
            if (!group.codes.includes(item.code)) return false;
            if (!q) return true;
            const matchSelf = item.label.toLowerCase().includes(q);
            const matchSub = item.subMenus?.some(s => s.label.toLowerCase().includes(q));
            return matchSelf || matchSub;
          });

          if (groupItems.length === 0) return null;
          const isGroupCollapsed = !q && !!collapsedGroups[group.title];

          return (
            <div key={group.title} className="space-y-1">
              {/* Group Section Header */}
              {!isCollapsed ? (
                <button
                  type="button"
                  onClick={() => toggleGroup(group.title)}
                  className="w-full px-2 py-1 flex items-center justify-between text-[10px] font-mono uppercase tracking-wider text-muted hover:text-ink font-bold cursor-pointer transition-colors"
                >
                  <span>{group.title}</span>
                  {isGroupCollapsed ? (
                    <ChevronRight className="w-3 h-3 text-muted" />
                  ) : (
                    <ChevronDown className="w-3 h-3 text-muted" />
                  )}
                </button>
              ) : (
                <div className="h-px bg-border/80 mx-2 my-2" />
              )}

              {/* Group Items */}
              {!isGroupCollapsed && (
                <nav className="space-y-1">
                  {groupItems.map(item => {
                    const targetPath = item.path || `/${item.code.toLowerCase()}`;
                    const hasSubMenus = !!(item.subMenus && item.subMenus.length > 0);
                    const isSubExpanded = !!q || !!expandedSubMenus[item.code];
                    const isParentActive =
                      location.pathname === targetPath ||
                      !!item.subMenus?.some(
                        sub => location.pathname === sub.path || location.pathname.startsWith(sub.path + '/')
                      );

                    if (!item.enabled) {
                      return (
                        <div
                          key={item.code}
                          title={isCollapsed ? `${item.label} (Restricted)` : undefined}
                          className={`w-full flex items-center ${
                            isCollapsed ? 'justify-center p-2' : 'justify-between px-2.5 py-2'
                          } rounded-xl text-xs font-medium text-disabled opacity-45 cursor-not-allowed`}
                        >
                          <div className="flex items-center gap-2.5">
                            <div className="w-7 h-7 rounded-lg bg-slate-100 flex items-center justify-center">
                              {getIcon(item.icon)}
                            </div>
                            {!isCollapsed && <span className="truncate">{item.label}</span>}
                          </div>
                          {!isCollapsed && <Lock className="w-3 h-3 text-disabled shrink-0" />}
                        </div>
                      );
                    }

                    return (
                      <div key={item.code} className="space-y-0.5">
                        <NavLink
                          to={targetPath}
                          title={isCollapsed ? item.label : undefined}
                          onClick={(e) => {
                            if (hasSubMenus) {
                              if (location.pathname === targetPath) {
                                e.preventDefault();
                                setExpandedSubMenus(prev => (prev[item.code] ? {} : { [item.code]: true }));
                              } else {
                                setExpandedSubMenus({ [item.code]: true });
                              }
                            } else {
                              setExpandedSubMenus({});
                            }
                          }}
                          className={() =>
                            `group relative w-full flex items-center ${
                              isCollapsed ? 'justify-center p-1.5' : 'justify-between px-2.5 py-1.5'
                            } rounded-xl text-xs transition-all duration-150 ${
                              isParentActive
                                ? 'bg-primary/10 text-primary font-bold shadow-2xs'
                                : 'text-ink hover:text-primary hover:bg-[#f3f5fc] font-medium'
                            }`
                          }
                        >
                          {() => (
                            <>
                              {/* Left Active Accent Bar */}
                              {isParentActive && (
                                <span className="absolute left-0 top-2 bottom-2 w-1 rounded-r-full bg-primary" />
                              )}

                              <div className="flex items-center gap-2.5 min-w-0">
                                <div
                                  className={`w-7 h-7 rounded-lg flex items-center justify-center shrink-0 transition-all duration-150 ${
                                    isParentActive
                                      ? 'bg-primary text-white shadow-xs'
                                      : 'bg-[#faf8ff] border border-border/80 text-muted group-hover:text-primary group-hover:border-primary/30 group-hover:bg-white'
                                  }`}
                                >
                                  {getIcon(item.icon)}
                                </div>
                                {!isCollapsed && (
                                  <span className="truncate text-[12px]">{item.label}</span>
                                )}
                              </div>

                              {/* Submenu Accordion Toggle Button */}
                              {!isCollapsed && hasSubMenus && (
                                <button
                                  type="button"
                                  onClick={(e) => toggleSubMenu(item.code, e)}
                                  className="p-1 rounded-md hover:bg-primary/10 text-muted hover:text-primary transition-colors cursor-pointer"
                                >
                                  {isSubExpanded ? (
                                    <ChevronDown className="w-3.5 h-3.5" />
                                  ) : (
                                    <ChevronRight className="w-3.5 h-3.5" />
                                  )}
                                </button>
                              )}
                            </>
                          )}
                        </NavLink>

                        {/* Collapsible Indented Sub-Menus with Section Subheadings */}
                        {!isCollapsed && hasSubMenus && isSubExpanded && (
                          <div className="pl-3 pr-1 py-1 space-y-1 border-l-2 border-primary/20 ml-5 my-0.5">
                            {(() => {
                              const filteredSubs = item.subMenus!.filter(sub => !q || sub.label.toLowerCase().includes(q));
                              let lastGroup: string | undefined = undefined;

                              return filteredSubs.map(sub => {
                                const showSubheading = sub.subGroup && sub.subGroup !== lastGroup;
                                if (sub.subGroup) {
                                  lastGroup = sub.subGroup;
                                }

                                return (
                                  <React.Fragment key={sub.code}>
                                    {showSubheading && (
                                      <div className="pt-2 pb-1 px-2 text-[10px] font-mono font-bold uppercase tracking-wider text-muted border-t border-border/60 first:border-t-0 first:pt-0">
                                        {sub.subGroup}
                                      </div>
                                    )}
                                    <NavLink
                                      to={sub.path}
                                      end={sub.path === '/reports'}
                                      className={({ isActive }) =>
                                        `block px-2 py-1 rounded-lg text-[11px] transition-colors truncate ${
                                          isActive
                                            ? 'text-primary font-bold bg-primary/10'
                                            : 'text-muted hover:text-ink hover:bg-[#f3f5fc]'
                                        }`
                                      }
                                    >
                                      {sub.label}
                                    </NavLink>
                                  </React.Fragment>
                                );
                              });
                            })()}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </nav>
              )}
            </div>
          );
        })}
      </div>

      {/* Bottom User Profile Trigger (Works in Expanded & Collapsed Icon-Rail Modes) */}
      <div className="p-2.5 border-t border-border bg-[#faf8ff] sticky bottom-0">
        <button
          type="button"
          onClick={() => setIsProfileDrawerOpen(true)}
          title={isCollapsed ? `${username} (${currentRole})` : undefined}
          className={`w-full ${
            isCollapsed ? 'p-1.5 justify-center' : 'p-2 justify-between'
          } rounded-xl bg-white border border-border hover:border-primary/40 hover:bg-[#eef1fb] transition-all flex items-center shadow-2xs group text-left cursor-pointer`}
        >
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="relative shrink-0">
              <div className="w-8 h-8 rounded-lg bg-primary text-white flex items-center justify-center font-mono text-xs font-bold shadow-xs uppercase">
                {username ? username.substring(0, 2) : 'OP'}
              </div>
              <span className={`absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full border-2 border-white ${
                shiftStatus === 'ON_DUTY' ? 'bg-accent' : 'bg-slate-400'
              }`} />
            </div>

            {!isCollapsed && (
              <div className="min-w-0">
                <div className="text-xs font-bold text-ink truncate flex items-center gap-1">
                  <span className="truncate">{username.split('@')[0]}</span>
                  <BadgeCheck className="w-3.5 h-3.5 text-accent shrink-0" />
                </div>
                <div className="text-[10px] font-mono text-muted flex items-center gap-1.5 mt-0.5">
                  <span className="px-1.5 py-0.2 rounded bg-primary/10 text-primary text-[9px] font-bold">
                    {currentRole}
                  </span>
                </div>
              </div>
            )}
          </div>

          {!isCollapsed && (
            <ChevronUp className="w-4 h-4 text-muted group-hover:text-primary transition-transform group-hover:-translate-y-0.5 shrink-0" />
          )}
        </button>

        <UserProfileDrawer
          isOpen={isProfileDrawerOpen}
          onClose={() => setIsProfileDrawerOpen(false)}
          currentRole={currentRole}
          onRoleChange={onRoleChange}
          shiftStatus={shiftStatus}
          onShiftStatusToggle={onShiftStatusToggle}
          onLogout={onLogout}
          username={username}
        />
      </div>
    </aside>
  );
};
