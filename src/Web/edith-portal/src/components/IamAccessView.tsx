import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { usePageMeta } from '../config/pageMeta';
import { 
  Users, 
  ShieldCheck, 
  Search, 
  UserPlus, 
  Check, 
  X, 
  Trash2, 
  RefreshCw, 
  Mail, 
  Copy, 
  Send, 
  AlertCircle, 
  CheckCircle2, 
  FolderTree, 
  GitMerge 
} from 'lucide-react';
import { UserRole, IamUserRecord, IamGroupRecord } from '../types';
import { EdithApi } from '../services/api';
import { ShimmerButton } from './magicui/ShimmerButton';
import { GridPattern } from './magicui/GridPattern';
import { EdithLoader } from './EdithLogo';
import { reportError } from '../services/errorEngine';

interface SystemMenuNode {
  code: string;
  name: string;
  path: string;
  subMenus?: { code: string; name: string; path: string }[];
}

const SYSTEM_12_MENUS: SystemMenuNode[] = [
  { code: 'MENU_DASHBOARD', name: '1. Home / Dashboard', path: '/dashboard' },
  {
    code: 'MENU_BLACKLIST',
    name: '2. Blacklist',
    path: '/blacklist/screening',
    subMenus: [
      { code: 'MENU_BLACKLIST_SCREENING', name: 'Sanction Screening', path: '/blacklist/screening' },
      { code: 'MENU_BLACKLIST_WATCHLIST', name: 'Add/Edit Watch list', path: '/blacklist/watchlist' },
      { code: 'MENU_BLACKLIST_BULK', name: 'Bulk Screening', path: '/blacklist/bulk' }
    ]
  },
  { code: 'MENU_WHITELIST', name: '3. White List', path: '/whitelist' },
  {
    code: 'MENU_AML',
    name: '4. AML',
    path: '/aml/rules',
    subMenus: [
      { code: 'MENU_AML_RULES', name: 'Customize Rules', path: '/aml/rules' },
      { code: 'MENU_AML_PROCESS', name: 'Process AML', path: '/aml/process' },
      { code: 'MENU_AML_STR_HISTORY', name: 'STR History', path: '/aml/str-history' },
      { code: 'MENU_AML_MLRO_MONITORING', name: 'ISTR MLRO Monitoring', path: '/aml/mlro-monitoring' }
    ]
  },
  {
    code: 'MENU_STR_PROCESS',
    name: '5. STR Process',
    path: '/str-process/alerts',
    subMenus: [
      { code: 'MENU_STR_ALERTS', name: 'Alerts', path: '/str-process/alerts' },
      { code: 'MENU_STR_EDD_ANALYST', name: 'EDD ANALYST & STR Branch [KYC]', path: '/str-process/edd-analyst' },
      { code: 'MENU_STR_ISTR_MLRO', name: 'ISTR MLRO (Freeze/Unfreeze)', path: '/str-process/istr-mlro' },
      { code: 'MENU_STR_EDD_MONITORING', name: 'EDD Analyst Monitoring', path: '/str-process/edd-monitoring' }
    ]
  },
  {
    code: 'MENU_REPORTS',
    name: '6. Reports',
    path: '/reports/alerts',
    subMenus: [
      { code: 'MENU_REPORTS_ALERTS', name: 'Alert Reports', path: '/reports/alerts' },
      { code: 'MENU_REPORTS_TRANSACTIONS', name: 'Transaction Report', path: '/reports/transactions' },
      { code: 'MENU_REPORTS_BLACKLIST', name: 'Blacklist Report (WC/PEP)', path: '/reports/blacklist' },
      { code: 'MENU_REPORTS_RELEASES', name: 'Transaction Release Report', path: '/reports/releases' },
      { code: 'MENU_REPORTS_ONLINE_STR', name: 'Online STR Report', path: '/reports/online-str' },
      { code: 'MENU_REPORTS_ANALYTICS', name: 'Transaction Analytics Report', path: '/reports/analytics' }
    ]
  },
  {
    code: 'MENU_LOG_REPORT',
    name: '7. Log Report',
    path: '/log-report/process',
    subMenus: [
      { code: 'MENU_LOG_PROCESS', name: 'Process Log', path: '/log-report/process' },
      { code: 'MENU_LOG_UPLOAD', name: 'Upload Log', path: '/log-report/upload' },
      { code: 'MENU_LOG_SUSPICIOUS', name: 'Suspicious Log', path: '/log-report/suspicious' },
      { code: 'MENU_LOG_CUSTOM', name: 'Custom Reports', path: '/log-report/custom' },
      { code: 'MENU_LOG_MONTHLY_SCREEN', name: 'Monthly Screen Data Report', path: '/log-report/monthly-screen' }
    ]
  },
  {
    code: 'MENU_SETTINGS',
    name: '8. Settings',
    path: '/settings/users',
    subMenus: [
      { code: 'MENU_SETTINGS_GROUPS', name: 'User Groups', path: '/settings/user-groups' },
      { code: 'MENU_SETTINGS_USERS', name: 'Users', path: '/settings/users' },
      { code: 'MENU_SETTINGS_MENU_MASTER', name: 'Menu Master', path: '/settings/menu-master' },
      { code: 'MENU_SETTINGS_MENU_MAPPINGS', name: 'Menu Mappings', path: '/settings/menu-mappings' },
      { code: 'MENU_SETTINGS_RISK_RATES', name: 'Risk Rate Settings', path: '/settings/risk-rates' },
      { code: 'MENU_SETTINGS_SCREENING_SCORE', name: 'Screening Score', path: '/settings/screening-score' },
      { code: 'MENU_SETTINGS_SETUP_CONTROL', name: 'Setup Control', path: '/settings/setup-control' },
      { code: 'MENU_SETTINGS_CHANGE_PASSWORD', name: 'Change Password', path: '/settings/change-password' },
      { code: 'MENU_SETTINGS_RISK_ASSESSMENT', name: 'Risk Assessment Master', path: '/settings/risk-assessment' }
    ]
  },
  {
    code: 'MENU_MASTER',
    name: '9. Master',
    path: '/master/country',
    subMenus: [
      { code: 'MENU_MASTER_NATIONALITY', name: 'Nationality Master', path: '/master/nationality' },
      { code: 'MENU_MASTER_COUNTRY', name: 'Country Master', path: '/master/country' },
      { code: 'MENU_MASTER_UNBLOCK', name: 'Unblock Customer', path: '/master/unblock-customer' }
    ]
  },
  {
    code: 'MENU_AUTHORIZATION',
    name: '10. Authorization',
    path: '/authorization/sanction-alerts',
    subMenus: [
      { code: 'MENU_AUTH_SANCTION_ALERTS', name: 'Sanction Alert Authorization', path: '/authorization/sanction-alerts' },
      { code: 'MENU_AUTH_RISK_CALC', name: 'Risk Calculate', path: '/authorization/risk-calculate' },
      { code: 'MENU_AUTH_CUSTOMER', name: 'Customer Authorization', path: '/authorization/customer' }
    ]
  },
  {
    code: 'MENU_CUSTOMER',
    name: '11. Customer',
    path: '/customer/kyc',
    subMenus: [
      { code: 'MENU_CUSTOMER_SCREENING', name: 'Customer Screening Report', path: '/customer/screening-report' },
      { code: 'MENU_CUSTOMER_KYC', name: 'Customer KYC', path: '/customer/kyc' }
    ]
  },
  {
    code: 'MENU_DELTA',
    name: '12. Delta',
    path: '/delta/authorization',
    subMenus: [
      { code: 'MENU_DELTA_AUTHORIZATION', name: 'Authorization', path: '/delta/authorization' },
      { code: 'MENU_DELTA_LOG_REPORT', name: 'Log Report', path: '/delta/log-report' }
    ]
  }
];

