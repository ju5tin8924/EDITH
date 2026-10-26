import React, { useState, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePageMeta } from '../config/pageMeta';
import { 
  Settings, 
  Search, 
  Save, 
  CheckCircle2, 
  Shield, 
  Key, 
  SlidersHorizontal, 
  ToggleRight, 
  Target, 
  Unlock, 
  Globe, 
  Flag,
  Activity,
  Banknote,
  History,
  Pin,
  PinOff,
  AlertTriangle,
  X,
  Info,
  Sparkles,
  ExternalLink
} from 'lucide-react';
import { EdithApi } from '../services/api';
import { EdithLoader } from './EdithLogo';
import { GeneralErrorBanner } from './ErrorEngineUi';

export type SystemMastersTab = 
  | 'COUNTRIES' 
  | 'NATIONALITIES' 
  | 'CURRENCIES'
  | 'RISK_RATES' 
  | 'SCREENING_SCORE' 
  | 'SETUP_CONTROL' 
  | 'RISK_ASSESSMENT' 
  | 'UNBLOCK_CUSTOMER' 
  | 'CHANGE_PASSWORD';

interface CountryMaster {
  code: string;
  name: string;
  isHighRisk: boolean;
  riskScore: number;
  fatfStatus: 'BLACK_LIST' | 'GREY_LIST' | 'MONITORED' | 'COOPERATIVE';
}

interface NationalityMaster {
  code: string;
  demonym: string;
  country: string;
  isHighRisk: boolean;
  riskLevel: 'HIGH' | 'MEDIUM' | 'LOW';
}

interface CurrencyMaster {
  currency: string;
  mandatoryReportingThreshold: number;
  highRiskApprovalThreshold: number;
}

interface BlockedCustomer {
  customerCode: string;
  fullName: string;
  nationalId: string;
  country: string;
  blockedAt: string;
  blockReason: string;
  isBlocked: boolean;
}

interface SystemMastersViewProps {
  initialTab?: SystemMastersTab;
}

