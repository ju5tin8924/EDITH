import React, { useState, useEffect } from 'react';
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
    { id: 'p1', title: 'Executive AML Dashboard', group: 'Operational Screening', path: '/dashboard', icon: <BarChart3 className="w-4 h-4 text-primary" />, hint: 'Page 1' },
    { id: 'p2', title: 'Single Entity Screening Console', group: 'Operational Screening', path: '/screening', icon: <ShieldAlert className="w-4 h-4 text-critical" />, hint: 'Page 2' },
    { id: 'p3', title: 'Bulk File Screening Engine', group: 'Operational Screening', path: '/bulk', icon: <Layers className="w-4 h-4 text-primary" />, hint: 'Page 3' },
    { id: 'p14', title: 'Delta Rescreening & Watchlist Sync', group: 'Operational Screening', path: '/delta', icon: <RefreshCw className="w-4 h-4 text-accent" />, hint: 'Page 14' },
    { id: 'p6', title: 'Whitelist & Exception Governance', group: 'Operational Screening', path: '/whitelist', icon: <ShieldCheck className="w-4 h-4 text-accent" />, hint: 'Page 6' },
    
    // Investigation
    { id: 'p4', title: '3-Pane Analyst Case Workspace', group: 'Case Investigation', path: '/cases', icon: <Briefcase className="w-4 h-4 text-warning" />, hint: 'Page 4' },
    { id: 'p5', title: 'Customer KYC 360 Dossier', group: 'Case Investigation', path: '/kyc360', icon: <UserCheck className="w-4 h-4 text-primary" />, hint: 'Page 5' },
    { id: 'p9', title: 'Automated goAML Reporting Studio', group: 'Case Investigation', path: '/goaml', icon: <FileText className="w-4 h-4 text-primary" />, hint: 'Page 9' },

    // Rules & Governance
    { id: 'p7', title: 'Visual Rule Builder & Engine', group: 'Rules & Governance', path: '/rules', icon: <Sliders className="w-4 h-4 text-primary" />, hint: 'Page 7' },
    { id: 'p11', title: 'Rule Sandbox & Backtester', group: 'Rules & Governance', path: '/sandbox', icon: <FlaskConical className="w-4 h-4 text-accent" />, hint: 'Page 11' },
    { id: 'p8', title: 'Reports, Analytics & Audit Ledger', group: 'Rules & Governance', path: '/reports', icon: <FileSpreadsheet className="w-4 h-4 text-primary" />, hint: 'Page 8' },
    { id: 'p10', title: 'Dynamic Notification Admin', group: 'Rules & Governance', path: '/notifications', icon: <BellRing className="w-4 h-4 text-primary" />, hint: 'Page 10' },
    { id: 'p12', title: 'System Masters & Scoring Settings', group: 'Rules & Governance', path: '/settings', icon: <Settings className="w-4 h-4 text-muted" />, hint: 'Page 12' },
    { id: 'p13', title: 'IAM, RBAC & Shift Governance', group: 'Rules & Governance', path: '/iam', icon: <Users className="w-4 h-4 text-primary" />, hint: 'Page 13' }
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
        setSelectedIndex(prev => (prev + 1) % filteredCommands.length);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        setSelectedIndex(prev => (prev - 1 + filteredCommands.length) % filteredCommands.length);
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

  return (
    <>
      <div 
        onClick={onClose} 
        className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs transition-opacity" 
      />

      <div className="fixed top-24 left-1/2 -translate-x-1/2 w-full max-w-2xl z-50 bg-surface border border-border shadow-2xl rounded-2xl overflow-hidden font-mono text-xs animate-in fade-in-50 zoom-in-95 duration-150">
        {/* Search Input Bar */}
        <div className="p-4 border-b border-border flex items-center gap-3 bg-surface">
          <Search className="w-5 h-5 text-primary shrink-0" />
          <input
            type="text"
            autoFocus
            placeholder="Type a screen name or command to jump... (e.g. 'goAML', 'Delta', 'Whitelist')"
            value={query}
            onChange={e => setQuery(e.target.value)}
            className="w-full bg-transparent text-sm text-ink placeholder:text-muted focus:outline-none"
          />
          <kbd className="px-2 py-0.5 rounded bg-surface-muted border border-border text-[10px] text-muted">
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
                    isSelected ? 'bg-primary/10 border-l-4 border-primary font-bold' : 'hover:bg-surface-hover'
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className="p-1.5 rounded-lg bg-surface border border-border shadow-2xs">
                      {cmd.icon}
                    </div>
                    <div>
                      <div className="text-ink text-xs">{cmd.title}</div>
                      <div className="text-[10px] text-muted font-normal">{cmd.group}</div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    {cmd.hint && (
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-surface-muted text-muted border border-border">
                        {cmd.hint}
                      </span>
                    )}
                    {isSelected && (
                      <CornerDownLeft className="w-3.5 h-3.5 text-primary" />
                    )}
                  </div>
                </div>
              );
            })
          ) : (
            <div className="p-8 text-center text-muted">
              No matching modules or commands found for "{query}".
            </div>
          )}
        </div>

        {/* Footer shortcuts */}
        <div className="p-2.5 bg-surface-muted border-t border-border flex justify-between items-center text-[10px] text-muted px-4">
          <div className="flex items-center gap-3">
            <span>↑↓ Navigate</span>
            <span>↵ Select</span>
            <span>ESC Dismiss</span>
          </div>
          <span>EDITH Master Screen Catalog (14 Modules)</span>
        </div>
      </div>
    </>
  );
};
