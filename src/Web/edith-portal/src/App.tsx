import React, { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { Footer } from './components/Footer';
import { CommandPaletteModal } from './components/CommandPaletteModal';

// Master Views (Lazy Loaded for Route-Based Code Splitting)
const DashboardView = React.lazy(() => import('./components/DashboardView').then(m => ({ default: m.DashboardView })));
const NameSearchView = React.lazy(() => import('./components/NameSearchView').then(m => ({ default: m.NameSearchView })));
const ScreeningConsole = React.lazy(() => import('./components/ScreeningConsole').then(m => ({ default: m.ScreeningConsole })));
const BulkScreeningView = React.lazy(() => import('./components/BulkScreeningView').then(m => ({ default: m.BulkScreeningView })));
const DeltaRescreeningView = React.lazy(() => import('./components/DeltaRescreeningView').then(m => ({ default: m.DeltaRescreeningView })));
const WhitelistGovernanceView = React.lazy(() => import('./components/WhitelistGovernanceView').then(m => ({ default: m.WhitelistGovernanceView })));
const CaseWorkspace = React.lazy(() => import('./components/CaseWorkspace').then(m => ({ default: m.CaseWorkspace })));
const CustomerKyc360View = React.lazy(() => import('./components/CustomerKyc360View').then(m => ({ default: m.CustomerKyc360View })));
const GoAmlReportingView = React.lazy(() => import('./components/GoAmlReportingView').then(m => ({ default: m.GoAmlReportingView })));
const RuleBuilderView = React.lazy(() => import('./components/RuleBuilderView').then(m => ({ default: m.RuleBuilderView })));
const RuleSandboxView = React.lazy(() => import('./components/RuleSandboxView').then(m => ({ default: m.RuleSandboxView })));
const ReportsAuditView = React.lazy(() => import('./components/ReportsAuditView').then(m => ({ default: m.ReportsAuditView })));
const NotificationAdminView = React.lazy(() => import('./components/NotificationAdminView').then(m => ({ default: m.NotificationAdminView })));
const SystemMastersView = React.lazy(() => import('./components/SystemMastersView').then(m => ({ default: m.SystemMastersView })));
const IamAccessView = React.lazy(() => import('./components/IamAccessView').then(m => ({ default: m.IamAccessView })));
const OnboardingView = React.lazy(() => import('./components/OnboardingView').then(m => ({ default: m.OnboardingView })));
const LoginView = React.lazy(() => import('./components/LoginView').then(m => ({ default: m.LoginView })));

const ViewLoadingFallback: React.FC = () => (
  <div className="flex-1 flex flex-col items-center justify-center p-12 min-h-[400px]">
    <div className="w-10 h-10 border-2 border-primary border-t-transparent rounded-full animate-spin mb-4" />
    <span className="text-xs font-mono text-muted tracking-wider uppercase">Loading Workspace Module...</span>
  </div>
);
import { EdithErrorBoundary, GlobalErrorHost } from './components/ErrorEngineUi';
import { RoleGuard } from './components/RoleGuard';
import { SessionTimeoutModal } from './components/SessionTimeoutModal';
import { useIdleTimer } from './hooks/useIdleTimer';

import { UserRole, MenuItemEntitlement, UserSession } from './types';
import { EdithApi, getSession, setSession, isAuthenticated } from './services/api';

const ScreeningPageWrapper: React.FC = () => {
  const navigate = useNavigate();
  return <ScreeningConsole onOpenCase={() => navigate('/cases')} />;
};

interface AppContentProps {
  onLogout: () => void;
  session: UserSession;
  onRoleChange: (role: UserRole) => void;
  timeoutSeconds: number;
  onUpdateTimeout: (sec: number) => void;
  onTestTimeout: () => void;
}

export const AppContent: React.FC<AppContentProps> = ({ 
  onLogout, 
  session, 
  onRoleChange,
  timeoutSeconds,
  onUpdateTimeout,
  onTestTimeout
}) => {
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
          timeoutSeconds={timeoutSeconds}
          onUpdateTimeout={onUpdateTimeout}
          onTestTimeout={onTestTimeout}
        />

        {/* Routed Multi-Page Content Area */}
        <main className="flex-1 overflow-y-auto bg-background p-4 min-w-0">
          <React.Suspense fallback={<ViewLoadingFallback />}>
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
              <Route path="/rules" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="AML Rules Builder"><RuleBuilderView /></RoleGuard>} />
              <Route path="/aml/rules" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="AML Rules Builder"><RuleBuilderView /></RoleGuard>} />
              <Route path="/sandbox" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Rule Sandbox"><RuleSandboxView /></RoleGuard>} />
              <Route path="/aml/process" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Process AML"><RuleSandboxView /></RoleGuard>} />
              <Route path="/aml/str-history" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER', 'ISTR_MLRO']} currentRole={session.role} moduleName="STR History"><ReportsAuditView /></RoleGuard>} />
              <Route path="/aml/mlro-monitoring" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="MLRO Monitoring"><CaseWorkspace currentRole={session.role} /></RoleGuard>} />

              {/* 5. STR Process */}
              <Route path="/cases" element={<CaseWorkspace currentRole={session.role} />} />
              <Route path="/str-process/alerts" element={<CaseWorkspace currentRole={session.role} />} />
              <Route path="/str-process/edd-analyst" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'EDD_ANALYST']} currentRole={session.role} moduleName="EDD Analyst & Branch KYC"><CustomerKyc360View /></RoleGuard>} />
              <Route path="/str-process/istr-mlro" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="ISTR MLRO Freeze Desk"><CaseWorkspace currentRole={session.role} /></RoleGuard>} />
              <Route path="/str-process/edd-monitoring" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'EDD_ANALYST']} currentRole={session.role} moduleName="EDD Monitoring"><CaseWorkspace currentRole={session.role} /></RoleGuard>} />

              {/* 6. Reports */}
              <Route path="/reports" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="Audit Reports"><ReportsAuditView /></RoleGuard>} />
              <Route path="/reports/*" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="Audit Reports"><ReportsAuditView /></RoleGuard>} />

              {/* 7. Log Report */}
              <Route path="/notifications" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Notifications"><NotificationAdminView /></RoleGuard>} />
              <Route path="/log-report" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="Process Logs"><ReportsAuditView /></RoleGuard>} />
              <Route path="/log-report/*" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="Process Logs"><ReportsAuditView /></RoleGuard>} />

              {/* 8. Settings (Restricted to Administrators) */}
              <Route path="/settings" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Settings"><SystemMastersView initialTab="RISK_RATES" /></RoleGuard>} />
              <Route path="/settings/user-groups" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="User Groups"><IamAccessView initialTab="GROUPS" /></RoleGuard>} />
              <Route path="/settings/users" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="User Management"><IamAccessView initialTab="USERS" /></RoleGuard>} />
              <Route path="/settings/blacklist-management" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Blacklist Settings"><SystemMastersView initialTab="BLACKLIST_MANAGEMENT" /></RoleGuard>} />
              <Route path="/settings/menu-master" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Menu Master"><IamAccessView initialTab="MENU_MASTER" /></RoleGuard>} />
              <Route path="/settings/menu-mappings" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Menu Permissions"><IamAccessView initialTab="MENU_MAPPINGS" /></RoleGuard>} />
              <Route path="/settings/risk-rates" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Risk Rate Settings"><SystemMastersView initialTab="RISK_RATES" /></RoleGuard>} />
              <Route path="/settings/screening-score" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Screening Scores"><SystemMastersView initialTab="SCREENING_SCORE" /></RoleGuard>} />
              <Route path="/settings/setup-control" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Setup Control"><SystemMastersView initialTab="SETUP_CONTROL" /></RoleGuard>} />
              <Route path="/settings/risk-assessment" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Risk Assessment Master"><SystemMastersView initialTab="RISK_ASSESSMENT" /></RoleGuard>} />
              <Route path="/settings/currencies" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Currencies"><SystemMastersView initialTab="CURRENCIES" /></RoleGuard>} />
              <Route path="/settings/change-password" element={<SystemMastersView initialTab="CHANGE_PASSWORD" />} />
              <Route path="/iam" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="IAM Management"><IamAccessView initialTab="USERS" /></RoleGuard>} />

              {/* 9. Master */}
              <Route path="/master" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Master Registry"><SystemMastersView initialTab="COUNTRIES" /></RoleGuard>} />
              <Route path="/master/country" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Country Master"><SystemMastersView initialTab="COUNTRIES" /></RoleGuard>} />
              <Route path="/master/nationality" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Nationality Master"><SystemMastersView initialTab="NATIONALITIES" /></RoleGuard>} />
              <Route path="/master/currencies" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Currencies"><SystemMastersView initialTab="CURRENCIES" /></RoleGuard>} />
              <Route path="/master/unblock-customer" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN']} currentRole={session.role} moduleName="Unblock Customer"><SystemMastersView initialTab="UNBLOCK_CUSTOMER" /></RoleGuard>} />

              {/* 10. Authorization */}
              <Route path="/goaml" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'ISTR_MLRO']} currentRole={session.role} moduleName="goAML Gateway"><GoAmlReportingView /></RoleGuard>} />
              <Route path="/authorization" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Authorization Queue"><GoAmlReportingView /></RoleGuard>} />
              <Route path="/authorization/sanction-alerts" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Sanction Authorization"><CaseWorkspace currentRole={session.role} /></RoleGuard>} />
              <Route path="/authorization/risk-calculate" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Risk Calculation"><GoAmlReportingView /></RoleGuard>} />
              <Route path="/authorization/customer" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Customer Authorization"><GoAmlReportingView /></RoleGuard>} />

              {/* 11. Customer */}
              <Route path="/kyc360" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'EDD_ANALYST', 'ISTR_MLRO']} currentRole={session.role} moduleName="Customer KYC 360"><CustomerKyc360View /></RoleGuard>} />
              <Route path="/customer/kyc" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'EDD_ANALYST', 'ISTR_MLRO']} currentRole={session.role} moduleName="Customer KYC 360"><CustomerKyc360View /></RoleGuard>} />
              <Route path="/customer/screening-report" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'EDD_ANALYST']} currentRole={session.role} moduleName="Customer Screening Report"><ReportsAuditView /></RoleGuard>} />

              {/* 12. Delta */}
              <Route path="/delta" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Delta Rescreening"><DeltaRescreeningView /></RoleGuard>} />
              <Route path="/delta/authorization" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Delta Authorization"><DeltaRescreeningView /></RoleGuard>} />
              <Route path="/delta/log-report" element={<RoleGuard allowedRoles={['ADMIN', 'SUPER_ADMIN', 'AML_OFFICER']} currentRole={session.role} moduleName="Delta Log Report"><DeltaRescreeningView /></RoleGuard>} />

              <Route path="*" element={<Navigate to="/dashboard" replace />} />
            </Routes>
          </React.Suspense>
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

  useEffect(() => {
    const handleExpired = () => {
      setSessionState(getSession());
      setAuthed(false);
    };
    const handleFocusCheck = () => {
      if (!isAuthenticated() && authed) {
        setSessionState(getSession());
        setAuthed(false);
      }
    };
    window.addEventListener('edith:session-expired', handleExpired);
    window.addEventListener('focus', handleFocusCheck);
    return () => {
      window.removeEventListener('edith:session-expired', handleExpired);
      window.removeEventListener('focus', handleFocusCheck);
    };
  }, [authed]);

  const handleLoginSuccess = () => {
    const active = getSession();
    setSessionState(active);
    setAuthed(true);
  };

  const handleLogout = async () => {
    await EdithApi.logout();
    setSessionState(getSession());
    setAuthed(false);
  };

  const handleRoleChange = (newRole: UserRole) => {
    const updated = { ...session, role: newRole };
    setSession(updated);
    setSessionState(updated);
  };

  // Inactivity tracking: configurable timeout with 30s warning
  const { 
    isWarning, 
    remainingSeconds, 
    timeoutSeconds,
    extendSession, 
    updateTimeoutSeconds, 
    triggerTestWarning 
  } = useIdleTimer({
    onTimeout: async () => {
      await EdithApi.logout();
      setSessionState(getSession());
      setAuthed(false);
      window.location.href = '/login?reason=inactivity';
    },
    onRefresh: EdithApi.refreshToken,
    enabled: authed
  });

  return (
    <EdithErrorBoundary>
      <BrowserRouter>
        <GlobalErrorHost />

        {/* Compliance Inactivity Timeout Dialog */}
        <SessionTimeoutModal
          isOpen={isWarning}
          remainingSeconds={remainingSeconds}
          onExtend={extendSession}
          onLogout={handleLogout}
        />

        <React.Suspense fallback={<ViewLoadingFallback />}>
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
                    timeoutSeconds={timeoutSeconds}
                    onUpdateTimeout={updateTimeoutSeconds}
                    onTestTimeout={triggerTestWarning}
                  />
                ) : (
                  <Navigate to="/login" replace />
                )
              }
            />
          </Routes>
        </React.Suspense>
      </BrowserRouter>
    </EdithErrorBoundary>
  );
};

export default App;
