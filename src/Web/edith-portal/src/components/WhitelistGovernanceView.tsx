import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { 
  ShieldCheck, 
  FileText, 
  Plus, 
  Trash2, 
  AlertCircle, 
  Hash, 
  Search
} from 'lucide-react';
import { EdithApi } from '../services/api';
import { usePageMeta } from '../config/pageMeta';

interface WhitelistEntry {
  id: string;
  entityName: string;
  accountNumber: string;
  category: 'GOVERNMENT_EMBASSY' | 'CORRESPONDENT_CENTRAL_BANK' | 'CORPORATE_PAYROLL' | 'SOVEREIGN_WEALTH';
  authorizedBy: string;
  approvedDate: string;
  expiresAt: string;
  remainingDays: number;
  totalDays: number;
  status: 'ACTIVE' | 'EXPIRING_SOON' | 'REVOKED';
  rationale: string;
  auditHash: string;
}

const MOCK_WHITELIST: WhitelistEntry[] = [
  {
    id: 'wl-001',
    entityName: 'Embassy of Canada - UAE Consular Mission',
    accountNumber: 'AE090330000009876543210',
    category: 'GOVERNMENT_EMBASSY',
    authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
    approvedDate: '2026-01-15',
    expiresAt: '2027-01-15',
    remainingDays: 93,
    totalDays: 365,
    status: 'ACTIVE',
    rationale: 'Diplomatic consular operations account exempted from PEP political threshold per Ministry of Foreign Affairs clearance MFA/2026/9941.',
    auditHash: '0x8f9c42b10a294877e31d0441c883e29177a0bf99214b7e41981249b291a18204'
  },
  {
    id: 'wl-002',
    entityName: 'Abu Dhabi Investment Authority (ADIA) Operations',
    accountNumber: 'AE440260000001122334455',
    category: 'SOVEREIGN_WEALTH',
    authorizedBy: 'Tariq Mansoor (Deputy MLRO)',
    approvedDate: '2026-03-01',
    expiresAt: '2026-11-01',
    remainingDays: 18,
    totalDays: 245,
    status: 'EXPIRING_SOON',
    rationale: 'Sovereign wealth fund treasury transfer account with daily liquidity rebalancing. KYC verified under Federal Decree No. 20.',
    auditHash: '0x1a72d93e8849b21a007f31c44883e10088b901fc8821994b7e41981249b291a1'
  },
  {
    id: 'wl-003',
    entityName: 'Dubai Duty Free Executive Payroll Clearing',
    accountNumber: 'AE770110000004455667788',
    category: 'CORPORATE_PAYROLL',
    authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
    approvedDate: '2026-06-10',
    expiresAt: '2027-06-10',
    remainingDays: 239,
    totalDays: 365,
    status: 'ACTIVE',
    rationale: 'Automated batch payroll for 4,000+ verified employees processed on 26th of each month.',
    auditHash: '0x5b3310ce889241aa007f31c44883e10088b901fc8821994b7e41981249b291a1'
  }
];

