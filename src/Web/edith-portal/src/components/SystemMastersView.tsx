import React, { useState } from 'react';
import { 
  Settings, 
  Search, 
  Save, 
  CheckCircle2
} from 'lucide-react';

interface CountryMaster {
  code: string;
  name: string;
  isHighRisk: boolean;
  riskScore: number;
  fatfStatus: 'BLACK_LIST' | 'GREY_LIST' | 'COOPERATIVE';
}

const MOCK_COUNTRIES: CountryMaster[] = [
  { code: 'ARE', name: 'United Arab Emirates', isHighRisk: false, riskScore: 10.0, fatfStatus: 'COOPERATIVE' },
  { code: 'SAU', name: 'Saudi Arabia', isHighRisk: false, riskScore: 10.0, fatfStatus: 'COOPERATIVE' },
  { code: 'OMN', name: 'Oman', isHighRisk: false, riskScore: 10.0, fatfStatus: 'COOPERATIVE' },
  { code: 'CHE', name: 'Switzerland', isHighRisk: false, riskScore: 15.0, fatfStatus: 'COOPERATIVE' },
  { code: 'GBR', name: 'United Kingdom', isHighRisk: false, riskScore: 10.0, fatfStatus: 'COOPERATIVE' },
  { code: 'USA', name: 'United States', isHighRisk: false, riskScore: 10.0, fatfStatus: 'COOPERATIVE' },
  { code: 'IRN', name: 'Iran (Islamic Republic of)', isHighRisk: true, riskScore: 99.0, fatfStatus: 'BLACK_LIST' },
  { code: 'PRK', name: "Democratic People's Republic of Korea", isHighRisk: true, riskScore: 99.0, fatfStatus: 'BLACK_LIST' },
  { code: 'MMR', name: 'Myanmar', isHighRisk: true, riskScore: 92.0, fatfStatus: 'BLACK_LIST' }
];

export const SystemMastersView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'COUNTRIES' | 'NATIONALITIES' | 'SANCTION_LISTS'>('COUNTRIES');
  const [countries, setCountries] = useState<CountryMaster[]>(MOCK_COUNTRIES);
  const [searchTerm, setSearchTerm] = useState('');
  const [saveSuccess, setSaveSuccess] = useState(false);

  const filteredCountries = countries.filter(c => 
    c.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    c.code.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const handleScoreChange = (code: string, newScore: number) => {
    setCountries(prev => prev.map(c => c.code === code ? { ...c, riskScore: newScore } : c));
  };

  const handleSave = () => {
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 12 // MOD-SET-MAST
            </span>
            <span className="text-[10px] font-mono text-muted">PostgreSQL 17 Reference Data Masters</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <Settings className="w-5 h-5 text-primary" />
            System Masters & Scoring Settings
          </h1>
          <p className="text-xs text-muted mt-1">
            Manage ISO country risk tiers, FATF gray/blacklists, nationality masters, and sanctions catalog synchronizers.
          </p>
        </div>

        <button 
          onClick={handleSave}
          className="px-4 py-2 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-2 shadow-xs"
        >
          <Save className="w-4 h-4" />
          <span>Save Changes</span>
        </button>
      </div>

      {saveSuccess && (
        <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs font-mono flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>Country master risk configurations successfully committed to PostgreSQL 17 database.</span>
        </div>
      )}

      {/* Main Tabs Container */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-4">
        <div className="flex gap-2 border-b border-border text-xs font-mono">
          {[
            { id: 'COUNTRIES', label: '1. Country Masters & FATF Tiers' },
            { id: 'NATIONALITIES', label: '2. Nationality & Demonym Registry' },
            { id: 'SANCTION_LISTS', label: '3. Sanctions List Sync Catalog' }
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

        {/* Tab 1: Country Masters */}
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

            <div className="overflow-x-auto">
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

        {/* Tab 2: Nationalities */}
        {activeTab === 'NATIONALITIES' && (
          <div className="p-4 rounded-lg bg-surface-muted border border-border font-mono text-xs space-y-2">
            <div className="font-bold text-ink">Nationality Mapping Table (`rules.nationality_masters`)</div>
            <div className="text-muted text-[11px]">
              Nationalities are normalized against ISO 3166-1 demonym variants to prevent bypassing checks through alternate spellings (e.g. "Emirati", "UAE National", "Arabian").
            </div>
          </div>
        )}

        {/* Tab 3: Sanctions List Catalog */}
        {activeTab === 'SANCTION_LISTS' && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 font-mono text-xs">
            {[
              { name: 'US OFAC SDN & Consolidated', entities: '18,419', status: 'SYNCHRONIZED', date: '2026-10-14 02:00 UTC' },
              { name: 'United Nations Consolidated', entities: '892', status: 'SYNCHRONIZED', date: '2026-10-14 02:00 UTC' },
              { name: 'EU Financial Sanctions (EEAS)', entities: '4,102', status: 'SYNCHRONIZED', date: '2026-10-14 02:00 UTC' }
            ].map(list => (
              <div key={list.name} className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
                <div className="font-bold text-ink text-sm">{list.name}</div>
                <div className="text-xl font-bold text-primary">{list.entities} Entities</div>
                <div className="text-[11px] text-muted">Last sync: {list.date}</div>
                <span className="inline-block px-2 py-0.5 rounded text-[10px] font-bold bg-accent/20 text-accent">
                  ✓ {list.status}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
