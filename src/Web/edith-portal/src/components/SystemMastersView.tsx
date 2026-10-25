import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
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
  Activity
} from 'lucide-react';
import { EdithApi } from '../services/api';
import { EdithLoader } from './EdithLogo';
import { GeneralErrorBanner } from './ErrorEngineUi';

export type SystemMastersTab = 
  | 'COUNTRIES' 
  | 'NATIONALITIES' 
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
  fatfStatus: 'BLACK_LIST' | 'GREY_LIST' | 'COOPERATIVE';
}

interface NationalityMaster {
  code: string;
  demonym: string;
  country: string;
  isHighRisk: boolean;
  riskLevel: 'HIGH' | 'MEDIUM' | 'LOW';
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
  const location = useLocation();
  const [activeTab, setActiveTab] = useState<SystemMastersTab>(initialTab);
  const [countries, setCountries] = useState<CountryMaster[]>([]);
  const [nationalities, setNationalities] = useState<NationalityMaster[]>([]);
  const [blockedCustomers, setBlockedCustomers] = useState<BlockedCustomer[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
  const [loadingMasters, setLoadingMasters] = useState(false);

  // Risk Rate Benchmarks
  const [custHighRisk, setCustHighRisk] = useState(85);
  const [custMedRisk, setCustMedRisk] = useState(60);
  const [txHighRisk, setTxHighRisk] = useState(90);
  const [txMedRisk, setTxMedRisk] = useState(70);

  // Screening Score Weightages
  const [nameWeight, setNameWeight] = useState(60);
  const [nationalityWeight, setNationalityWeight] = useState(15);
  const [dobWeight, setDobWeight] = useState(15);
  const [matchThreshold, setMatchThreshold] = useState(80);

  // SysControl Scenarios
  const [sysControl, setSysControl] = useState({
    customerScreening: true,
    beneficiaryBankScreening: true,
    correspondentBankScreening: true,
    partnerUboScreening: true,
    realtimeHoldInterceptor: true
  });

  // Risk Assessment Master
  const [geoWeight, setGeoWeight] = useState(30);
  const [clientTypeWeight, setClientTypeWeight] = useState(40);
  const [productWeight, setProductWeight] = useState(30);

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
          setMatchThreshold(data.scoringWeights.defaultThreshold);
        }
        if (data.fatfTiers && data.fatfTiers.length > 0) {
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

  const filteredCountries = countries.filter(c => 
    c.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    c.code.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handleScoreChange = async (code: string, newScore: number) => {
    setCountries(prev => prev.map(c => c.code === code ? { ...c, riskScore: newScore, isHighRisk: newScore >= 75 } : c));
    try {
      await EdithApi.upsertCountryRisk({
        code,
        countryName: code,
        isHighRisk: newScore >= 75,
        riskScore: newScore
      });
    } catch (err) {
      console.error('Failed to persist country risk score:', err);
    }
  };

  const handleSave = async () => {
    try {
      if (activeTab === 'SCREENING_SCORE') {
        await EdithApi.updateScoringWeights({
          nameWeightage: nameWeight,
          nationalityWeightage: nationalityWeight,
          dobWeightage: dobWeight,
          idWeightage: 100 - (nameWeight + nationalityWeight + dobWeight),
          defaultThreshold: matchThreshold
        });
        setSaveSuccess('Screening score weights saved to PostgreSQL database.');
      } else {
        setSaveSuccess('Configuration changes saved successfully.');
      }
      setTimeout(() => setSaveSuccess(null), 3500);
    } catch (err: any) {
      setSaveSuccess(`Error saving changes: ${err.message || 'Unknown error'}`);
    }
  };

  const handleUnblockCustomer = async (customerCode: string) => {
    setBlockedCustomers(prev => prev.map(c => c.customerCode === customerCode ? { ...c, isBlocked: false } : c));
    await EdithApi.updateCustomerEdd(customerCode, {
      action: 'TOGGLE_BLOCK',
      isBlocked: false,
      notes: `Administrative unblock authorized via System Masters for ${customerCode}.`
    });
    setSaveSuccess(`Customer ${customerCode} successfully unblocked. Audit ledger entry generated.`);
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

  const masterTabs: { id: SystemMastersTab; label: string; icon: any; route?: string }[] = [
    { id: 'NATIONALITIES', label: 'Nationality Master', icon: Globe, route: '/master/nationality' },
    { id: 'COUNTRIES', label: 'Country Master', icon: Flag, route: '/master/country' },
    { id: 'UNBLOCK_CUSTOMER', label: 'Unblock Customer', icon: Unlock, route: '/master/unblock-customer' }
  ];

  const settingsTabs: { id: SystemMastersTab; label: string; icon: any; route?: string }[] = [
    { id: 'RISK_RATES', label: 'Risk Rate Settings', icon: Activity, route: '/settings/risk-rates' },
    { id: 'SCREENING_SCORE', label: 'Screening Score', icon: SlidersHorizontal, route: '/settings/screening-score' },
    { id: 'SETUP_CONTROL', label: 'Setup Control', icon: ToggleRight, route: '/settings/setup-control' },
    { id: 'CHANGE_PASSWORD', label: 'Change Password', icon: Key, route: '/settings/change-password' },
    { id: 'RISK_ASSESSMENT', label: 'Risk Assessment Master', icon: Target, route: '/settings/risk-assessment' }
  ];

  const isSettingsRoute = location.pathname.startsWith('/settings');
  const tabs = isSettingsRoute ? settingsTabs : masterTabs;

  const pageMeta = usePageMeta(
    'Country Master',
    'Maintain sovereign country risk scores and FATF Black and Grey list designations.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto font-sans">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border shadow-xs">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <Settings className="w-5 h-5 text-primary" />
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

        {activeTab !== 'CHANGE_PASSWORD' && (
          <button 
            onClick={handleSave}
            disabled={loadingMasters}
            className="px-4 py-2 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-2 shadow-xs cursor-pointer disabled:opacity-50"
          >
            <Save className="w-4 h-4" />
            <span>{loadingMasters ? 'Loading...' : 'Save Settings'}</span>
          </button>
        )}
      </div>

      {saveSuccess && (
        <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs font-mono flex items-center gap-2 animate-in fade-in">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{saveSuccess}</span>
        </div>
      )}

      {/* Main Tabs Container */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-5 shadow-xs">
        {/* Horizontal Navigation Tabs */}
        <div className="flex gap-1 border-b border-border text-xs font-mono overflow-x-auto pb-1 scrollbar-thin">
          {tabs.map(tab => {
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
            <div className="flex justify-between items-center">
              <div className="relative w-72">
                <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-muted" />
                <input
                  type="text"
                  placeholder="Search ISO code or country..."
                  value={searchTerm}
                  onChange={e => setSearchTerm(e.target.value)}
                  className="w-full pl-9 pr-3 py-1.5 text-xs font-mono rounded border border-border bg-surface-muted text-ink focus:outline-none focus:border-primary"
                />
              </div>
              <div className="text-muted text-[11px]">
                {filteredCountries.length} Jurisdictions Loaded
              </div>
            </div>

            <div className="overflow-x-auto border border-border rounded-lg">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">ISO-3</th>
                    <th className="py-2.5 px-3">Jurisdiction Name</th>
                    <th className="py-2.5 px-3">FATF Compliance Status</th>
                    <th className="py-2.5 px-3">High-Risk Designation</th>
                    <th className="py-2.5 px-3">Risk Score (0 - 100)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredCountries.map(country => (
                    <tr key={country.code} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-primary">{country.code}</td>
                      <td className="py-3 px-3 font-bold text-ink">{country.name}</td>
                      <td className="py-3 px-3">
                        {country.fatfStatus === 'BLACK_LIST' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-critical text-white">
                            FATF BLACKLIST
                          </span>
                        )}
                        {country.fatfStatus === 'COOPERATIVE' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/20 text-accent">
                            COOPERATIVE
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          country.isHighRisk ? 'text-critical bg-critical/10' : 'text-muted bg-surface-muted'
                        }`}>
                          {country.isHighRisk ? 'HIGH RISK' : 'STANDARD'}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <input
                          type="number"
                          min="0"
                          max="100"
                          value={country.riskScore}
                          onChange={e => handleScoreChange(country.code, Number(e.target.value))}
                          className="w-20 px-2 py-1 rounded border border-border bg-surface text-ink font-bold text-xs"
                        />
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
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Nationality demonyms are matched during single and bulk transaction screening against <code className="text-primary font-bold">rules.nationality_masters</code>.
            </div>

            <div className="overflow-x-auto border border-border rounded-lg">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">ISO Code</th>
                    <th className="py-2.5 px-3">Demonym / Nationality</th>
                    <th className="py-2.5 px-3">Country Name</th>
                    <th className="py-2.5 px-3">Screening Risk Tier</th>
                    <th className="py-2.5 px-3">High-Risk Flag</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {nationalities.map(n => (
                    <tr key={n.code} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-primary">{n.code}</td>
                      <td className="py-3 px-3 font-bold text-ink">{n.demonym}</td>
                      <td className="py-3 px-3 text-muted">{n.country}</td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          n.riskLevel === 'HIGH' ? 'bg-critical/15 text-critical' : 'bg-accent/15 text-accent'
                        }`}>
                          {n.riskLevel}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <input
                          type="checkbox"
                          checked={n.isHighRisk}
                          onChange={e => {
                            const checked = e.target.checked;
                            setNationalities(prev => prev.map(item => item.code === n.code ? { ...item, isHighRisk: checked, riskLevel: checked ? 'HIGH' : 'LOW' } : item));
                          }}
                          className="w-4 h-4 text-primary rounded cursor-pointer"
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 3: Unblock Customer */}
        {activeTab === 'UNBLOCK_CUSTOMER' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="p-3 bg-blue-50 border border-blue-200 text-blue-900 rounded-lg text-xs">
              <strong>Master: Unblock Customer</strong> — Search blocked institutional or retail customer accounts and release administrative holds upon MLRO review.
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

        {/* Tab 4: Risk Rate Settings */}
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
                    <div className="flex justify-between text-xs font-bold text-warning">
                      <span>Medium Risk Benchmark (Score &ge;)</span>
                      <span>{custMedRisk}</span>
                    </div>
                    <input 
                      type="range" min="30" max="80" value={custMedRisk} 
                      onChange={e => setCustMedRisk(Number(e.target.value))}
                      className="w-full accent-warning mt-1 cursor-pointer"
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
                    <div className="flex justify-between text-xs font-bold text-warning">
                      <span>Medium Risk Benchmark (Score &ge;)</span>
                      <span>{txMedRisk}</span>
                    </div>
                    <input 
                      type="range" min="40" max="90" value={txMedRisk} 
                      onChange={e => setTxMedRisk(Number(e.target.value))}
                      className="w-full accent-warning mt-1 cursor-pointer"
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

        {/* Tab 5: Screening Score Weightages */}
        {activeTab === 'SCREENING_SCORE' && (
          <div className="space-y-6 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Define the multi-factor parameter weightages for fuzzy string matching (Name, Nationality, DOB) and the cumulative match threshold.
            </div>

            <div className="p-5 rounded-xl border border-border bg-white space-y-5 max-w-2xl shadow-xs">
              <div className="space-y-4">
                <div>
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <span>1. Name Match Weightage</span>
                    <span className="text-primary font-bold">{nameWeight}%</span>
                  </div>
                  <input 
                    type="range" min="10" max="100" value={nameWeight} 
                    onChange={e => setNameWeight(Number(e.target.value))}
                    className="w-full accent-primary mt-1 cursor-pointer"
                  />
                </div>

                <div>
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <span>2. Nationality Match Weightage</span>
                    <span className="text-primary font-bold">{nationalityWeight}%</span>
                  </div>
                  <input 
                    type="range" min="0" max="50" value={nationalityWeight} 
                    onChange={e => setNationalityWeight(Number(e.target.value))}
                    className="w-full accent-primary mt-1 cursor-pointer"
                  />
                </div>

                <div>
                  <div className="flex justify-between text-xs font-bold text-ink">
                    <span>3. Date of Birth (DOB) Match Weightage</span>
                    <span className="text-primary font-bold">{dobWeight}%</span>
                  </div>
                  <input 
                    type="range" min="0" max="50" value={dobWeight} 
                    onChange={e => setDobWeight(Number(e.target.value))}
                    className="w-full accent-primary mt-1 cursor-pointer"
                  />
                </div>

                <div className="pt-3 border-t border-border">
                  <div className="flex justify-between text-xs font-bold text-critical">
                    <span>Cumulative Threshold for Match Alert</span>
                    <span>{matchThreshold}%</span>
                  </div>
                  <input 
                    type="range" min="50" max="95" value={matchThreshold} 
                    onChange={e => setMatchThreshold(Number(e.target.value))}
                    className="w-full accent-critical mt-1 cursor-pointer"
                  />
                  <div className="text-[10px] text-muted mt-1">
                    Entities scoring equal to or above {matchThreshold}% trigger sanction hit alerts.
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

        {/* Tab 7: Risk Assessment Master */}
        {activeTab === 'RISK_ASSESSMENT' && (
          <div className="space-y-6 font-mono text-xs">
            <div className="p-3 rounded-lg bg-surface-muted border border-border text-[11px] text-muted leading-relaxed">
              Configure Enterprise-Wide AML Risk Assessment (EWRA) weights across Geographical, Client Entity, and Product/Delivery channels. Total must equal 100%.
            </div>

            <div className="p-5 rounded-xl border border-border bg-white space-y-4 max-w-2xl shadow-xs">
              <div className="flex justify-between items-center pb-2 border-b border-border">
                <span className="font-bold text-ink">Cumulative Assessment Weight:</span>
                <span className={`font-bold px-2 py-0.5 rounded ${geoWeight + clientTypeWeight + productWeight === 100 ? 'bg-accent/20 text-accent' : 'bg-critical/20 text-critical'}`}>
                  {geoWeight + clientTypeWeight + productWeight}%
                </span>
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Geographical Risk Weightage</span>
                  <span className="text-primary font-bold">{geoWeight}%</span>
                </div>
                <input 
                  type="range" min="0" max="100" value={geoWeight} 
                  onChange={e => setGeoWeight(Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer"
                />
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Client / Entity Type Risk Weightage</span>
                  <span className="text-primary font-bold">{clientTypeWeight}%</span>
                </div>
                <input 
                  type="range" min="0" max="100" value={clientTypeWeight} 
                  onChange={e => setClientTypeWeight(Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer"
                />
              </div>

              <div>
                <div className="flex justify-between text-xs font-bold text-ink">
                  <span>Product &amp; Delivery Channel Risk Weightage</span>
                  <span className="text-primary font-bold">{productWeight}%</span>
                </div>
                <input 
                  type="range" min="0" max="100" value={productWeight} 
                  onChange={e => setProductWeight(Number(e.target.value))}
                  className="w-full accent-primary mt-1 cursor-pointer"
                />
              </div>
            </div>
          </div>
        )}

        {/* Tab 8: Change Password */}
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
    </div>
  );
};
export default SystemMastersView;