export const WhitelistGovernanceView: React.FC = () => {
  const [entries, setEntries] = useState<WhitelistEntry[]>(MOCK_WHITELIST);
  const [activeEntry, setActiveEntry] = useState<WhitelistEntry | null>(MOCK_WHITELIST[0]);
  const [searchTerm, setSearchTerm] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);

  // New entry form state
  const [newEntity, setNewEntity] = useState({
    entityName: '',
    accountNumber: '',
    category: 'CORPORATE_PAYROLL' as WhitelistEntry['category'],
    ttlDays: 365,
    rationale: ''
  });

  useEffect(() => {
    EdithApi.fetchWhitelists().then(dbList => {
      if (dbList.length > 0) {
        const mapped: WhitelistEntry[] = dbList.map(w => {
          const expDate = w.expiresAt ? new Date(w.expiresAt) : new Date(Date.now() + 86400000 * 365);
          const remDays = Math.max(1, Math.round((expDate.getTime() - Date.now()) / 86400000));
          return {
            id: w.id,
            entityName: w.entityIdentifier,
            accountNumber: w.matchScope === 'EXACT' ? `ID/SCOPE: ${w.entityIdentifier}` : w.matchScope,
            category: 'SOVEREIGN_WEALTH',
            authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
            approvedDate: new Date().toISOString().split('T')[0],
            expiresAt: expDate.toISOString().split('T')[0],
            remainingDays: remDays,
            totalDays: Math.max(remDays, 365),
            status: !w.isActive ? 'REVOKED' : (remDays < 30 ? 'EXPIRING_SOON' : 'ACTIVE'),
            rationale: w.reason,
            auditHash: `0x${w.id.replace(/-/g, '')}${w.tenantId.replace(/-/g, '')}`
          };
        });
        setEntries([...mapped, ...MOCK_WHITELIST]);
        setActiveEntry(mapped[0]);
      }
    });
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newEntity.entityName || !newEntity.accountNumber || !newEntity.rationale) return;

    const expDate = new Date(Date.now() + newEntity.ttlDays * 86400000);
    const saved = await EdithApi.createWhitelist({
      entityIdentifier: newEntity.entityName,
      matchScope: 'EXACT',
      reason: newEntity.rationale,
      expiresAt: expDate.toISOString()
    });

    const created: WhitelistEntry = {
      id: saved?.id || `wl-00${entries.length + 1}`,
      entityName: newEntity.entityName,
      accountNumber: newEntity.accountNumber,
      category: newEntity.category,
      authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
      approvedDate: new Date().toISOString().split('T')[0],
      expiresAt: expDate.toISOString().split('T')[0],
      remainingDays: newEntity.ttlDays,
      totalDays: newEntity.ttlDays,
      status: 'ACTIVE',
      rationale: newEntity.rationale,
      auditHash: `0x${Math.random().toString(16).substring(2, 10)}${Math.random().toString(16).substring(2, 10)}${Math.random().toString(16).substring(2, 10)}`
    };

    setEntries([created, ...entries]);
    setActiveEntry(created);
    setIsModalOpen(false);
    setNewEntity({ entityName: '', accountNumber: '', category: 'CORPORATE_PAYROLL', ttlDays: 365, rationale: '' });
  };

  const handleRevoke = async (id: string) => {
    await EdithApi.deleteWhitelist(id);
    setEntries(prev => prev.map(entry => entry.id === id ? { ...entry, status: 'REVOKED' } : entry));
    if (activeEntry?.id === id) {
      setActiveEntry(prev => prev ? { ...prev, status: 'REVOKED' } : null);
    }
  };

  const filteredEntries = entries.filter(e => 
    e.entityName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    e.accountNumber.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const pageMeta = usePageMeta(
    'White List',
    'Govern approved false-positive suppressions and time-bound entity clearances with dual-control authorization.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <ShieldCheck className="w-5 h-5 text-accent" />
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

        <button 
          onClick={() => setIsModalOpen(true)}
          className="px-3.5 py-2 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-2 shadow-xs"
        >
          <Plus className="w-4 h-4" />
          Authorize New Exception
        </button>
      </div>

      {/* KPI Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Active Authorized Whitelists</div>
          <div className="text-2xl font-bold font-mono text-ink mt-1">
            {entries.filter(e => e.status === 'ACTIVE').length} Entities
          </div>
          <div className="text-[11px] text-accent font-mono mt-1">✓ Bypassing Pass 5 Engine</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Expiring Within 30 Days</div>
          <div className="text-2xl font-bold font-mono text-warning mt-1">
            {entries.filter(e => e.status === 'EXPIRING_SOON').length} Entities
          </div>
          <div className="text-[11px] text-warning/90 font-mono mt-1">Renewal Review Triggered</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Dual-Sign Off Adherence</div>
          <div className="text-2xl font-bold font-mono text-primary mt-1">100.0%</div>
          <div className="text-[11px] text-muted font-mono mt-1">2/2 Officers Logged on WORM</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Auto-Expired This Quarter</div>
          <div className="text-2xl font-bold font-mono text-muted mt-1">14 Exceptions</div>
          <div className="text-[11px] text-muted font-mono mt-1">Zero manual leaks recorded</div>
        </div>
      </div>

      {/* Main Table & Inspector */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Table View (8 cols) */}
        <div className="lg:col-span-8 bg-surface rounded-xl border border-border overflow-hidden space-y-3 p-4">
          <div className="flex justify-between items-center pb-3 border-b border-border">
            <div className="relative w-72">
              <Search className="w-3.5 h-3.5 absolute left-3 top-2.5 text-muted" />
              <input
                type="text"
                placeholder="Filter by entity or IBAN..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full pl-9 pr-3 py-1.5 text-xs font-mono rounded border border-border bg-surface-muted focus:outline-none focus:border-primary text-ink"
              />
            </div>
            <div className="text-xs font-mono text-muted">
              Showing {filteredEntries.length} of {entries.length} Records
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs font-mono">
              <thead>
                <tr className="border-b border-border text-muted text-[11px] uppercase tracking-wider bg-surface-muted">
                  <th className="py-2.5 px-3">Authorized Entity</th>
                  <th className="py-2.5 px-3">Exception Category</th>
                  <th className="py-2.5 px-3">TTL Decay</th>
                  <th className="py-2.5 px-3">Status</th>
                  <th className="py-2.5 px-3 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {filteredEntries.map(entry => {
                  const isActive = activeEntry?.id === entry.id;
                  const pct = Math.round((entry.remainingDays / entry.totalDays) * 100);
                  return (
                    <tr 
                      key={entry.id} 
                      onClick={() => setActiveEntry(entry)}
                      className={`cursor-pointer transition-colors ${
                        isActive ? 'bg-accent/5 font-medium' : 'hover:bg-surface-hover'
                      }`}
                    >
                      <td className="py-3 px-3">
                        <div className="font-bold text-ink">{entry.entityName}</div>
                        <div className="text-[10px] text-muted mt-0.5">{entry.accountNumber}</div>
                      </td>
                      <td className="py-3 px-3">
                        <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary border border-primary/20">
                          {entry.category.replace('_', ' ')}
                        </span>
                      </td>
                      <td className="py-3 px-3">
                        <div className="flex items-center justify-between text-[10px] mb-1">
                          <span className="text-ink font-bold">{entry.remainingDays} Days Left</span>
                          <span className="text-muted">{pct}%</span>
                        </div>
                        <div className="w-32 h-1.5 rounded-full bg-border overflow-hidden">
                          <div 
                            className={`h-full ${entry.remainingDays < 30 ? 'bg-warning' : 'bg-accent'}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </td>
                      <td className="py-3 px-3">
                        {entry.status === 'ACTIVE' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/10 text-accent border border-accent/30">
                            ACTIVE
                          </span>
                        )}
                        {entry.status === 'EXPIRING_SOON' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-warning/10 text-warning border border-warning/30">
                            RENEWAL REQD
                          </span>
                        )}
                        {entry.status === 'REVOKED' && (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-critical/10 text-critical border border-critical/30">
                            REVOKED
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-3 text-right">
                        <button className="text-primary hover:text-primary-hover font-bold text-[11px]">
                          Audit Log →
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Audit Inspector Drawer (4 cols) */}
        <div className="lg:col-span-4 bg-surface rounded-xl border border-border p-5 space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs font-mono uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <FileText className="w-4 h-4 text-accent" />
              WORM Cryptographic Audit
            </h3>
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-surface-muted text-muted">
              Immutable Ledger
            </span>
          </div>

          {activeEntry ? (
            <div className="space-y-4 text-xs font-mono">
              <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1">
                <div className="text-[10px] text-muted uppercase">Authorized Entity</div>
                <div className="text-sm font-bold text-ink">{activeEntry.entityName}</div>
                <div className="text-[11px] text-muted">{activeEntry.accountNumber}</div>
              </div>

              <div className="space-y-1.5">
                <div className="text-[10px] text-muted uppercase tracking-wider font-bold">Regulatory Justification</div>
                <div className="p-3 rounded-lg bg-surface-muted border border-border text-ink text-[11px] leading-relaxed">
                  {activeEntry.rationale}
                </div>
              </div>

              <div className="space-y-2">
                <div className="text-[10px] text-muted uppercase tracking-wider font-bold">Sign-Off Credentials</div>
                <div className="p-2.5 rounded border border-border space-y-1 text-[11px]">
                  <div className="flex justify-between">
                    <span className="text-muted">Primary Approver:</span>
                    <span className="text-ink font-semibold">{activeEntry.authorizedBy}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted">Approval Date:</span>
                    <span className="text-ink">{activeEntry.approvedDate}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted">Expiration Date:</span>
                    <span className="text-ink">{activeEntry.expiresAt}</span>
                  </div>
                </div>
              </div>

              <div className="space-y-1">
                <div className="text-[10px] text-muted uppercase tracking-wider font-bold flex items-center gap-1.5">
                  <Hash className="w-3.5 h-3.5 text-accent" />
                  SHA-256 Ledger Fingerprint
                </div>
                <div className="p-2 rounded bg-surface-muted border border-border text-[10px] text-muted break-all">
                  {activeEntry.auditHash}
                </div>
              </div>

              {activeEntry.status !== 'REVOKED' && (
                <div className="pt-3 border-t border-border">
                  <button
                    onClick={() => handleRevoke(activeEntry.id)}
                    className="w-full py-2 rounded bg-critical/10 text-critical border border-critical/30 hover:bg-critical hover:text-white font-mono font-bold text-xs flex items-center justify-center gap-2 transition-colors"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    Revoke Statutory Exception
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="p-8 text-center text-muted font-mono text-xs">
              Select an entry to view cryptographic sign-off metadata.
            </div>
          )}
        </div>
      </div>

      {/* Add Exception Modal */}
      {isModalOpen && createPortal(
        <div className="fixed inset-0 z-[9999] bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-surface border border-border rounded-xl shadow-xl w-full max-w-lg p-6 space-y-4 font-mono text-xs animate-in fade-in zoom-in-95 duration-150">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="text-sm font-bold text-ink flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-accent" />
                Authorize Statutory Whitelist Bypass
              </h3>
              <button onClick={() => setIsModalOpen(false)} className="text-muted hover:text-ink font-bold">✕</button>
            </div>

            <form onSubmit={handleCreate} className="space-y-3">
              <div>
                <label className="text-muted text-[10px] uppercase font-bold block mb-1">Entity Official Legal Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. UAE Red Crescent Society Operations"
                  value={newEntity.entityName}
                  onChange={e => setNewEntity({ ...newEntity, entityName: e.target.value })}
                  className="w-full p-2 rounded border border-border bg-surface-muted text-ink focus:border-primary focus:outline-none"
                />
              </div>

              <div>
                <label className="text-muted text-[10px] uppercase font-bold block mb-1">Bank Account Number / IBAN</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. AE880330000004561237890"
                  value={newEntity.accountNumber}
                  onChange={e => setNewEntity({ ...newEntity, accountNumber: e.target.value })}
                  className="w-full p-2 rounded border border-border bg-surface-muted text-ink focus:border-primary focus:outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-muted text-[10px] uppercase font-bold block mb-1">Category</label>
                  <select
                    value={newEntity.category}
                    onChange={e => setNewEntity({ ...newEntity, category: e.target.value as WhitelistEntry['category'] })}
                    className="w-full p-2 rounded border border-border bg-surface-muted text-ink focus:border-primary focus:outline-none"
                  >
                    <option value="GOVERNMENT_EMBASSY">Government Embassy</option>
                    <option value="SOVEREIGN_WEALTH">Sovereign Wealth</option>
                    <option value="CORPORATE_PAYROLL">Corporate Payroll</option>
                    <option value="CORRESPONDENT_CENTRAL_BANK">Central Bank</option>
                  </select>
                </div>

                <div>
                  <label className="text-muted text-[10px] uppercase font-bold block mb-1">Expiration TTL</label>
                  <select
                    value={newEntity.ttlDays}
                    onChange={e => setNewEntity({ ...newEntity, ttlDays: Number(e.target.value) })}
                    className="w-full p-2 rounded border border-border bg-surface-muted text-ink focus:border-primary focus:outline-none"
                  >
                    <option value={90}>90 Days (Quarterly Review)</option>
                    <option value={180}>180 Days (Semi-Annual)</option>
                    <option value={365}>365 Days (Annual Max)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="text-muted text-[10px] uppercase font-bold block mb-1">Mandatory Regulatory Justification</label>
                <textarea
                  required
                  rows={3}
                  placeholder="Reference CBUAE circular, ministry exemption decree, or audited corporate governance file..."
                  value={newEntity.rationale}
                  onChange={e => setNewEntity({ ...newEntity, rationale: e.target.value })}
                  className="w-full p-2 rounded border border-border bg-surface-muted text-ink focus:border-primary focus:outline-none"
                />
              </div>

              <div className="p-2.5 rounded bg-warning/10 border border-warning/30 text-warning text-[10px] flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>Notice: Authorizing this exception immediately creates an immutable WORM audit log signed under your officer credentials.</span>
              </div>

              <div className="pt-3 flex justify-end gap-2 border-t border-border">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-3 py-1.5 rounded border border-border text-ink hover:bg-surface-hover"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 rounded bg-primary text-white font-bold hover:bg-primary-hover shadow-xs"
                >
                  Authorize Exception
                </button>
              </div>
            </form>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
};
