import React, { useState } from 'react';
import { 
  Users, 
  ShieldCheck, 
  Search, 
  UserPlus
} from 'lucide-react';
import { UserRole } from '../types';

interface OfficerUser {
  id: string;
  fullName: string;
  username: string;
  role: UserRole;
  tenantId: string;
  department: string;
  mfaEnabled: boolean;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  lastLogin: string;
}

const MOCK_USERS: OfficerUser[] = [
  {
    id: 'usr-001',
    fullName: 'Fatima Al-Nuaimi',
    username: 'analyst.fatima@adcb.ae',
    role: 'MLRO',
    tenantId: '11111111-1111-1111-1111-111111111111',
    department: 'Financial Crime Compliance (Unit 04)',
    mfaEnabled: true,
    shiftStatus: 'ON_DUTY',
    lastLogin: '2026-10-14 07:10 UTC'
  },
  {
    id: 'usr-002',
    fullName: 'Tariq Mansoor',
    username: 'tariq.mansoor@adcb.ae',
    role: 'ANALYST',
    tenantId: '11111111-1111-1111-1111-111111111111',
    department: 'Triage Desk',
    mfaEnabled: true,
    shiftStatus: 'ON_DUTY',
    lastLogin: '2026-10-14 06:45 UTC'
  },
  {
    id: 'usr-003',
    fullName: 'Zayed Al-Husseini',
    username: 'zayed.husseini@adcb.ae',
    role: 'AUDITOR',
    tenantId: '11111111-1111-1111-1111-111111111111',
    department: 'Internal Audit & Inspection',
    mfaEnabled: true,
    shiftStatus: 'OFF_DUTY',
    lastLogin: '2026-10-13 14:15 UTC'
  },
  {
    id: 'usr-004',
    fullName: 'Infrastructure Admin',
    username: 'admin@edith.aml.cloud',
    role: 'SUPER_ADMIN',
    tenantId: '00000000-0000-0000-0000-000000000001',
    department: 'DevOps & Architecture',
    mfaEnabled: true,
    shiftStatus: 'ON_DUTY',
    lastLogin: '2026-10-14 01:20 UTC'
  }
];

