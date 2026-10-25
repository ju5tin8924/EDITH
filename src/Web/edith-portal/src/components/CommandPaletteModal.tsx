import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { 
  Search, 
  ShieldAlert, 
  Briefcase, 
  Layers, 
  RefreshCw, 
  ShieldCheck, 
  UserCheck, 
  FileText, 
  Sliders, 
  FlaskConical, 
  FileSpreadsheet, 
  BellRing, 
  Settings, 
  Users, 
  BarChart3,
  CornerDownLeft
} from 'lucide-react';
import { GridPattern } from './magicui';

interface CommandPaletteModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface CommandItem {
  id: string;
  title: string;
  group: string;
  path: string;
  icon: React.ReactNode;
  hint?: string;
}

export const CommandPaletteModal: React.FC<CommandPaletteModalProps> = ({ isOpen, onClose }) => {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);

  const commands: CommandItem[] = [
    // Operational
    { id: 'p1', title: 'Dashboard', group: 'Operational Screening', path: '/dashboard', icon: <BarChart3 className="w-4 h-4 text-primary" /> },
    { id: 'p_ns', title: 'Name Search', group: 'Operational Screening', path: '/name-search', icon: <Search className="w-4 h-4 text-accent" /> },
    { id: 'p2', title: 'Transaction Screening', group: 'Operational Screening', path: '/screening', icon: <ShieldAlert className="w-4 h-4 text-critical" /> },
    { id: 'p3', title: 'Bulk Screening', group: 'Operational Screening', path: '/bulk', icon: <Layers className="w-4 h-4 text-primary" /> },
    { id: 'p14', title: 'Delta Rescreening', group: 'Operational Screening', path: '/delta', icon: <RefreshCw className="w-4 h-4 text-accent" /> },
    { id: 'p6', title: 'Whitelist Management', group: 'Operational Screening', path: '/whitelist', icon: <ShieldCheck className="w-4 h-4 text-accent" /> },
    
    // Investigation
    { id: 'p4', title: 'Case Workspace', group: 'Case Investigation', path: '/cases', icon: <Briefcase className="w-4 h-4 text-warning" /> },
    { id: 'p5', title: 'Customer KYC 360', group: 'Case Investigation', path: '/kyc360', icon: <UserCheck className="w-4 h-4 text-primary" /> },
    { id: 'p9', title: 'goAML Reporting', group: 'Case Investigation', path: '/goaml', icon: <FileText className="w-4 h-4 text-primary" /> },

    // Rules & Governance
    { id: 'p7', title: 'AML Rules', group: 'Rules & Governance', path: '/rules', icon: <Sliders className="w-4 h-4 text-primary" /> },
    { id: 'p11', title: 'Rule Sandbox', group: 'Rules & Governance', path: '/sandbox', icon: <FlaskConical className="w-4 h-4 text-accent" /> },
    { id: 'p8', title: 'Reports & Audit', group: 'Rules & Governance', path: '/reports', icon: <FileSpreadsheet className="w-4 h-4 text-primary" /> },
    { id: 'p10', title: 'Notifications & Webhooks', group: 'Rules & Governance', path: '/notifications', icon: <BellRing className="w-4 h-4 text-primary" /> },
    { id: 'p12', title: 'System Masters & Settings', group: 'Rules & Governance', path: '/settings', icon: <Settings className="w-4 h-4 text-muted" /> },
    { id: 'p13', title: 'User & Access Management', group: 'Rules & Governance', path: '/iam', icon: <Users className="w-4 h-4 text-primary" /> }
  ];

  const filteredCommands = commands.filter(cmd => 
    cmd.title.toLowerCase().includes(query.toLowerCase()) ||
    cmd.group.toLowerCase().includes(query.toLowerCase())
  );

  useEffect(() => {
    setSelectedIndex(0);
  }, [query]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isOpen) return;

      if (e.key === 'Escape') {
        onClose();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        setSelectedIndex(prev => (filteredCommands.length > 0 ? (prev + 1) % filteredCommands.length : 0));
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setSelectedIndex(prev => (filteredCommands.length > 0 ? (prev - 1 + filteredCommands.length) % filteredCommands.length : 0));
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (filteredCommands[selectedIndex]) {
          navigate(filteredCommands[selectedIndex].path);
          onClose();
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, selectedIndex, filteredCommands, navigate, onClose]);

  if (!isOpen) return null;

  return createPortal(
    <div className="fixed inset-0 z-[9999] flex items-start justify-center pt-20 px-4">
      {/* Backdrop */}
      <div 
        onClick={onClose} 
        className="fixed inset-0 bg-slate-950/60 backdrop-blur-xs transition-opacity duration-200" 
      />

      <div 
        role="dialog"
        aria-modal="true"
        aria-label="Universal Command Palette"
        className="relative z-10 w-full max-w-2xl bg-white border border-border shadow-2xl rounded-2xl overflow-hidden font-mono text-xs animate-in fade-in-50 zoom-in-95 duration-150"
      >
        <GridPattern className="opacity-25 pointer-events-none" width={24} height={24} />

        {/* Search Input Bar */}
        <div className="relative p-4 border-b border-border flex items-center gap-3 bg-white">
          <Search className="w-5 h-5 text-primary shrink-0" />
          <input
            type="text"
            autoFocus
            placeholder="Type a screen name or command to jump... (e.g. 'goAML', 'Delta', 'Whitelist')"
            value={query}
            onChange={e => setQuery(e.target.value)}
            className="w-full bg-transparent text-sm text-ink placeholder:text-muted focus:outline-none font-mono"
          />
          <kbd className="px-2 py-0.5 rounded-lg bg-slate-100 border border-border text-[10px] text-muted font-bold">
            ESC
          </kbd>
        </div>

        {/* Results List */}
        <div className="max-h-96 overflow-y-auto p-2 divide-y divide-border/40">
          {filteredCommands.length > 0 ? (
            filteredCommands.map((cmd, idx) => {
              const isSelected = idx === selectedIndex;
              return (
                <div
                  key={cmd.id}
                  onClick={() => {
                    navigate(cmd.path);
                    onClose();
                  }}
                  onMouseEnter={() => setSelectedIndex(idx)}
                  className={`p-3 rounded-xl flex items-center justify-between cursor-pointer transition-colors ${
                    isSelected ? 'bg-primary/10 border-l-4 border-primary font-bold shadow-2xs' : 'hover:bg-slate-50'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className="p-2 rounded-lg bg-white border border-border shadow-2xs">
                      {cmd.icon}
                    </div>
                    <div>
                      <div className="text-ink text-xs font-bold">{cmd.title}</div>
                      <div className="text-[10px] text-muted font-normal">{cmd.group}</div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    {cmd.hint && (
                      <span className="text-[10px] px-2 py-0.5 rounded-lg bg-slate-100 text-muted border border-border font-bold">
                        {cmd.hint}
                      </span>
                    )}
                    {isSelected && (
                      <CornerDownLeft className="w-4 h-4 text-primary" />
                    )}
                  </div>
                </div>
              );
            })
          ) : (
            <div className="p-8 text-center text-muted font-mono">
              No matching modules or commands found for "{query}".
            </div>
          )}
        </div>

        {/* Footer shortcuts */}
        <div className="p-3 bg-slate-50 border-t border-border flex justify-between items-center text-[10px] text-muted px-4 font-mono">
          <div className="flex items-center gap-4">
            <span>↑↓ Navigate</span>
            <span>↵ Select</span>
            <span>ESC Close</span>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
};
