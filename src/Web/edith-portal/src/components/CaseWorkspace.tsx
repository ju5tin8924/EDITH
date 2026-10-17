import React, { useState, useEffect } from 'react';
import { 
  Briefcase, 
  Lock, 
  Unlock, 
  Clock, 
  Send, 
  FileCode, 
  Sparkles, 
  CheckCircle2, 
  XCircle, 
  AlertTriangle,
  History,
  MessageSquare
} from 'lucide-react';
import { 
  CaseSummary, 
  CaseDetail, 
  UserRole, 
  CaseResolution 
} from '../types';
import { EdithApi } from '../services/api';

interface CaseWorkspaceProps {
  currentRole: UserRole;
}

export const CaseWorkspace: React.FC<CaseWorkspaceProps> = ({ currentRole }) => {
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null);
  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [newComment, setNewComment] = useState('');
  const [isLockedByMe, setIsLockedByMe] = useState(false);
  const [modalType, setModalType] = useState<'GOAML' | 'AI_NARRATIVE' | 'RESOLVE' | null>(null);
  const [modalContent, setModalContent] = useState('');
  const [resolveType, setResolveType] = useState<CaseResolution>('FalsePositive');
  const [resolveRationale, setResolveRationale] = useState('');

  useEffect(() => {
    loadCases();
  }, []);

  const loadCases = async () => {
    const list = await EdithApi.fetchCases();
    setCases(list);
    if (list.length > 0 && !selectedCaseId) {
      handleSelectCase(list[0].id);
    }
  };

  const handleSelectCase = async (id: string) => {
    setSelectedCaseId(id);
    setLoading(true);
    try {
      const detail = await EdithApi.fetchCaseDetail(id);
      setCaseDetail(detail);
      setIsLockedByMe(detail.isLocked && detail.lockedByUserName === 'Fatima Al-Nuaimi');
    } finally {
      setLoading(false);
    }
  };

  const toggleLock = async () => {
    if (!selectedCaseId) return;
    if (isLockedByMe) {
      await EdithApi.releaseLock(selectedCaseId);
      setIsLockedByMe(false);
    } else {
      await EdithApi.acquireLock(selectedCaseId);
      setIsLockedByMe(true);
    }
    handleSelectCase(selectedCaseId);
  };

  const handleAddComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim() || !selectedCaseId) return;
    await EdithApi.addComment(selectedCaseId, newComment);
    setNewComment('');
    handleSelectCase(selectedCaseId);
  };

  const handleExportGoAml = async () => {
    if (!selectedCaseId) return;
    const xml = await EdithApi.generateGoAmlXml(selectedCaseId);
    setModalContent(xml);
    setModalType('GOAML');
  };

  const handleGenerateAiNarrative = async () => {
    if (!selectedCaseId) return;
    const narrative = await EdithApi.generateAiNarrative(selectedCaseId);
    setModalContent(narrative);
    setModalType('AI_NARRATIVE');
  };

  const handleConfirmResolution = async () => {
    if (!selectedCaseId || !resolveRationale.trim()) return;
    await EdithApi.resolveCase(selectedCaseId, resolveType, resolveRationale);
    setModalType(null);
    setResolveRationale('');
    const list = await EdithApi.fetchCases();
    setCases(list);
    handleSelectCase(selectedCaseId);
  };

  const handleEscalateToMlro = async () => {
    if (!selectedCaseId) return;
    await EdithApi.transitionCase(selectedCaseId, 'PendingApproval', 'Escalated by Analyst to MLRO senior review queue');
    const list = await EdithApi.fetchCases();
    setCases(list);
    handleSelectCase(selectedCaseId);
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Top Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5">
            <Briefcase className="w-6 h-6 text-primary" />
            P4 Case Investigation Canvas & Adjudication
          </h1>
          <p className="text-xs text-muted mt-1">
            Three-pane investigative workspace with atomic pessimistic locking, diagnostic diffing, and MLRO sign-off.
          </p>
        </div>

        {caseDetail && (
          <div className="flex items-center gap-3">
            {/* Lock Control Banner */}
            <button
              onClick={toggleLock}
              className={`px-3 py-1.5 rounded-lg text-xs font-mono font-semibold flex items-center gap-2 transition ${
                isLockedByMe
                  ? 'bg-warning/10 text-warning border border-warning/40 hover:bg-warning/20'
                  : 'bg-primary/20 text-primary border border-primary/40 hover:bg-primary/30'
              }`}
            >
              {isLockedByMe ? (
                <>
                  <Lock className="w-4 h-4 text-warning" />
                  Locked by You (Release)
                </>
              ) : (
                <>
                  <Unlock className="w-4 h-4 text-primary" />
                  Acquire Case Lock
                </>
              )}
            </button>

            {/* AI Narrative Copilot */}
            <button
              onClick={handleGenerateAiNarrative}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-secondary/10 text-secondary border border-secondary/40 hover:bg-secondary/20 transition flex items-center gap-1.5"
            >
              <Sparkles className="w-4 h-4 text-secondary" />
              AI STR Narrative
            </button>

            {/* goAML XML Export */}
            <button
              onClick={handleExportGoAml}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-accent/10 text-accent border border-accent/40 hover:bg-accent/20 transition flex items-center gap-1.5"
            >
              <FileCode className="w-4 h-4 text-accent" />
              goAML XML Export
            </button>
          </div>
        )}
      </div>

      {/* Case Navigator Bar */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        {cases.map((c) => {
          const isSelected = c.id === selectedCaseId;
          return (
            <div
              key={c.id}
              onClick={() => handleSelectCase(c.id)}
              className={`p-3.5 rounded-xl border cursor-pointer transition-all ${
                isSelected
                  ? 'bg-surface-muted border-primary shadow-sm'
                  : 'bg-surface border-border hover:border-border-light'
              }`}
            >
              <div className="flex justify-between items-start">
                <div className="font-mono text-xs font-bold text-ink">{c.caseNumber}</div>
                <span
                  className={`text-[10px] font-mono px-2 py-0.5 rounded font-bold uppercase ${
                    c.priority === 'CRITICAL'
                      ? 'bg-critical/20 text-critical border border-critical/40'
                      : c.priority === 'HIGH'
                      ? 'bg-warning/20 text-warning border border-warning/40'
                      : 'bg-accent/20 text-accent border border-accent/40'
                  }`}
                >
                  {c.priority}
                </span>
              </div>
              <div className="text-sm font-semibold text-ink mt-1 truncate">
                {c.primarySubjectName}
              </div>
              <div className="flex justify-between items-center text-[11px] text-muted font-mono mt-2 pt-2 border-t border-border/60">
                <span>Score: <strong className="text-critical">{c.riskScore.toFixed(1)}</strong></span>
                <span className="flex items-center gap-1">
                  <Clock className="w-3 h-3 text-muted" /> 3-Day SLA
                </span>
              </div>
            </div>
          );
        })}
      </div>

      {/* 3-Pane Investigation Canvas */}
      {loading ? (
        <div className="p-12 text-center text-muted font-mono text-xs flex items-center justify-center gap-2">
          <Clock className="w-4 h-4 animate-spin text-primary" />
          Loading case investigation telemetry...
        </div>
      ) : caseDetail ? (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          {/* Pane 1: Transaction Dossier (3 cols) */}
          <div className="lg:col-span-3 bg-surface rounded-xl border border-border p-4 space-y-4">
            <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink pb-2 border-b border-border flex items-center gap-2">
              <Briefcase className="w-4 h-4 text-primary" />
              1. Intercept Dossier
            </h2>

            <div className="space-y-3 text-xs">
              <div className="p-3 rounded-lg bg-surface-muted/80 border border-border space-y-1">
                <span className="text-[10px] text-muted font-mono uppercase">Transaction Amount</span>
                <div className="text-lg font-bold font-mono text-ink">
                  {caseDetail.transactionDetails.amount.toLocaleString()} {caseDetail.transactionDetails.currency}
                </div>
                <div className="text-[11px] font-mono text-muted">
                  Ref: {caseDetail.transactionDetails.transactionId}
                </div>
              </div>

              <div className="p-3 rounded-lg bg-surface-muted/80 border border-border space-y-2">
                <div>
                  <span className="text-[10px] text-muted font-mono uppercase">Originating Party</span>
                  <div className="font-semibold text-ink">Rashid International FZE</div>
                  <div className="text-[10px] font-mono text-muted truncate">
                    {caseDetail.transactionDetails.sourceAccount} ({caseDetail.transactionDetails.originatingCountry})
                  </div>
                </div>
                <div className="pt-2 border-t border-border/60">
                  <span className="text-[10px] text-muted font-mono uppercase">Target Beneficiary</span>
                  <div className="font-semibold text-critical">{caseDetail.primarySubjectName}</div>
                  <div className="text-[10px] font-mono text-muted truncate">
                    {caseDetail.transactionDetails.destinationAccount} ({caseDetail.transactionDetails.destinationCountry})
                  </div>
                </div>
              </div>

              <div className="p-3 rounded-lg bg-surface-muted/80 border border-border space-y-1 text-[11px]">
                <div className="flex justify-between">
                  <span className="text-muted">Case Status:</span>
                  <span className="font-mono font-semibold text-warning">{caseDetail.status}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Risk Severity:</span>
                  <span className="font-mono font-semibold text-critical">{caseDetail.priority}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">EDD Due In:</span>
                  <span className="font-mono text-ink">68 Hours Remaining</span>
                </div>
              </div>
            </div>
          </div>

          {/* Pane 2: Diagnostic Diff Viewer (5 cols) */}
          <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-4 space-y-4">
            <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink pb-2 border-b border-border flex items-center justify-between">
              <span>2. Diagnostic Diff Viewer</span>
              <span className="text-critical font-bold">{caseDetail.riskScore.toFixed(1)}% Confidence</span>
            </h2>

            <div className="space-y-4 text-xs">
              {caseDetail.matchedCandidates.map((cand) => (
                <div key={cand.candidateId} className="space-y-3">
                  <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-2">
                    <div className="text-[11px] font-mono text-muted flex justify-between">
                      <span>Designated Entity Match:</span>
                      <span className="text-critical font-semibold uppercase">{cand.category}</span>
                    </div>
                    <div className="text-base font-bold text-ink">{cand.canonicalName}</div>
                    <div className="text-xs text-muted font-mono">
                      SDN Reference: {cand.candidateId} ({cand.sanctionsList})
                    </div>
                  </div>

                  {/* Token Diff Comparison Bar */}
                  <div className="p-3 rounded-lg bg-canvas-subtle border border-border space-y-2.5">
                    <div className="text-[10px] font-mono uppercase tracking-wider text-muted">
                      Token Alignment Breakdown (Red = Exact Match, Amber = Phonetic)
                    </div>

                    {cand.tokenBreakdown.map((t, idx) => (
                      <div key={idx} className="flex items-center justify-between p-2 rounded bg-surface-muted border border-border text-xs font-mono">
                        <div className="flex items-center gap-2">
                          <span className="text-muted">Incoming:</span>
                          <span className="text-primary-light font-bold">"{t.queryToken}"</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="text-muted">Watchlist:</span>
                          <span className="text-critical font-bold">"{t.candidateToken}"</span>
                        </div>
                        <div className="px-2 py-0.5 rounded bg-critical/20 text-critical text-[10px] font-bold">
                          {t.tokenScore}% ({t.matchType})
                        </div>
                      </div>
                    ))}
                  </div>

                  <div className="p-3 rounded-lg bg-surface-muted/60 border border-border/60 text-xs text-ink space-y-1">
                    <div className="font-semibold text-muted font-mono text-[10px] uppercase">Designation Details</div>
                    <p className="text-ink italic">{cand.designationDetails}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Pane 3: Audit Trail & Action Panel (4 cols) */}
          <div className="lg:col-span-4 bg-surface rounded-xl border border-border p-4 space-y-4">
            <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink pb-2 border-b border-border flex items-center justify-between">
              <span className="flex items-center gap-2">
                <History className="w-4 h-4 text-primary" />
                3. Adjudication & Audit
              </span>
              <span className="text-[10px] font-mono text-muted">Immutable Trail</span>
            </h2>

            {/* Resolution Actions (Role-gated) */}
            <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-2.5">
              <div className="text-[10px] font-mono uppercase tracking-wider text-muted font-semibold flex items-between justify-between">
                <span>Final Case Adjudication</span>
                {currentRole === 'MLRO' || currentRole === 'SUPER_ADMIN' ? (
                  <span className="text-accent text-[10px]">MLRO Authorized</span>
                ) : (
                  <span className="text-warning text-[10px]">Analyst (Escalation Only)</span>
                )}
              </div>

              {currentRole === 'MLRO' || currentRole === 'SUPER_ADMIN' ? (
                <div className="grid grid-cols-2 gap-2">
                  <button
                    onClick={() => {
                      setResolveType('TruePositiveReported');
                      setModalType('RESOLVE');
                    }}
                    className="py-2 px-2.5 rounded bg-critical/20 text-critical border border-critical/40 hover:bg-critical/30 transition text-xs font-semibold flex items-center justify-center gap-1.5"
                  >
                    <AlertTriangle className="w-3.5 h-3.5" />
                    Confirm Hit (STR)
                  </button>
                  <button
                    onClick={() => {
                      setResolveType('FalsePositive');
                      setModalType('RESOLVE');
                    }}
                    className="py-2 px-2.5 rounded bg-accent/20 text-accent border border-accent/40 hover:bg-accent/30 transition text-xs font-semibold flex items-center justify-center gap-1.5"
                  >
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    Dismiss (False +)
                  </button>
                </div>
              ) : (
                <button
                  onClick={handleEscalateToMlro}
                  className="w-full py-2 rounded bg-warning/10 text-warning border border-warning/40 hover:bg-warning/20 transition text-xs font-semibold flex items-center justify-center gap-1.5"
                >
                  <Send className="w-3.5 h-3.5" />
                  Escalate Case to MLRO Queue
                </button>
              )}
            </div>

            {/* Comments & Activity Stream */}
            <div className="space-y-3">
              <div className="text-[10px] font-mono uppercase tracking-wider text-muted font-semibold flex items-center gap-1.5">
                <MessageSquare className="w-3.5 h-3.5" />
                Case Comments & Investigation Log
              </div>

              <div className="max-h-48 overflow-y-auto space-y-2 pr-1">
                {caseDetail.comments.map((comm) => (
                  <div key={comm.id} className="p-2.5 rounded bg-surface-muted border border-border/80 text-xs space-y-1">
                    <div className="flex justify-between items-center text-[10px] font-mono text-muted">
                      <span className="font-semibold text-ink">{comm.authorName}</span>
                      <span>{new Date(comm.timestampUtc).toLocaleTimeString()}</span>
                    </div>
                    <p className="text-ink">{comm.content}</p>
                  </div>
                ))}
              </div>

              <form onSubmit={handleAddComment} className="flex gap-2">
                <input
                  type="text"
                  placeholder="Add analyst note..."
                  value={newComment}
                  onChange={(e) => setNewComment(e.target.value)}
                  className="flex-1 bg-canvas-subtle border border-border rounded px-3 py-1.5 text-xs text-ink focus:border-primary focus:outline-none"
                />
                <button
                  type="submit"
                  className="p-2 rounded bg-primary text-white hover:bg-primary-hover transition font-medium"
                >
                  <Send className="w-3.5 h-3.5" />
                </button>
              </form>
            </div>
          </div>
        </div>
      ) : null}

      {/* Modal Dialog for goAML / AI Narrative / Resolution */}
      {modalType && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/40 backdrop-blur-sm p-4">
          <div className="bg-surface border border-border rounded-xl max-w-2xl w-full p-6 space-y-4 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="font-mono font-bold text-base text-ink flex items-center gap-2">
                {modalType === 'GOAML' && <FileCode className="w-5 h-5 text-accent" />}
                {modalType === 'AI_NARRATIVE' && <Sparkles className="w-5 h-5 text-secondary" />}
                {modalType === 'RESOLVE' && <AlertTriangle className="w-5 h-5 text-warning" />}
                {modalType === 'GOAML' && 'UAE Financial Intelligence Unit (goAML XML Export)'}
                {modalType === 'AI_NARRATIVE' && 'FATF Compliant 5-Paragraph STR Copilot Narrative'}
                {modalType === 'RESOLVE' && `Adjudicate Case: ${resolveType}`}
              </h3>
              <button onClick={() => setModalType(null)} className="text-muted hover:text-ink">
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            {modalType === 'RESOLVE' ? (
              <div className="space-y-4 text-xs">
                <div>
                  <label className="text-muted block mb-1">Formal Adjudication Rationale</label>
                  <textarea
                    rows={4}
                    required
                    placeholder="Provide detailed statutory rationale for this adjudication decision..."
                    value={resolveRationale}
                    onChange={(e) => setResolveRationale(e.target.value)}
                    className="w-full bg-canvas-subtle border border-border rounded p-3 text-ink font-mono focus:border-primary focus:outline-none"
                  />
                </div>
                <div className="flex justify-end gap-3">
                  <button
                    onClick={() => setModalType(null)}
                    className="px-4 py-2 rounded bg-surface-hover text-ink hover:bg-surface-hover"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleConfirmResolution}
                    className="px-4 py-2 rounded bg-primary text-white font-bold hover:bg-primary-hover font-mono"
                  >
                    Submit Statutory Decision
                  </button>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                <pre className="p-4 rounded-lg bg-canvas-subtle border border-border text-xs font-mono text-ink overflow-x-auto max-h-96 whitespace-pre-wrap">
                  {modalContent}
                </pre>
                <div className="flex justify-end">
                  <button
                    onClick={() => setModalType(null)}
                    className="px-4 py-2 rounded bg-primary text-white font-semibold font-mono text-xs hover:bg-primary-hover"
                  >
                    Close & Return to Case
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