export const IamAccessView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'USERS' | 'PERMISSIONS' | 'SHIFTS'>('USERS');
  const [users] = useState<OfficerUser[]>(MOCK_USERS);
  const [searchTerm, setSearchTerm] = useState('');

  const filteredUsers = users.filter(u => 
    u.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.username.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 13 // MOD-CORE-IAM
            </span>
            <span className="text-[10px] font-mono text-muted">Multi-Tenant IAM & RBAC Matrix</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <Users className="w-5 h-5 text-primary" />
            IAM, Role-Based Access Control & Shift Governance
          </h1>
          <p className="text-xs text-muted mt-1">
            Manage multi-tenant officer directories, dynamic menu entitlements, FIPS token MFA, and shift presence rosters.
          </p>
        </div>

        <button className="px-3.5 py-2 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-2 shadow-xs">
          <UserPlus className="w-4 h-4" />
          Provision Compliance Officer
        </button>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4 font-mono">
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Provisioned Officers</div>
          <div className="text-2xl font-bold text-ink mt-1">4 Active</div>
          <div className="text-[11px] text-accent mt-1">All MFA Hardware Enrolled</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">On-Duty Shift Officers</div>
          <div className="text-2xl font-bold text-primary mt-1">3 On-Shift</div>
          <div className="text-[11px] text-muted mt-1">Active P1 SLA Alert Queue</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Multi-Tenant Isolation</div>
          <div className="text-2xl font-bold text-accent mt-1">Enforced</div>
          <div className="text-[11px] text-muted mt-1">Composite Key Guard Active</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Security Clearance</div>
          <div className="text-2xl font-bold text-primary mt-1">Level 4 FIU</div>
          <div className="text-[11px] text-muted mt-1">Highest Regional Clearance</div>
        </div>
      </div>

      {/* Main Tabs Container */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-4">
        <div className="flex gap-2 border-b border-border text-xs font-mono">
          {[
            { id: 'USERS', label: '1. Officer Directory' },
            { id: 'PERMISSIONS', label: '2. Dynamic Menu Entitlements Matrix' },
            { id: 'SHIFTS', label: '3. Shift Presence Schedule' }
          ].map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id as typeof activeTab)}
              className={`pb-2 px-3 font-semibold transition-colors border-b-2 ${
                activeTab === tab.id
                  ? 'border-primary text-primary'
                  : 'border-transparent text-muted hover:text-ink'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab 1: User Directory */}
        {activeTab === 'USERS' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="flex justify-between items-center">
              <div className="relative w-72">
                <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-muted" />
                <input
                  type="text"
                  placeholder="Filter by officer name or email..."
                  value={searchTerm}
                  onChange={e => setSearchTerm(e.target.value)}
                  className="w-full pl-9 pr-3 py-1.5 text-xs font-mono rounded border border-border bg-surface-muted text-ink focus:outline-none focus:border-primary"
                />
              </div>
              <div className="text-muted text-[11px]">
                Showing {filteredUsers.length} Officers
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">Officer Name</th>
                    <th className="py-2.5 px-3">Role Designation</th>
                    <th className="py-2.5 px-3">Department</th>
                    <th className="py-2.5 px-3">MFA Hardware Token</th>
                    <th className="py-2.5 px-3">Shift Presence</th>
                    <th className="py-2.5 px-3">Last Active Login</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredUsers.map(user => (
                    <tr key={user.id} className="hover:bg-surface-hover">
                      <td className="py-3 px-3">
                        <div className="font-bold text-ink">{user.fullName}</div>
                        <div className="text-[10px] text-muted">{user.username}</div>
                      </td>
                      <td className="py-3 px-3">
                        <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary border border-primary/20">
                          {user.role}
                        </span>
                      </td>
                      <td className="py-3 px-3 text-muted">{user.department}</td>
                      <td className="py-3 px-3">
                        <span className="text-accent font-bold flex items-center gap-1 text-[11px]">
                          <ShieldCheck className="w-3.5 h-3.5" /> FIPS 140-3 Active
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          user.shiftStatus === 'ON_DUTY'
                            ? 'bg-accent/20 text-accent border border-accent/30'
                            : 'bg-surface-muted text-muted border border-border'
                        }`}>
                          {user.shiftStatus === 'ON_DUTY' ? '● ON-DUTY' : '○ OFF-DUTY'}
                        </span>
                      </td>
                      <td className="py-3 px-3 text-muted text-[11px]">{user.lastLogin}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 2: Entitlements Matrix */}
        {activeTab === 'PERMISSIONS' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="text-muted text-[11px]">
              Matrix determining which modules each role can access and execute:
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">Module Name</th>
                    <th className="py-2.5 px-3 text-center">ANALYST</th>
                    <th className="py-2.5 px-3 text-center">MLRO</th>
                    <th className="py-2.5 px-3 text-center">AUDITOR</th>
                    <th className="py-2.5 px-3 text-center">SUPER_ADMIN</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {[
                    { name: 'Screening Console (Real-time)', analyst: true, mlro: true, auditor: true, admin: true },
                    { name: 'Case Workspace (3-Pane Investigation)', analyst: true, mlro: true, auditor: true, admin: true },
                    { name: 'Bulk & Delta Rescreening Hub', analyst: true, mlro: true, auditor: false, admin: true },
                    { name: 'goAML Regulatory Reporting Studio', analyst: false, mlro: true, auditor: true, admin: true },
                    { name: 'Rule Builder & Sandbox Backtesting', analyst: false, mlro: true, auditor: false, admin: true },
                    { name: 'Reports & WORM Audit Explorer', analyst: true, mlro: true, auditor: true, admin: true },
                    { name: 'Dynamic Notification Admin', analyst: false, mlro: true, auditor: false, admin: true },
                    { name: 'System Masters & Scoring Settings', analyst: false, mlro: true, auditor: false, admin: true },
                    { name: 'IAM & Access Management', analyst: false, mlro: false, auditor: false, admin: true }
                  ].map(row => (
                    <tr key={row.name} className="hover:bg-surface-hover">
                      <td className="py-2.5 px-3 font-semibold text-ink">{row.name}</td>
                      <td className="py-2.5 px-3 text-center">{row.analyst ? <span className="text-accent font-bold">✓</span> : <span className="text-muted opacity-40">-</span>}</td>
                      <td className="py-2.5 px-3 text-center">{row.mlro ? <span className="text-accent font-bold">✓</span> : <span className="text-muted opacity-40">-</span>}</td>
                      <td className="py-2.5 px-3 text-center">{row.auditor ? <span className="text-accent font-bold">✓</span> : <span className="text-muted opacity-40">-</span>}</td>
                      <td className="py-2.5 px-3 text-center">{row.admin ? <span className="text-accent font-bold">✓</span> : <span className="text-muted opacity-40">-</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 3: Shifts */}
        {activeTab === 'SHIFTS' && (
          <div className="p-4 rounded-lg bg-surface-muted border border-border font-mono text-xs space-y-2">
            <div className="font-bold text-ink">Active Shift Routing Architecture</div>
            <div className="text-muted text-[11px] leading-relaxed">
              When an urgent P1 statutory freeze occurs, the EDITH notification router checks the live HR shift schedule. It automatically bypasses off-duty personnel and delivers high-priority alerts to logged-in on-duty officers to eliminate any SLA response delays.
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
