import React, { useState } from 'react';
import { 
  Download, 
  FileSpreadsheet, 
  CheckCircle2, 
  FileText
} from 'lucide-react';

interface AuditLedgerEntry {
  id: string;
  timestamp: string;
  actor: string;
  role: string;
  action: string;
  targetObject: string;
  sha256Hash: string;
}

const MOCK_AUDIT_LOGS: AuditLedgerEntry[] = [
  {
    id: 'aud-001',
    timestamp: '2026-10-14 07:15:22 UTC',
    actor: 'Fatima Al-Nuaimi',
    role: 'MLRO',
    action: 'CASE_STATUTORY_FREEZE_DISPATCH',
    targetObject: 'EDITH-CAS-2026-0042',
    sha256Hash: '0x8f9c42b10a294877e31d0441c883e29177a0bf99214b7e41981249b291a18204'
  },
  {
    id: 'aud-002',
    timestamp: '2026-10-14 06:48:10 UTC',
    actor: 'Tariq Mansoor',
    role: 'ANALYST',
    action: 'CASE_ADJUDICATION_LOCKED',
    targetObject: 'EDITH-CAS-2026-0043',
    sha256Hash: '0x1a72d93e8849b21a007f31c44883e10088b901fc8821994b7e41981249b291a1'
  },
  {
    id: 'aud-003',
    timestamp: '2026-10-14 04:30:00 UTC',
    actor: 'System Worker (MOD-SCRN-DELTA)',
    role: 'DAEMON',
    action: 'NIGHTLY_DELTA_WATCHLIST_SYNC',
    targetObject: 'BATCH-2026-10-14-0200',
    sha256Hash: '0x99214ab7710c441890e31d0441c883e29177a0bf99214b7e41981249b291a188'
  },
  {
    id: 'aud-004',
    timestamp: '2026-10-13 18:22:15 UTC',
    actor: 'Super Admin',
    role: 'SUPER_ADMIN',
    action: 'RULE_WEIGHTAGE_UPDATED',
    targetObject: 'SCORE-CONFIG-DEFAULT',
    sha256Hash: '0x334411ee8849b21a007f31c44883e10088b901fc8821994b7e41981249b291a2'
  }
];

export const ReportsAuditView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'AUDIT' | 'CORRIDORS' | 'KPI'>('AUDIT');
  const [downloadSuccess, setDownloadSuccess] = useState<string | null>(null);

  const triggerExport = (format: 'PDF' | 'CSV') => {
    setDownloadSuccess(`Exporting cryptographic ${format} report bundle. Verified by central audit ledger.`);
    setTimeout(() => setDownloadSuccess(null), 4000);
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 8 // MOD-REP-BASE
            </span>
            <span className="text-[10px] font-mono text-muted">Immutable WORM Audit Trail</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <FileSpreadsheet className="w-5 h-5 text-primary" />
            Compliance Reports, Analytics & Audit Ledger
          </h1>
          <p className="text-xs text-muted mt-1">
            Central bank regulatory reporting center, velocity anomaly aggregations, and tamper-proof WORM audit logs.
          </p>
        </div>

        <div className="flex items-center gap-2 font-mono text-xs">
          <button
            onClick={() => triggerExport('CSV')}
            className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink flex items-center gap-1.5 shadow-xs"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Export CSV</span>
          </button>
          <button
            onClick={() => triggerExport('PDF')}
            className="px-3.5 py-1.5 rounded-lg bg-primary text-white font-bold hover:bg-primary-hover flex items-center gap-1.5 shadow-xs"
          >
            <FileText className="w-3.5 h-3.5" />
            <span>Generate Certified PDF</span>
          </button>
        </div>
      </div>

      {downloadSuccess && (
        <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs font-mono flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{downloadSuccess}</span>
        </div>
      )}

      {/* Tabs */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-4">
        <div className="flex gap-2 border-b border-border text-xs font-mono">
          {[
            { id: 'AUDIT', label: '1. Immutable WORM Audit Ledger' },
            { id: 'CORRIDORS', label: '2. High-Risk Jurisdiction Risk Heatmap' },
            { id: 'KPI', label: '3. SLA Velocity Anomaly Bands' }
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

        {/* Tab 1: WORM Audit Ledger */}
        {activeTab === 'AUDIT' && (
          <div className="space-y-3 font-mono text-xs">
            <div className="flex justify-between items-center text-muted text-[11px] pb-2">
              <span>Showing last 24h certified cryptographic audit ledger entries</span>
              <span className="text-accent font-bold">SHA-256 Chained Integrity OK</span>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                    <th className="py-2.5 px-3">Timestamp (UTC)</th>
                    <th className="py-2.5 px-3">Authorized Actor</th>
                    <th className="py-2.5 px-3">Action Type</th>
                    <th className="py-2.5 px-3">Target Object</th>
                    <th className="py-2.5 px-3">Cryptographic SHA-256 Hash</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {MOCK_AUDIT_LOGS.map(log => (
                    <tr key={log.id} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 text-muted">{log.timestamp}</td>
                      <td className="py-3 px-3">
                        <div className="font-bold text-ink">{log.actor}</div>
                        <div className="text-[10px] text-muted">{log.role}</div>
                      </td>
                      <td className="py-3 px-3">
                        <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary border border-primary/20">
                          {log.action}
                        </span>
                      </td>
                      <td className="py-3 px-3 font-bold text-ink">{log.targetObject}</td>
                      <td className="py-3 px-3 text-[10px] text-muted break-all max-w-[280px]">
                        {log.sha256Hash}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 2: Corridors Heatmap */}
        {activeTab === 'CORRIDORS' && (
          <div className="space-y-4 font-mono text-xs">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {[
                { country: 'United Arab Emirates (ARE)', wires: '1,840,291', status: 'Domestic Clearing', risk: 'LOW (10/100)', color: 'text-accent' },
                { country: 'Switzerland (CHE)', wires: '248,190', status: 'Private Banking Wire', risk: 'MEDIUM (35/100)', color: 'text-primary' },
                { country: 'Iran (IRN)', wires: '12', status: 'Sanctions Quarantined', risk: 'CRITICAL (99/100)', color: 'text-critical' }
              ].map(item => (
                <div key={item.country} className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
                  <div className="flex justify-between items-center">
                    <span className="font-bold text-ink">{item.country}</span>
                    <span className={`text-[10px] font-bold ${item.color}`}>{item.risk}</span>
                  </div>
                  <div className="text-xl font-bold text-ink">{item.wires} Wires</div>
                  <div className="text-[11px] text-muted">{item.status}</div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tab 3: KPI Velocity */}
        {activeTab === 'KPI' && (
          <div className="space-y-3 font-mono text-xs">
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
              <div className="font-bold text-ink text-sm">Real-Time Throughput Velocity Bands</div>
              <div className="text-muted text-[11px]">
                Peak ingestion observed at 14:00 UTC with 8,420 transactions/minute. Average processing latency remained strictly at 8.4ms (within the 20ms SLA budget).
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