interface GroupMappingRow {
  menuCode: string;
  menuName: string;
  admin: boolean;
  amlOfficer: boolean;
  eddAnalyst: boolean;
  istrMlro: boolean;
}

interface IamAccessViewProps {
  initialTab?: 'USERS' | 'GROUPS' | 'MENU_MASTER' | 'MENU_MAPPINGS';
}

export const IamAccessView: React.FC<IamAccessViewProps> = ({ initialTab = 'USERS' }) => {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<'USERS' | 'GROUPS' | 'MENU_MASTER' | 'MENU_MAPPINGS'>(initialTab);

  useEffect(() => {
    if (initialTab) {
      setActiveTab(initialTab);
    }
  }, [initialTab]);

  const [users, setUsers] = useState<IamUserRecord[]>([]);
  const [mappings, setMappings] = useState<GroupMappingRow[]>([]);
  const [groups, setGroups] = useState<IamGroupRecord[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [loading, setLoading] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  // User Provisioning Modal
  const [showProvisionModal, setShowProvisionModal] = useState(false);
  const [newUserEmail, setNewUserEmail] = useState('');
  const [newUserName, setNewUserName] = useState('');
  const [newUserRole, setNewUserRole] = useState<UserRole>('AML_OFFICER');
  const [newUserDept, setNewUserDept] = useState('Financial Crime Compliance');
  const [autoGeneratePassword, setAutoGeneratePassword] = useState(true);
  const [customPassword, setCustomPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Creation Result Dialog (displays auto-generated password and Gmail SMTP status)
  const [creationResult, setCreationResult] = useState<{
    user: IamUserRecord;
    tempPassword?: string;
    emailStatus: string;
    emailError?: string;
  } | null>(null);

  useEffect(() => {
    loadUsers();
    loadMappings();
  }, []);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (showProvisionModal) setShowProvisionModal(false);
        if (creationResult) setCreationResult(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [showProvisionModal, creationResult]);

  const loadUsers = async () => {
    setLoading(true);
    try {
      const fetched = await EdithApi.fetchIamUsers();
      if (fetched) {
        setUsers(fetched);
      }
    } catch (err) {
      reportError(err, { category: 'SERVER' });
    } finally {
      setLoading(false);
    }
  };

  const loadMappings = async () => {
    try {
      const [fetchedGroups, fetchedMenus] = await Promise.all([
        EdithApi.fetchIamGroups(),
        EdithApi.fetchMenus()
      ]);
      setGroups(fetchedGroups || []);

      if (fetchedGroups && fetchedGroups.length > 0 && fetchedMenus && fetchedMenus.length > 0) {
        const mainMenus = fetchedMenus.filter((m: any) => !m.parentMenuId);
        const adminGrp = fetchedGroups.find(g => g.groupName.includes('ADMIN'));
        const amlGrp = fetchedGroups.find(g => g.groupName.includes('AML'));
        const eddGrp = fetchedGroups.find(g => g.groupName.includes('EDD'));
        const mlroGrp = fetchedGroups.find(g => g.groupName.includes('MLRO'));

        const rows: GroupMappingRow[] = mainMenus.map((m: any) => {
          const code = m.menuCode || m.menuName;
          const adminHas = adminGrp ? adminGrp.permissions.some(p => (p.menuId === code || p.menuId === m.id) && p.canView) : true;
          const amlHas = amlGrp ? amlGrp.permissions.some(p => (p.menuId === code || p.menuId === m.id) && p.canView) : false;
          const eddHas = eddGrp ? eddGrp.permissions.some(p => (p.menuId === code || p.menuId === m.id) && p.canView) : false;
          const mlroHas = mlroGrp ? mlroGrp.permissions.some(p => (p.menuId === code || p.menuId === m.id) && p.canView) : false;

          return {
            menuCode: code,
            menuName: m.menuName,
            admin: adminHas,
            amlOfficer: amlHas,
            eddAnalyst: eddHas,
            istrMlro: mlroHas
          };
        });
        if (rows.length > 0) {
          setMappings(rows);
        }
      }
    } catch (err) {
      console.error('Failed to load menu mappings:', err);
    }
  };

  const toggleMapping = async (index: number, role: 'admin' | 'amlOfficer' | 'eddAnalyst' | 'istrMlro') => {
    const row = mappings[index];
    if (!row) return;

    const nextVal = !row[role];
    setMappings(prev => prev.map((r, idx) => idx === index ? { ...r, [role]: nextVal } : r));

    const groupNameKey = role === 'admin' ? 'ADMIN'
      : role === 'amlOfficer' ? 'AML'
      : role === 'eddAnalyst' ? 'EDD'
      : 'MLRO';

    const targetGroup = groups.find(g => g.groupName.includes(groupNameKey));
    if (targetGroup) {
      const currentPerms = targetGroup.permissions ? [...targetGroup.permissions] : [];
      const existingIdx = currentPerms.findIndex(p => p.menuId === row.menuCode);
      if (nextVal) {
        if (existingIdx >= 0) {
          currentPerms[existingIdx] = { ...currentPerms[existingIdx], canView: true };
        } else {
          currentPerms.push({ menuId: row.menuCode, canView: true, canEdit: true, canAuthorize: role === 'istrMlro' || role === 'admin' });
        }
      } else {
        if (existingIdx >= 0) {
          currentPerms.splice(existingIdx, 1);
        }
      }

      try {
        await EdithApi.updateIamGroupPermissions(targetGroup.id, currentPerms);
        setStatusMessage(`Updated permissions for ${targetGroup.groupName} in PostgreSQL.`);
      } catch (err: any) {
        setStatusMessage(`Failed to update permissions: ${err.message || 'Unknown error'}`);
      }
    } else {
      setStatusMessage('Updated dynamic menu mapping permissions.');
    }
    setTimeout(() => setStatusMessage(null), 3000);
  };

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUserEmail.trim()) return;

    setIsSubmitting(true);
    try {
      const res = await EdithApi.createIamUser({
        username: newUserEmail.trim(),
        fullName: newUserName.trim() || undefined,
        role: newUserRole,
        department: newUserDept.trim(),
        password: autoGeneratePassword ? undefined : customPassword.trim(),
        shiftStatus: 'ON_DUTY'
      });

      if (res && res.user) {
        setUsers(prev => [res.user, ...prev.filter(u => u.id !== res.user.id)]);
        setCreationResult({
          user: res.user,
          tempPassword: res.generatedTemporaryPassword,
          emailStatus: res.emailStatus,
          emailError: res.emailError
        });
        setStatusMessage(`Provisioned operator ${res.user.fullName || res.user.username} successfully.`);
      } else if (res && res.id) {
        setUsers(prev => [res, ...prev.filter(u => u.id !== res.id)]);
        setCreationResult({
          user: res,
          tempPassword: autoGeneratePassword ? 'EdithCompliance2026!' : customPassword,
          emailStatus: 'SENT',
          emailError: undefined
        });
        setStatusMessage(`Provisioned operator ${res.fullName || res.username} successfully.`);
      }

      setShowProvisionModal(false);
      setNewUserEmail('');
      setNewUserName('');
      setCustomPassword('');
    } catch (err) {
      reportError(err, { category: 'SERVER' });
      setStatusMessage('Unable to complete user provisioning right now. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResendInvite = async (user: IamUserRecord) => {
    try {
      setStatusMessage(`Sending activation invitation to ${user.username}...`);
      const res = await EdithApi.resendUserInvite(user.id);
      if (res && res.success) {
        setCreationResult({
          user,
          tempPassword: res.temporaryPassword,
          emailStatus: res.emailStatus,
          emailError: res.emailError
        });
        setStatusMessage(`Onboarding invitation sent to ${user.username}.`);
      } else {
        setStatusMessage('Invitation email could not be delivered automatically. Please share the temporary credentials manually.');
      }
    } catch (err) {
      reportError(err, { category: 'NETWORK' });
      setStatusMessage('Unable to send invitation right now. Please try again.');
    }
  };

  const handleDeleteUser = async (id: string, name: string) => {
    if (!window.confirm(`Revoke clearance and de-provision officer ${name}?`)) return;
    try {
      await EdithApi.deleteIamUser(id);
      setUsers(prev => prev.filter(u => u.id !== id));
      setStatusMessage(`De-provisioned officer ${name}. Access revoked.`);
      setTimeout(() => setStatusMessage(null), 3500);
    } catch {
      // ignore
    }
  };

  const toggleUserShift = async (user: IamUserRecord) => {
    const nextShift = user.shiftStatus === 'ON_DUTY' ? 'OFF_DUTY' : 'ON_DUTY';
    try {
      await EdithApi.updateIamUser(user.id, { shiftStatus: nextShift });
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, shiftStatus: nextShift } : u));
    } catch {
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, shiftStatus: nextShift } : u));
    }
  };

  const filteredUsers = users.filter(u => 
    u.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.username.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.role.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const pageMeta = usePageMeta(
    'Users',
    'Provision compliance officers, manage 2FA enrollment, and control shift duty status.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="relative overflow-hidden flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-white p-4 rounded-2xl border border-border shadow-xs">
        <GridPattern className="opacity-40" width={24} height={24} />
        
        <div className="relative z-10 min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <Users className="w-5 h-5 text-primary shrink-0" />
              <span>{pageMeta.title}</span>
            </h1>
            {pageMeta.parent && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {pageMeta.parent}
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1">
            {pageMeta.definition}
          </p>
        </div>

        <div className="relative z-10 flex items-center gap-2 shrink-0 w-full sm:w-auto justify-end">
          <button
            type="button"
            onClick={loadUsers}
            className="p-2.5 rounded-xl bg-slate-50 text-ink border border-border hover:bg-slate-100 transition text-xs font-mono cursor-pointer shadow-xs"
            title="Refresh Users"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          
          <ShimmerButton
            onClick={() => setShowProvisionModal(true)}
            className="rounded-xl shadow-md py-2.5 px-4"
          >
            <UserPlus className="w-4 h-4" />
            <span>Provision Operator</span>
          </ShimmerButton>
        </div>
      </div>

      {(loading || isSubmitting) && (
        <EdithLoader
          overlay
          size="md"
          label={isSubmitting ? 'PROVISIONING ACCOUNT...' : 'LOADING ACCOUNTS...'}
          sublabel="Updating user access settings, please wait"
        />
      )}

      {statusMessage && (
        <div className="p-3 rounded-lg bg-accent/15 border border-accent/30 text-accent text-xs font-mono flex items-center justify-between animate-in fade-in duration-150">
          <span>{statusMessage}</span>
          <button onClick={() => setStatusMessage(null)}>
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Tab Navigation */}
      <div className="flex items-center gap-1.5 border-b border-border pb-px overflow-x-auto no-scrollbar">
        <button
          type="button"
          onClick={() => {
            setActiveTab('USERS');
            navigate('/settings/users');
          }}
          className={`px-4 py-2 text-xs font-bold font-mono transition-all border-b-2 whitespace-nowrap cursor-pointer ${
            activeTab === 'USERS'
              ? 'border-primary text-primary bg-primary/5 rounded-t-lg font-bold'
              : 'border-transparent text-muted hover:text-ink hover:bg-slate-50 rounded-t-lg'
          }`}
        >
          Users ({users.length})
        </button>
        <button
          type="button"
          onClick={() => {
            setActiveTab('GROUPS');
            navigate('/settings/user-groups');
          }}
          className={`px-4 py-2 text-xs font-bold font-mono transition-all border-b-2 whitespace-nowrap cursor-pointer ${
            activeTab === 'GROUPS'
              ? 'border-primary text-primary bg-primary/5 rounded-t-lg font-bold'
              : 'border-transparent text-muted hover:text-ink hover:bg-slate-50 rounded-t-lg'
          }`}
        >
          User Groups (4)
        </button>
        <button
          type="button"
          onClick={() => {
            setActiveTab('MENU_MASTER');
            navigate('/settings/menu-master');
          }}
          className={`px-4 py-2 text-xs font-bold font-mono transition-all border-b-2 whitespace-nowrap cursor-pointer ${
            activeTab === 'MENU_MASTER'
              ? 'border-primary text-primary bg-primary/5 rounded-t-lg font-bold'
              : 'border-transparent text-muted hover:text-ink hover:bg-slate-50 rounded-t-lg'
          }`}
        >
          Menu Master (12 Menus)
        </button>
        <button
          type="button"
          onClick={() => {
            setActiveTab('MENU_MAPPINGS');
            navigate('/settings/menu-mappings');
          }}
          className={`px-4 py-2 text-xs font-bold font-mono transition-all border-b-2 whitespace-nowrap cursor-pointer ${
            activeTab === 'MENU_MAPPINGS'
              ? 'border-primary text-primary bg-primary/5 rounded-t-lg font-bold'
              : 'border-transparent text-muted hover:text-ink hover:bg-slate-50 rounded-t-lg'
          }`}
        >
          Menu Mappings (Matrix)
        </button>
      </div>

      {/* TAB 1: USERS DIRECTORY */}
      {activeTab === 'USERS' && (
        <div className="space-y-4">
          <div className="flex items-center gap-3 bg-white p-3 rounded-xl border border-border shadow-xs">
            <Search className="w-4 h-4 text-muted shrink-0" />
            <input
              type="text"
              placeholder="Search by officer name, email ID, role, or department..."
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="w-full text-xs bg-transparent border-none outline-none text-ink placeholder-muted font-mono"
            />
            {searchTerm && (
              <button
                type="button"
                onClick={() => setSearchTerm('')}
                className="p-1 rounded hover:bg-slate-100 text-muted hover:text-ink cursor-pointer"
                title="Clear search"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
            <span className="text-[10px] font-mono text-muted px-2 py-0.5 rounded bg-slate-100 border border-border shrink-0">
              {filteredUsers.length} of {users.length}
            </span>
          </div>

          <div className="bg-surface rounded-xl border border-border overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-muted border-b border-border text-[10px] font-mono uppercase text-muted font-bold">
                    <th className="p-3">Officer & Credentials</th>
                    <th className="p-3">Assigned Role</th>
                    <th className="p-3">Security & 2FA Status</th>
                    <th className="p-3">Department</th>
                    <th className="p-3">Duty Status</th>
                    <th className="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border text-xs">
                  {filteredUsers.map(user => (
                    <tr key={user.id} className="hover:bg-white/50 transition">
                      <td className="p-3">
                        <div className="font-bold text-ink">{user.fullName}</div>
                        <div className="text-[11px] font-mono text-muted">{user.username}</div>
                      </td>
                      <td className="p-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ${
                          user.role === 'SUPER_ADMIN' ? 'bg-purple-100 text-purple-700 border-purple-200' :
                          user.role === 'ADMIN' ? 'bg-blue-100 text-blue-700 border-blue-200' :
                          user.role === 'ISTR_MLRO' || user.role === 'MLRO' ? 'bg-red-100 text-red-700 border-red-200' :
                          user.role === 'EDD_ANALYST' ? 'bg-amber-100 text-amber-700 border-amber-200' :
                          'bg-emerald-100 text-emerald-700 border-emerald-200'
                        }`}>
                          {user.role}
                        </span>
                      </td>
                      <td className="p-3">
                        {user.mfaEnabled ? (
                          <div className="flex items-center gap-1.5 text-accent font-mono text-[11px] font-bold">
                            <ShieldCheck className="w-4 h-4" />
                            <span>2FA Enrolled (Active)</span>
                          </div>
                        ) : (
                          <div className="flex items-center gap-1.5 text-warning font-mono text-[11px] font-bold">
                            <AlertCircle className="w-4 h-4" />
                            <span>2FA Pending Activation</span>
                          </div>
                        )}
                        <div className="text-[10px] font-mono text-muted mt-0.5">
                          {user.mustChangePassword ? 'Requires password rotation' : 'Password active'}
                        </div>
                      </td>
                      <td className="p-3 text-muted text-[11px]">
                        {user.department}
                      </td>
                      <td className="p-3">
                        <button
                          onClick={() => toggleUserShift(user)}
                          className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold flex items-center gap-1.5 border transition ${
                            user.shiftStatus === 'ON_DUTY'
                              ? 'bg-accent/15 text-accent border-accent/30 hover:bg-accent/25'
                              : 'bg-muted/15 text-muted border-border hover:bg-muted/25'
                          }`}
                        >
                          <span className={`w-1.5 h-1.5 rounded-full ${user.shiftStatus === 'ON_DUTY' ? 'bg-accent' : 'bg-muted'}`} />
                          {user.shiftStatus}
                        </button>
                      </td>
                      <td className="p-3 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            onClick={() => handleResendInvite(user)}
                            className="p-1.5 text-primary hover:bg-primary/10 rounded transition text-xs flex items-center gap-1"
                            title="Resend Activation Email via Gmail SMTP"
                          >
                            <Mail className="w-3.5 h-3.5" />
                            <span className="text-[10px] font-mono font-bold hidden sm:inline">Invite</span>
                          </button>
                          <button
                            onClick={() => handleDeleteUser(user.id, user.fullName)}
                            className="p-1.5 text-critical hover:bg-critical/10 rounded transition"
                            title="Revoke clearance"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: USER GROUPS */}
      {activeTab === 'GROUPS' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="p-5 bg-surface rounded-xl border border-border space-y-3">
            <div className="flex items-center justify-between">
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-blue-100 text-blue-700 border border-blue-200">
                SYSTEM GROUP
              </span>
              <span className="text-[10px] font-mono text-muted">12/12 MENUS</span>
            </div>
            <h3 className="font-bold text-ink text-sm">ADMINISTRATOR_GROUP</h3>
            <p className="text-xs text-muted">
              Full access to all 12 menus, Settings (User Groups, Users, Menu Master, Menu Mappings, Risk Rates, Screening Score, Setup Control), Master, and Authorization.
            </p>
          </div>

          <div className="p-5 bg-surface rounded-xl border border-border space-y-3">
            <div className="flex items-center justify-between">
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-100 text-emerald-700 border border-emerald-200">
                OPERATIONAL
              </span>
              <span className="text-[10px] font-mono text-muted">6/12 MENUS</span>
            </div>
            <h3 className="font-bold text-ink text-sm">AML_AUTHORIZED_OFFICERS</h3>
            <p className="text-xs text-muted">
              Access to Authorization, STR Alerts, AML (Customize Rules, Process AML, STR History), Blacklist, Whitelist, and Delta Authorization.
            </p>
          </div>

          <div className="p-5 bg-surface rounded-xl border border-border space-y-3">
            <div className="flex items-center justify-between">
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-100 text-amber-700 border border-amber-200">
                INVESTIGATIONS
              </span>
              <span className="text-[10px] font-mono text-muted">4/12 MENUS</span>
            </div>
            <h3 className="font-bold text-ink text-sm">EDD_INVESTIGATION_DESK</h3>
            <p className="text-xs text-muted">
              Access to STR Process (EDD Analyst with KYC Details window, EDD Analyst Monitoring with 3-day / 7-day auto-escalation), Customer Screening Report, and Customer KYC.
            </p>
          </div>

          <div className="p-5 bg-surface rounded-xl border border-border space-y-3">
            <div className="flex items-center justify-between">
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-red-100 text-red-700 border border-red-200">
                REGULATORY
              </span>
              <span className="text-[10px] font-mono text-muted">6/12 MENUS</span>
            </div>
            <h3 className="font-bold text-ink text-sm">MLRO_APPROVAL_DEPT</h3>
            <p className="text-xs text-muted">
              Supervises suspicious STR cases, reviews KYC details, monitors STR history, views Reports and Log Reports. Holds exclusive Freeze and Unfreeze authority.
            </p>
          </div>
        </div>
      )}

      {/* TAB 3: MENU MASTER */}
      {activeTab === 'MENU_MASTER' && (
        <div className="bg-surface rounded-xl border border-border p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-bold text-ink text-sm flex items-center gap-2">
              <FolderTree className="w-4 h-4 text-primary" />
              Complete 12 Main Menus & Sub-Menu Architecture
            </h3>
            <span className="text-[10px] font-mono text-muted">Source: core.menus</span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {SYSTEM_12_MENUS.map(menu => (
              <div key={menu.code} className="p-4 bg-white rounded-xl border border-border space-y-2.5 shadow-xs">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs font-bold text-ink truncate">{menu.name}</span>
                  <span className="px-1.5 py-0.5 rounded text-[9px] font-mono bg-surface text-muted border border-border shrink-0">
                    {menu.path}
                  </span>
                </div>

                {menu.subMenus && (
                  <div className="pl-3 border-l-2 border-primary/30 space-y-1.5">
                    {menu.subMenus.map(sub => (
                      <div key={sub.code} className="text-[11px] text-muted flex items-center justify-between gap-2 font-mono">
                        <span className="truncate">• {sub.name}</span>
                        <span className="text-[9px] text-slate-400 shrink-0">{sub.path}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 4: MENU MAPPINGS MATRIX */}
      {activeTab === 'MENU_MAPPINGS' && (
        <div className="bg-surface rounded-xl border border-border overflow-hidden">
          <div className="p-4 bg-surface-muted border-b border-border flex items-center justify-between">
            <div>
              <h3 className="text-sm font-bold text-ink flex items-center gap-2">
                <GitMerge className="w-4 h-4 text-primary" />
                Menu Mappings by User Role Group
              </h3>
              <p className="text-xs text-muted mt-0.5">
                Configures which main menus and sub-menus appear in the navigation bar for each group.
              </p>
            </div>
            <span className="px-2 py-1 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30">
              LIVE POSTGRESQL SYNC
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-white border-b border-border text-[10px] font-mono uppercase text-muted font-bold">
                  <th className="p-3">System Menu</th>
                  <th className="p-3 text-center">Administrator (Admin)</th>
                  <th className="p-3 text-center">AML Officer</th>
                  <th className="p-3 text-center">EDD Analyst</th>
                  <th className="p-3 text-center">ISTR MLRO</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border text-xs">
                {mappings.map((row, idx) => (
                  <tr key={row.menuCode} className="hover:bg-white/50 transition">
                    <td className="p-3 font-medium text-ink">
                      {row.menuName}
                    </td>
                    <td className="p-3 text-center">
                      <button
                        type="button"
                        onClick={() => toggleMapping(idx, 'admin')}
                        className={`w-6 h-6 rounded inline-flex items-center justify-center transition ${
                          row.admin ? 'bg-primary text-white' : 'bg-surface border border-border text-muted'
                        }`}
                      >
                        {row.admin ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                      </button>
                    </td>
                    <td className="p-3 text-center">
                      <button
                        type="button"
                        onClick={() => toggleMapping(idx, 'amlOfficer')}
                        className={`w-6 h-6 rounded inline-flex items-center justify-center transition ${
                          row.amlOfficer ? 'bg-primary text-white' : 'bg-surface border border-border text-muted'
                        }`}
                      >
                        {row.amlOfficer ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                      </button>
                    </td>
                    <td className="p-3 text-center">
                      <button
                        type="button"
                        onClick={() => toggleMapping(idx, 'eddAnalyst')}
                        className={`w-6 h-6 rounded inline-flex items-center justify-center transition ${
                          row.eddAnalyst ? 'bg-primary text-white' : 'bg-surface border border-border text-muted'
                        }`}
                      >
                        {row.eddAnalyst ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                      </button>
                    </td>
                    <td className="p-3 text-center">
                      <button
                        type="button"
                        onClick={() => toggleMapping(idx, 'istrMlro')}
                        className={`w-6 h-6 rounded inline-flex items-center justify-center transition ${
                          row.istrMlro ? 'bg-primary text-white' : 'bg-surface border border-border text-muted'
                        }`}
                      >
                        {row.istrMlro ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* PROVISION OPERATOR MODAL */}
      {showProvisionModal && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
          {/* Backdrop */}
          <div 
            onClick={() => setShowProvisionModal(false)}
            className="fixed inset-0 bg-slate-950/60 backdrop-blur-xs transition-opacity duration-200" 
          />

          <div 
            role="dialog"
            aria-modal="true"
            aria-label="Provision Compliance Operator"
            className="relative z-10 w-full max-w-lg bg-white rounded-2xl border border-border shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150"
          >
            <div className="p-5 bg-[#0f172a] text-white flex items-center justify-between">
              <div>
                <h3 className="font-bold text-sm flex items-center gap-2">
                  <UserPlus className="w-4 h-4 text-primary-light" />
                  <span>Provision Compliance Operator</span>
                </h3>
                <p className="text-[11px] font-mono text-slate-400 mt-0.5">Automated Gmail SMTP Activation &amp; Mandatory 2FA</p>
              </div>
              <button
                type="button"
                onClick={() => setShowProvisionModal(false)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-white/10 transition cursor-pointer"
                title="Close"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateUser} className="p-6 space-y-4 font-mono text-xs">
              <div>
                <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                  Email / Login ID (Recipient) <span className="text-critical">*</span>
                </label>
                <input
                  type="email"
                  required
                  placeholder="officer.name@adcb.ae"
                  value={newUserEmail}
                  onChange={e => setNewUserEmail(e.target.value)}
                  className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition font-mono"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                  Full Name
                </label>
                <input
                  type="text"
                  placeholder="e.g. Tariq Mansoor"
                  value={newUserName}
                  onChange={e => setNewUserName(e.target.value)}
                  className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                />
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                    Assigned Role
                  </label>
                  <select
                    value={newUserRole}
                    onChange={e => setNewUserRole(e.target.value as UserRole)}
                    className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition font-mono cursor-pointer"
                  >
                    <option value="ADMIN">ADMIN (Full Access)</option>
                    <option value="AML_OFFICER">AML Officer</option>
                    <option value="EDD_ANALYST">EDD Analyst</option>
                    <option value="ISTR_MLRO">ISTR MLRO</option>
                    <option value="SUPER_ADMIN">SUPER_ADMIN</option>
                  </select>
                </div>

                <div>
                  <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                    Department
                  </label>
                  <input
                    type="text"
                    value={newUserDept}
                    onChange={e => setNewUserDept(e.target.value)}
                    className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                  />
                </div>
              </div>

              {/* Auto-generate password toggle */}
              <div className="p-3.5 bg-[#faf8ff] rounded-xl border border-border space-y-2">
                <label className="flex items-center gap-2.5 text-xs text-ink cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={autoGeneratePassword}
                    onChange={e => setAutoGeneratePassword(e.target.checked)}
                    className="rounded border-border text-primary focus:ring-0 w-4 h-4 cursor-pointer"
                  />
                  <span className="font-bold">Auto-generate secure password &amp; dispatch via Gmail SMTP</span>
                </label>
                <p className="text-[10px] text-muted pl-6 leading-relaxed">
                  System generates a 16-character high-entropy password. User will receive an activation email with mandatory 2FA enrollment.
                </p>
              </div>

              {!autoGeneratePassword && (
                <div>
                  <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                    Specify Temporary Password
                  </label>
                  <input
                    type="password"
                    value={customPassword}
                    onChange={e => setCustomPassword(e.target.value)}
                    placeholder="Min 8 characters"
                    className="w-full px-3.5 py-2.5 text-xs font-mono border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                  />
                </div>
              )}

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowProvisionModal(false)}
                  className="px-4 py-2.5 border border-border text-ink text-xs font-bold rounded-xl hover:bg-slate-100 transition cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="flex-1 py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition flex items-center justify-center gap-2 shadow-xs disabled:opacity-50 cursor-pointer"
                >
                  {isSubmitting ? (
                    <>
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      <span>Sending Email...</span>
                    </>
                  ) : (
                    <>
                      <Send className="w-3.5 h-3.5" />
                      <span>Provision &amp; Send Email</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>,
        document.body
      )}

      {/* CREATION RESULT / CREDENTIALS DIALOG */}
      {creationResult && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
          {/* Backdrop */}
          <div 
            onClick={() => setCreationResult(null)}
            className="fixed inset-0 bg-slate-950/60 backdrop-blur-xs transition-opacity duration-200" 
          />

          <div 
            role="dialog"
            aria-modal="true"
            aria-label="Operator Provisioned Result"
            className="relative z-10 w-full max-w-lg bg-white rounded-2xl border border-border shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150"
          >
            <div className="p-5 bg-[#0f172a] text-white flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <CheckCircle2 className="w-5 h-5 text-accent" />
                <div>
                  <h3 className="font-bold text-sm">Operator Provisioned</h3>
                  <p className="text-[11px] font-mono text-slate-400">{creationResult.user.username}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setCreationResult(null)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-white/10 transition cursor-pointer"
                title="Close"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4 font-mono text-xs">
              {/* Email Delivery Status Badge */}
              <div className={`p-3.5 rounded-xl border text-xs flex items-start gap-2.5 ${
                creationResult.emailStatus === 'SENT'
                  ? 'bg-accent/10 border-accent/30 text-accent'
                  : 'bg-warning/10 border-warning/30 text-warning'
              }`}>
                <Mail className="w-4 h-4 shrink-0 mt-0.5" />
                <div>
                  <div className="font-bold font-mono">
                    {creationResult.emailStatus === 'SENT' ? 'Activation Email Delivered' : 'Delivery Notice'}
                  </div>
                  <div className="text-[11px] mt-0.5 leading-relaxed">
                    {creationResult.emailStatus === 'SENT'
                      ? `Sent invitation to ${creationResult.user.username}.`
                      : 'Email could not be delivered automatically. Please copy and share the temporary credentials below.'}
                  </div>
                </div>
              </div>

              {/* Temporary Password Box */}
              {creationResult.tempPassword && (
                <div className="p-4 bg-[#faf8ff] rounded-xl border border-border space-y-2">
                  <div className="text-[11px] font-bold text-muted uppercase tracking-wider flex items-center justify-between">
                    <span>Temporary Password (Shown Once):</span>
                    <button
                      type="button"
                      onClick={() => {
                        navigator.clipboard.writeText(creationResult.tempPassword || '');
                        setStatusMessage('Copied temporary password to clipboard.');
                        setTimeout(() => setStatusMessage(null), 2000);
                      }}
                      className="text-primary hover:underline flex items-center gap-1 font-mono text-[10px] cursor-pointer"
                    >
                      <Copy className="w-3.5 h-3.5" />
                      <span>Copy</span>
                    </button>
                  </div>
                  <div className="px-3.5 py-2.5 bg-white rounded-lg border border-border font-mono font-bold text-sm text-primary select-all shadow-2xs">
                    {creationResult.tempPassword}
                  </div>
                </div>
              )}

              <p className="text-xs text-muted leading-relaxed">
                The user must rotate this temporary password and scan their 2FA QR code in Google Authenticator or Microsoft Authenticator before accessing operational workspaces.
              </p>

              <button
                type="button"
                onClick={() => setCreationResult(null)}
                className="w-full py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition shadow-xs cursor-pointer"
              >
                Done
              </button>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
};
