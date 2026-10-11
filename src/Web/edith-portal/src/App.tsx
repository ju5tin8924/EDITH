import React, { useState, useEffect } from 'react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { ScreeningConsole } from './components/ScreeningConsole';
import { CaseWorkspace } from './components/CaseWorkspace';
import { BulkScreeningView } from './components/BulkScreeningView';
import { RuleBuilderView } from './components/RuleBuilderView';
import { DashboardView } from './components/DashboardView';
import { UserRole, MenuItemEntitlement } from './types';
import { EdithApi } from './services/api';

export const App: React.FC = () => {
  const [currentRole, setCurrentRole] = useState<UserRole>('ANALYST');
  const [activeTab, setActiveTab] = useState<string>('SCREENING');
  const [menuItems, setMenuItems] = useState<MenuItemEntitlement[]>([]);

  useEffect(() => {
    loadEntitlements();
  }, [currentRole]);

  const loadEntitlements = async () => {
    const data = await EdithApi.fetchEntitlements();
    setMenuItems(data.menuItems);
  };

  const handleRoleChange = (newRole: UserRole) => {
    setCurrentRole(newRole);
  };

  return (
    <div className="min-h-screen bg-background text-slate-100 flex flex-col font-sans selection:bg-primary selection:text-slate-950">
      {/* Top Application Header */}
      <Header 
        currentRole={currentRole} 
        onRoleChange={handleRoleChange} 
        activeLatencyMs={8.4} 
      />

      {/* Main Layout Area */}
      <div className="flex-1 flex overflow-hidden">
        {/* Navigation Sidebar */}
        <Sidebar 
          activeTab={activeTab} 
          onTabChange={setActiveTab} 
          menuItems={menuItems} 
        />

        {/* Content Pane */}
        <main className="flex-1 overflow-y-auto bg-background/50">
          {activeTab === 'SCREENING' && (
            <ScreeningConsole onOpenCase={() => setActiveTab('CASES')} />
          )}

          {activeTab === 'CASES' && (
            <CaseWorkspace currentRole={currentRole} />
          )}

          {activeTab === 'BULK' && (
            <BulkScreeningView />
          )}

          {activeTab === 'RULES' && (
            <RuleBuilderView />
          )}

          {(activeTab === 'ANALYTICS' || activeTab === 'GOAML') && (
            <DashboardView />
          )}
        </main>
      </div>
    </div>
  );
};

export default App;
