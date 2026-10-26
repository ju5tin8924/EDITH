import React, { useState, useEffect, useMemo } from 'react';
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
  GitMerge,
  Download,
  Filter,
  ChevronDown,
  ChevronRight,
  Edit2,
  ShieldAlert,
  Activity,
  LogOut,
  Clock,
  UserX,
  Layers,
  Shield
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

interface SubMappingRow {
  code: string;
  name: string;
  path: string;
  admin: boolean;
  amlOfficer: boolean;
  eddAnalyst: boolean;
  istrMlro: boolean;
}

interface GroupMappingRow {
  menuCode: string;
  menuName: string;
  admin: boolean;
  amlOfficer: boolean;
  eddAnalyst: boolean;
  istrMlro: boolean;
  subMenus?: SubMappingRow[];
}

interface ActiveSessionItem {
  id: string;
  username: string;
  fullName: string;
  role: string;
  ipAddress: string;
  device: string;
  loginTime: string;
  status: 'ACTIVE' | 'TERMINATED';
}

interface SecurityAuditItem {
  id: string;
  timestamp: string;
  actor: string;
  action: string;
  targetUser: string;
  details: string;
  severity: 'INFO' | 'WARNING' | 'CRITICAL';
}

interface IamAccessViewProps {
  initialTab?: 'USERS' | 'GROUPS' | 'MENU_MASTER' | 'MENU_MAPPINGS' | 'AUDIT_SESSIONS';
}

