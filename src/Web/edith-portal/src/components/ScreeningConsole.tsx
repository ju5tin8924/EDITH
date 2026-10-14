import React, { useState } from 'react';
import { 
  ShieldAlert, 
  CheckCircle2, 
  AlertTriangle, 
  Clock, 
  Zap, 
  Search, 
  ArrowRight,
  ShieldCheck,
  Building,
  UserCheck
} from 'lucide-react';
import { 
  TransactionScreeningRequest, 
  ScreeningResult 
} from '../types';
import { EdithApi } from '../services/api';

interface ScreeningConsoleProps {
  onOpenCase?: (caseId: string) => void;
}

export const ScreeningConsole: React.FC<ScreeningConsoleProps> = ({ onOpenCase }) => {
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState<TransactionScreeningRequest>({
    sourceAccount: 'AE820330000001234567890',
    destinationAccount: 'CH9300762011623852957',
    amount: 450000,
    currency: 'USD',
    originatingCountry: 'AE',
    destinationCountry: 'CH',
    parties: [
      {
        role: 'BENEFICIARY',
        fullName: 'Victor Bout',
        country: 'RU',
        dateOfBirth: '1967-01-13'
      },
      {
        role: 'SENDER',
        fullName: 'Rashid Trading Corp FZE',
        country: 'AE'
      }
    ]
  });

  const [result, setResult] = useState<ScreeningResult | null>(null);

  const applyPreset = (presetType: 'sanction' | 'clean' | 'high_amount') => {
    if (presetType === 'sanction') {
      setFormData({
        sourceAccount: 'AE820330000001234567890',
        destinationAccount: 'CH9300762011623852957',
        amount: 450000,
        currency: 'USD',
        originatingCountry: 'AE',
        destinationCountry: 'CH',
        parties: [
          {
            role: 'BENEFICIARY',
            fullName: 'Victor Bout',
            country: 'RU',
            dateOfBirth: '1967-01-13'
          },
          {
            role: 'SENDER',
            fullName: 'Rashid International FZE',
            country: 'AE'
          }
        ]
      });
    } else if (presetType === 'clean') {
      setFormData({
        sourceAccount: 'AE440330000009876543210',
        destinationAccount: 'AE120330000001122334455',
        amount: 3200,
        currency: 'AED',
        originatingCountry: 'AE',
        destinationCountry: 'AE',
        parties: [
          {
            role: 'BENEFICIARY',
            fullName: 'Salem Mohammed Al-Maktoum',
            country: 'AE'
          },
          {
            role: 'SENDER',
            fullName: 'Dubai Retail Holdings LLC',
            country: 'AE'
          }
        ]
      });
    } else {
      setFormData({
        sourceAccount: 'AE550330000005544332211',
        destinationAccount: 'GB29NWBK60161331926819',
        amount: 850000,
        currency: 'AED',
        originatingCountry: 'AE',
        destinationCountry: 'GB',
        parties: [
          {
            role: 'BENEFICIARY',
            fullName: 'Caspian Petrochemical Logistics',
            country: 'IR'
          },
          {
            role: 'SENDER',
            fullName: 'Al-Khaleej Marine Services',
            country: 'AE'
          }
        ]
      });
    }
  };

  const handleScreen = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const res = await EdithApi.screenTransaction(formData);
      setResult(res);
    } finally {
      setLoading(false);
    }
  };

  const getDecisionBadge = (decision: string) => {
    switch (decision) {
      case 'BLOCKED':
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-critical/20 border border-critical text-critical font-mono font-bold text-sm tracking-wider shadow-sm">
            <ShieldAlert className="w-4 h-4" />
            BLOCKED (STATUTORY FREEZE)
          </div>
        );
      case 'HELD':
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-warning/20 border border-warning text-warning font-mono font-bold text-sm tracking-wider shadow-sm">
            <AlertTriangle className="w-4 h-4" />
            HELD (PENDING MLRO REVIEW)
          </div>
        );
      default:
        return (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-accent/20 border border-accent text-accent font-mono font-bold text-sm tracking-wider shadow-sm">
            <CheckCircle2 className="w-4 h-4" />
            CLEARED (AUTO-RELEASED)
          </div>
        );
    }
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Top Banner & Quick Presets */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5">
            <ShieldAlert className="w-6 h-6 text-primary" />
            P2 Real-Time Transaction Screening Console
          </h1>
          <p className="text-xs text-muted mt-1">
            5-Pass deterministic pipeline evaluating Jaro-Winkler, Damerau-Levenshtein, and Double Metaphone phonetics.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-xs text-muted font-medium">Quick Test Scenarios:</span>
          <button
            onClick={() => applyPreset('sanction')}
            className="px-2.5 py-1 text-xs rounded bg-critical/15 text-critical border border-critical/30 hover:bg-critical/25 transition font-medium"
          >
            Sanctions Hit (Victor Bout)
          </button>
          <button
            onClick={() => applyPreset('clean')}
            className="px-2.5 py-1 text-xs rounded bg-accent/15 text-accent border border-accent/30 hover:bg-accent/25 transition font-medium"
          >
            Clean Domestic Wire
          </button>
          <button
            onClick={() => applyPreset('high_amount')}
            className="px-2.5 py-1 text-xs rounded bg-warning/15 text-warning border border-warning/30 hover:bg-warning/25 transition font-medium"
          >
            High Risk Corridor
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Screening Input Form (Left 5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-4">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h2 className="text-sm font-semibold text-ink uppercase tracking-wider font-mono flex items-center gap-2">
              <Zap className="w-4 h-4 text-primary" />
              Transaction Payload
            </h2>
            <span className="text-[11px] font-mono text-muted">Idempotency Key Auto-Generated</span>
          </div>

          <form onSubmit={handleScreen} className="space-y-4 text-xs">
            {/* Beneficiary Details */}
            <div className="p-3 bg-surface-muted/60 rounded-lg border border-border/80 space-y-3">
              <div className="font-semibold text-ink flex items-center gap-1.5">
                <UserCheck className="w-4 h-4 text-primary" />
                Primary Beneficiary (Screening Target)
              </div>
              <div>
                <label className="text-muted block mb-1">Full Name / Legal Entity</label>
                <input
                  type="text"
                  required
                  value={formData.parties[0]?.fullName || ''}
                  onChange={(e) => {
                    const newParties = [...formData.parties];
                    newParties[0].fullName = e.target.value;
                    setFormData({ ...formData, parties: newParties });
                  }}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono focus:border-primary focus:outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-muted block mb-1">Country (ISO-2)</label>
                  <input
                    type="text"
                    maxLength={2}
                    value={formData.parties[0]?.country || 'AE'}
                    onChange={(e) => {
                      const newParties = [...formData.parties];
                      newParties[0].country = e.target.value.toUpperCase();
                      setFormData({ ...formData, parties: newParties });
                    }}
                    className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono uppercase focus:border-primary focus:outline-none"
                  />
                </div>
                <div>
                  <label className="text-muted block mb-1">Date of Birth</label>
                  <input
                    type="date"
                    value={formData.parties[0]?.dateOfBirth || ''}
                    onChange={(e) => {
                      const newParties = [...formData.parties];
                      newParties[0].dateOfBirth = e.target.value;
                      setFormData({ ...formData, parties: newParties });
                    }}
                    className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono focus:border-primary focus:outline-none"
                  />
                </div>
              </div>
            </div>

            {/* Sender Details */}
            <div className="p-3 bg-surface-muted/60 rounded-lg border border-border/80 space-y-3">
              <div className="font-semibold text-ink flex items-center gap-1.5">
                <Building className="w-4 h-4 text-muted" />
                Originating Counterparty (Sender)
              </div>
              <div>
                <label className="text-muted block mb-1">Sender Name</label>
                <input
                  type="text"
                  value={formData.parties[1]?.fullName || ''}
                  onChange={(e) => {
                    const newParties = [...formData.parties];
                    newParties[1].fullName = e.target.value;
                    setFormData({ ...formData, parties: newParties });
                  }}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono focus:border-primary focus:outline-none"
                />
              </div>
            </div>

            {/* Financial Details */}
            <div className="grid grid-cols-3 gap-3">
              <div className="col-span-2">
                <label className="text-muted block mb-1">Transfer Amount</label>
                <input
                  type="number"
                  step="any"
                  required
                  value={formData.amount}
                  onChange={(e) => setFormData({ ...formData, amount: parseFloat(e.target.value) || 0 })}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono focus:border-primary focus:outline-none font-semibold"
                />
              </div>
              <div>
                <label className="text-muted block mb-1">Currency</label>
                <select
                  value={formData.currency}
                  onChange={(e) => setFormData({ ...formData, currency: e.target.value })}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono focus:border-primary focus:outline-none"
                >
                  <option value="AED">AED</option>
                  <option value="USD">USD</option>
                  <option value="EUR">EUR</option>
                  <option value="GBP">GBP</option>
                  <option value="SAR">SAR</option>
                </select>
              </div>
            </div>

            {/* Account IBANs */}
            <div className="space-y-2">
              <div>
                <label className="text-muted block mb-1 font-mono text-[11px]">Debtor IBAN (Source)</label>
                <input
                  type="text"
                  value={formData.sourceAccount}
                  onChange={(e) => setFormData({ ...formData, sourceAccount: e.target.value })}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono text-[11px] focus:border-primary focus:outline-none"
                />
              </div>
              <div>
                <label className="text-muted block mb-1 font-mono text-[11px]">Creditor IBAN (Destination)</label>
                <input
                  type="text"
                  value={formData.destinationAccount}
                  onChange={(e) => setFormData({ ...formData, destinationAccount: e.target.value })}
                  className="w-full bg-canvas-subtle border border-border rounded px-3 py-1.5 text-ink font-mono text-[11px] focus:border-primary focus:outline-none"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 rounded-lg bg-primary hover:bg-primary-hover text-white font-semibold font-mono text-xs flex items-center justify-center gap-2 transition-all shadow-sm disabled:opacity-50"
            >
              {loading ? (
                <>
                  <Clock className="w-4 h-4 animate-spin" />
                  Running 5-Pass Screening Pipeline...
                </>
              ) : (
                <>
                  <Zap className="w-4 h-4 fill-white" />
                  Execute Real-Time Screening (Pass 1-5)
                </>
              )}
            </button>
          </form>
        </div>

        {/* Screening Results & Diagnostics (Right 7 cols) */}
        <div className="lg:col-span-7 space-y-4">
          {result ? (
            <div className="bg-surface rounded-xl border border-border p-5 space-y-5">
              {/* Verdict Header */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-border">
                <div className="space-y-1">
                  <div className="text-[10px] font-mono uppercase tracking-wider text-muted">Screening Verdict</div>
                  {getDecisionBadge(result.decision)}
                </div>

                <div className="flex items-center gap-3 font-mono text-xs">
                  <div className="px-3 py-1.5 rounded bg-surface-muted border border-border">
                    <span className="text-muted">Risk Score:</span>{' '}
                    <span className={`font-bold ${result.riskScore >= 80 ? 'text-critical' : result.riskScore >= 50 ? 'text-warning' : 'text-accent'}`}>
                      {result.riskScore.toFixed(1)} / 100
                    </span>
                  </div>

                  <div className="px-3 py-1.5 rounded bg-surface-muted border border-border flex items-center gap-1.5 text-ink">
                    <Clock className="w-3.5 h-3.5 text-primary" />
                    <span>Latency:</span>{' '}
                    <span className="text-primary font-bold">{result.evaluationLatencyMs}ms</span>
                  </div>
                </div>
              </div>

              {/* 5-Pass Diagnostic Progress Bar */}
              <div className="space-y-2 bg-surface-muted/60 p-3.5 rounded-lg border border-border">
                <div className="text-[11px] font-mono text-muted uppercase tracking-wider font-semibold flex justify-between">
                  <span>5-Pass Pipeline Execution</span>
                  <span className="text-accent flex items-center gap-1">
                    <ShieldCheck className="w-3.5 h-3.5" /> All Passes Verified
                  </span>
                </div>
                <div className="grid grid-cols-5 gap-1.5 text-[10px] font-mono text-center">
                  <div className="p-1.5 rounded bg-accent/10 border border-accent/30 text-accent">
                    Pass 1: Normalize
                  </div>
                  <div className="p-1.5 rounded bg-accent/10 border border-accent/30 text-accent">
                    Pass 2: Candidate
                  </div>
                  <div className="p-1.5 rounded bg-accent/10 border border-accent/30 text-accent">
                    Pass 3: Phonetics
                  </div>
                  <div className="p-1.5 rounded bg-accent/10 border border-accent/30 text-accent">
                    Pass 4: Multi-Role
                  </div>
                  <div className="p-1.5 rounded bg-accent/10 border border-accent/30 text-accent">
                    Pass 5: Whitelist
                  </div>
                </div>
              </div>

              {/* Matches List */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink">
                    Candidate Matches Found ({result.matchCount})
                  </h3>
                  {result.primaryHitReason && (
                    <span className="text-[11px] font-mono text-critical bg-critical/10 px-2 py-0.5 rounded border border-critical/30">
                      Hit: {result.primaryHitReason}
                    </span>
                  )}
                </div>

                {result.partyEvaluations.map((evalParty, pIdx) => (
                  <div key={pIdx} className="space-y-2">
                    {evalParty.matchedCandidates.length === 0 ? (
                      <div className="p-4 rounded-lg bg-surface-muted/40 border border-dashed border-border text-center text-xs text-muted">
                        No sanctions, PEP, or adverse media hits detected for "{evalParty.queryName}".
                      </div>
                    ) : (
                      evalParty.matchedCandidates.map((cand) => (
                        <div
                          key={cand.candidateId}
                          className="p-4 rounded-lg bg-surface-muted border border-border hover:border-border-light transition space-y-3"
                        >
                          <div className="flex justify-between items-start">
                            <div>
                              <div className="flex items-center gap-2">
                                <span className="font-semibold text-ink text-sm">{cand.canonicalName}</span>
                                <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-critical/20 text-critical border border-critical/40 uppercase font-bold">
                                  {cand.category}
                                </span>
                              </div>
                              <div className="text-[11px] text-muted font-mono mt-0.5">
                                List: {cand.sanctionsList || 'OFAC / UN'} • DOB: {cand.dateOfBirth || 'N/A'} • Nat: {cand.nationality || 'N/A'}
                              </div>
                            </div>

                            <div className="text-right font-mono">
                              <div className="text-lg font-bold text-critical">
                                {cand.confidenceScore.toFixed(1)}%
                              </div>
                              <div className="text-[10px] text-muted">Match Confidence</div>
                            </div>
                          </div>

                          {/* Diagnostic Token Comparison */}
                          {cand.tokenBreakdown && cand.tokenBreakdown.length > 0 && (
                            <div className="p-2.5 rounded bg-canvas-subtle border border-border/80 space-y-1.5">
                              <div className="text-[10px] font-mono uppercase tracking-wider text-muted">
                                Token Alignment & Phonetics Diagnostic
                              </div>
                              <div className="flex flex-wrap gap-2">
                                {cand.tokenBreakdown.map((token, tIdx) => (
                                  <div
                                    key={tIdx}
                                    className="flex items-center gap-1.5 px-2 py-1 rounded bg-surface-muted border border-border text-[11px] font-mono"
                                  >
                                    <span className="text-primary-light font-medium">"{token.queryToken}"</span>
                                    <ArrowRight className="w-3 h-3 text-disabled" />
                                    <span className="text-ink font-medium">"{token.candidateToken}"</span>
                                    <span className="text-[10px] px-1 rounded bg-critical/20 text-critical font-bold">
                                      {token.tokenScore}% ({token.matchType})
                                    </span>
                                  </div>
                                ))}
                              </div>
                            </div>
                          )}

                          {cand.designationDetails && (
                            <p className="text-xs text-muted italic bg-canvas-subtle/60 p-2 rounded border border-border/50">
                              "{cand.designationDetails}"
                            </p>
                          )}
                        </div>
                      ))
                    )}
                  </div>
                ))}

                {result.matchCount > 0 && onOpenCase && (
                  <button
                    onClick={() => onOpenCase('c1111111-2222-3333-4444-555555555555')}
                    className="w-full py-2.5 rounded-lg bg-critical/20 text-critical border border-critical/40 hover:bg-critical/30 transition text-xs font-semibold flex items-center justify-center gap-2 font-mono"
                  >
                    <ShieldAlert className="w-4 h-4" />
                    Open Intercepted Case in Investigation Canvas (P4)
                  </button>
                )}
              </div>
            </div>
          ) : (
            <div className="h-full min-h-[400px] flex flex-col items-center justify-center bg-surface rounded-xl border border-border p-8 text-center space-y-3">
              <div className="w-14 h-14 rounded-2xl bg-surface-muted border border-border flex items-center justify-center text-muted">
                <Search className="w-7 h-7" />
              </div>
              <h3 className="text-ink font-semibold text-sm">Real-Time Evaluation Engine Ready</h3>
              <p className="text-xs text-muted max-w-sm">
                Provide transaction details on the left and trigger 5-Pass screening to view instant scoring diagnostics, token comparisons, and sanctions resolutions.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
