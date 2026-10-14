import React, { useState } from 'react';
import { 
  UserCheck, 
  FileText, 
  ShieldAlert
} from 'lucide-react';

interface KycDocument {
  id: string;
  docType: 'PASSPORT' | 'COMMERCIAL_REGISTRY' | 'BOARD_RESOLUTION' | 'UTILITY_BILL';
  docNumber: string;
  issueCountry: string;
  issuedDate: string;
  expiresDate: string;
  ocrStatus: 'VERIFIED_100' | 'VERIFIED_WARNING' | 'PENDING_OCR';
  ocrConfidence: number;
}

interface HistoricalTransaction {
  txId: string;
  timestamp: string;
  counterparty: string;
  amount: string;
  corridor: string;
  decision: 'CLEARED' | 'HELD' | 'BLOCKED';
  riskScore: number;
}

export const CustomerKyc360View: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'OVERVIEW' | 'DOCUMENTS' | 'TIMELINE' | 'ALERTS'>('OVERVIEW');

  // Customer Profile
  const customer = {
    code: 'CUST-AE-88902',
    legalName: 'Rashid Al-Maktoum Trading FZE',
    alias: 'Rashid Maritime Logistics',
    type: 'Corporate Entity',
    registrationNo: 'DED-DXB-9921448',
    jurisdiction: 'United Arab Emirates (JAFZA Free Zone)',
    pepStatus: 'PEP Linked (Tier 2 Shareholder)',
    riskRating: 'HIGH_RISK',
    compositeScore: 84.5,
    onboardedDate: '2023-04-18',
    relationshipManager: 'Mansoor Al-Falasi (Commercial Banking)',
    primaryAccount: 'AE820330000001234567890'
  };

  const documents: KycDocument[] = [
    {
      id: 'doc-01',
      docType: 'COMMERCIAL_REGISTRY',
      docNumber: 'CR-2023-991204',
      issueCountry: 'ARE',
      issuedDate: '2023-04-12',
      expiresDate: '2027-04-11',
      ocrStatus: 'VERIFIED_100',
      ocrConfidence: 99.4
    },
    {
      id: 'doc-02',
      docType: 'BOARD_RESOLUTION',
      docNumber: 'BR-DXB-2026-01',
      issueCountry: 'ARE',
      issuedDate: '2026-01-08',
      expiresDate: '2028-01-08',
      ocrStatus: 'VERIFIED_100',
      ocrConfidence: 98.2
    },
    {
      id: 'doc-03',
      docType: 'PASSPORT',
      docNumber: 'N-8841299-UAE',
      issueCountry: 'ARE',
      issuedDate: '2021-08-15',
      expiresDate: '2026-08-14',
      ocrStatus: 'VERIFIED_WARNING',
      ocrConfidence: 89.0
    }
  ];

  const transactions: HistoricalTransaction[] = [
    {
      txId: 'TX-2026-9921448-AE',
      timestamp: '2026-10-14 07:12 UTC',
      counterparty: 'Victor Bout (Zurich, Switzerland)',
      amount: '$450,000.00 USD',
      corridor: 'ARE -> CHE',
      decision: 'BLOCKED',
      riskScore: 96.4
    },
    {
      txId: 'TX-2026-8812044-AE',
      timestamp: '2026-09-28 14:35 UTC',
      counterparty: 'Global Petrochem Services Ltd (Singapore)',
      amount: '$1,200,000.00 USD',
      corridor: 'ARE -> SGP',
      decision: 'CLEARED',
      riskScore: 18.2
    },
    {
      txId: 'TX-2026-7731902-AE',
      timestamp: '2026-08-14 11:20 UTC',
      counterparty: 'Caspian Shipping Agency (Baku)',
      amount: '$340,000.00 USD',
      corridor: 'ARE -> AZE',
      decision: 'HELD',
      riskScore: 78.5
    }
  ];

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 5 // MOD-KYC-360
            </span>
            <span className="text-[10px] font-mono text-muted">Customer Due Diligence (CDD / EDD)</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <UserCheck className="w-5 h-5 text-primary" />
            Customer KYC 360 Unified Profile Dossier
          </h1>
          <p className="text-xs text-muted mt-1">
            Consolidated 360-degree risk telemetry, document vault OCR verifications, and historical 90-day transaction flow.
          </p>
        </div>

        <div className="flex items-center gap-2 font-mono text-xs">
          <div className="px-3 py-1.5 rounded-lg bg-critical/10 border border-critical/30 text-critical font-bold flex items-center gap-2">
            <ShieldAlert className="w-4 h-4" />
            <span>High-Risk PEP Dossier</span>
          </div>
        </div>
      </div>

      {/* Customer Master Header Card */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-4">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 pb-4 border-b border-border">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary/10 border border-primary/30 flex items-center justify-center text-primary font-mono text-lg font-bold">
              RA
            </div>
            <div>
              <div className="flex items-center gap-2.5">
                <h2 className="text-lg font-bold text-ink">{customer.legalName}</h2>
                <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded bg-critical text-white">
                  RISK: {customer.compositeScore} / 100
                </span>
              </div>
              <div className="text-xs font-mono text-muted flex flex-wrap gap-x-3 gap-y-1 mt-1">
                <span>Code: {customer.code}</span>
                <span>•</span>
                <span>Reg: {customer.registrationNo}</span>
                <span>•</span>
                <span>Jurisdiction: {customer.jurisdiction}</span>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2 text-xs font-mono">
            <div className="text-right">
              <div className="text-[10px] text-muted">PRIMARY ACCOUNT</div>
              <div className="font-bold text-ink">{customer.primaryAccount}</div>
            </div>
          </div>
        </div>

        {/* Navigation Tabs */}
        <div className="flex gap-2 border-b border-border text-xs font-mono">
          {[
            { id: 'OVERVIEW', label: '1. KYC Profile & UBO Radar' },
            { id: 'DOCUMENTS', label: '2. Document Vault & OCR Overlays (3)' },
            { id: 'TIMELINE', label: '3. 90-Day Transaction Flow' },
            { id: 'ALERTS', label: '4. Historical Alert Matrix' }
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

        {/* Tab 1: Overview */}
        {activeTab === 'OVERVIEW' && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 font-mono text-xs pt-2">
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
              <div className="text-muted text-[10px] uppercase font-bold">Corporate Governance & UBOs</div>
              <div className="space-y-1.5 text-[11px]">
                <div className="flex justify-between">
                  <span className="text-muted">Entity Type:</span>
                  <span className="font-bold text-ink">{customer.type}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">PEP Link:</span>
                  <span className="font-bold text-warning">{customer.pepStatus}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Ultimate Beneficial Owner:</span>
                  <span className="font-bold text-ink">Rashid Al-Maktoum (65%)</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Minority Shareholder:</span>
                  <span className="text-ink">Kovalev Trading LLC (35%)</span>
                </div>
              </div>
            </div>

            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
              <div className="text-muted text-[10px] uppercase font-bold">Risk Weighting Breakdown</div>
              <div className="space-y-2 text-[11px]">
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">Corridor Velocity Risk</span>
                    <span className="font-bold text-critical">92%</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className="h-full bg-critical w-[92%]" />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">PEP Exposure Score</span>
                    <span className="font-bold text-warning">78%</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className="h-full bg-warning w-[78%]" />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">Watchlist Proximity</span>
                    <span className="font-bold text-critical">84%</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className="h-full bg-critical w-[84%]" />
                  </div>
                </div>
              </div>
            </div>

            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-2">
              <div className="text-muted text-[10px] uppercase font-bold">Banking Relationship</div>
              <div className="space-y-1.5 text-[11px]">
                <div className="flex justify-between">
                  <span className="text-muted">Relationship Manager:</span>
                  <span className="font-bold text-ink">{customer.relationshipManager}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Onboarding Date:</span>
                  <span className="text-ink">{customer.onboardedDate}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Next Periodic Review:</span>
                  <span className="font-bold text-critical">2026-11-15 (OVERDUE)</span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 2: Document Vault */}
        {activeTab === 'DOCUMENTS' && (
          <div className="space-y-3 font-mono text-xs pt-2">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {documents.map(doc => (
                <div key={doc.id} className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary border border-primary/20">
                      {doc.docType.replace('_', ' ')}
                    </span>
                    <span className={`text-[10px] font-bold ${doc.ocrStatus === 'VERIFIED_100' ? 'text-accent' : 'text-warning'}`}>
                      OCR {doc.ocrConfidence}%
                    </span>
                  </div>
                  <div className="space-y-1 text-[11px]">
                    <div className="font-bold text-ink text-sm">{doc.docNumber}</div>
                    <div className="text-muted">Jurisdiction: {doc.issueCountry}</div>
                    <div className="text-muted">Valid: {doc.issuedDate} → {doc.expiresDate}</div>
                  </div>
                  <button className="w-full py-1.5 rounded border border-border bg-surface hover:bg-surface-hover text-ink text-[11px] font-bold flex items-center justify-center gap-1.5">
                    <FileText className="w-3.5 h-3.5 text-primary" />
                    <span>View OCR Bounding Boxes</span>
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Tab 3: 90-Day Timeline */}
        {activeTab === 'TIMELINE' && (
          <div className="overflow-x-auto font-mono text-xs pt-2">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                  <th className="py-2.5 px-3">Transaction ID</th>
                  <th className="py-2.5 px-3">Timestamp</th>
                  <th className="py-2.5 px-3">Counterparty</th>
                  <th className="py-2.5 px-3">Corridor</th>
                  <th className="py-2.5 px-3">Amount</th>
                  <th className="py-2.5 px-3">Decision</th>
                  <th className="py-2.5 px-3 text-right">Score</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {transactions.map(tx => (
                  <tr key={tx.txId} className="hover:bg-surface-hover">
                    <td className="py-3 px-3 font-bold text-ink">{tx.txId}</td>
                    <td className="py-3 px-3 text-muted">{tx.timestamp}</td>
                    <td className="py-3 px-3 text-ink">{tx.counterparty}</td>
                    <td className="py-3 px-3 text-muted">{tx.corridor}</td>
                    <td className="py-3 px-3 font-bold text-ink">{tx.amount}</td>
                    <td className="py-3 px-3">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        tx.decision === 'BLOCKED' ? 'bg-critical text-white' : tx.decision === 'HELD' ? 'bg-warning/20 text-warning' : 'bg-accent/20 text-accent'
                      }`}>
                        {tx.decision}
                      </span>
                    </td>
                    <td className="py-3 px-3 text-right font-bold text-ink">{tx.riskScore.toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Tab 4: Historical Alerts */}
        {activeTab === 'ALERTS' && (
          <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3 font-mono text-xs">
            <div className="text-ink font-bold text-sm">3 Total Historical AML Escalations</div>
            <div className="space-y-2 text-[11px]">
              <div className="p-3 rounded bg-surface border border-border flex justify-between items-center">
                <div>
                  <div className="font-bold text-critical">AL-2026-0921: Statutory Freeze Triggered</div>
                  <div className="text-muted text-[10px]">OFAC SDN hit on beneficiary Victor Bout in wire TX-2026-9921448-AE</div>
                </div>
                <span className="px-2 py-0.5 rounded bg-critical text-white text-[10px] font-bold">FROZEN</span>
              </div>
              <div className="p-3 rounded bg-surface border border-border flex justify-between items-center">
                <div>
                  <div className="font-bold text-warning">AL-2026-0618: Structuring Threshold Anomaly</div>
                  <div className="text-muted text-[10px]">3 successive wires below $10,000 within 4 hours. Remediated under EDD file 441.</div>
                </div>
                <span className="px-2 py-0.5 rounded bg-accent/20 text-accent text-[10px] font-bold">RESOLVED</span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