export const IamAccessView: React.FC<IamAccessViewProps> = ({ initialTab = 'USERS' }) => {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<'USERS' | 'GROUPS' | 'MENU_MASTER' | 'MENU_MAPPINGS' | 'AUDIT_SESSIONS'>(initialTab);

  useEffect(() => {
    if (initialTab) {
      setActiveTab(initialTab);
    }
  }, [initialTab]);

  const [users, setUsers] = useState<IamUserRecord[]>([]);
  const [mappings, setMappings] = useState<GroupMappingRow[]>([]);
  const [groups, setGroups] = useState<IamGroupRecord[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  // Faceted Filter States
  const [searchTerm, setSearchTerm] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'ON_DUTY' | 'OFF_DUTY' | 'MFA_PENDING' | 'SUSPENDED'>('ALL');
  const [deptFilter, setDeptFilter] = useState<string>('ALL');

  // Slide-Out Drawer for User Edit
  const [selectedUserForDrawer, setSelectedUserForDrawer] = useState<IamUserRecord | null>(null);
  const [drawerFullName, setDrawerFullName] = useState('');
  const [drawerDepartment, setDrawerDepartment] = useState('');
  const [drawerRole, setDrawerRole] = useState<UserRole>('AML_OFFICER');
  const [drawerIsSaving, setDrawerIsSaving] = useState(false);

  // Structured De-Provisioning & Handover Modal
  const [userToDeProvision, setUserToDeProvision] = useState<IamUserRecord | null>(null);
  const [reassigneeId, setReassigneeId] = useState<string>('');
  const [isDeProvisioning, setIsDeProvisioning] = useState(false);

  // User Provisioning Modal
  const [showProvisionModal, setShowProvisionModal] = useState(false);
  const [newUserEmail, setNewUserEmail] = useState('');
  const [newUserName, setNewUserName] = useState('');
  const [newUserRole, setNewUserRole] = useState<UserRole>('AML_OFFICER');
  const [newUserDept, setNewUserDept] = useState('Financial Crime Compliance');
  const [autoGeneratePassword, setAutoGeneratePassword] = useState(true);
  const [customPassword, setCustomPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Creation Result Dialog (with Onboarding Link)
  const [creationResult, setCreationResult] = useState<{
    user: IamUserRecord;
    tempPassword?: string;
    emailStatus: string;
    emailError?: string;
    invitationToken?: string;
  } | null>(null);

  // Menu Mapping Accordion
  const [expandedMenus, setExpandedMenus] = useState<Record<string, boolean>>({});

  // Custom Group Modal
  const [showGroupModal, setShowGroupModal] = useState(false);
  const [newGroupName, setNewGroupName] = useState('');
  const [newGroupDesc, setNewGroupDesc] = useState('');

  // Active Sessions
  const [sessions, setSessions] = useState<ActiveSessionItem[]>([
    {
      id: 'SES-001',
      username: 'admin@edith.aml.cloud',
      fullName: 'System Administrator',
      role: 'SUPER_ADMIN',
      ipAddress: '10.24.110.15 (ADCB Core VPN)',
      device: 'Edge 130 on Windows 11 Enterprise',
      loginTime: '2026-10-26 08:15:22 UTC',
      status: 'ACTIVE'
    },
    {
      id: 'SES-002',
      username: 'tariq.mansoor@adcb.ae',
      fullName: 'Tariq Mansoor',
      role: 'AML_OFFICER',
      ipAddress: '10.24.110.42 (ADCB Internal)',
      device: 'Chrome 131 on macOS Sequoia',
      loginTime: '2026-10-26 07:42:10 UTC',
      status: 'ACTIVE'
    },
    {
      id: 'SES-003',
      username: 'fatima.alzahra@adcb.ae',
      fullName: 'Fatima Al-Zahra',
      role: 'EDD_ANALYST',
      ipAddress: '10.24.112.88 (Compliance Hub)',
      device: 'Firefox 132 on Windows 11 Enterprise',
      loginTime: '2026-10-26 06:30:00 UTC',
      status: 'ACTIVE'
    },
    {
      id: 'SES-004',
      username: 'mlro.lead@adcb.ae',
      fullName: 'Dr. Rashid Al-Nuaimi',
      role: 'ISTR_MLRO',
      ipAddress: '10.24.100.5 (Executive Net)',
      device: 'Safari 18 on iPadOS 18',
      loginTime: '2026-10-26 08:00:15 UTC',
      status: 'ACTIVE'
    }
  ]);

  // Security Audit Entries
  const [securityAudits] = useState<SecurityAuditItem[]>([
    {
      id: 'AUD-881',
      timestamp: '2026-10-26 08:35:10 UTC',
      actor: 'admin@edith.aml.cloud',
      action: 'IAM_USER_PROVISIONED',
      targetUser: 'zayed.k@adcb.ae',
      details: 'Provisioned AML Officer with auto-generated credential & Gmail SMTP dispatch',
      severity: 'INFO'
    },
    {
      id: 'AUD-880',
      timestamp: '2026-10-26 08:15:22 UTC',
      actor: 'admin@edith.aml.cloud',
      action: 'IAM_MFA_AUTHENTICATED',
      targetUser: 'admin@edith.aml.cloud',
      details: 'TOTP 2FA Verification Successful (skew=0)',
      severity: 'INFO'
    },
    {
      id: 'AUD-879',
      timestamp: '2026-10-26 07:45:12 UTC',
      actor: 'admin@edith.aml.cloud',
      action: 'MENU_PERMISSIONS_SYNCED',
      targetUser: 'MLRO_APPROVAL_DEPT',
      details: 'Updated core.group_menu_permissions for Freeze/Unfreeze authority',
      severity: 'WARNING'
    },
    {
      id: 'AUD-878',
      timestamp: '2026-10-26 06:12:05 UTC',
      actor: 'system',
      action: 'IAM_SESSION_EXPIRED',
      targetUser: 'investigator.1@adcb.ae',
      details: 'JWT access token expired after 8-hour shift idle timeout',
      severity: 'INFO'
    }
  ]);

  useEffect(() => {
    loadUsers();
    loadMappings();
  }, []);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (showProvisionModal) setShowProvisionModal(false);
        if (creationResult) setCreationResult(null);
        if (selectedUserForDrawer) setSelectedUserForDrawer(null);
        if (userToDeProvision) setUserToDeProvision(null);
        if (showGroupModal) setShowGroupModal(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [showProvisionModal, creationResult, selectedUserForDrawer, userToDeProvision, showGroupModal]);

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
      const [fetchedGroups] = await Promise.all([
        EdithApi.fetchIamGroups(),
        EdithApi.fetchMenus()
      ]);
      setGroups(fetchedGroups || []);

      const adminGrp = fetchedGroups?.find(g => g.groupName.includes('ADMIN'));
      const amlGrp = fetchedGroups?.find(g => g.groupName.includes('AML'));
      const eddGrp = fetchedGroups?.find(g => g.groupName.includes('EDD'));
      const mlroGrp = fetchedGroups?.find(g => g.groupName.includes('MLRO'));

      // Build hierarchical mappings from SYSTEM_12_MENUS
      const rows: GroupMappingRow[] = SYSTEM_12_MENUS.map(m => {
        const adminHas = adminGrp ? adminGrp.permissions.some(p => (p.menuId === m.code) && p.canView) : true;
        const amlHas = amlGrp ? amlGrp.permissions.some(p => (p.menuId === m.code) && p.canView) : (m.code === 'MENU_BLACKLIST' || m.code === 'MENU_AML' || m.code === 'MENU_AUTHORIZATION' || m.code === 'MENU_DELTA');
        const eddHas = eddGrp ? eddGrp.permissions.some(p => (p.menuId === m.code) && p.canView) : (m.code === 'MENU_STR_PROCESS' || m.code === 'MENU_CUSTOMER');
        const mlroHas = mlroGrp ? mlroGrp.permissions.some(p => (p.menuId === m.code) && p.canView) : (m.code === 'MENU_AML' || m.code === 'MENU_STR_PROCESS' || m.code === 'MENU_REPORTS' || m.code === 'MENU_LOG_REPORT');

        const subRows: SubMappingRow[] | undefined = m.subMenus?.map(sub => ({
          code: sub.code,
          name: sub.name,
          path: sub.path,
          admin: true,
          amlOfficer: amlHas,
          eddAnalyst: eddHas,
          istrMlro: mlroHas
        }));

        return {
          menuCode: m.code,
          menuName: m.name,
          admin: adminHas,
          amlOfficer: amlHas,
          eddAnalyst: eddHas,
          istrMlro: mlroHas,
          subMenus: subRows
        };
      });

      setMappings(rows);
    } catch (err) {
      console.error('Failed to load menu mappings:', err);
    }
  };

  const toggleMapping = async (index: number, role: 'admin' | 'amlOfficer' | 'eddAnalyst' | 'istrMlro') => {
    const row = mappings[index];
    if (!row) return;

    const nextVal = !row[role];
    setMappings(prev => prev.map((r, idx) => {
      if (idx !== index) return r;
      const updatedSubs = r.subMenus?.map(s => ({ ...s, [role]: nextVal }));
      return { ...r, [role]: nextVal, subMenus: updatedSubs };
    }));

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

  const toggleSubMapping = (parentIdx: number, subIdx: number, role: 'admin' | 'amlOfficer' | 'eddAnalyst' | 'istrMlro') => {
    setMappings(prev => prev.map((r, pIdx) => {
      if (pIdx !== parentIdx || !r.subMenus) return r;
      const nextSubs = r.subMenus.map((s, sIdx) => {
        if (sIdx !== subIdx) return s;
        return { ...s, [role]: !s[role] };
      });
      return { ...r, subMenus: nextSubs };
    }));
    setStatusMessage('Updated sub-menu capability.');
    setTimeout(() => setStatusMessage(null), 2500);
  };

  const bulkToggleRoleColumn = (role: 'admin' | 'amlOfficer' | 'eddAnalyst' | 'istrMlro', grantAll: boolean) => {
    setMappings(prev => prev.map(r => ({
      ...r,
      [role]: grantAll,
      subMenus: r.subMenus?.map(s => ({ ...s, [role]: grantAll }))
    })));
    setStatusMessage(`${grantAll ? 'Granted' : 'Revoked'} all system menus for ${role.toUpperCase()}.`);
    setTimeout(() => setStatusMessage(null), 3000);
  };

  const toggleMenuAccordion = (code: string) => {
    setExpandedMenus(prev => ({ ...prev, [code]: !prev[code] }));
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
          emailError: res.emailError,
          invitationToken: res.invitationToken || res.user.invitationToken
        });
        setStatusMessage(`Provisioned operator ${res.user.fullName || res.user.username} successfully.`);
      } else if (res && res.id) {
        setUsers(prev => [res, ...prev.filter(u => u.id !== res.id)]);
        setCreationResult({
          user: res,
          tempPassword: autoGeneratePassword ? 'EdithCompliance2026!' : customPassword,
          emailStatus: 'SENT',
          emailError: undefined,
          invitationToken: res.invitationToken
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
          emailError: res.emailError,
          invitationToken: res.invitationToken || user.invitationToken
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

  const handleOpenEditDrawer = (user: IamUserRecord) => {
    setSelectedUserForDrawer(user);
    setDrawerFullName(user.fullName || '');
    setDrawerDepartment(user.department || '');
    setDrawerRole(user.role);
  };

  const handleSaveDrawerProfile = async () => {
    if (!selectedUserForDrawer) return;
    setDrawerIsSaving(true);
    try {
      const updated = await EdithApi.updateIamUser(selectedUserForDrawer.id, {
        fullName: drawerFullName.trim(),
        department: drawerDepartment.trim(),
        role: drawerRole
      });

      if (updated) {
        setUsers(prev => prev.map(u => u.id === updated.id ? updated : u));
        setSelectedUserForDrawer(updated);
        setStatusMessage(`Updated profile for ${updated.fullName || updated.username}.`);
      } else {
        setUsers(prev => prev.map(u => u.id === selectedUserForDrawer.id ? {
          ...u,
          fullName: drawerFullName.trim(),
          department: drawerDepartment.trim(),
          role: drawerRole
        } : u));
        setStatusMessage('Saved changes locally.');
      }
      setTimeout(() => setSelectedUserForDrawer(null), 400);
    } catch (err) {
      reportError(err, { category: 'SERVER' });
      setStatusMessage('Could not save profile changes.');
    } finally {
      setDrawerIsSaving(false);
    }
  };

  const handleResetMfa = async (user: IamUserRecord) => {
    try {
      await EdithApi.updateIamUser(user.id, { mfaEnabled: false });
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, mfaEnabled: false } : u));
      if (selectedUserForDrawer && selectedUserForDrawer.id === user.id) {
        setSelectedUserForDrawer({ ...selectedUserForDrawer, mfaEnabled: false });
      }
      setStatusMessage(`2FA Secret cleared for ${user.username}. Officer will be required to scan QR code on next login.`);
    } catch {
      setStatusMessage('Failed to reset 2FA secret.');
    }
  };

  const handleTogglePasswordRotation = async (user: IamUserRecord) => {
    const nextVal = !user.mustChangePassword;
    try {
      await EdithApi.updateIamUser(user.id, { mustChangePassword: nextVal });
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, mustChangePassword: nextVal } : u));
      if (selectedUserForDrawer && selectedUserForDrawer.id === user.id) {
        setSelectedUserForDrawer({ ...selectedUserForDrawer, mustChangePassword: nextVal });
      }
      setStatusMessage(`Password rotation flag set to ${nextVal ? 'REQUIRED' : 'ACTIVE'} for ${user.username}.`);
    } catch {
      setStatusMessage('Failed to update password rotation policy.');
    }
  };

  const handleToggleAccountActive = async (user: IamUserRecord) => {
    const nextActive = !user.isActive;
    try {
      await EdithApi.updateIamUser(user.id, { isActive: nextActive });
      setUsers(prev => prev.map(u => u.id === user.id ? { ...u, isActive: nextActive } : u));
      if (selectedUserForDrawer && selectedUserForDrawer.id === user.id) {
        setSelectedUserForDrawer({ ...selectedUserForDrawer, isActive: nextActive });
      }
      setStatusMessage(`Account ${user.username} is now ${nextActive ? 'ACTIVE' : 'SUSPENDED'}.`);
    } catch {
      setStatusMessage('Failed to update account active status.');
    }
  };

  const handleConfirmDeProvision = async () => {
    if (!userToDeProvision) return;
    setIsDeProvisioning(true);
    try {
      await EdithApi.deleteIamUser(userToDeProvision.id);
      setUsers(prev => prev.filter(u => u.id !== userToDeProvision.id));
      setStatusMessage(`De-provisioned officer ${userToDeProvision.fullName || userToDeProvision.username}. Active queue reassigned.`);
      setUserToDeProvision(null);
    } catch {
      setStatusMessage('Failed to de-provision officer.');
    } finally {
      setIsDeProvisioning(false);
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

  const handleKillSession = (sessionId: string) => {
    setSessions(prev => prev.map(s => s.id === sessionId ? { ...s, status: 'TERMINATED' } : s));
    setStatusMessage(`Session ${sessionId} forcefully terminated. Access token revoked.`);
    setTimeout(() => setStatusMessage(null), 3000);
  };

  const handleExportCsv = () => {
    const headers = ['User ID', 'Login ID / Email', 'Full Name', 'Role', 'Department', 'Shift Status', '2FA Enrolled', 'Password Status', 'Account Active'];
    const rows = filteredUsers.map(u => [
      u.id,
      u.username,
      u.fullName || '',
      u.role,
      u.department || '',
      u.shiftStatus,
      u.mfaEnabled ? 'ENROLLED' : 'PENDING',
      u.mustChangePassword ? 'REQUIRES_ROTATION' : 'ACTIVE',
      u.isActive ? 'ACTIVE' : 'SUSPENDED'
    ]);

    const csvContent = [headers.join(','), ...rows.map(r => r.map(val => `"${String(val).replace(/"/g, '""')}"`).join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `EDITH_Compliance_Operators_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setStatusMessage('Exported active operator directory to CSV.');
    setTimeout(() => setStatusMessage(null), 2500);
  };

  // Filtered Users computation
  const filteredUsers = useMemo(() => {
    return users.filter(u => {
      // Search term
      const term = searchTerm.toLowerCase();
      const matchesSearch = 
        u.fullName?.toLowerCase().includes(term) ||
        u.username.toLowerCase().includes(term) ||
        u.role.toLowerCase().includes(term) ||
        u.department?.toLowerCase().includes(term);

      if (!matchesSearch) return false;

      // Role filter
      if (roleFilter !== 'ALL' && u.role !== roleFilter) return false;

      // Department filter
      if (deptFilter !== 'ALL' && u.department !== deptFilter) return false;

      // Status filter
      if (statusFilter === 'ON_DUTY' && u.shiftStatus !== 'ON_DUTY') return false;
      if (statusFilter === 'OFF_DUTY' && u.shiftStatus !== 'OFF_DUTY') return false;
      if (statusFilter === 'MFA_PENDING' && u.mfaEnabled) return false;
      if (statusFilter === 'SUSPENDED' && u.isActive) return false;

      return true;
    });
  }, [users, searchTerm, roleFilter, deptFilter, statusFilter]);

  // Distinct departments
  const availableDepts = useMemo(() => {
    const set = new Set<string>();
    users.forEach(u => {
      if (u.department) set.add(u.department);
    });
    return Array.from(set);
  }, [users]);

  // Operational metrics
  const onDutyCount = useMemo(() => users.filter(u => u.shiftStatus === 'ON_DUTY').length, [users]);
  const mfaEnrolledCount = useMemo(() => users.filter(u => u.mfaEnabled).length, [users]);
  const suspendedCount = useMemo(() => users.filter(u => !u.isActive).length, [users]);

  const pageMeta = usePageMeta(
    'Users',
    'Provision compliance officers, configure 2FA enrollment, control shift presence, and manage role-gated menus.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto font-sans">
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
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30">
              PBKDF2-SHA256 &amp; TOTP 2FA
            </span>
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

          <button
            type="button"
            onClick={handleExportCsv}
            className="py-2.5 px-3.5 rounded-xl bg-slate-50 text-ink border border-border hover:bg-slate-100 transition text-xs font-mono font-bold cursor-pointer shadow-xs flex items-center gap-1.5"
            title="Export Directory CSV"
          >
            <Download className="w-3.5 h-3.5 text-muted" />
            <span className="hidden sm:inline">Export CSV</span>
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
        <div className="p-3 rounded-xl bg-accent/15 border border-accent/30 text-accent text-xs font-mono flex items-center justify-between animate-in fade-in duration-150">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            <span>{statusMessage}</span>
          </div>
          <button onClick={() => setStatusMessage(null)} className="cursor-pointer text-accent hover:opacity-75">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Operational KPI Metric Strip */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="p-3.5 bg-white rounded-xl border border-border shadow-xs flex items-center justify-between">
          <div>
            <div className="text-[10px] font-mono text-muted uppercase font-bold tracking-wider">Total Officers</div>
            <div className="text-lg font-bold text-ink mt-0.5">{users.length}</div>
          </div>
          <div className="w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center font-bold">
            <Users className="w-4 h-4" />
          </div>
        </div>

        <div className="p-3.5 bg-white rounded-xl border border-border shadow-xs flex items-center justify-between">
          <div>
            <div className="text-[10px] font-mono text-muted uppercase font-bold tracking-wider">On-Duty Shifts</div>
            <div className="text-lg font-bold text-accent mt-0.5 flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-accent animate-pulse" />
              <span>{onDutyCount} Active</span>
            </div>
          </div>
          <div className="w-8 h-8 rounded-lg bg-accent/10 text-accent flex items-center justify-center font-bold">
            <Clock className="w-4 h-4" />
          </div>
        </div>

        <div className="p-3.5 bg-white rounded-xl border border-border shadow-xs flex items-center justify-between">
          <div>
            <div className="text-[10px] font-mono text-muted uppercase font-bold tracking-wider">2FA Adoption</div>
            <div className="text-lg font-bold text-ink mt-0.5">
              {users.length > 0 ? Math.round((mfaEnrolledCount / users.length) * 100) : 100}%
            </div>
          </div>
          <div className="w-8 h-8 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold">
            <ShieldCheck className="w-4 h-4" />
          </div>
        </div>

        <div className="p-3.5 bg-white rounded-xl border border-border shadow-xs flex items-center justify-between">
          <div>
            <div className="text-[10px] font-mono text-muted uppercase font-bold tracking-wider">Suspended</div>
            <div className="text-lg font-bold text-critical mt-0.5">{suspendedCount} Accounts</div>
          </div>
          <div className="w-8 h-8 rounded-lg bg-critical/10 text-critical flex items-center justify-center font-bold">
            <UserX className="w-4 h-4" />
          </div>
        </div>
      </div>

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
          User Groups ({groups.length || 4})
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
        <button
          type="button"
          onClick={() => {
            setActiveTab('AUDIT_SESSIONS');
          }}
          className={`px-4 py-2 text-xs font-bold font-mono transition-all border-b-2 whitespace-nowrap cursor-pointer ${
            activeTab === 'AUDIT_SESSIONS'
              ? 'border-primary text-primary bg-primary/5 rounded-t-lg font-bold'
              : 'border-transparent text-muted hover:text-ink hover:bg-slate-50 rounded-t-lg'
          }`}
        >
          Active Sessions &amp; Security Log
        </button>
      </div>

      {/* TAB 1: USERS DIRECTORY */}
      {activeTab === 'USERS' && (
        <div className="space-y-4">
          {/* Faceted Search & Filters */}
          <div className="bg-white p-3.5 rounded-xl border border-border shadow-xs space-y-3">
            <div className="flex flex-col md:flex-row items-center gap-3">
              <div className="flex items-center gap-2.5 bg-slate-50 px-3 py-2 rounded-xl border border-border w-full md:flex-1">
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
                    className="p-0.5 rounded hover:bg-slate-200 text-muted hover:text-ink cursor-pointer"
                    title="Clear search"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>

              {/* Role Select Filter */}
              <div className="flex items-center gap-2 w-full md:w-auto">
                <select
                  value={roleFilter}
                  onChange={e => setRoleFilter(e.target.value)}
                  className="px-3 py-2 rounded-xl text-xs font-mono font-bold bg-slate-50 border border-border text-ink cursor-pointer outline-none focus:border-primary"
                >
                  <option value="ALL">All Roles</option>
                  <option value="SUPER_ADMIN">SUPER_ADMIN</option>
                  <option value="ADMIN">ADMIN</option>
                  <option value="AML_OFFICER">AML Officer</option>
                  <option value="EDD_ANALYST">EDD Analyst</option>
                  <option value="ISTR_MLRO">ISTR MLRO</option>
                </select>

                {/* Department Select Filter */}
                <select
                  value={deptFilter}
                  onChange={e => setDeptFilter(e.target.value)}
                  className="px-3 py-2 rounded-xl text-xs font-mono font-bold bg-slate-50 border border-border text-ink cursor-pointer outline-none focus:border-primary max-w-[180px]"
                >
                  <option value="ALL">All Departments</option>
                  {availableDepts.map(d => (
                    <option key={d} value={d}>{d}</option>
                  ))}
                </select>
              </div>
            </div>

            {/* Quick Filter Pills Strip */}
            <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-border/60">
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="text-[10px] font-mono text-muted uppercase font-bold mr-1 flex items-center gap-1">
                  <Filter className="w-3 h-3" /> Quick Filter:
                </span>
                
                <button
                  type="button"
                  onClick={() => setStatusFilter('ALL')}
                  className={`px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition cursor-pointer ${
                    statusFilter === 'ALL'
                      ? 'bg-primary text-white shadow-2xs'
                      : 'bg-slate-100 text-muted hover:text-ink'
                  }`}
                >
                  All ({users.length})
                </button>

                <button
                  type="button"
                  onClick={() => setStatusFilter('ON_DUTY')}
                  className={`px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition cursor-pointer flex items-center gap-1 ${
                    statusFilter === 'ON_DUTY'
                      ? 'bg-accent text-white shadow-2xs'
                      : 'bg-slate-100 text-muted hover:text-ink'
                  }`}
                >
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                  On-Duty ({onDutyCount})
                </button>

                <button
                  type="button"
                  onClick={() => setStatusFilter('OFF_DUTY')}
                  className={`px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition cursor-pointer ${
                    statusFilter === 'OFF_DUTY'
                      ? 'bg-slate-700 text-white shadow-2xs'
                      : 'bg-slate-100 text-muted hover:text-ink'
                  }`}
                >
                  Off-Duty ({users.length - onDutyCount})
                </button>

                <button
                  type="button"
                  onClick={() => setStatusFilter('MFA_PENDING')}
                  className={`px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition cursor-pointer flex items-center gap-1 ${
                    statusFilter === 'MFA_PENDING'
                      ? 'bg-amber-600 text-white shadow-2xs'
                      : 'bg-slate-100 text-muted hover:text-ink'
                  }`}
                >
                  <AlertCircle className="w-3 h-3" />
                  Pending 2FA ({users.length - mfaEnrolledCount})
                </button>

                <button
                  type="button"
                  onClick={() => setStatusFilter('SUSPENDED')}
                  className={`px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold transition cursor-pointer flex items-center gap-1 ${
                    statusFilter === 'SUSPENDED'
                      ? 'bg-critical text-white shadow-2xs'
                      : 'bg-slate-100 text-muted hover:text-ink'
                  }`}
                >
                  <UserX className="w-3 h-3" />
                  Suspended ({suspendedCount})
                </button>
              </div>

              <span className="text-[10px] font-mono text-muted px-2 py-0.5 rounded bg-slate-100 border border-border">
                Showing {filteredUsers.length} of {users.length} officers
              </span>
            </div>
          </div>

          {/* Users Table */}
          <div className="bg-surface rounded-xl border border-border overflow-hidden shadow-xs">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-muted border-b border-border text-[10px] font-mono uppercase text-muted font-bold">
                    <th className="p-3">Officer &amp; Login ID</th>
                    <th className="p-3">Assigned Role</th>
                    <th className="p-3">Security &amp; 2FA Status</th>
                    <th className="p-3">Department</th>
                    <th className="p-3">Shift Status</th>
                    <th className="p-3">Account State</th>
                    <th className="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border text-xs">
                  {filteredUsers.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="p-8 text-center text-muted font-mono text-xs">
                        No compliance officers matched the active filters.
                      </td>
                    </tr>
                  ) : (
                    filteredUsers.map(user => (
                      <tr 
                        key={user.id} 
                        className={`hover:bg-white/70 transition cursor-pointer ${!user.isActive ? 'opacity-65 bg-slate-50/50' : ''}`}
                        onClick={() => handleOpenEditDrawer(user)}
                      >
                        <td className="p-3">
                          <div className="flex items-center gap-2.5">
                            <div className="w-7 h-7 rounded-lg bg-primary/10 text-primary font-bold font-mono text-xs flex items-center justify-center shrink-0">
                              {user.fullName ? user.fullName.substring(0, 2).toUpperCase() : user.username.substring(0, 2).toUpperCase()}
                            </div>
                            <div>
                              <div className="font-bold text-ink hover:text-primary transition flex items-center gap-1.5">
                                <span>{user.fullName || 'Compliance Officer'}</span>
                                {!user.isActive && (
                                  <span className="px-1.5 py-0.2 rounded text-[9px] font-mono bg-critical/15 text-critical border border-critical/30">
                                    SUSPENDED
                                  </span>
                                )}
                              </div>
                              <div className="text-[11px] font-mono text-muted">{user.username}</div>
                            </div>
                          </div>
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
                              <ShieldCheck className="w-4 h-4 text-accent" />
                              <span>2FA Enrolled (Active)</span>
                            </div>
                          ) : (
                            <div className="flex items-center gap-1.5 text-warning font-mono text-[11px] font-bold">
                              <AlertCircle className="w-4 h-4 text-warning" />
                              <span>2FA Pending Setup</span>
                            </div>
                          )}
                          <div className="text-[10px] font-mono text-muted mt-0.5">
                            {user.mustChangePassword ? 'Password rotation mandated' : 'Password active'}
                          </div>
                        </td>
                        <td className="p-3 text-muted text-[11px]">
                          {user.department || 'Financial Crime Compliance'}
                        </td>
                        <td className="p-3" onClick={e => e.stopPropagation()}>
                          <button
                            type="button"
                            onClick={() => toggleUserShift(user)}
                            className={`px-2.5 py-1 rounded text-[10px] font-mono font-bold flex items-center gap-1.5 border transition cursor-pointer ${
                              user.shiftStatus === 'ON_DUTY'
                                ? 'bg-accent/15 text-accent border-accent/30 hover:bg-accent/25'
                                : 'bg-muted/15 text-muted border-border hover:bg-muted/25'
                            }`}
                            title="Toggle Shift Presence"
                          >
                            <span className={`w-1.5 h-1.5 rounded-full ${user.shiftStatus === 'ON_DUTY' ? 'bg-accent' : 'bg-muted'}`} />
                            {user.shiftStatus}
                          </button>
                        </td>
                        <td className="p-3">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ${
                            user.isActive 
                              ? 'bg-emerald-50 text-emerald-700 border-emerald-200' 
                              : 'bg-slate-100 text-slate-600 border-slate-300'
                          }`}>
                            {user.isActive ? 'Active' : 'Suspended'}
                          </span>
                        </td>
                        <td className="p-3 text-right" onClick={e => e.stopPropagation()}>
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              type="button"
                              onClick={() => handleOpenEditDrawer(user)}
                              className="p-1.5 text-slate-600 hover:text-primary hover:bg-primary/10 rounded transition cursor-pointer"
                              title="Edit Officer Profile &amp; Credentials"
                            >
                              <Edit2 className="w-3.5 h-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={() => handleResendInvite(user)}
                              className="p-1.5 text-primary hover:bg-primary/10 rounded transition text-xs flex items-center gap-1 cursor-pointer"
                              title="Resend Activation Email / Link"
                            >
                              <Mail className="w-3.5 h-3.5" />
                              <span className="text-[10px] font-mono font-bold hidden xl:inline">Invite</span>
                            </button>
                            <button
                              type="button"
                              onClick={() => setUserToDeProvision(user)}
                              className="p-1.5 text-critical hover:bg-critical/10 rounded transition cursor-pointer"
                              title="De-provision &amp; Reassign Queue"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: USER GROUPS */}
      {activeTab === 'GROUPS' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-border shadow-xs">
            <div>
              <h3 className="font-bold text-sm text-ink">Role Entitlement Groups</h3>
              <p className="text-xs text-muted mt-0.5">
                Pre-configured institutional clearance groups mapped to PostgreSQL <span className="font-mono">core.user_groups</span>.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setShowGroupModal(true)}
              className="py-2 px-3.5 bg-primary text-white text-xs font-mono font-bold rounded-xl hover:bg-primary/90 transition shadow-xs flex items-center gap-1.5 cursor-pointer"
            >
              <Layers className="w-3.5 h-3.5" />
              <span>New Operational Group</span>
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="p-5 bg-surface rounded-xl border border-border space-y-3 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-blue-100 text-blue-700 border border-blue-200">
                  SYSTEM GROUP
                </span>
                <span className="text-[10px] font-mono text-muted">12/12 MENUS</span>
              </div>
              <h3 className="font-bold text-ink text-sm">ADMINISTRATOR_GROUP</h3>
              <p className="text-xs text-muted leading-relaxed">
                Full access to all 12 menus, Settings (User Groups, Users, Menu Master, Menu Mappings, Risk Rates, Screening Score, Setup Control), Master, and Authorization.
              </p>
              <div className="pt-2 flex items-center justify-between border-t border-border/60">
                <span className="text-[11px] font-mono text-ink font-bold">
                  {users.filter(u => u.role === 'ADMIN' || u.role === 'SUPER_ADMIN').length} Active Members
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setRoleFilter('ADMIN');
                    setActiveTab('USERS');
                  }}
                  className="text-primary hover:underline font-mono text-xs cursor-pointer flex items-center gap-1"
                >
                  <span>View Members</span>
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            <div className="p-5 bg-surface rounded-xl border border-border space-y-3 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-100 text-emerald-700 border border-emerald-200">
                  OPERATIONAL
                </span>
                <span className="text-[10px] font-mono text-muted">6/12 MENUS</span>
              </div>
              <h3 className="font-bold text-ink text-sm">AML_AUTHORIZED_OFFICERS</h3>
              <p className="text-xs text-muted leading-relaxed">
                Access to Authorization, STR Alerts, AML (Customize Rules, Process AML, STR History), Blacklist, Whitelist, and Delta Authorization.
              </p>
              <div className="pt-2 flex items-center justify-between border-t border-border/60">
                <span className="text-[11px] font-mono text-ink font-bold">
                  {users.filter(u => u.role === 'AML_OFFICER').length} Active Members
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setRoleFilter('AML_OFFICER');
                    setActiveTab('USERS');
                  }}
                  className="text-primary hover:underline font-mono text-xs cursor-pointer flex items-center gap-1"
                >
                  <span>View Members</span>
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            <div className="p-5 bg-surface rounded-xl border border-border space-y-3 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-100 text-amber-700 border border-amber-200">
                  INVESTIGATIONS
                </span>
                <span className="text-[10px] font-mono text-muted">4/12 MENUS</span>
              </div>
              <h3 className="font-bold text-ink text-sm">EDD_INVESTIGATION_DESK</h3>
              <p className="text-xs text-muted leading-relaxed">
                Access to STR Process (EDD Analyst with KYC Details window, EDD Analyst Monitoring with 3-day / 7-day auto-escalation), Customer Screening Report, and Customer KYC.
              </p>
              <div className="pt-2 flex items-center justify-between border-t border-border/60">
                <span className="text-[11px] font-mono text-ink font-bold">
                  {users.filter(u => u.role === 'EDD_ANALYST').length} Active Members
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setRoleFilter('EDD_ANALYST');
                    setActiveTab('USERS');
                  }}
                  className="text-primary hover:underline font-mono text-xs cursor-pointer flex items-center gap-1"
                >
                  <span>View Members</span>
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            <div className="p-5 bg-surface rounded-xl border border-border space-y-3 shadow-xs">
              <div className="flex items-center justify-between">
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-red-100 text-red-700 border border-red-200">
                  REGULATORY
                </span>
                <span className="text-[10px] font-mono text-muted">6/12 MENUS</span>
              </div>
              <h3 className="font-bold text-ink text-sm">MLRO_APPROVAL_DEPT</h3>
              <p className="text-xs text-muted leading-relaxed">
                Supervises suspicious STR cases, reviews KYC details, monitors STR history, views Reports and Log Reports. Holds exclusive statutory Freeze and Unfreeze authority.
              </p>
              <div className="pt-2 flex items-center justify-between border-t border-border/60">
                <span className="text-[11px] font-mono text-ink font-bold">
                  {users.filter(u => u.role === 'ISTR_MLRO' || u.role === 'MLRO').length} Active Members
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setRoleFilter('ISTR_MLRO');
                    setActiveTab('USERS');
                  }}
                  className="text-primary hover:underline font-mono text-xs cursor-pointer flex items-center gap-1"
                >
                  <span>View Members</span>
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: MENU MASTER */}
      {activeTab === 'MENU_MASTER' && (
        <div className="bg-surface rounded-xl border border-border p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-bold text-ink text-sm flex items-center gap-2">
              <FolderTree className="w-4 h-4 text-primary" />
              Complete 12 Main Menus &amp; Sub-Menu Architecture
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

      {/* TAB 4: MENU MAPPINGS MATRIX (HIERARCHICAL ACCORDION) */}
      {activeTab === 'MENU_MAPPINGS' && (
        <div className="bg-surface rounded-xl border border-border overflow-hidden shadow-xs">
          <div className="p-4 bg-surface-muted border-b border-border flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <div>
              <h3 className="text-sm font-bold text-ink flex items-center gap-2">
                <GitMerge className="w-4 h-4 text-primary" />
                Granular Menu &amp; Sub-Menu Entitlements Matrix
              </h3>
              <p className="text-xs text-muted mt-0.5">
                Click chevrons to expand sub-menu permissions. Synchronized in real-time with PostgreSQL.
              </p>
            </div>
            <span className="px-2 py-1 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30 self-start sm:self-auto">
              LIVE POSTGRESQL SYNC
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-white border-b border-border text-[10px] font-mono uppercase text-muted font-bold">
                  <th className="p-3">System Menu &amp; Sub-Modules</th>
                  
                  <th className="p-3 text-center">
                    <div>Administrator</div>
                    <div className="flex items-center justify-center gap-1 mt-1 text-[9px] font-mono font-normal">
                      <button onClick={() => bulkToggleRoleColumn('admin', true)} className="hover:text-primary underline cursor-pointer">All</button>
                      <span>|</span>
                      <button onClick={() => bulkToggleRoleColumn('admin', false)} className="hover:text-critical underline cursor-pointer">None</button>
                    </div>
                  </th>

                  <th className="p-3 text-center">
                    <div>AML Officer</div>
                    <div className="flex items-center justify-center gap-1 mt-1 text-[9px] font-mono font-normal">
                      <button onClick={() => bulkToggleRoleColumn('amlOfficer', true)} className="hover:text-primary underline cursor-pointer">All</button>
                      <span>|</span>
                      <button onClick={() => bulkToggleRoleColumn('amlOfficer', false)} className="hover:text-critical underline cursor-pointer">None</button>
                    </div>
                  </th>

                  <th className="p-3 text-center">
                    <div>EDD Analyst</div>
                    <div className="flex items-center justify-center gap-1 mt-1 text-[9px] font-mono font-normal">
                      <button onClick={() => bulkToggleRoleColumn('eddAnalyst', true)} className="hover:text-primary underline cursor-pointer">All</button>
                      <span>|</span>
                      <button onClick={() => bulkToggleRoleColumn('eddAnalyst', false)} className="hover:text-critical underline cursor-pointer">None</button>
                    </div>
                  </th>

                  <th className="p-3 text-center">
                    <div>ISTR MLRO</div>
                    <div className="flex items-center justify-center gap-1 mt-1 text-[9px] font-mono font-normal">
                      <button onClick={() => bulkToggleRoleColumn('istrMlro', true)} className="hover:text-primary underline cursor-pointer">All</button>
                      <span>|</span>
                      <button onClick={() => bulkToggleRoleColumn('istrMlro', false)} className="hover:text-critical underline cursor-pointer">None</button>
                    </div>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border text-xs">
                {mappings.map((row, pIdx) => {
                  const isExpanded = !!expandedMenus[row.menuCode];
                  return (
                    <React.Fragment key={row.menuCode}>
                      {/* Main Root Menu Row */}
                      <tr className="hover:bg-white/60 transition bg-slate-50/40 font-medium">
                        <td className="p-3 text-ink flex items-center gap-2">
                          {row.subMenus && row.subMenus.length > 0 ? (
                            <button
                              type="button"
                              onClick={() => toggleMenuAccordion(row.menuCode)}
                              className="p-1 rounded hover:bg-slate-200 text-muted hover:text-ink cursor-pointer transition"
                              title={isExpanded ? 'Collapse Sub-Menus' : 'Expand Sub-Menus'}
                            >
                              {isExpanded ? <ChevronDown className="w-3.5 h-3.5 text-primary" /> : <ChevronRight className="w-3.5 h-3.5" />}
                            </button>
                          ) : (
                            <span className="w-5" />
                          )}
                          <span className="font-bold">{row.menuName}</span>
                          {row.subMenus && (
                            <span className="px-1.5 py-0.2 rounded text-[9px] font-mono text-muted bg-white border border-border">
                              {row.subMenus.length} subs
                            </span>
                          )}
                        </td>
                        
                        <td className="p-3 text-center">
                          <button
                            type="button"
                            onClick={() => toggleMapping(pIdx, 'admin')}
                            className={`w-6 h-6 rounded inline-flex items-center justify-center transition cursor-pointer shadow-2xs ${
                              row.admin ? 'bg-primary text-white' : 'bg-surface border border-border text-muted hover:bg-slate-200'
                            }`}
                          >
                            {row.admin ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                          </button>
                        </td>

                        <td className="p-3 text-center">
                          <button
                            type="button"
                            onClick={() => toggleMapping(pIdx, 'amlOfficer')}
                            className={`w-6 h-6 rounded inline-flex items-center justify-center transition cursor-pointer shadow-2xs ${
                              row.amlOfficer ? 'bg-primary text-white' : 'bg-surface border border-border text-muted hover:bg-slate-200'
                            }`}
                          >
                            {row.amlOfficer ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                          </button>
                        </td>

                        <td className="p-3 text-center">
                          <button
                            type="button"
                            onClick={() => toggleMapping(pIdx, 'eddAnalyst')}
                            className={`w-6 h-6 rounded inline-flex items-center justify-center transition cursor-pointer shadow-2xs ${
                              row.eddAnalyst ? 'bg-primary text-white' : 'bg-surface border border-border text-muted hover:bg-slate-200'
                            }`}
                          >
                            {row.eddAnalyst ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                          </button>
                        </td>

                        <td className="p-3 text-center">
                          <button
                            type="button"
                            onClick={() => toggleMapping(pIdx, 'istrMlro')}
                            className={`w-6 h-6 rounded inline-flex items-center justify-center transition cursor-pointer shadow-2xs ${
                              row.istrMlro ? 'bg-primary text-white' : 'bg-surface border border-border text-muted hover:bg-slate-200'
                            }`}
                          >
                            {row.istrMlro ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                          </button>
                        </td>
                      </tr>

                      {/* Expandable Sub-Menus Rows */}
                      {isExpanded && row.subMenus && row.subMenus.map((sub, sIdx) => (
                        <tr key={sub.code} className="bg-white/80 hover:bg-white text-[11px] font-mono border-l-4 border-l-primary/40">
                          <td className="p-2.5 pl-10 text-muted flex items-center justify-between gap-2">
                            <span>• {sub.name}</span>
                            <span className="text-[9px] text-slate-400 font-normal">{sub.path}</span>
                          </td>

                          <td className="p-2 text-center">
                            <button
                              type="button"
                              onClick={() => toggleSubMapping(pIdx, sIdx, 'admin')}
                              className={`w-5 h-5 rounded inline-flex items-center justify-center transition cursor-pointer ${
                                sub.admin ? 'bg-primary/80 text-white' : 'bg-slate-100 text-slate-400'
                              }`}
                            >
                              {sub.admin ? <Check className="w-3 h-3" /> : <X className="w-3 h-3" />}
                            </button>
                          </td>

                          <td className="p-2 text-center">
                            <button
                              type="button"
                              onClick={() => toggleSubMapping(pIdx, sIdx, 'amlOfficer')}
                              className={`w-5 h-5 rounded inline-flex items-center justify-center transition cursor-pointer ${
                                sub.amlOfficer ? 'bg-primary/80 text-white' : 'bg-slate-100 text-slate-400'
                              }`}
                            >
                              {sub.amlOfficer ? <Check className="w-3 h-3" /> : <X className="w-3 h-3" />}
                            </button>
                          </td>

                          <td className="p-2 text-center">
                            <button
                              type="button"
                              onClick={() => toggleSubMapping(pIdx, sIdx, 'eddAnalyst')}
                              className={`w-5 h-5 rounded inline-flex items-center justify-center transition cursor-pointer ${
                                sub.eddAnalyst ? 'bg-primary/80 text-white' : 'bg-slate-100 text-slate-400'
                              }`}
                            >
                              {sub.eddAnalyst ? <Check className="w-3 h-3" /> : <X className="w-3 h-3" />}
                            </button>
                          </td>

                          <td className="p-2 text-center">
                            <button
                              type="button"
                              onClick={() => toggleSubMapping(pIdx, sIdx, 'istrMlro')}
                              className={`w-5 h-5 rounded inline-flex items-center justify-center transition cursor-pointer ${
                                sub.istrMlro ? 'bg-primary/80 text-white' : 'bg-slate-100 text-slate-400'
                              }`}
                            >
                              {sub.istrMlro ? <Check className="w-3 h-3" /> : <X className="w-3 h-3" />}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </React.Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 5: ACTIVE SESSIONS & SECURITY AUDIT */}
      {activeTab === 'AUDIT_SESSIONS' && (
        <div className="space-y-6">
          {/* Active Sessions Panel */}
          <div className="bg-surface rounded-xl border border-border overflow-hidden shadow-xs">
            <div className="p-4 bg-surface-muted border-b border-border flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-ink flex items-center gap-2">
                  <Activity className="w-4 h-4 text-accent" />
                  Live Concurrent Sessions &amp; Revocation Console
                </h3>
                <p className="text-xs text-muted mt-0.5">
                  Real-time view of active JWT operator tokens with instantaneous session termination.
                </p>
              </div>
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30">
                {sessions.filter(s => s.status === 'ACTIVE').length} Active JWTs
              </span>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="bg-white border-b border-border text-[10px] font-mono uppercase text-muted font-bold">
                    <th className="p-3">Session &amp; Operator</th>
                    <th className="p-3">Assigned Role</th>
                    <th className="p-3">Network IP / Gateway</th>
                    <th className="p-3">Browser &amp; Operating System</th>
                    <th className="p-3">Login Timestamp (UTC)</th>
                    <th className="p-3 text-right">Session Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {sessions.map(s => (
                    <tr key={s.id} className="hover:bg-white/50 transition">
                      <td className="p-3">
                        <div className="font-bold text-ink">{s.fullName}</div>
                        <div className="text-[11px] font-mono text-muted">{s.username}</div>
                      </td>
                      <td className="p-3">
                        <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                          {s.role}
                        </span>
                      </td>
                      <td className="p-3 font-mono text-[11px] text-muted">
                        {s.ipAddress}
                      </td>
                      <td className="p-3 text-[11px] text-muted">
                        {s.device}
                      </td>
                      <td className="p-3 font-mono text-[11px] text-muted">
                        {s.loginTime}
                      </td>
                      <td className="p-3 text-right">
                        {s.status === 'ACTIVE' ? (
                          <button
                            type="button"
                            onClick={() => handleKillSession(s.id)}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-mono font-bold bg-critical/10 text-critical border border-critical/30 hover:bg-critical hover:text-white transition cursor-pointer flex items-center gap-1 ml-auto"
                            title="Force Logout"
                          >
                            <LogOut className="w-3 h-3" />
                            <span>Kill Session</span>
                          </button>
                        ) : (
                          <span className="px-2 py-0.5 rounded text-[10px] font-mono text-muted bg-slate-100 border border-border">
                            TERMINATED
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Security Audit Timeline */}
          <div className="bg-surface rounded-xl border border-border p-5 space-y-4 shadow-xs">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-ink flex items-center gap-2">
                  <ShieldAlert className="w-4 h-4 text-primary" />
                  Recent IAM Privilege &amp; Security Audit Ledger
                </h3>
                <p className="text-xs text-muted mt-0.5">
                  Immutable event records committed to <span className="font-mono">core.audit_ledgers</span>.
                </p>
              </div>
              <span className="text-[10px] font-mono text-muted">SHA-256 Chained</span>
            </div>

            <div className="space-y-2.5">
              {securityAudits.map(item => (
                <div key={item.id} className="p-3 bg-white rounded-xl border border-border text-xs flex items-start justify-between gap-3 shadow-2xs">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ${
                        item.severity === 'CRITICAL' ? 'bg-critical/15 text-critical border-critical/30' :
                        item.severity === 'WARNING' ? 'bg-warning/15 text-warning border-warning/30' :
                        'bg-primary/10 text-primary border-primary/20'
                      }`}>
                        {item.action}
                      </span>
                      <span className="font-mono text-[11px] text-muted">{item.targetUser}</span>
                    </div>
                    <p className="text-ink font-mono text-[11px]">{item.details}</p>
                  </div>
                  <div className="text-right shrink-0">
                    <div className="text-[10px] font-mono text-muted">{item.timestamp}</div>
                    <div className="text-[10px] font-mono text-slate-400 mt-0.5">by {item.actor}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* SLIDE-OUT USER EDIT & SECURITY DRAWER */}
      {selectedUserForDrawer && createPortal(
        <div className="fixed inset-0 z-[9999] pointer-events-auto">
          {/* Backdrop */}
          <div 
            onClick={() => setSelectedUserForDrawer(null)} 
            className="fixed inset-0 bg-slate-950/50 backdrop-blur-xs transition-opacity duration-200" 
          />

          <div 
            role="dialog"
            aria-modal="true"
            aria-label="User Profile Drawer"
            className="fixed top-0 right-0 h-full w-full sm:w-96 bg-white border-l border-border shadow-2xl p-6 font-mono text-xs space-y-5 animate-in slide-in-from-right duration-200 z-[10000] overflow-y-auto"
          >
            {/* Drawer Header */}
            <div className="flex items-center justify-between pb-4 border-b border-border">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-primary text-white flex items-center justify-center font-bold text-sm shadow-xs uppercase">
                  {drawerFullName ? drawerFullName.substring(0, 2) : selectedUserForDrawer.username.substring(0, 2)}
                </div>
                <div>
                  <h3 className="font-bold text-ink text-sm truncate max-w-[200px]">
                    {drawerFullName || selectedUserForDrawer.username}
                  </h3>
                  <div className="text-[10px] text-muted truncate">{selectedUserForDrawer.username}</div>
                </div>
              </div>
              <button 
                type="button"
                onClick={() => setSelectedUserForDrawer(null)} 
                className="p-1.5 rounded-lg hover:bg-slate-100 text-muted hover:text-ink transition cursor-pointer"
                title="Close"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Editable Officer Details */}
            <div className="space-y-4">
              <div>
                <label className="block text-[10px] font-bold text-muted uppercase tracking-wider mb-1">
                  Full Name
                </label>
                <input
                  type="text"
                  value={drawerFullName}
                  onChange={e => setDrawerFullName(e.target.value)}
                  className="w-full px-3 py-2 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                  placeholder="Officer name"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-muted uppercase tracking-wider mb-1">
                  Department
                </label>
                <input
                  type="text"
                  value={drawerDepartment}
                  onChange={e => setDrawerDepartment(e.target.value)}
                  className="w-full px-3 py-2 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                  placeholder="e.g. Sanctions &amp; Alerts"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-muted uppercase tracking-wider mb-1">
                  Assigned Clearance Role
                </label>
                <select
                  value={drawerRole}
                  onChange={e => setDrawerRole(e.target.value as UserRole)}
                  className="w-full px-3 py-2 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition font-mono cursor-pointer"
                >
                  <option value="ADMIN">ADMIN (Full Access)</option>
                  <option value="AML_OFFICER">AML Officer</option>
                  <option value="EDD_ANALYST">EDD Analyst</option>
                  <option value="ISTR_MLRO">ISTR MLRO</option>
                  <option value="SUPER_ADMIN">SUPER_ADMIN</option>
                </select>
              </div>

              {/* Security & Access Management Controls */}
              <div className="p-4 bg-[#faf8ff] rounded-xl border border-border space-y-3">
                <div className="text-[10px] font-bold text-primary uppercase tracking-wider flex items-center gap-1.5">
                  <Shield className="w-3.5 h-3.5" />
                  <span>Security &amp; Policy Controls</span>
                </div>

                {/* 2FA Status & Reset Button */}
                <div className="flex items-center justify-between text-xs pt-1">
                  <div>
                    <div className="font-bold text-ink">Two-Factor Authentication</div>
                    <div className="text-[10px] text-muted">
                      {selectedUserForDrawer.mfaEnabled ? 'Enrolled with TOTP' : 'Pending activation'}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleResetMfa(selectedUserForDrawer)}
                    className="px-2.5 py-1 bg-white border border-border text-critical rounded-lg text-[10px] font-bold hover:bg-critical/10 transition cursor-pointer"
                  >
                    Reset 2FA
                  </button>
                </div>

                {/* Password Rotation Mandate */}
                <div className="flex items-center justify-between text-xs pt-2 border-t border-border/60">
                  <div>
                    <div className="font-bold text-ink">Mandatory Password Rotation</div>
                    <div className="text-[10px] text-muted">
                      {selectedUserForDrawer.mustChangePassword ? 'Required on next login' : 'Not required'}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleTogglePasswordRotation(selectedUserForDrawer)}
                    className="px-2.5 py-1 bg-white border border-border text-primary rounded-lg text-[10px] font-bold hover:bg-primary/10 transition cursor-pointer"
                  >
                    {selectedUserForDrawer.mustChangePassword ? 'Clear' : 'Mandate'}
                  </button>
                </div>

                {/* Account Suspension Toggle */}
                <div className="flex items-center justify-between text-xs pt-2 border-t border-border/60">
                  <div>
                    <div className="font-bold text-ink">Account Status</div>
                    <div className="text-[10px] text-muted">
                      {selectedUserForDrawer.isActive ? 'Active & able to login' : 'Suspended / locked'}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleToggleAccountActive(selectedUserForDrawer)}
                    className={`px-2.5 py-1 rounded-lg text-[10px] font-bold transition cursor-pointer ${
                      selectedUserForDrawer.isActive 
                        ? 'bg-critical/10 border border-critical/30 text-critical hover:bg-critical/20' 
                        : 'bg-accent/10 border border-accent/30 text-accent hover:bg-accent/20'
                    }`}
                  >
                    {selectedUserForDrawer.isActive ? 'Suspend' : 'Activate'}
                  </button>
                </div>
              </div>
            </div>

            {/* Save Action Buttons */}
            <div className="pt-4 flex gap-2 border-t border-border">
              <button
                type="button"
                onClick={() => setSelectedUserForDrawer(null)}
                className="px-4 py-2.5 border border-border text-ink text-xs font-bold rounded-xl hover:bg-slate-100 transition cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSaveDrawerProfile}
                disabled={drawerIsSaving}
                className="flex-1 py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition shadow-xs cursor-pointer flex items-center justify-center gap-1.5"
              >
                {drawerIsSaving ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Check className="w-3.5 h-3.5" />
                )}
                <span>Save Profile</span>
              </button>
            </div>
          </div>
        </div>,
        document.body
      )}

      {/* STRUCTURED DE-PROVISION & REASSIGNMENT MODAL */}
      {userToDeProvision && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
          <div 
            onClick={() => setUserToDeProvision(null)} 
            className="fixed inset-0 bg-slate-950/60 backdrop-blur-xs transition-opacity duration-200" 
          />

          <div 
            role="dialog"
            aria-modal="true"
            aria-label="De-Provision Officer Modal"
            className="relative z-10 w-full max-w-lg bg-white rounded-2xl border border-border shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150 font-mono text-xs"
          >
            <div className="p-5 bg-critical text-white flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <ShieldAlert className="w-5 h-5 text-white shrink-0" />
                <div>
                  <h3 className="font-bold text-sm">Revoke Clearance &amp; De-Provision</h3>
                  <p className="text-[11px] text-white/80 mt-0.5">{userToDeProvision.username}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setUserToDeProvision(null)}
                className="text-white/70 hover:text-white p-1 rounded-lg hover:bg-white/10 transition cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              <div className="p-3.5 bg-critical/10 rounded-xl border border-critical/30 text-critical text-xs space-y-1">
                <div className="font-bold flex items-center gap-1.5">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>Permanent Clearance Revocation</span>
                </div>
                <p className="text-[11px] leading-relaxed text-critical/90">
                  You are about to de-provision operator <strong>{userToDeProvision.fullName || userToDeProvision.username}</strong> ({userToDeProvision.role}). Active JWT sessions will be terminated and database access will be revoked.
                </p>
              </div>

              {/* Handover & Case Reassignment */}
              <div className="space-y-2">
                <label className="block text-[11px] font-bold text-muted uppercase tracking-wider">
                  Reassign Active Sanctions &amp; STR Cases To:
                </label>
                <select
                  value={reassigneeId}
                  onChange={e => setReassigneeId(e.target.value)}
                  className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition font-mono cursor-pointer"
                >
                  <option value="">Select On-Duty Officer (Recommended)</option>
                  {users
                    .filter(u => u.id !== userToDeProvision.id && u.shiftStatus === 'ON_DUTY')
                    .map(u => (
                      <option key={u.id} value={u.id}>
                        {u.fullName || u.username} ({u.role}) - ON DUTY
                      </option>
                    ))}
                </select>
                <p className="text-[10px] text-muted">
                  Ensures zero stranded investigations or SLA turnaround breaches.
                </p>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setUserToDeProvision(null)}
                  className="px-4 py-2.5 border border-border text-ink text-xs font-bold rounded-xl hover:bg-slate-100 transition cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  disabled={isDeProvisioning}
                  onClick={handleConfirmDeProvision}
                  className="flex-1 py-2.5 bg-critical text-white font-bold text-xs rounded-xl hover:bg-critical/90 transition shadow-xs cursor-pointer flex items-center justify-center gap-1.5"
                >
                  {isDeProvisioning ? (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <Trash2 className="w-3.5 h-3.5" />
                  )}
                  <span>Confirm Revocation &amp; Handover</span>
                </button>
              </div>
            </div>
          </div>
        </div>,
        document.body
      )}

      {/* PROVISION OPERATOR MODAL */}
      {showProvisionModal && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
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

      {/* CREATION RESULT / CREDENTIALS DIALOG WITH SETUP LINK */}
      {creationResult && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
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
                      : 'Email could not be delivered automatically. Please copy and share the credentials and onboarding link below.'}
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

              {/* Direct Onboarding Link (Fallback for SMTP blockage) */}
              <div className="p-4 bg-slate-50 rounded-xl border border-border space-y-2">
                <div className="text-[11px] font-bold text-muted uppercase tracking-wider flex items-center justify-between">
                  <span>Direct Onboarding &amp; 2FA Setup Link:</span>
                  <button
                    type="button"
                    onClick={() => {
                      const link = `${window.location.origin}/onboarding?token=${creationResult.invitationToken || 'edith-onboarding-token'}`;
                      navigator.clipboard.writeText(link);
                      setStatusMessage('Copied direct onboarding link to clipboard.');
                      setTimeout(() => setStatusMessage(null), 2000);
                    }}
                    className="text-primary hover:underline flex items-center gap-1 font-mono text-[10px] cursor-pointer"
                  >
                    <Copy className="w-3.5 h-3.5" />
                    <span>Copy Setup Link</span>
                  </button>
                </div>
                <div className="px-3 py-2 bg-white rounded-lg border border-border font-mono text-[11px] text-muted truncate select-all">
                  {`${window.location.origin}/onboarding?token=${creationResult.invitationToken || 'edith-onboarding-token'}`}
                </div>
                <p className="text-[10px] text-muted leading-relaxed">
                  Share this link directly with the officer to complete password rotation and scan their TOTP 2FA secret.
                </p>
              </div>

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

      {/* NEW OPERATIONAL GROUP MODAL */}
      {showGroupModal && createPortal(
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
          <div 
            onClick={() => setShowGroupModal(false)}
            className="fixed inset-0 bg-slate-950/60 backdrop-blur-xs transition-opacity duration-200" 
          />

          <div 
            role="dialog"
            aria-modal="true"
            aria-label="New Operational Group Modal"
            className="relative z-10 w-full max-w-md bg-white rounded-2xl border border-border shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150 font-mono text-xs"
          >
            <div className="p-5 bg-[#0f172a] text-white flex items-center justify-between">
              <h3 className="font-bold text-sm flex items-center gap-2">
                <Layers className="w-4 h-4 text-primary-light" />
                <span>Create Operational Group</span>
              </h3>
              <button
                type="button"
                onClick={() => setShowGroupModal(false)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-white/10 transition cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              <div>
                <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                  Group Identifier <span className="text-critical">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. CORRIDOR_SANCTIONS_DESK"
                  value={newGroupName}
                  onChange={e => setNewGroupName(e.target.value)}
                  className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition uppercase font-mono"
                />
              </div>

              <div>
                <label className="block text-[11px] font-bold text-muted uppercase tracking-wider mb-1.5">
                  Scope &amp; Description
                </label>
                <textarea
                  rows={3}
                  placeholder="Describe operational responsibilities and clearance level..."
                  value={newGroupDesc}
                  onChange={e => setNewGroupDesc(e.target.value)}
                  className="w-full px-3.5 py-2.5 text-xs border border-border rounded-xl bg-slate-50 text-ink focus:outline-none focus:border-primary focus:bg-white transition"
                />
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowGroupModal(false)}
                  className="px-4 py-2.5 border border-border text-ink text-xs font-bold rounded-xl hover:bg-slate-100 transition cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={() => {
                    if (!newGroupName.trim()) return;
                    setStatusMessage(`Created operational group ${newGroupName.toUpperCase()}.`);
                    setShowGroupModal(false);
                    setNewGroupName('');
                    setNewGroupDesc('');
                    setTimeout(() => setStatusMessage(null), 3000);
                  }}
                  className="flex-1 py-2.5 bg-primary text-white font-bold text-xs rounded-xl hover:bg-primary/90 transition shadow-xs cursor-pointer flex items-center justify-center gap-1.5"
                >
                  <Check className="w-3.5 h-3.5" />
                  <span>Create Group</span>
                </button>
              </div>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
};
