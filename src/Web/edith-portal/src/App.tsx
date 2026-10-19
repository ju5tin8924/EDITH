import React, { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { Footer } from './components/Footer';
import { CommandPaletteModal } from './components/CommandPaletteModal';

// Master Views
import { DashboardView } from './components/DashboardView';
import { NameSearchView } from './components/NameSearchView';
import { ScreeningConsole } from './components/ScreeningConsole';
import { BulkScreeningView } from './components/BulkScreeningView';
import { DeltaRescreeningView } from './components/DeltaRescreeningView';
import { WhitelistGovernanceView } from './components/WhitelistGovernanceView';
import { CaseWorkspace } from './components/CaseWorkspace';
import { CustomerKyc360View } from './components/CustomerKyc360View';
import { GoAmlReportingView } from './components/GoAmlReportingView';
import { RuleBuilderView } from './components/RuleBuilderView';
import { RuleSandboxView } from './components/RuleSandboxView';
import { ReportsAuditView } from './components/ReportsAuditView';
import { NotificationAdminView } from './components/NotificationAdminView';
import { SystemMastersView } from './components/SystemMastersView';
import { IamAccessView } from './components/IamAccessView';

import { UserRole, MenuItemEntitlement } from './types';
import { EdithApi } from './services/api';

const ScreeningPageWrapper: React.FC = () => {
  const navigate = useNavigate();
  return <ScreeningConsole onOpenCase={() => navigate('/cases')} />;
};

export const AppContent: React.FC = () => {
  const [currentRole, setCurrentRole] = useState<UserRole>('ANALYST');
  const [shiftStatus, setShiftStatus] = useState<'ON_DUTY' | 'OFF_DUTY'>('ON_DUTY');
  const [menuItems, setMenuItems] = useState<MenuItemEntitlement[]>([]);
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);

  useEffect(() => {
    loadEntitlements();
  }, [currentRole]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsCommandPaletteOpen(prev => !prev);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const loadEntitlements = async () => {
    const data = await EdithApi.fetchEntitlements();
    setMenuItems(data.menuItems);
  };

  const handleRoleChange = (newRole: UserRole) => {
    setCurrentRole(newRole);
  };

  const handleShiftToggle = () => {
    setShiftStatus(prev => prev === 'ON_DUTY' ? 'OFF_DUTY' : 'ON_DUTY');
  };

  return (
    <div className="h-screen w-screen overflow-hidden bg-background text-ink flex flex-col font-sans selection:bg-border selection:text-primary">
      {/* Top Global Navigation Bar in Deep Navy Shell */}
      <Header 
        onOpenCommandPalette={() => setIsCommandPaletteOpen(true)}
        activeLatencyMs={8.4} 
        shiftStatus={shiftStatus}
      />

      {/* Central Multi-Page Workspace */}
      <div className="flex-1 flex overflow-hidden min-h-0">
        {/* Persistent Left Sidebar with 14 Grouped Menus & User Profile Drawer at the bottom */}
        <Sidebar 
          menuItems={menuItems} 
          currentRole={currentRole}
          onRoleChange={handleRoleChange}
          shiftStatus={shiftStatus}
          onShiftStatusToggle={handleShiftToggle}
        />

        {/* Routed Multi-Page Content Area */}
        <main className="flex-1 overflow-y-auto bg-background p-4 min-w-0">
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            
            {/* Group 1: Operational Screening & Ingestion */}
            <Route path="/dashboard" element={<DashboardView />} />
            <Route path="/name-search" element={<NameSearchView />} />
            <Route path="/screening" element={<ScreeningPageWrapper />} />
            <Route path="/bulk" element={<BulkScreeningView />} />
            <Route path="/delta" element={<DeltaRescreeningView />} />
            <Route path="/whitelist" element={<WhitelistGovernanceView />} />

            {/* Group 2: Case Investigation & Regulatory */}
            <Route path="/cases" element={<CaseWorkspace currentRole={currentRole} />} />
            <Route path="/kyc360" element={<CustomerKyc360View />} />
            <Route path="/goaml" element={<GoAmlReportingView />} />

            {/* Group 3: Rules, Analytics & Governance */}
            <Route path="/rules" element={<RuleBuilderView />} />
            <Route path="/sandbox" element={<RuleSandboxView />} />
            <Route path="/reports" element={<ReportsAuditView />} />
            <Route path="/notifications" element={<NotificationAdminView />} />
            <Route path="/settings" element={<SystemMastersView />} />
            <Route path="/iam" element={<IamAccessView />} />

            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </Routes>
        </main>
      </div>

      {/* Bottom Hotkey Shortcuts Dock Bar */}
      <Footer />

      {/* Global Command Palette (Ctrl+K) */}
      <CommandPaletteModal
        isOpen={isCommandPaletteOpen}
        onClose={() => setIsCommandPaletteOpen(false)}
      />
    </div>
  );
};

export const App: React.FC = () => {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
};

export default App;