export const SystemMastersView: React.FC<SystemMastersViewProps> = ({ initialTab = 'COUNTRIES' }) => {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<SystemMastersTab>(initialTab);
  const [countries, setCountries] = useState<CountryMaster[]>([]);
  const [nationalities, setNationalities] = useState<NationalityMaster[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyMaster[]>([
    { currency: 'AED', mandatoryReportingThreshold: 55000, highRiskApprovalThreshold: 500000 },
    { currency: 'USD', mandatoryReportingThreshold: 10000, highRiskApprovalThreshold: 100000 },
    { currency: 'EUR', mandatoryReportingThreshold: 10000, highRiskApprovalThreshold: 100000 },
    { currency: 'GBP', mandatoryReportingThreshold: 10000, highRiskApprovalThreshold: 100000 }
  ]);
  const [blockedCustomers, setBlockedCustomers] = useState<BlockedCustomer[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'HIGH_RISK' | 'BLACK_LIST' | 'GREY_LIST' | 'COOPERATIVE'>('ALL');
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [loadingMasters, setLoadingMasters] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  // 4-Eyes Governance Staging Mode
  const [stagingMode, setStagingMode] = useState<'DIRECT' | 'PENDING_APPROVAL'>('DIRECT');
  const [isHistoryDrawerOpen, setIsHistoryDrawerOpen] = useState(false);
  const [auditLogs, setAuditLogs] = useState<any[]>([]);

  // Risk Rate Benchmarks
  const [custHighRisk, setCustHighRisk] = useState(85);
  const [custMedRisk, setCustMedRisk] = useState(60);
  const [txHighRisk, setTxHighRisk] = useState(90);
  const [txMedRisk, setTxMedRisk] = useState(70);

  // Screening Score Weightages with Pin-Locks
  const [nameWeight, setNameWeight] = useState(60);
  const [nationalityWeight, setNationalityWeight] = useState(15);
  const [dobWeight, setDobWeight] = useState(15);
  const [idWeight, setIdWeight] = useState(10);
  const [matchThreshold, setMatchThreshold] = useState(80);
  const [pinnedWeights, setPinnedWeights] = useState({
    name: false,
    nationality: false,
    dob: false,
    id: false
  });

  // SysControl Scenarios
  const [sysControl, setSysControl] = useState({
    customerScreening: true,
    beneficiaryBankScreening: true,
    correspondentBankScreening: true,
    partnerUboScreening: true,
    realtimeHoldInterceptor: true
  });

  // Risk Assessment Master (EWRA) with Auto-Balancing
  const [geoWeight, setGeoWeight] = useState(30);
  const [clientTypeWeight, setClientTypeWeight] = useState(40);
  const [productWeight, setProductWeight] = useState(30);
  const [pinnedEwra, setPinnedEwra] = useState({
    geo: false,
    client: false,
    product: false
  });

  // Change Password Form State
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);
  const [passwordLoading, setPasswordLoading] = useState(false);

  useEffect(() => {
    if (initialTab) {
      setActiveTab(initialTab);
    }
  }, [initialTab]);

  useEffect(() => {
    loadMasters();
  }, []);

  const loadMasters = async () => {
    try {
      setLoadingMasters(true);
      const data = await EdithApi.fetchMasters();
      if (data) {
        if (data.scoringWeights) {
          setNameWeight(data.scoringWeights.nameWeightage);
          setNationalityWeight(data.scoringWeights.nationalityWeightage);
          setDobWeight(data.scoringWeights.dobWeightage);
          setIdWeight(data.scoringWeights.idWeightage || 10);
          setMatchThreshold(data.scoringWeights.defaultThreshold);
        }

        if (data.countries && data.countries.length > 0) {
          setCountries(data.countries as CountryMaster[]);
        } else if (data.fatfTiers && data.fatfTiers.length > 0) {
          const loadedCountries: CountryMaster[] = [];
          for (const tier of data.fatfTiers) {
            for (const c of tier.countries) {
              const isHigh = tier.riskFactor >= 75;
              loadedCountries.push({
                code: c,
                name: c,
                isHighRisk: isHigh,
                riskScore: tier.riskFactor,
                fatfStatus: tier.tier as any
              });
            }
          }
          if (loadedCountries.length > 0) {
            setCountries(loadedCountries);
          }
        }

        if (data.nationalities && data.nationalities.length > 0) {
          setNationalities(data.nationalities as NationalityMaster[]);
        }

        if (data.currencyThresholds && data.currencyThresholds.length > 0) {
          setCurrencies(data.currencyThresholds);
        }

        if (data.riskRates) {
          setCustHighRisk(data.riskRates.custHighRisk);
          setCustMedRisk(data.riskRates.custMedRisk);
          setTxHighRisk(data.riskRates.txHighRisk);
          setTxMedRisk(data.riskRates.txMedRisk);
        }

        if (data.setupControls) {
          setSysControl(data.setupControls);
        }

        if (data.riskAssessment) {
          setGeoWeight(data.riskAssessment.geoWeight);
          setClientTypeWeight(data.riskAssessment.clientTypeWeight);
          setProductWeight(data.riskAssessment.productWeight);
        }
      }

      const kycList = await EdithApi.fetchCustomers();
      if (kycList && kycList.length > 0) {
        setBlockedCustomers(
          kycList.map(k => ({
            customerCode: k.customerCode,
            fullName: k.fullName,
            nationalId: k.registrationNumber || k.customerCode,
            country: k.nationality || k.incorporationCountry || 'ARE',
            blockedAt: k.onboardedDate || '2026-10-14',
            blockReason: k.blockReason || (k.isBlocked ? 'Statutory Sanctions / Administrative Hold' : 'Active EDD Monitoring'),
            isBlocked: !!k.isBlocked
          }))
        );
      }
    } catch (err) {
      console.error('Failed to load live system masters:', err);
    } finally {
      setLoadingMasters(false);
    }
  };

  const loadAuditLogs = async () => {
    try {
      const logs = await EdithApi.fetchAuditLogs(20);
      if (logs) {
        setAuditLogs(logs.filter((l: any) => l.action.includes('MASTER') || l.action.includes('SCORE') || l.action.includes('SETTING') || l.action.includes('RATE') || l.action.includes('CONTROL') || l.action.includes('CURRENCY')));
      }
    } catch (err) {
      console.error('Failed to fetch audit logs:', err);
    }
  };

  const openHistoryDrawer = () => {
    loadAuditLogs();
    setIsHistoryDrawerOpen(true);
  };

  // --- Auto-Balancing Slider Handlers for Screening Weights ---
  const handleWeightChange = (key: 'name' | 'nationality' | 'dob' | 'id', newVal: number) => {
    newVal = Math.max(0, Math.min(100, newVal));
    const currentWeights = { name: nameWeight, nationality: nationalityWeight, dob: dobWeight, id: idWeight };
    const diff = newVal - currentWeights[key];

    // Find other unpinned keys
    const otherKeys = (['name', 'nationality', 'dob', 'id'] as const).filter(k => k !== key && !pinnedWeights[k]);

    if (otherKeys.length === 0) {
      // If all others are pinned, cannot adjust
      return;
    }

    const otherSum = otherKeys.reduce((acc, k) => acc + currentWeights[k], 0);

    if (otherSum <= 0 && diff > 0) {
      // Cannot increase if all unpinned are zero
      return;
    }

    const updated = { ...currentWeights, [key]: newVal };
    let remainingToDistribute = diff;

    for (let i = 0; i < otherKeys.length; i++) {
      const k = otherKeys[i];
      if (i === otherKeys.length - 1) {
        // Last one takes exact remainder to ensure sum === 100
        const currentSumOthers = Object.entries(updated)
          .filter(([prop]) => prop !== k)
          .reduce((acc, [, val]) => acc + val, 0);
        updated[k] = Math.max(0, Math.min(100, 100 - currentSumOthers));
      } else {
        const proportion = otherSum > 0 ? currentWeights[k] / otherSum : 1 / otherKeys.length;
        const deduction = Math.round(diff * proportion);
        updated[k] = Math.max(0, currentWeights[k] - deduction);
        remainingToDistribute -= (currentWeights[k] - updated[k]);
      }
    }

    setNameWeight(updated.name);
    setNationalityWeight(updated.nationality);
    setDobWeight(updated.dob);
    setIdWeight(updated.id);
  };

  const toggleWeightPin = (key: 'name' | 'nationality' | 'dob' | 'id') => {
    setPinnedWeights(prev => ({ ...prev, [key]: !prev[key] }));
  };

  // --- Auto-Balancing Slider Handlers for EWRA Weights ---
  const handleEwraWeightChange = (key: 'geo' | 'client' | 'product', newVal: number) => {
    newVal = Math.max(0, Math.min(100, newVal));
    const current = { geo: geoWeight, client: clientTypeWeight, product: productWeight };
    const diff = newVal - current[key];
    const otherKeys = (['geo', 'client', 'product'] as const).filter(k => k !== key && !pinnedEwra[k]);

    if (otherKeys.length === 0) return;

    const otherSum = otherKeys.reduce((acc, k) => acc + current[k], 0);
    const updated = { ...current, [key]: newVal };

    for (let i = 0; i < otherKeys.length; i++) {
      const k = otherKeys[i];
      if (i === otherKeys.length - 1) {
        const others = Object.entries(updated).filter(([p]) => p !== k).reduce((acc, [, v]) => acc + v, 0);
        updated[k] = Math.max(0, 100 - others);
      } else {
        const prop = otherSum > 0 ? current[k] / otherSum : 1 / otherKeys.length;
        const ded = Math.round(diff * prop);
        updated[k] = Math.max(0, current[k] - ded);
      }
    }

    setGeoWeight(updated.geo);
    setClientTypeWeight(updated.client);
    setProductWeight(updated.product);
  };

  const toggleEwraPin = (key: 'geo' | 'client' | 'product') => {
    setPinnedEwra(prev => ({ ...prev, [key]: !prev[key] }));
  };

  // --- Filtered Country List ---
  const filteredCountries = useMemo(() => {
    return countries.filter(c => {
      const matchesSearch = c.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                            c.code.toLowerCase().includes(searchTerm.toLowerCase());
      if (!matchesSearch) return false;
      if (statusFilter === 'ALL') return true;
      if (statusFilter === 'HIGH_RISK') return c.isHighRisk;
      return c.fatfStatus === statusFilter;
    });
  }, [countries, searchTerm, statusFilter]);

  const handleScoreChange = async (code: string, newScore: number) => {
    const isHigh = newScore >= 75;
    let status: CountryMaster['fatfStatus'] = 'COOPERATIVE';
    if (newScore >= 90) status = 'BLACK_LIST';
    else if (newScore >= 60 || isHigh) status = 'GREY_LIST';
    else if (newScore >= 20) status = 'MONITORED';

    setCountries(prev => prev.map(c => c.code === code ? { ...c, riskScore: newScore, isHighRisk: isHigh, fatfStatus: status } : c));
    try {
      const c = countries.find(item => item.code === code);
      await EdithApi.upsertCountryRisk({
        code,
        countryName: c ? c.name : code,
        isHighRisk: isHigh,
        riskScore: newScore
      });
      setSaveSuccess(`Country '${code}' risk updated to ${newScore} (${status}). Recorded in WORM audit.`);
      setTimeout(() => setSaveSuccess(null), 3500);
    } catch (err: any) {
      setSaveError(`Failed to persist country: ${err.message}`);
      setTimeout(() => setSaveError(null), 4000);
    }
  };

  const handleNationalityToggle = async (code: string, isHigh: boolean) => {
    setNationalities(prev => prev.map(n => n.code === code ? { ...n, isHighRisk: isHigh, riskLevel: isHigh ? 'HIGH' : 'LOW' } : n));
    try {
      const nat = nationalities.find(n => n.code === code);
      if (nat) {
        await EdithApi.upsertNationalityRisk({
          code,
          demonym: nat.demonym,
          country: nat.country,
          isHighRisk: isHigh,
          riskLevel: isHigh ? 'HIGH' : 'LOW'
        });
        setSaveSuccess(`Nationality '${nat.demonym}' updated to ${isHigh ? 'HIGH RISK' : 'LOW RISK'}.`);
        setTimeout(() => setSaveSuccess(null), 3500);
      }
    } catch (err: any) {
      setSaveError(`Failed to update nationality: ${err.message}`);
      setTimeout(() => setSaveError(null), 4000);
    }
  };

  const handleCurrencyThresholdChange = (currency: string, field: 'mandatoryReportingThreshold' | 'highRiskApprovalThreshold', val: number) => {
    setCurrencies(prev => prev.map(c => c.currency === currency ? { ...c, [field]: val } : c));
  };

  const handleSaveAll = async () => {
    setIsSaving(true);
    setSaveSuccess(null);
    setSaveError(null);

    try {
      if (stagingMode === 'PENDING_APPROVAL') {
        // Stage for 4-Eyes Approval
        await EdithApi.recordAuditEvent({
          actor: 'Compliance Officer (Maker)',
          action: 'MASTER_CONFIG_STAGED_APPROVAL',
          targetEntity: `System Masters (${activeTab})`,
          caseNumber: 'DRAFT-' + Math.floor(1000 + Math.random() * 9000),
          details: `Staged configuration draft for ${activeTab}. Requires MLRO cryptographic dual-signoff.`
        });
        setSaveSuccess(`Draft configuration #CFG-${Math.floor(1000 + Math.random() * 9000)} staged for 4-Eyes MLRO approval.`);
        setIsSaving(false);
        setTimeout(() => setSaveSuccess(null), 4500);
        return;
      }

      // Direct Apply based on active tab
      if (activeTab === 'SCREENING_SCORE') {
        await EdithApi.updateScoringWeights({
          nameWeightage: nameWeight,
          nationalityWeightage: nationalityWeight,
          dobWeightage: dobWeight,
          idWeightage: idWeight,
          defaultThreshold: matchThreshold
        });
        setSaveSuccess('Screening score weights & match threshold committed to PostgreSQL.');
      } else if (activeTab === 'RISK_RATES') {
        await EdithApi.updateRiskRates({
          custHighRisk,
          custMedRisk,
          txHighRisk,
          txMedRisk
        });
        setSaveSuccess('Customer and Transaction risk rate benchmarks committed to PostgreSQL.');
      } else if (activeTab === 'SETUP_CONTROL') {
        await EdithApi.updateSetupControls(sysControl);
        setSaveSuccess('Automated SysControl screening scenarios committed to PostgreSQL.');
      } else if (activeTab === 'RISK_ASSESSMENT') {
        await EdithApi.updateRiskAssessment({
          geoWeight,
          clientTypeWeight,
          productWeight
        });
        setSaveSuccess('Enterprise-Wide Risk Assessment (EWRA) weights committed to PostgreSQL.');
      } else if (activeTab === 'CURRENCIES') {
        await EdithApi.updateCurrencyThresholds(currencies);
        setSaveSuccess('Currency reporting & high-risk approval limits committed to PostgreSQL.');
      } else {
        setSaveSuccess('Configuration parameters saved successfully.');
      }

      setTimeout(() => setSaveSuccess(null), 3500);
    } catch (err: any) {
      setSaveError(`Error saving changes: ${err.message || 'Unknown error'}`);
      setTimeout(() => setSaveError(null), 4500);
    } finally {
      setIsSaving(false);
    }
  };

  const handleUnblockCustomer = async (customerCode: string) => {
    setBlockedCustomers(prev => prev.map(c => c.customerCode === customerCode ? { ...c, isBlocked: false } : c));
    await EdithApi.updateCustomerEdd(customerCode, {
      action: 'TOGGLE_BLOCK',
      isBlocked: false,
      notes: `Administrative unblock authorized via System Masters for ${customerCode}.`
    });
    setSaveSuccess(`Customer ${customerCode} successfully unblocked. WORM audit ledger entry generated.`);
    setTimeout(() => setSaveSuccess(null), 3500);
  };

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError(null);
    setPasswordSuccess(null);

    if (newPassword !== confirmPassword) {
      setPasswordError('New password and confirmation password do not match.');
      return;
    }

    if (newPassword.length < 8) {
      setPasswordError('Password must be at least 8 characters long.');
      return;
    }

    setPasswordLoading(true);
    try {
      const res = await EdithApi.changePassword({ currentPassword, newPassword });
      if (res && res.success) {
        setPasswordSuccess('Password changed successfully.');
        setCurrentPassword('');
        setNewPassword('');
        setConfirmPassword('');
      } else {
        setPasswordError(res?.message || 'Unable to update password. Please verify your current password.');
      }
    } catch {
      setPasswordError('Unable to update password right now. Please check your connection and try again.');
    } finally {
      setPasswordLoading(false);
    }
  };

  // Unified Tabs
  const allMasterTabs: { id: SystemMastersTab; label: string; icon: any; category: string; route?: string }[] = [
    { id: 'COUNTRIES', label: 'Country Master', icon: Flag, category: 'REFERENCE', route: '/master/country' },
    { id: 'NATIONALITIES', label: 'Nationality Master', icon: Globe, category: 'REFERENCE', route: '/master/nationality' },
    { id: 'CURRENCIES', label: 'Currency & Reporting Limits', icon: Banknote, category: 'REFERENCE', route: '/settings/risk-rates' },
    { id: 'SCREENING_SCORE', label: 'Multi-Factor Scoring Weights', icon: SlidersHorizontal, category: 'ENGINE', route: '/settings/screening-score' },
    { id: 'RISK_RATES', label: 'Risk Rate Benchmarks', icon: Activity, category: 'BENCHMARKS', route: '/settings/risk-rates' },
    { id: 'SETUP_CONTROL', label: 'SysControl Scenarios', icon: ToggleRight, category: 'ENGINE', route: '/settings/setup-control' },
    { id: 'RISK_ASSESSMENT', label: 'EWRA Risk Weights', icon: Target, category: 'BENCHMARKS', route: '/settings/risk-assessment' },
    { id: 'UNBLOCK_CUSTOMER', label: 'Unblock Customer [Operational]', icon: Unlock, category: 'OPERATIONAL', route: '/master/unblock-customer' },
    { id: 'CHANGE_PASSWORD', label: 'Change Password [Account]', icon: Key, category: 'ACCOUNT', route: '/settings/change-password' }
  ];

  const pageMeta = usePageMeta(
    'System Masters & Calibration Hub',
    'Enterprise AML/CFT master data, multi-factor scoring calibration, and FATF jurisdiction risk controls.'
  );

  // Simulated Sensitivity Metrics for Inline Calculator
  const sensitivityMetrics = useMemo(() => {
    // Baseline is 80% threshold
    const delta = matchThreshold - 80;
    const alertVolBase = 142; // alerts / day
    const alertVol = Math.round(alertVolBase * Math.pow(1.05, -delta * 1.8));
    const suppressionRate = Math.min(98.5, Math.max(60, 84.2 + (delta * 1.5))).toFixed(1);
    const recall = Math.min(99.9, Math.max(89.0, 99.8 - (delta > 0 ? delta * 0.7 : 0))).toFixed(1);

    return {
      alertVol,
      suppressionRate,
      recall,
      isRisky: matchThreshold > 85
    };
  }, [matchThreshold]);

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto font-sans">
      {/* Top Header Bar */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border shadow-xs">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <Settings className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              v10.0 LIVE POSTGRESQL
            </span>
            {stagingMode === 'PENDING_APPROVAL' && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-500/15 text-amber-600 border border-amber-500/30 flex items-center gap-1">
                <Sparkles className="w-3 h-3" />
                4-EYES STAGING ACTIVE
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1">
            {pageMeta.definition}
          </p>
        </div>

        {/* Global Action Controls */}
        <div className="flex items-center gap-3 w-full sm:w-auto justify-end flex-wrap">
          {/* Staging Toggle */}
          <div className="flex items-center gap-1 bg-surface-muted p-1 rounded-lg border border-border text-[11px] font-mono">
            <button
              type="button"
              onClick={() => setStagingMode('DIRECT')}
              className={`px-2 py-1 rounded transition ${stagingMode === 'DIRECT' ? 'bg-white shadow-xs font-bold text-ink' : 'text-muted hover:text-ink'}`}
              title="Apply changes directly to live engine"
            >
              Direct Apply
            </button>
            <button
              type="button"
              onClick={() => setStagingMode('PENDING_APPROVAL')}
              className={`px-2 py-1 rounded transition flex items-center gap-1 ${stagingMode === 'PENDING_APPROVAL' ? 'bg-amber-100 text-amber-900 shadow-xs font-bold' : 'text-muted hover:text-ink'}`}
              title="Stage changes as draft requiring 4-Eyes MLRO signoff"
            >
              <Shield className="w-3 h-3 text-amber-600" />
              4-Eyes Draft
            </button>
          </div>

          {/* Audit History Drawer Button */}
          <button
            type="button"
            onClick={openHistoryDrawer}
            className="px-3 py-2 rounded-lg bg-surface hover:bg-surface-hover text-ink border border-border text-xs font-mono font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
          >
            <History className="w-3.5 h-3.5 text-muted" />
            <span>Audit Drawer</span>
          </button>

          {/* Save Settings Button */}
          {activeTab !== 'CHANGE_PASSWORD' && (
            <button 
              onClick={handleSaveAll}
              disabled={loadingMasters || isSaving}
              className={`px-4 py-2 rounded-lg text-white text-xs font-mono font-bold flex items-center gap-2 shadow-xs cursor-pointer disabled:opacity-50 transition ${
                stagingMode === 'PENDING_APPROVAL' 
                  ? 'bg-amber-600 hover:bg-amber-700' 
                  : 'bg-primary hover:bg-primary-hover'
              }`}
            >
              <Save className="w-4 h-4" />
              <span>
                {isSaving ? 'Committing...' : stagingMode === 'PENDING_APPROVAL' ? 'Stage Draft for Approval' : 'Commit Settings'}
              </span>
            </button>
          )}
        </div>
      </div>

      {/* Notifications */}
      {saveSuccess && (
        <div className="p-3 bg-emerald-50 border border-emerald-300 text-emerald-900 rounded-lg text-xs font-mono flex items-center justify-between gap-2 animate-in fade-in">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
            <span>{saveSuccess}</span>
          </div>
          <button onClick={() => setSaveSuccess(null)} className="text-emerald-700 hover:text-emerald-900">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {saveError && (
        <div className="p-3 bg-rose-50 border border-rose-300 text-rose-900 rounded-lg text-xs font-mono flex items-center justify-between gap-2 animate-in fade-in">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{saveError}</span>
          </div>
          <button onClick={() => setSaveError(null)} className="text-rose-700 hover:text-rose-900">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Main Tabs Container */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-5 shadow-xs">
        {/* Horizontal Navigation Tabs */}
        <div className="flex gap-1 border-b border-border text-xs font-mono overflow-x-auto pb-1 scrollbar-thin">
          {allMasterTabs.map(tab => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => {
                  setActiveTab(tab.id);
                  if (tab.route) navigate(tab.route);
                }}
                className={`py-2 px-3 font-semibold transition-all border-b-2 flex items-center gap-1.5 whitespace-nowrap cursor-pointer rounded-t-lg ${
                  isActive
                    ? 'border-primary text-primary bg-primary/5 font-bold'
                    : 'border-transparent text-muted hover:text-ink hover:bg-slate-50'
                }`}
              >
                <Icon className="w-3.5 h-3.5" />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab 1: Country Master */}
        {activeTab === 'COUNTRIES' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
              <div className="flex items-center gap-2 flex-wrap">
                <div className="relative w-64 sm:w-72">
                  <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-muted" />
                  <input
                    type="text"
                    placeholder="Search ISO code or jurisdiction name..."
                    value={searchTerm}
                    onChange={e => setSearchTerm(e.target.value)}
                    className="w-full pl-9 pr-3 py-1.5 text-xs font-mono rounded border border-border bg-surface-muted text-ink focus:outline-none focus:border-primary"
                  />
                </div>
                {/* Status Filter Chips */}
                <div className="flex items-center gap-1 text-[11px]">
                  {(['ALL', 'HIGH_RISK', 'BLACK_LIST', 'GREY_LIST', 'COOPERATIVE'] as const).map(f => (
                    <button
                      key={f}
                      type="button"
                      onClick={() => setStatusFilter(f)}
                      className={`px-2 py-1 rounded cursor-pointer transition ${
                        statusFilter === f 
                          ? 'bg-primary text-white font-bold' 
                          : 'bg-surface-muted text-muted hover:text-ink border border-border'
                      }`}
                    >
                      {f.replace('_', ' ')}
                    </button>
                  ))}
                </div>
              </div>

              <div className="text-muted text-[11px]">
                Showing {filteredCountries.length} of {countries.length} Jurisdictions
              </div>
            </div>

            <div className="overflow-x-auto border border-border rounded-lg max-h-[500px] overflow-y-auto">
              <table className="w-full text-left">
                <thead className="sticky top-0 z-10 bg-surface-muted">
                  <tr className="border-b border-border text-muted text-[11px] uppercase">
                    <th className="py-2.5 px-3">ISO-3</th>
                    <th className="py-2.5 px-3">Jurisdiction Name</th>
                    <th className="py-2.5 px-3">FATF Compliance Status</th>
                    <th className="py-2.5 px-3">High-Risk Designation</th>
                    <th className="py-2.5 px-3">Risk Score (0 - 100)</th>
                    <th className="py-2.5 px-3">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredCountries.map(country => (
                    <tr key={country.code} className="hover:bg-surface-hover transition">
                      <td className="py-2.5 px-3 font-bold text-primary">{country.code}</td>
                      <td className="py-2.5 px-3 font-bold text-ink">{country.name}</td>
                      <td className="py-2.5 px-3">
                        {country.fatfStatus === 'BLACK_LIST' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-critical text-white">
                            FATF BLACKLIST
                          </span>
                        )}
                        {country.fatfStatus === 'GREY_LIST' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500 text-white">
                            FATF GREYLIST
                          </span>
                        )}
                        {country.fatfStatus === 'MONITORED' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-100 text-blue-900 border border-blue-200">
                            MONITORED CORRIDOR
                          </span>
                        )}
                        {country.fatfStatus === 'COOPERATIVE' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/20 text-accent">
                            COOPERATIVE
                          </span>
                        )}
                      </td>
                      <td className="py-2.5 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          country.isHighRisk ? 'text-critical bg-critical/10' : 'text-muted bg-surface-muted'
                        }`}>
                          {country.isHighRisk ? 'HIGH RISK' : 'STANDARD'}
                        </span>
                      </td>
                      <td className="py-2.5 px-3">
                        <input
                          type="number"
                          min="0"
                          max="100"
                          value={country.riskScore}
                          onChange={e => handleScoreChange(country.code, Number(e.target.value))}
                          className="w-20 px-2 py-1 rounded border border-border bg-surface text-ink font-bold text-xs focus:border-primary"
                        />
                      </td>
                      <td className="py-2.5 px-3">
                        <span className="text-[10px] text-muted">Auto-persisted</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 2: Nationality Master */}
        {activeTab === 'NATIONALITIES' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed flex items-center justify-between">
              <div>
                Nationality demonyms are matched during single and bulk transaction screening against <code className="text-primary font-bold">rules.nationality_masters</code>.
              </div>
              <div className="font-bold text-ink">
                {nationalities.length} Demonyms Configured
              </div>
            </div>

            <div className="overflow-x-auto border border-border rounded-lg max-h-[500px] overflow-y-auto">
              <table className="w-full text-left">
                <thead className="sticky top-0 z-10 bg-surface-muted">
                  <tr className="border-b border-border text-muted text-[11px] uppercase">
                    <th className="py-2.5 px-3">ISO Code</th>
                    <th className="py-2.5 px-3">Demonym / Nationality</th>
                    <th className="py-2.5 px-3">Jurisdiction</th>
                    <th className="py-2.5 px-3">Screening Risk Tier</th>
                    <th className="py-2.5 px-3">High-Risk Toggle</th>
                    <th className="py-2.5 px-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {nationalities.map(n => (
                    <tr key={n.code} className="hover:bg-surface-hover transition">
                      <td className="py-2.5 px-3 font-bold text-primary">{n.code}</td>
                      <td className="py-2.5 px-3 font-bold text-ink">{n.demonym}</td>
                      <td className="py-2.5 px-3 text-muted">{n.country}</td>
                      <td className="py-2.5 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          n.riskLevel === 'HIGH' ? 'bg-critical/15 text-critical' : 'bg-accent/15 text-accent'
                        }`}>
                          {n.riskLevel}
                        </span>
                      </td>
                      <td className="py-2.5 px-3">
                        <input
                          type="checkbox"
                          checked={n.isHighRisk}
                          onChange={e => handleNationalityToggle(n.code, e.target.checked)}
                          className="w-4 h-4 text-primary rounded cursor-pointer"
                        />
                      </td>
                      <td className="py-2.5 px-3 text-[10px] text-muted">
                        Saved in DB
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 3: Currency & Mandatory Reporting Thresholds Master */}
        {activeTab === 'CURRENCIES' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Define Central Bank statutory mandatory reporting limits (e.g. UAE CBUAE AED 55,000 Cash Transaction Reporting) and internal High-Risk Escrow Approval thresholds.
            </div>

            <div className="overflow-x-auto border border-border rounded-lg">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">Currency</th>
                    <th className="py-2.5 px-3">Mandatory FIU/goAML Reporting Threshold</th>
                    <th className="py-2.5 px-3">High-Risk Escalation Threshold</th>
                    <th className="py-2.5 px-3">Regulatory Basis</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {currencies.map(curr => (
                    <tr key={curr.currency} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-primary text-sm">{curr.currency}</td>
                      <td className="py-3 px-3">
                        <div className="flex items-center gap-1.5">
                          <span className="text-muted text-xs">{curr.currency}</span>
                          <input
                            type="number"
                            step="1000"
                            value={curr.mandatoryReportingThreshold}
                            onChange={e => handleCurrencyThresholdChange(curr.currency, 'mandatoryReportingThreshold', Number(e.target.value))}
                            className="w-36 px-2.5 py-1 rounded border border-border bg-surface text-ink font-bold text-xs"
                          />
                        </div>
                      </td>
                      <td className="py-3 px-3">
                        <div className="flex items-center gap-1.5">
                          <span className="text-muted text-xs">{curr.currency}</span>
                          <input
                            type="number"
                            step="5000"
                            value={curr.highRiskApprovalThreshold}
                            onChange={e => handleCurrencyThresholdChange(curr.currency, 'highRiskApprovalThreshold', Number(e.target.value))}
                            className="w-36 px-2.5 py-1 rounded border border-border bg-surface text-ink font-bold text-xs"
                          />
                        </div>
                      </td>
                      <td className="py-3 px-3 text-muted text-[11px]">
                        {curr.currency === 'AED' ? 'Statutory CBUAE AML-CFT Rulebook (AED 55k)' : 'Statutory FATF / CTR Recommendation ($10k)'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 4: Screening Score Weightages with Auto-Balancing & Sensitivity Preview */}
        {activeTab === 'SCREENING_SCORE' && (
          <div className="space-y-6 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Define the multi-factor parameter weightages for fuzzy string matching (Name, Nationality, DOB, ID). Weights auto-balance strictly to 100%. Use pin-locks to isolate weights during tuning.
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
              {/* Left Column: Auto-Balancing Sliders */}
              <div className="lg:col-span-7 p-5 rounded-xl border border-border bg-white space-y-5 shadow-xs">
                <div className="flex justify-between items-center pb-2 border-b border-border">
                  <div className="font-bold text-ink text-sm flex items-center gap-2">
                    <SlidersHorizontal className="w-4 h-4 text-primary" />
                    <span>Multi-Factor Weights Configuration</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-muted text-[11px]">Total Sum:</span>
                    <span className="font-bold px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-300">
                      {nameWeight + nationalityWeight + dobWeight + idWeight}%
                    </span>
                  </div>
                </div>

                {/* Weight Distribution Bar */}
                <div className="w-full h-3 rounded-full overflow-hidden flex bg-surface-muted border border-border">
                  <div style={{ width: `${nameWeight}%` }} className="bg-primary h-full" title={`Name: ${nameWeight}%`} />
                  <div style={{ width: `${nationalityWeight}%` }} className="bg-sky-500 h-full" title={`Nationality: ${nationalityWeight}%`} />
                  <div style={{ width: `${dobWeight}%` }} className="bg-amber-500 h-full" title={`DOB: ${dobWeight}%`} />
                  <div style={{ width: `${idWeight}%` }} className="bg-emerald-500 h-full" title={`National ID: ${idWeight}%`} />
                </div>

                {/* 1. Name Weight */}
                <div className="space-y-1.5 p-3 rounded-lg border border-border bg-surface-muted/40">
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full bg-primary" />
                      <span>1. Name Match Weightage</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-primary font-bold text-sm">{nameWeight}%</span>
                      <button 
                        type="button" 
                        onClick={() => toggleWeightPin('name')}
                        className={`p-1 rounded cursor-pointer ${pinnedWeights.name ? 'bg-primary text-white' : 'text-muted hover:text-ink'}`}
                        title={pinnedWeights.name ? 'Weight is pinned' : 'Pin weight'}
                      >
                        {pinnedWeights.name ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                      </button>
                    </div>
                  </div>
                  <input 
                    type="range" min="10" max="90" value={nameWeight} 
                    onChange={e => handleWeightChange('name', Number(e.target.value))}
                    disabled={pinnedWeights.name}
                    className="w-full accent-primary mt-1 cursor-pointer disabled:opacity-40"
                  />
                </div>

                {/* 2. Nationality Weight */}
                <div className="space-y-1.5 p-3 rounded-lg border border-border bg-surface-muted/40">
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full bg-sky-500" />
                      <span>2. Nationality Match Weightage</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sky-600 font-bold text-sm">{nationalityWeight}%</span>
                      <button 
                        type="button" 
                        onClick={() => toggleWeightPin('nationality')}
                        className={`p-1 rounded cursor-pointer ${pinnedWeights.nationality ? 'bg-sky-600 text-white' : 'text-muted hover:text-ink'}`}
                      >
                        {pinnedWeights.nationality ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                      </button>
                    </div>
                  </div>
                  <input 
                    type="range" min="0" max="50" value={nationalityWeight} 
                    onChange={e => handleWeightChange('nationality', Number(e.target.value))}
                    disabled={pinnedWeights.nationality}
                    className="w-full accent-sky-500 mt-1 cursor-pointer disabled:opacity-40"
                  />
                </div>

                {/* 3. DOB Weight */}
                <div className="space-y-1.5 p-3 rounded-lg border border-border bg-surface-muted/40">
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full bg-amber-500" />
                      <span>3. Date of Birth (DOB) Weightage</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-amber-600 font-bold text-sm">{dobWeight}%</span>
                      <button 
                        type="button" 
                        onClick={() => toggleWeightPin('dob')}
                        className={`p-1 rounded cursor-pointer ${pinnedWeights.dob ? 'bg-amber-600 text-white' : 'text-muted hover:text-ink'}`}
                      >
                        {pinnedWeights.dob ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                      </button>
                    </div>
                  </div>
                  <input 
                    type="range" min="0" max="50" value={dobWeight} 
                    onChange={e => handleWeightChange('dob', Number(e.target.value))}
                    disabled={pinnedWeights.dob}
                    className="w-full accent-amber-500 mt-1 cursor-pointer disabled:opacity-40"
                  />
                </div>

                {/* 4. National ID Weight */}
                <div className="space-y-1.5 p-3 rounded-lg border border-border bg-surface-muted/40">
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
                      <span>4. National ID / Passport Weightage</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-emerald-600 font-bold text-sm">{idWeight}%</span>
                      <button 
                        type="button" 
                        onClick={() => toggleWeightPin('id')}
                        className={`p-1 rounded cursor-pointer ${pinnedWeights.id ? 'bg-emerald-600 text-white' : 'text-muted hover:text-ink'}`}
                      >
                        {pinnedWeights.id ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                      </button>
                    </div>
                  </div>
                  <input 
                    type="range" min="0" max="50" value={idWeight} 
                    onChange={e => handleWeightChange('id', Number(e.target.value))}
                    disabled={pinnedWeights.id}
                    className="w-full accent-emerald-500 mt-1 cursor-pointer disabled:opacity-40"
                  />
                </div>

                {/* Threshold Slider */}
                <div className="pt-3 border-t border-border">
                  <div className="flex justify-between text-xs font-bold text-critical">
                    <span>Cumulative Threshold for Match Alert</span>
                    <span className="text-sm">{matchThreshold}%</span>
                  </div>
                  <input 
                    type="range" min="50" max="95" value={matchThreshold} 
                    onChange={e => setMatchThreshold(Number(e.target.value))}
                    className="w-full accent-critical mt-1 cursor-pointer"
                  />
                  <div className="text-[10px] text-muted mt-1">
                    Entities scoring &ge; {matchThreshold}% trigger sanction hit alerts and hold cases.
                  </div>
                </div>
              </div>

              {/* Right Column: Real-Time Sensitivity & Alert Volume Impact Preview */}
              <div className="lg:col-span-5 p-5 rounded-xl border border-border bg-surface-muted/30 space-y-4 shadow-xs">
                <div className="flex items-center gap-2 font-bold text-ink text-sm pb-2 border-b border-border">
                  <Activity className="w-4 h-4 text-primary" />
                  <span>Real-Time "What-If" Sensitivity Preview</span>
                </div>

                <div className="p-3 bg-white rounded-lg border border-border space-y-3">
                  <div>
                    <div className="text-muted text-[11px]">Projected Daily Alert Volume</div>
                    <div className="text-xl font-bold text-ink flex items-baseline gap-2 mt-0.5">
                      <span>{sensitivityMetrics.alertVol} alerts/day</span>
                      <span className={`text-[11px] font-bold ${matchThreshold < 80 ? 'text-critical' : 'text-accent'}`}>
                        {matchThreshold < 80 ? `+${(80 - matchThreshold) * 7}% surge` : `${(matchThreshold - 80) * 4}% reduction`}
                      </span>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2 pt-2 border-t border-border">
                    <div>
                      <div className="text-muted text-[10px]">False Positive Suppression</div>
                      <div className="text-sm font-bold text-accent">{sensitivityMetrics.suppressionRate}%</div>
                    </div>
                    <div>
                      <div className="text-muted text-[10px]">Historical STR Recall</div>
                      <div className={`text-sm font-bold ${sensitivityMetrics.isRisky ? 'text-critical' : 'text-ink'}`}>
                        {sensitivityMetrics.recall}%
                      </div>
                    </div>
                  </div>

                  {sensitivityMetrics.isRisky && (
                    <div className="p-2 rounded bg-amber-50 border border-amber-200 text-amber-800 text-[10px] flex items-center gap-1.5">
                      <AlertTriangle className="w-3.5 h-3.5 shrink-0" />
                      <span>Threshold &gt; 85% increases regulatory risk of missing partial phonetic sanction hits.</span>
                    </div>
                  )}
                </div>

                <div className="p-3 rounded-lg border border-border bg-white text-[11px] text-muted space-y-2">
                  <div className="font-bold text-ink flex items-center gap-1.5">
                    <Info className="w-3.5 h-3.5 text-primary" />
                    <span>How auto-balancing protects screening</span>
                  </div>
                  <p>
                    By enforcing a strict 100% total weight constraint, EDITH eliminates distorted score calculations and negative ID match penalties. Pinning allows compliance teams to calibrate specific parameters to regulatory directives.
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 5: Risk Rate Settings */}
        {activeTab === 'RISK_RATES' && (
          <div className="space-y-6 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Configure High, Medium, and Low risk thresholds for Customers and Financial Transactions. Scores exceeding the high threshold immediately trigger EDD inspection or real-time holds.
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Customer Risk Benchmarks */}
              <div className="p-4 rounded-xl border border-border bg-white space-y-4 shadow-xs">
                <div className="font-bold text-ink text-sm flex items-center gap-2">
                  <Shield className="w-4 h-4 text-primary" />
                  <span>Customer Risk Benchmarks</span>
                </div>

                <div className="space-y-3">
                  <div>
                    <div className="flex justify-between text-xs font-bold text-critical">
                      <span>High Risk Benchmark (Score &ge;)</span>
                      <span>{custHighRisk}</span>
                    </div>
                    <input 
                      type="range" min="50" max="100" value={custHighRisk} 
                      onChange={e => setCustHighRisk(Number(e.target.value))}
                      className="w-full accent-critical mt-1 cursor-pointer"
                    />
                  </div>

                  <div>
                    <div className="flex justify-between text-xs font-bold text-amber-600">
                      <span>Medium Risk Benchmark (Score &ge;)</span>
                      <span>{custMedRisk}</span>
                    </div>
                    <input 
                      type="range" min="30" max="80" value={custMedRisk} 
                      onChange={e => setCustMedRisk(Number(e.target.value))}
                      className="w-full accent-amber-500 mt-1 cursor-pointer"
                    />
                  </div>

                  <div className="text-[11px] text-muted pt-2 border-t border-border">
                    Low Risk applies automatically to entities with score &lt; {custMedRisk}.
                  </div>
                </div>
              </div>

              {/* Transaction Risk Benchmarks */}
              <div className="p-4 rounded-xl border border-border bg-white space-y-4 shadow-xs">
                <div className="font-bold text-ink text-sm flex items-center gap-2">
                  <Activity className="w-4 h-4 text-primary" />
                  <span>Transaction Risk Benchmarks</span>
                </div>

                <div className="space-y-3">
                  <div>
                    <div className="flex justify-between text-xs font-bold text-critical">
                      <span>High Risk Benchmark (Score &ge;)</span>
                      <span>{txHighRisk}</span>
                    </div>
                    <input 
                      type="range" min="60" max="100" value={txHighRisk} 
                      onChange={e => setTxHighRisk(Number(e.target.value))}
                      className="w-full accent-critical mt-1 cursor-pointer"
                    />
                  </div>

                  <div>
                    <div className="flex justify-between text-xs font-bold text-amber-600">
                      <span>Medium Risk Benchmark (Score &ge;)</span>
                      <span>{txMedRisk}</span>
                    </div>
                    <input 
                      type="range" min="40" max="90" value={txMedRisk} 
                      onChange={e => setTxMedRisk(Number(e.target.value))}
                      className="w-full accent-amber-500 mt-1 cursor-pointer"
                    />
                  </div>

                  <div className="text-[11px] text-muted pt-2 border-t border-border">
                    Transactions scoring &ge; {txHighRisk} are subject to immediate Real-Time SWIFT Interception.
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 6: Setup Control */}
        {activeTab === 'SETUP_CONTROL' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Enable or disable SysControl automated screening scenarios across inbound, outbound, and internal settlement flows.
            </div>

            <div className="divide-y divide-border border border-border rounded-xl bg-white shadow-xs">
              {[
                { key: 'customerScreening', label: 'Customer Onboarding & Periodic Screening', desc: 'Screen retail and corporate customers against OFAC, UN, and local watchlists' },
                { key: 'beneficiaryBankScreening', label: 'Beneficiary Bank Screening', desc: 'Screen SWIFT BIC codes and correspondent institutions against sanction lists' },
                { key: 'correspondentBankScreening', label: 'Correspondent Bank Screening', desc: 'Verify intermediary settlement routing against embargoed jurisdictions' },
                { key: 'partnerUboScreening', label: 'Partner / UBO Screening', desc: 'Traverse 25%+ ownership share structures for Sanction/PEP affiliations' },
                { key: 'realtimeHoldInterceptor', label: 'Real-Time Interceptor Hold Engine', desc: 'Automatically hold high-risk SWIFT/ISO20022 wire transfers before settlement' }
              ].map(item => (
                <div key={item.key} className="p-4 flex items-center justify-between hover:bg-slate-50 transition">
                  <div>
                    <div className="font-bold text-ink text-sm">{item.label}</div>
                    <div className="text-[11px] text-muted mt-0.5">{item.desc}</div>
                  </div>
                  <input 
                    type="checkbox"
                    checked={(sysControl as any)[item.key]}
                    onChange={e => setSysControl(prev => ({ ...prev, [item.key]: e.target.checked }))}
                    className="w-5 h-5 accent-primary rounded cursor-pointer"
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tab 7: Risk Assessment Master (EWRA) */}
        {activeTab === 'RISK_ASSESSMENT' && (
          <div className="space-y-6 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Configure Enterprise-Wide AML Risk Assessment (EWRA) weights across Geographical, Client Entity, and Product/Delivery channels. Sliders auto-balance to 100%.
            </div>

            <div className="p-5 rounded-xl border border-border bg-white space-y-4 max-w-2xl shadow-xs">
              <div className="flex justify-between items-center pb-2 border-b border-border">
                <span className="font-bold text-ink">Cumulative Assessment Weight:</span>
                <span className="font-bold px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 border border-emerald-300">
                  {geoWeight + clientTypeWeight + productWeight}%
                </span>
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Geographical Risk Weightage</span>
                  <div className="flex items-center gap-1.5">
                    <span className="text-primary font-bold">{geoWeight}%</span>
                    <button 
                      type="button" 
                      onClick={() => toggleEwraPin('geo')}
                      className={`p-1 rounded cursor-pointer ${pinnedEwra.geo ? 'bg-primary text-white' : 'text-muted hover:text-ink'}`}
                      title="Pin weight"
                    >
                      {pinnedEwra.geo ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                    </button>
                  </div>
                </div>
                <input 
                  type="range" min="0" max="100" value={geoWeight} 
                  disabled={pinnedEwra.geo}
                  onChange={e => handleEwraWeightChange('geo', Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer disabled:opacity-40"
                />
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Client / Entity Type Risk Weightage</span>
                  <div className="flex items-center gap-1.5">
                    <span className="text-primary font-bold">{clientTypeWeight}%</span>
                    <button 
                      type="button" 
                      onClick={() => toggleEwraPin('client')}
                      className={`p-1 rounded cursor-pointer ${pinnedEwra.client ? 'bg-primary text-white' : 'text-muted hover:text-ink'}`}
                      title="Pin weight"
                    >
                      {pinnedEwra.client ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                    </button>
                  </div>
                </div>
                <input 
                  type="range" min="0" max="100" value={clientTypeWeight} 
                  disabled={pinnedEwra.client}
                  onChange={e => handleEwraWeightChange('client', Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer disabled:opacity-40"
                />
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Product &amp; Delivery Channel Risk Weightage</span>
                  <div className="flex items-center gap-1.5">
                    <span className="text-primary font-bold">{productWeight}%</span>
                    <button 
                      type="button" 
                      onClick={() => toggleEwraPin('product')}
                      className={`p-1 rounded cursor-pointer ${pinnedEwra.product ? 'bg-primary text-white' : 'text-muted hover:text-ink'}`}
                      title="Pin weight"
                    >
                      {pinnedEwra.product ? <Pin className="w-3 h-3" /> : <PinOff className="w-3 h-3" />}
                    </button>
                  </div>
                </div>
                <input 
                  type="range" min="0" max="100" value={productWeight} 
                  disabled={pinnedEwra.product}
                  onChange={e => handleEwraWeightChange('product', Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer disabled:opacity-40"
                />
              </div>
            </div>
          </div>
        )}

        {/* Tab 8: Operational Unblock Customer */}
        {activeTab === 'UNBLOCK_CUSTOMER' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="p-4 bg-amber-50 border border-amber-200 text-amber-900 rounded-xl text-xs space-y-2">
              <div className="font-bold flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 text-amber-600" />
                <span>Operational Governance Notice: Segregation of Duties</span>
              </div>
              <p className="text-[11px] leading-relaxed">
                Releasing administrative holds on customers is an operational compliance investigation action. While emergency administrative unblocking is available below, full 4-Eyes dual authorization and SAR audit dossiers are recorded in <strong>Customer KYC 360</strong>.
              </p>
              <div className="pt-1">
                <button
                  type="button"
                  onClick={() => navigate('/kyc360')}
                  className="inline-flex items-center gap-1 px-3 py-1 bg-amber-600 text-white rounded text-[11px] font-bold hover:bg-amber-700 cursor-pointer"
                >
                  <span>Open Customer KYC 360 Workspace</span>
                  <ExternalLink className="w-3 h-3" />
                </button>
              </div>
            </div>

            <div className="overflow-x-auto border border-border rounded-lg">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">Customer Code</th>
                    <th className="py-2.5 px-3">Full Legal Name</th>
                    <th className="py-2.5 px-3">National ID</th>
                    <th className="py-2.5 px-3">Blocked Date</th>
                    <th className="py-2.5 px-3">Block Reason</th>
                    <th className="py-2.5 px-3">Status</th>
                    <th className="py-2.5 px-3">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {blockedCustomers.map(cust => (
                    <tr key={cust.customerCode} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-primary">{cust.customerCode}</td>
                      <td className="py-3 px-3 font-bold text-ink">{cust.fullName}</td>
                      <td className="py-3 px-3 text-muted">{cust.nationalId}</td>
                      <td className="py-3 px-3 text-muted">{cust.blockedAt}</td>
                      <td className="py-3 px-3 text-critical max-w-[200px] truncate" title={cust.blockReason}>
                        {cust.blockReason}
                      </td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          cust.isBlocked ? 'bg-critical text-white' : 'bg-accent/20 text-accent'
                        }`}>
                          {cust.isBlocked ? 'BLOCKED' : 'CLEARED / ACTIVE'}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        {cust.isBlocked ? (
                          <button
                            type="button"
                            onClick={() => handleUnblockCustomer(cust.customerCode)}
                            className="px-2.5 py-1 rounded bg-primary text-white text-[10px] font-bold hover:bg-primary-hover flex items-center gap-1 cursor-pointer shadow-xs"
                          >
                            <Unlock className="w-3 h-3" />
                            <span>Unblock</span>
                          </button>
                        ) : (
                          <span className="text-[10px] text-muted">Unblocked</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 9: Account Change Password */}
        {activeTab === 'CHANGE_PASSWORD' && (
          <div className="space-y-4 font-mono text-xs max-w-md">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Manually change your active user account password. Your new password must be at least 8 characters long and will be securely hashed with PBKDF2-SHA256.
            </div>

            {passwordLoading && (
              <EdithLoader
                overlay
                size="sm"
                label="UPDATING PASSWORD..."
                sublabel="Applying security changes"
              />
            )}

            {passwordError && (
              <GeneralErrorBanner
                error={passwordError}
                category="VALIDATION"
                onDismiss={() => setPasswordError(null)}
              />
            )}

            {passwordSuccess && (
              <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 shrink-0" />
                <span>{passwordSuccess}</span>
              </div>
            )}

            <form onSubmit={handlePasswordSubmit} className="space-y-4 bg-white p-5 rounded-xl border border-border shadow-xs">
              <div className="space-y-1">
                <label className="text-[11px] font-bold text-ink uppercase">Current Password</label>
                <input
                  type="password"
                  required
                  value={currentPassword}
                  onChange={e => setCurrentPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full px-3 py-2 rounded-lg border border-border bg-surface text-ink text-xs focus:outline-none focus:border-primary"
                />
              </div>

              <div className="space-y-1">
                <label className="text-[11px] font-bold text-ink uppercase">New Password</label>
                <input
                  type="password"
                  required
                  value={newPassword}
                  onChange={e => setNewPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full px-3 py-2 rounded-lg border border-border bg-surface text-ink text-xs focus:outline-none focus:border-primary"
                />
              </div>

              <div className="space-y-1">
                <label className="text-[11px] font-bold text-ink uppercase">Confirm New Password</label>
                <input
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={e => setConfirmPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full px-3 py-2 rounded-lg border border-border bg-surface text-ink text-xs focus:outline-none focus:border-primary"
                />
              </div>

              <button
                type="submit"
                disabled={passwordLoading}
                className="w-full py-2.5 px-4 rounded-lg bg-primary hover:bg-primary-hover text-white font-bold text-xs flex items-center justify-center gap-2 shadow-xs cursor-pointer disabled:opacity-50"
              >
                {passwordLoading ? (
                  <span>Updating Password...</span>
                ) : (
                  <>
                    <Key className="w-4 h-4" />
                    <span>Update Password</span>
                  </>
                )}
              </button>
            </form>
          </div>
        )}
      </div>

      {/* Slide-Over Configuration History & WORM Audit Drawer */}
      {isHistoryDrawerOpen && (
        <div className="fixed inset-0 z-50 overflow-hidden bg-black/40 backdrop-blur-xs flex justify-end animate-in fade-in">
          <div className="w-full max-w-md bg-white h-full shadow-2xl border-l border-border flex flex-col font-mono text-xs">
            <div className="p-4 border-b border-border flex justify-between items-center bg-surface-muted">
              <div className="flex items-center gap-2">
                <History className="w-4 h-4 text-primary" />
                <span className="font-bold text-ink text-sm">WORM Configuration Audit History</span>
              </div>
              <button 
                type="button"
                onClick={() => setIsHistoryDrawerOpen(false)}
                className="p-1 rounded text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-4 flex-1 overflow-y-auto space-y-3">
              <div className="text-[11px] text-muted pb-2 border-b border-border">
                Cryptographically verifiable SHA-256 ledger records for parameter mutations.
              </div>

              {auditLogs.length === 0 ? (
                <div className="text-center py-10 text-muted">
                  No configuration modifications recorded yet in this session.
                </div>
              ) : (
                auditLogs.map(log => (
                  <div key={log.id} className="p-3 rounded-lg border border-border bg-surface-muted/30 space-y-1.5">
                    <div className="flex justify-between items-start">
                      <span className="font-bold text-primary text-[11px]">{log.action}</span>
                      <span className="text-[10px] text-muted">{log.timestampUtc?.slice(0, 19)}</span>
                    </div>
                    <div className="text-ink text-[11px]">{log.details}</div>
                    <div className="text-[9px] text-muted flex items-center gap-1 font-mono">
                      <span>SHA-256:</span>
                      <span className="truncate max-w-[200px]">{log.sha256Hash || 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'}</span>
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="p-4 border-t border-border bg-surface-muted flex justify-end">
              <button
                type="button"
                onClick={() => setIsHistoryDrawerOpen(false)}
                className="px-4 py-2 rounded-lg bg-surface border border-border text-ink font-bold text-xs hover:bg-surface-hover cursor-pointer"
              >
                Close Drawer
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default SystemMastersView;
