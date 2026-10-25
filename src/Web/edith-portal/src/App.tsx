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
import { OnboardingView } from './components/OnboardingView';
import { LoginView } from './components/LoginView';
import { EdithErrorBoundary, GlobalErrorHost } from './components/ErrorEngineUi';

import { UserRole, MenuItemEntitlement, UserSession } from './types';
import { EdithApi, getSession, setSession, clearSession, isAuthenticated } from './services/api';

const ScreeningPageWrapper: React.FC = () => {
  const navigate = useNavigate();
  return <ScreeningConsole onOpenCase={() => navigate('/cases')} />;
};

interface AppContentProps {
  onLogout: () => void;
  session: UserSession;
  onRoleChange: (role: UserRole) => void;
}

export const AppContent: React.FC<AppContentProps> = ({ onLogout, session, onRoleChange }) => {
  const [shiftStatus, setShiftStatus] = useState<'ON_DUTY' | 'OFF_DUTY'>('ON_DUTY');
  const [menuItems, setMenuItems] = useState<MenuItemEntitlement[]>([]);
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);

  useEffect(() => {
    loadEntitlements();
  }, [session.role]);

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
        currentRole={session.role}
      />

      {/* Central Multi-Page Workspace */}
      <div className="flex-1 flex overflow-hidden min-h-0">
        {/* Persistent Left Sidebar with 12 Grouped Menus & User Profile Drawer at the bottom */}
        <Sidebar 
          menuItems={menuItems} 
          currentRole={session.role}
          onRoleChange={onRoleChange}
          shiftStatus={shiftStatus}
          onShiftStatusToggle={handleShiftToggle}
          onLogout={onLogout}
          username={session.username}
        />

        {/* Routed Multi-Page Content Area */}
        <main className="flex-1 overflow-y-auto bg-background p-4 min-w-0">
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            
            {/* 1. Home / Dashboard */}
            <Route path="/dashboard" element={<DashboardView currentRole={session.role} />} />

            {/* 2. Blacklist */}
            <Route path="/name-search" element={<NameSearchView />} />
            <Route path="/screening" element={<ScreeningPageWrapper />} />
            <Route path="/blacklist/screening" element={<ScreeningPageWrapper />} />
            <Route path="/blacklist/watchlist" element={<WhitelistGovernanceView />} />
            <Route path="/blacklist/bulk" element={<BulkScreeningView />} />
            <Route path="/bulk" element={<BulkScreeningView />} />

            {/* 3. White List */}
            <Route path="/whitelist" element={<WhitelistGovernanceView />} />

            {/* 4. AML */}
            <Route path="/rules" element={<RuleBuilderView />} />
            <Route path="/aml/rules" element={<RuleBuilderView />} />
            <Route path="/sandbox" element={<RuleSandboxView />} />
            <Route path="/aml/process" element={<RuleSandboxView />} />
            <Route path="/aml/str-history" element={<ReportsAuditView />} />
            <Route path="/aml/mlro-monitoring" element={<CaseWorkspace currentRole={session.role} />} />

            {/* 5. STR Process */}
            <Route path="/cases" element={<CaseWorkspace currentRole={session.role} />} />
            <Route path="/str-process/alerts" element={<CaseWorkspace currentRole={session.role} />} />
            <Route path="/str-process/edd-analyst" element={<CustomerKyc360View />} />
            <Route path="/str-process/istr-mlro" element={<CaseWorkspace currentRole={session.role} />} />
            <Route path="/str-process/edd-monitoring" element={<CaseWorkspace currentRole={session.role} />} />

            {/* 6. Reports */}
            <Route path="/reports" element={<ReportsAuditView />} />
            <Route path="/reports/*" element={<ReportsAuditView />} />

            {/* 7. Log Report */}
            <Route path="/notifications" element={<NotificationAdminView />} />
            <Route path="/log-report" element={<ReportsAuditView />} />
            <Route path="/log-report/*" element={<ReportsAuditView />} />

            {/* 8. Settings */}
            <Route path="/settings" element={<SystemMastersView initialTab="RISK_RATES" />} />
            <Route path="/settings/user-groups" element={<IamAccessView initialTab="GROUPS" />} />
            <Route path="/settings/users" element={<IamAccessView initialTab="USERS" />} />
            <Route path="/settings/menu-master" element={<IamAccessView initialTab="MENU_MASTER" />} />
            <Route path="/settings/menu-mappings" element={<IamAccessView initialTab="MENU_MAPPINGS" />} />
            <Route path="/settings/risk-rates" element={<SystemMastersView initialTab="RISK_RATES" />} />
            <Route path="/settings/screening-score" element={<SystemMastersView initialTab="SCREENING_SCORE" />} />
            <Route path="/settings/setup-control" element={<SystemMastersView initialTab="SETUP_CONTROL" />} />
            <Route path="/settings/risk-assessment" element={<SystemMastersView initialTab="RISK_ASSESSMENT" />} />
            <Route path="/settings/change-password" element={<SystemMastersView initialTab="CHANGE_PASSWORD" />} />
            <Route path="/iam" element={<IamAccessView initialTab="USERS" />} />

            {/* 9. Master */}
            <Route path="/master" element={<SystemMastersView initialTab="COUNTRIES" />} />
            <Route path="/master/country" element={<SystemMastersView initialTab="COUNTRIES" />} />
            <Route path="/master/nationality" element={<SystemMastersView initialTab="NATIONALITIES" />} />
            <Route path="/master/unblock-customer" element={<SystemMastersView initialTab="UNBLOCK_CUSTOMER" />} />

            {/* 10. Authorization */}
            <Route path="/goaml" element={<GoAmlReportingView />} />
            <Route path="/authorization" element={<GoAmlReportingView />} />
            <Route path="/authorization/sanction-alerts" element={<CaseWorkspace currentRole={session.role} />} />
            <Route path="/authorization/risk-calculate" element={<GoAmlReportingView />} />
            <Route path="/authorization/customer" element={<GoAmlReportingView />} />

            {/* 11. Customer */}
            <Route path="/kyc360" element={<CustomerKyc360View />} />
            <Route path="/customer/kyc" element={<CustomerKyc360View />} />
            <Route path="/customer/screening-report" element={<ReportsAuditView />} />

            {/* 12. Delta */}
            <Route path="/delta" element={<DeltaRescreeningView />} />
            <Route path="/delta/authorization" element={<DeltaRescreeningView />} />
            <Route path="/delta/log-report" element={<DeltaRescreeningView />} />

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
  const [session, setSessionState] = useState<UserSession>(getSession());
  const [authed, setAuthed] = useState<boolean>(isAuthenticated());

  const handleLoginSuccess = () => {
    const active = getSession();
    setSessionState(active);
    setAuthed(true);
  };

  const handleLogout = () => {
    clearSession();
    setSessionState(getSession());
    setAuthed(false);
  };

  const handleRoleChange = (newRole: UserRole) => {
    const updated = { ...session, role: newRole };
    setSession(updated);
    setSessionState(updated);
  };

  return (
    <EdithErrorBoundary>
      <BrowserRouter>
        <GlobalErrorHost />
        <Routes>
          {/* Public Onboarding Flow */}
          <Route path="/onboarding" element={<OnboardingView />} />

          {/* Dedicated Login Flow */}
          <Route 
            path="/login" 
            element={
              authed ? (
                <Navigate to="/dashboard" replace />
              ) : (
                <LoginView onLoginSuccess={handleLoginSuccess} />
              )
            } 
          />

          {/* Protected Application Workspace */}
          <Route
            path="/*"
            element={
              authed ? (
                <AppContent 
                  onLogout={handleLogout} 
                  session={session} 
                  onRoleChange={handleRoleChange} 
                />
              ) : (
                <Navigate to="/login" replace />
              )
            }
          />
        </Routes>
      </BrowserRouter>
    </EdithErrorBoundary>
  );
};

export default App;
