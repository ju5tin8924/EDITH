import React, { useState, useEffect, useMemo } from 'react';
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
  MessageSquare,
  Search,
  ChevronLeft,
  ChevronRight,
  Network,
  MapPin,
  UserCheck,
  FileText,
  Download,
  Copy,
  Check,
  Layers,
  Sliders,
  CheckSquare,
  RefreshCw
} from 'lucide-react';
import { 
  CaseSummary, 
  CaseDetail, 
  UserRole, 
  CaseResolution
} from '../types';
import { EdithApi, getSession } from '../services/api';

interface CaseWorkspaceProps {
  currentRole: UserRole;
}

type TabMode = 'DIFF' | 'GRAPH' | 'CORRIDOR' | 'KYC360';
type QueueFilter = 'ALL' | 'MY_LOCKS' | 'MLRO_QUEUE' | 'CRITICAL' | 'RESOLVED';
type NoteCategory = 'INVESTIGATION' | 'SUPERVISOR' | 'RATIONALE';

export const CaseWorkspace: React.FC<CaseWorkspaceProps> = ({ currentRole }) => {
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null);
  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [queueCollapsed, setQueueCollapsed] = useState(false);
  const [queueSearch, setQueueSearch] = useState('');
  const [queueFilter, setQueueFilter] = useState<QueueFilter>('ALL');

  // Multi-angle investigation tab
  const [activeTab, setActiveTab] = useState<TabMode>('DIFF');

  // Locking and lease timer
  const [locallyLockedIds, setLocallyLockedIds] = useState<string[]>([]);
  const [lockRemainingSeconds, setLockRemainingSeconds] = useState<number>(900); // 15 mins default
  const [showDurationPicker, setShowDurationPicker] = useState(false);

  // Notes and Activity
  const [newComment, setNewComment] = useState('');
  const [selectedNoteCategory, setSelectedNoteCategory] = useState<NoteCategory>('INVESTIGATION');
  const [showAuditLogsInTimeline, setShowAuditLogsInTimeline] = useState(true);

  // Modals
  const [modalType, setModalType] = useState<'GOAML' | 'AI_NARRATIVE' | 'RESOLVE' | 'EDD_CHECKLIST' | null>(null);
  const [modalContent, setModalContent] = useState('');
  const [editableNarrative, setEditableNarrative] = useState('');
  const [copiedSuccess, setCopiedSuccess] = useState(false);
  const [resolveType, setResolveType] = useState<CaseResolution>('FalsePositive');
  const [resolveRationale, setResolveRationale] = useState('');
  const [autoWhitelist, setAutoWhitelist] = useState(false);

  // EDD Checklist state
  const [eddChecklist, setEddChecklist] = useState({
    sourceOfFunds: true,
    commercialInvoices: true,
    uboAttestation: false,
    adverseMediaRecheck: false
  });

  // Selected Network Node for Graph Inspector
  const [selectedGraphNode, setSelectedGraphNode] = useState<{
    id: string;
    label: string;
    role: string;
    jurisdiction: string;
    riskScore: number;
    details: string;
  } | null>(null);

  const currentSession = getSession();
  const currentUsername = currentSession.username || 'analyst.fatima@adcb.ae';

  useEffect(() => {
    loadCases();
  }, []);

  // Lock countdown timer
  useEffect(() => {
    if (!caseDetail) return;
    const interval = setInterval(() => {
      setLockRemainingSeconds(prev => {
        if (prev <= 1) {
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [caseDetail?.id, caseDetail?.isLocked]);

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

      // Initialize lease countdown based on lockExpiresAtUtc or default
      if (detail.lockExpiresAtUtc) {
        const expires = new Date(detail.lockExpiresAtUtc).getTime();
        const diff = Math.max(0, Math.floor((expires - Date.now()) / 1000));
        setLockRemainingSeconds(diff > 0 ? diff : 900);
      } else {
        setLockRemainingSeconds(900);
      }
    } finally {
      setLoading(false);
    }
  };

  const isLockedByMe = useMemo(() => {
    if (!caseDetail?.isLocked) return false;
    if (locallyLockedIds.includes(caseDetail.id)) return true;
    const lockedName = caseDetail.lockedByUserName?.toLowerCase() || '';
    const myName = currentUsername.toLowerCase();
    const myRole = currentRole.toLowerCase();
    return (
      lockedName === myName ||
      lockedName.includes('fatima') ||
      lockedName === myRole ||
      (currentRole === 'MLRO' && lockedName.includes('mlro')) ||
      (currentRole === 'ANALYST' && lockedName.includes('analyst'))
    );
  }, [caseDetail, currentUsername, currentRole, locallyLockedIds]);

  const isLockedByOther = useMemo(() => {
    return !!(caseDetail?.isLocked && !isLockedByMe);
  }, [caseDetail, isLockedByMe]);

  const toggleLock = async () => {
    if (!selectedCaseId) return;
    if (isLockedByMe) {
      await EdithApi.releaseLock(selectedCaseId);
      setLocallyLockedIds(prev => prev.filter(id => id !== selectedCaseId));
      if (caseDetail) {
        setCaseDetail({ ...caseDetail, isLocked: false, lockedByUserName: undefined });
      }
    } else {
      await handleRenewLock(15);
    }
  };

  const handleRenewLock = async (durationMinutes: number) => {
    if (!selectedCaseId) return;
    setShowDurationPicker(false);
    await EdithApi.acquireLock(selectedCaseId, durationMinutes);
    setLocallyLockedIds(prev => Array.from(new Set([...prev, selectedCaseId])));
    setLockRemainingSeconds(durationMinutes * 60);

    const detail = await EdithApi.fetchCaseDetail(selectedCaseId);
    setCaseDetail(detail);
    const list = await EdithApi.fetchCases();
    setCases(list);
  };

  const handleAddComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim() || !selectedCaseId) return;
    await EdithApi.addComment(selectedCaseId, newComment, selectedNoteCategory);
    setNewComment('');
    const detail = await EdithApi.fetchCaseDetail(selectedCaseId);
    setCaseDetail(detail);
  };

  const handleExportGoAml = async () => {
    if (!selectedCaseId) return;
    const xml = await EdithApi.generateGoAmlXml(selectedCaseId);
    setModalContent(xml);
    setModalType('GOAML');
  };

  const handleDownloadGoAmlXml = () => {
    const filename = `goAML_STR_${caseDetail?.caseNumber || 'export'}.xml`;
    const blob = new Blob([modalContent], { type: 'application/xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const handleGenerateAiNarrative = async () => {
    if (!selectedCaseId) return;
    const narrative = await EdithApi.generateAiNarrative(selectedCaseId);
    setModalContent(narrative);
    setEditableNarrative(narrative);
    setModalType('AI_NARRATIVE');
  };

  const handleApplyNarrativeToRationale = () => {
    setResolveRationale(editableNarrative);
    setResolveType('TruePositiveReported');
    setModalType('RESOLVE');
  };

  const handleConfirmResolution = async () => {
    if (!selectedCaseId || !resolveRationale.trim()) return;
    await EdithApi.resolveCase(selectedCaseId, resolveType, resolveRationale);

    // Optional 1-Click Whitelist auto-provisioning
    if (autoWhitelist && caseDetail && resolveType === 'FalsePositive') {
      try {
        await EdithApi.createWhitelist({
          entityIdentifier: caseDetail.primarySubjectName,
          matchScope: 'EXACT',
          reason: `Auto-whitelisted on false-positive resolution of ${caseDetail.caseNumber}: ${resolveRationale}`,
          expiresAt: new Date(Date.now() + 365 * 86400000).toISOString()
        });
      } catch {
        // Safe fallback
      }
    }

    setModalType(null);
    setResolveRationale('');
    setAutoWhitelist(false);
    const list = await EdithApi.fetchCases();
    setCases(list);
    handleSelectCase(selectedCaseId);
  };

  const handleEscalateToMlro = async () => {
    if (!selectedCaseId) return;
    await EdithApi.transitionCase(selectedCaseId, 'PendingApproval', 'Escalated by Analyst to MLRO senior review queue for statutory sign-off');
    const list = await EdithApi.fetchCases();
    setCases(list);
    handleSelectCase(selectedCaseId);
  };

  const handleConfirmEddTransition = async () => {
    if (!selectedCaseId) return;
    const checklistNotes = Object.entries(eddChecklist)
      .map(([k, v]) => `${k}: ${v ? 'CHECKED' : 'PENDING'}`)
      .join(', ');
    await EdithApi.transitionCase(selectedCaseId, 'UnderReview', `Enhanced Due Diligence (EDD) initiated. Checklist: ${checklistNotes}`);
    setModalType(null);
    const list = await EdithApi.fetchCases();
    setCases(list);
    handleSelectCase(selectedCaseId);
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedSuccess(true);
    setTimeout(() => setCopiedSuccess(false), 2000);
  };

  const formatTimer = (totalSecs: number) => {
    const mins = Math.floor(totalSecs / 60);
    const secs = totalSecs % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  // Filtered case queue
  const filteredCases = useMemo(() => {
    return cases.filter(c => {
      // Filter by search
      if (queueSearch.trim()) {
        const q = queueSearch.toLowerCase();
        const matchesCaseNo = c.caseNumber.toLowerCase().includes(q);
        const matchesSubject = c.primarySubjectName.toLowerCase().includes(q);
        if (!matchesCaseNo && !matchesSubject) return false;
      }
      // Filter by chip
      if (queueFilter === 'MY_LOCKS') {
        const lockedName = c.lockedByUserName?.toLowerCase() || '';
        const myName = currentUsername.toLowerCase();
        return c.isLocked && (lockedName === myName || lockedName.includes('fatima') || locallyLockedIds.includes(c.id));
      }
      if (queueFilter === 'MLRO_QUEUE') {
        return c.status === 'PendingApproval';
      }
      if (queueFilter === 'CRITICAL') {
        return c.priority === 'CRITICAL' || c.priority === 'HIGH';
      }
      if (queueFilter === 'RESOLVED') {
        return c.status === 'Resolved';
      }
      return true;
    });
  }, [cases, queueSearch, queueFilter, currentUsername, locallyLockedIds]);

  // Combined timeline of comments and audit logs
  const combinedTimeline = useMemo(() => {
    if (!caseDetail) return [];
    const items: Array<{
      id: string;
      timestampUtc: string;
      author: string;
      role: string;
      category: 'NOTE' | 'SYSTEM' | 'AUDIT';
      noteType?: string;
      content: string;
    }> = [];

    // Comments
    caseDetail.comments.forEach(c => {
      items.push({
        id: c.id,
        timestampUtc: c.timestampUtc,
        author: c.authorName,
        role: c.authorRole,
        category: 'NOTE',
        noteType: c.noteType || c.authorRole || 'INVESTIGATION',
        content: c.content
      });
    });

    // Audit entries
    if (showAuditLogsInTimeline && caseDetail.auditLog) {
      caseDetail.auditLog.forEach(a => {
        items.push({
          id: a.id,
          timestampUtc: a.timestampUtc,
          author: a.actorName,
          role: a.actorRole,
          category: 'AUDIT',
          noteType: 'SYSTEM',
          content: `${a.action}: ${a.notes || ''}`
        });
      });
    }

    return items.sort((a, b) => new Date(a.timestampUtc).getTime() - new Date(b.timestampUtc).getTime());
  }, [caseDetail, showAuditLogsInTimeline]);

  return (
    <div className="p-4 lg:p-6 space-y-5 max-w-[1700px] mx-auto text-ink">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-surface p-4 rounded-xl border border-border shadow-sm">
        <div>
          <div className="flex items-center gap-2.5">
            <Briefcase className="w-6 h-6 text-primary" />
            <h1 className="text-xl font-bold font-mono tracking-tight text-ink">
              Case Workspace & 4-Eyes Adjudication
            </h1>
            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-mono font-bold uppercase bg-primary/10 text-primary border border-primary/30">
              Phase 12
            </span>
          </div>
          <p className="text-xs text-muted mt-1">
            Collapsible queue, multi-duration lock renewals (15m/30m/1hr), multi-angle investigation tabs, and WORM-signed adjudication.
          </p>
        </div>

        {caseDetail && (
          <div className="flex flex-wrap items-center gap-2.5">
            {/* Live Lease Timer & Lock Control */}
            <div className="relative">
              {isLockedByMe ? (
                <div className="flex items-center gap-1.5 bg-surface-muted border border-warning/40 rounded-lg p-1">
                  <div className={`px-2.5 py-1 rounded text-xs font-mono font-semibold flex items-center gap-2 ${
                    lockRemainingSeconds < 60
                      ? 'bg-critical/20 text-critical animate-pulse border border-critical/50'
                      : lockRemainingSeconds < 180
                      ? 'bg-warning/20 text-warning border border-warning/50'
                      : 'bg-primary/20 text-primary border border-primary/40'
                  }`}>
                    <Clock className="w-3.5 h-3.5" />
                    <span>Locked ({formatTimer(lockRemainingSeconds)})</span>
                  </div>

                  {/* Renew lock dropdown button */}
                  <button
                    onClick={() => setShowDurationPicker(!showDurationPicker)}
                    title="Extend Lock Lease Duration"
                    className="px-2 py-1 rounded text-xs font-semibold bg-primary text-white hover:bg-primary-hover transition flex items-center gap-1"
                  >
                    <RefreshCw className="w-3 h-3" />
                    <span>Renew</span>
                  </button>

                  {/* Release Lock button */}
                  <button
                    onClick={toggleLock}
                    title="Release active case lock"
                    className="px-2 py-1 rounded text-xs font-semibold bg-surface-hover text-muted hover:text-ink transition flex items-center gap-1"
                  >
                    <Unlock className="w-3 h-3" />
                    <span>Release</span>
                  </button>
                </div>
              ) : isLockedByOther ? (
                <div className="px-3 py-1.5 rounded-lg text-xs font-mono font-semibold bg-critical/10 text-critical border border-critical/40 flex items-center gap-2">
                  <Lock className="w-3.5 h-3.5 text-critical" />
                  <span>Locked by {caseDetail.lockedByUserName || 'Another Officer'}</span>
                </div>
              ) : (
                <button
                  onClick={() => handleRenewLock(15)}
                  className="px-3.5 py-1.5 rounded-lg text-xs font-mono font-semibold bg-primary text-white hover:bg-primary-hover shadow-sm transition flex items-center gap-2"
                >
                  <Lock className="w-3.5 h-3.5" />
                  <span>Acquire Lock (15m)</span>
                </button>
              )}

              {/* Multi-Duration Renewal Popover */}
              {showDurationPicker && (
                <div className="absolute right-0 mt-2 w-56 bg-surface border border-border rounded-xl shadow-2xl p-2 z-50 text-xs space-y-1">
                  <div className="px-2 py-1 font-mono text-[10px] uppercase font-bold text-muted border-b border-border/60">
                    Select Lease Extension
                  </div>
                  <button
                    onClick={() => handleRenewLock(15)}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg hover:bg-primary/10 hover:text-primary transition flex justify-between items-center"
                  >
                    <span>15 Minutes</span>
                    <span className="font-mono text-[10px] text-muted">Standard</span>
                  </button>
                  <button
                    onClick={() => handleRenewLock(30)}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg hover:bg-primary/10 hover:text-primary transition flex justify-between items-center"
                  >
                    <span>30 Minutes</span>
                    <span className="font-mono text-[10px] text-muted">Complex</span>
                  </button>
                  <button
                    onClick={() => handleRenewLock(60)}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg hover:bg-primary/10 hover:text-primary transition flex justify-between items-center"
                  >
                    <span>1 Hour (60m)</span>
                    <span className="font-mono text-[10px] text-muted">Deep Audit</span>
                  </button>
                </div>
              )}
            </div>

            {/* AI STR Narrative Copilot */}
            <button
              onClick={handleGenerateAiNarrative}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-secondary/10 text-secondary border border-secondary/40 hover:bg-secondary/20 transition flex items-center gap-1.5"
            >
              <Sparkles className="w-3.5 h-3.5 text-secondary" />
              <span>AI Copilot</span>
            </button>

            {/* goAML XML Export */}
            <button
              onClick={handleExportGoAml}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-accent/10 text-accent border border-accent/40 hover:bg-accent/20 transition flex items-center gap-1.5"
            >
              <FileCode className="w-3.5 h-3.5 text-accent" />
              <span>goAML XML</span>
            </button>
          </div>
        )}
      </div>

      {/* 2. Main Investigation Layout (Left Queue + Center Tabs + Right Dossier/Actions) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        
        {/* Left Column: Collapsible Vertical Case Queue (3 cols or icon bar) */}
        <div className={`${queueCollapsed ? 'lg:col-span-1' : 'lg:col-span-3'} bg-surface rounded-xl border border-border p-3.5 space-y-3 transition-all duration-300`}>
          <div className="flex justify-between items-center pb-2 border-b border-border">
            {!queueCollapsed && (
              <span className="font-mono text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
                <Layers className="w-4 h-4 text-primary" />
                Case Queue ({filteredCases.length})
              </span>
            )}
            <button
              onClick={() => setQueueCollapsed(!queueCollapsed)}
              title={queueCollapsed ? 'Expand Queue' : 'Collapse Queue'}
              className="p-1 rounded text-muted hover:text-ink hover:bg-surface-hover transition ml-auto"
            >
              {queueCollapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
            </button>
          </div>

          {!queueCollapsed && (
            <>
              {/* Search input */}
              <div className="relative">
                <Search className="w-3.5 h-3.5 text-muted absolute left-2.5 top-2.5" />
                <input
                  type="text"
                  placeholder="Filter Case # or Subject..."
                  value={queueSearch}
                  onChange={(e) => setQueueSearch(e.target.value)}
                  className="w-full bg-canvas-subtle border border-border rounded-lg pl-8 pr-3 py-1.5 text-xs text-ink focus:border-primary focus:outline-none font-mono"
                />
              </div>

              {/* Status Filter Chips */}
              <div className="flex flex-wrap gap-1 text-[10px] font-mono">
                {(['ALL', 'MY_LOCKS', 'MLRO_QUEUE', 'CRITICAL', 'RESOLVED'] as QueueFilter[]).map(chip => (
                  <button
                    key={chip}
                    onClick={() => setQueueFilter(chip)}
                    className={`px-2 py-0.5 rounded font-semibold transition ${
                      queueFilter === chip
                        ? 'bg-primary text-white'
                        : 'bg-surface-muted text-muted hover:text-ink hover:bg-surface-hover'
                    }`}
                  >
                    {chip.replace('_', ' ')}
                  </button>
                ))}
              </div>

              {/* Scrollable vertical queue */}
              <div className="space-y-2 max-h-[750px] overflow-y-auto pr-1">
                {filteredCases.map(c => {
                  const isSelected = c.id === selectedCaseId;
                  return (
                    <div
                      key={c.id}
                      onClick={() => handleSelectCase(c.id)}
                      className={`p-3 rounded-lg border cursor-pointer transition-all ${
                        isSelected
                          ? 'bg-primary/10 border-primary shadow-sm'
                          : 'bg-surface-muted/60 border-border/80 hover:border-border'
                      }`}
                    >
                      <div className="flex justify-between items-start gap-1">
                        <span className="font-mono text-xs font-bold text-ink truncate">{c.caseNumber}</span>
                        <span className={`text-[9px] font-mono px-1.5 py-0.5 rounded font-bold uppercase ${
                          c.priority === 'CRITICAL'
                            ? 'bg-critical/20 text-critical border border-critical/40'
                            : c.priority === 'HIGH'
                            ? 'bg-warning/20 text-warning border border-warning/40'
                            : 'bg-accent/20 text-accent border border-accent/40'
                        }`}>
                          {c.priority}
                        </span>
                      </div>

                      <div className="text-xs font-semibold text-ink mt-1 truncate">
                        {c.primarySubjectName}
                      </div>

                      <div className="flex justify-between items-center text-[10px] text-muted font-mono mt-2 pt-1.5 border-t border-border/60">
                        <span className="text-critical font-bold">{c.riskScore.toFixed(1)}%</span>
                        <div className="flex items-center gap-1.5">
                          {c.isLocked && <Lock className="w-3 h-3 text-warning" />}
                          <span className="uppercase text-[9px] font-semibold">{c.status}</span>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        {/* Center Column: Tabbed Multi-Angle Investigation Console (5 cols or 6 cols if left collapsed) */}
        <div className={`${queueCollapsed ? 'lg:col-span-7' : 'lg:col-span-5'} bg-surface rounded-xl border border-border p-4 space-y-4`}>
          {/* Tab Navigation */}
          <div className="flex flex-wrap items-center justify-between border-b border-border pb-2 gap-2">
            <div className="flex items-center gap-1 font-mono text-xs">
              <button
                onClick={() => setActiveTab('DIFF')}
                className={`px-3 py-1.5 rounded-lg font-semibold flex items-center gap-1.5 transition ${
                  activeTab === 'DIFF' ? 'bg-primary text-white' : 'text-muted hover:text-ink hover:bg-surface-hover'
                }`}
              >
                <Sliders className="w-3.5 h-3.5" />
                <span>1. Token Diff</span>
              </button>
              <button
                onClick={() => setActiveTab('GRAPH')}
                className={`px-3 py-1.5 rounded-lg font-semibold flex items-center gap-1.5 transition ${
                  activeTab === 'GRAPH' ? 'bg-primary text-white' : 'text-muted hover:text-ink hover:bg-surface-hover'
                }`}
              >
                <Network className="w-3.5 h-3.5" />
                <span>2. UBO Graph</span>
              </button>
              <button
                onClick={() => setActiveTab('CORRIDOR')}
                className={`px-3 py-1.5 rounded-lg font-semibold flex items-center gap-1.5 transition ${
                  activeTab === 'CORRIDOR' ? 'bg-primary text-white' : 'text-muted hover:text-ink hover:bg-surface-hover'
                }`}
              >
                <MapPin className="w-3.5 h-3.5" />
                <span>3. Rules & Corridor</span>
              </button>
              <button
                onClick={() => setActiveTab('KYC360')}
                className={`px-3 py-1.5 rounded-lg font-semibold flex items-center gap-1.5 transition ${
                  activeTab === 'KYC360' ? 'bg-primary text-white' : 'text-muted hover:text-ink hover:bg-surface-hover'
                }`}
              >
                <UserCheck className="w-3.5 h-3.5" />
                <span>4. KYC 360</span>
              </button>
            </div>

            {caseDetail && (
              <span className="text-xs font-mono text-critical font-bold">
                {caseDetail.riskScore.toFixed(1)}% Match
              </span>
            )}
          </div>

          {loading ? (
            <div className="p-16 text-center text-muted font-mono text-xs flex items-center justify-center gap-2">
              <Clock className="w-4 h-4 animate-spin text-primary" />
              Loading case telemetry...
            </div>
          ) : caseDetail ? (
            <div>
              {/* TAB 1: DIAGNOSTIC TOKEN DIFF */}
              {activeTab === 'DIFF' && (
                <div className="space-y-4 text-xs">
                  {caseDetail.matchedCandidates.map((cand) => (
                    <div key={cand.candidateId} className="space-y-3">
                      <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1">
                        <div className="text-[10px] font-mono text-muted flex justify-between">
                          <span>Designated Entity Reference:</span>
                          <span className="text-critical font-semibold uppercase">{cand.category}</span>
                        </div>
                        <div className="text-base font-bold text-ink">{cand.canonicalName}</div>
                        <div className="text-[11px] text-muted font-mono">
                          Watchlist UID: {cand.candidateId} • List: {cand.sanctionsList || 'OFAC / UN Sanctions'}
                        </div>
                      </div>

                      {/* Token Breakdown Bar */}
                      <div className="p-3 rounded-lg bg-canvas-subtle border border-border space-y-2">
                        <div className="text-[10px] font-mono uppercase tracking-wider text-muted">
                          Phonetic & Exact Token Alignment
                        </div>
                        {cand.tokenBreakdown.map((t, idx) => (
                          <div key={idx} className="flex items-center justify-between p-2 rounded bg-surface-muted border border-border text-xs font-mono">
                            <div className="flex items-center gap-1.5 truncate">
                              <span className="text-muted">Query:</span>
                              <span className="text-primary-light font-bold">"{t.queryToken}"</span>
                            </div>
                            <div className="flex items-center gap-1.5 truncate">
                              <span className="text-muted">Target:</span>
                              <span className="text-critical font-bold">"{t.candidateToken}"</span>
                            </div>
                            <span className="px-2 py-0.5 rounded bg-critical/20 text-critical text-[10px] font-bold">
                              {t.tokenScore}% ({t.matchType})
                            </span>
                          </div>
                        ))}
                      </div>

                      {/* Designation Details */}
                      <div className="p-3 rounded-lg bg-surface-muted/60 border border-border/60 space-y-1">
                        <div className="font-semibold text-muted font-mono text-[10px] uppercase">Designation Findings</div>
                        <p className="text-ink italic text-xs leading-relaxed">{cand.designationDetails}</p>
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {/* TAB 2: UBO & NETWORK RELATIONSHIP GRAPH */}
              {activeTab === 'GRAPH' && (
                <div className="space-y-4">
                  <div className="flex justify-between items-center text-xs text-muted">
                    <span>Multi-Hop Entity & UBO Ownership Visualizer</span>
                    <span className="font-mono text-[10px] bg-primary/10 text-primary px-2 py-0.5 rounded">Interactive Nodes</span>
                  </div>

                  {/* Interactive SVG Diagram */}
                  <div className="bg-canvas-subtle border border-border rounded-xl p-4 flex items-center justify-center relative overflow-hidden">
                    <svg viewBox="0 0 500 320" className="w-full h-72">
                      <defs>
                        <linearGradient id="edgeGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                          <stop offset="0%" stopColor="#00E5FF" stopOpacity="0.8" />
                          <stop offset="100%" stopColor="#FF1744" stopOpacity="0.8" />
                        </linearGradient>
                      </defs>

                      {/* Connection Lines */}
                      <line x1="100" y1="160" x2="250" y2="160" stroke="#475569" strokeWidth="2" strokeDasharray="4" />
                      <line x1="250" y1="160" x2="400" y2="160" stroke="#00E5FF" strokeWidth="2.5" />
                      <line x1="250" y1="160" x2="250" y2="60" stroke="#F59E0B" strokeWidth="2" />
                      <line x1="250" y1="160" x2="250" y2="260" stroke="#9333EA" strokeWidth="2" strokeDasharray="3" />

                      {/* Line labels */}
                      <text x="175" y="150" fill="#94A3B8" fontSize="10" textAnchor="middle" fontFamily="monospace">$1,450,000 Wire</text>
                      <text x="325" y="150" fill="#00E5FF" fontSize="10" textAnchor="middle" fontFamily="monospace">Direct Beneficiary</text>
                      <text x="260" y="110" fill="#F59E0B" fontSize="10" fontFamily="monospace">78% UBO Stake</text>
                      <text x="260" y="220" fill="#C084FC" fontSize="10" fontFamily="monospace">Nominee Director</text>

                      {/* Node: Originator (Left) */}
                      <g
                        className="cursor-pointer group"
                        onClick={() => setSelectedGraphNode({
                          id: 'node-1',
                          label: 'Rashid International FZE',
                          role: 'Originating Remitter',
                          jurisdiction: 'United Arab Emirates (AE)',
                          riskScore: 72.0,
                          details: 'Free zone commercial trader, multi-currency corporate account.'
                        })}
                      >
                        <circle cx="100" cy="160" r="28" fill="#1E293B" stroke="#00E5FF" strokeWidth="2" />
                        <text x="100" y="164" fill="#E2E8F0" fontSize="9" textAnchor="middle" fontFamily="monospace" fontWeight="bold">Rashid FZE</text>
                      </g>

                      {/* Node: Primary Match (Center Target) */}
                      <g
                        className="cursor-pointer"
                        onClick={() => setSelectedGraphNode({
                          id: 'node-2',
                          label: caseDetail.primarySubjectName,
                          role: 'Primary Hit / Target Beneficiary',
                          jurisdiction: 'Russian Federation (RU)',
                          riskScore: 94.5,
                          details: 'Specially Designated National (SDN) match under OFAC / UN sanctions.'
                        })}
                      >
                        <circle cx="250" cy="160" r="36" fill="#450A0A" stroke="#EF4444" strokeWidth="3" />
                        <circle cx="250" cy="160" r="42" fill="none" stroke="#EF4444" strokeWidth="1.5" strokeDasharray="4 4" opacity="0.85">
                          <animateTransform
                            attributeName="transform"
                            type="rotate"
                            from="0 250 160"
                            to="360 250 160"
                            dur="10s"
                            repeatCount="indefinite"
                          />
                        </circle>
                        <text x="250" y="158" fill="#FCA5A5" fontSize="10" textAnchor="middle" fontFamily="monospace" fontWeight="bold">TARGET</text>
                        <text x="250" y="172" fill="#FFFFFF" fontSize="8" textAnchor="middle" fontFamily="monospace">Viktor Bout</text>
                      </g>

                      {/* Node: Intermediary Bank (Right) */}
                      <g
                        className="cursor-pointer"
                        onClick={() => setSelectedGraphNode({
                          id: 'node-3',
                          label: 'Helvetic Offshore Clearing AG',
                          role: 'Correspondent Intermediary',
                          jurisdiction: 'Switzerland (CH)',
                          riskScore: 35.0,
                          details: 'Correspondent clearing routing transfer to Zurich terminal.'
                        })}
                      >
                        <circle cx="400" cy="160" r="28" fill="#1E293B" stroke="#3B82F6" strokeWidth="2" />
                        <text x="400" y="164" fill="#93C5FD" fontSize="8" textAnchor="middle" fontFamily="monospace" fontWeight="bold">Helvetic AG</text>
                      </g>

                      {/* Node: UBO Holding Entity (Top) */}
                      <g
                        className="cursor-pointer"
                        onClick={() => setSelectedGraphNode({
                          id: 'node-4',
                          label: 'Vostok Aviation Holdings Corp',
                          role: 'Ultimate Beneficial Owner (78%)',
                          jurisdiction: 'Marshall Islands (MH)',
                          riskScore: 88.0,
                          details: 'Offshore shell corporation controlling 78% beneficial interest.'
                        })}
                      >
                        <circle cx="250" cy="60" r="26" fill="#1E293B" stroke="#F59E0B" strokeWidth="2" />
                        <text x="250" y="64" fill="#FDE68A" fontSize="8" textAnchor="middle" fontFamily="monospace" fontWeight="bold">Vostok Corp</text>
                      </g>

                      {/* Node: Nominee Director (Bottom) */}
                      <g
                        className="cursor-pointer"
                        onClick={() => setSelectedGraphNode({
                          id: 'node-5',
                          label: 'Sergey V. Ivanov',
                          role: 'Corporate Officer / Nominee Director',
                          jurisdiction: 'Cyprus (CY)',
                          riskScore: 65.0,
                          details: 'Known associate with shared registered power of attorney.'
                        })}
                      >
                        <circle cx="250" cy="260" r="26" fill="#1E293B" stroke="#A855F7" strokeWidth="2" />
                        <text x="250" y="264" fill="#E9D5FF" fontSize="8" textAnchor="middle" fontFamily="monospace" fontWeight="bold">S. Ivanov</text>
                      </g>
                    </svg>
                  </div>

                  {/* Inspector Details Card */}
                  {selectedGraphNode ? (
                    <div className="p-3 bg-surface-muted border border-primary/40 rounded-xl space-y-1 text-xs">
                      <div className="flex justify-between items-center">
                        <span className="font-bold text-ink">{selectedGraphNode.label}</span>
                        <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-critical/20 text-critical font-bold">
                          Risk: {selectedGraphNode.riskScore}%
                        </span>
                      </div>
                      <div className="text-[11px] font-mono text-muted">{selectedGraphNode.role} • {selectedGraphNode.jurisdiction}</div>
                      <p className="text-ink text-[11px] pt-1 border-t border-border/60">{selectedGraphNode.details}</p>
                    </div>
                  ) : (
                    <div className="text-center text-[11px] text-muted font-mono">
                      Click any node above to inspect corporate ownership & KYC telemetry.
                    </div>
                  )}
                </div>
              )}

              {/* TAB 3: TRANSACTION CORRIDOR & TYPOLOGY RULES */}
              {activeTab === 'CORRIDOR' && (
                <div className="space-y-4 text-xs">
                  {/* Geographic Corridor Visual */}
                  <div className="p-3.5 rounded-xl bg-surface-muted border border-border space-y-2">
                    <span className="text-[10px] font-mono uppercase text-muted font-bold">Geographic Remittance Corridor</span>
                    <div className="flex items-center justify-between p-3 rounded-lg bg-canvas-subtle border border-border font-mono">
                      <div className="text-center">
                        <span className="text-lg font-bold text-ink">AE</span>
                        <div className="text-[10px] text-muted">United Arab Emirates</div>
                      </div>
                      <div className="flex flex-col items-center flex-1 px-4">
                        <span className="text-[10px] text-warning font-semibold">Cross-Border Wire Flow</span>
                        <div className="w-full h-0.5 bg-warning/50 relative my-1">
                          <div className="w-2 h-2 rounded-full bg-warning absolute right-0 -top-[3px]" />
                        </div>
                        <span className="text-[10px] text-muted">SWIFT MT103 Intercept</span>
                      </div>
                      <div className="text-center">
                        <span className="text-lg font-bold text-ink">CH</span>
                        <div className="text-[10px] text-muted">Switzerland (Zurich)</div>
                      </div>
                    </div>
                  </div>

                  {/* Tripped Typology Rules */}
                  <div className="space-y-2">
                    <span className="text-[10px] font-mono uppercase text-muted font-bold">Tripped AML Typology Rules</span>
                    
                    <div className="p-3 rounded-lg bg-surface-muted border border-warning/40 space-y-1">
                      <div className="flex justify-between font-mono text-[11px]">
                        <span className="font-bold text-warning">RULE-CORR-01: High-Risk Jurisdictional Flow</span>
                        <span className="text-warning font-semibold">+25% Weight</span>
                      </div>
                      <p className="text-ink text-[11px]">Direct transfer from GCC commercial account into non-resident offshore destination without prior trade contract on record.</p>
                    </div>

                    <div className="p-3 rounded-lg bg-surface-muted border border-critical/40 space-y-1">
                      <div className="flex justify-between font-mono text-[11px]">
                        <span className="font-bold text-critical">RULE-THRESH-04: Large Cash Threshold Exceeded</span>
                        <span className="text-critical font-semibold">+35% Weight</span>
                      </div>
                      <p className="text-ink text-[11px]">Wire remittance of 1,450,000 USD exceeds the 50,000 AED central bank regulatory pre-clearance reporting ceiling.</p>
                    </div>

                    <div className="p-3 rounded-lg bg-surface-muted border border-critical/40 space-y-1">
                      <div className="flex justify-between font-mono text-[11px]">
                        <span className="font-bold text-critical">RULE-SANCT-09: Designated Sanctions List Alignment</span>
                        <span className="text-critical font-semibold">+40% Weight</span>
                      </div>
                      <p className="text-ink text-[11px]">Target beneficiary matched high-confidence OFAC SDN and UAE Local Terrorist register listing with score &gt; 90%.</p>
                    </div>
                  </div>
                </div>
              )}

              {/* TAB 4: KYC 360 CUSTOMER PROFILE BRIDGE */}
              {activeTab === 'KYC360' && (
                <div className="space-y-4 text-xs">
                  {/* Customer Header */}
                  <div className="p-3.5 rounded-xl bg-surface-muted border border-border space-y-2">
                    <div className="flex justify-between items-start">
                      <div>
                        <span className="text-[10px] font-mono text-muted uppercase">Linked Customer Record</span>
                        <div className="text-sm font-bold text-ink">{caseDetail.primarySubjectName}</div>
                      </div>
                      <span className="px-2 py-0.5 rounded font-mono text-[10px] font-bold bg-critical/20 text-critical border border-critical/40">
                        EDD Active
                      </span>
                    </div>

                    <div className="grid grid-cols-2 gap-2 pt-2 border-t border-border/60 text-[11px] font-mono">
                      <div><span className="text-muted">Customer Code:</span> <strong className="text-ink">CUST-AE-88912</strong></div>
                      <div><span className="text-muted">Entity Type:</span> <strong className="text-ink">INDIVIDUAL</strong></div>
                      <div><span className="text-muted">Nationality:</span> <strong className="text-ink">Russian Federation</strong></div>
                      <div><span className="text-muted">Risk Tier:</span> <strong className="text-critical">CRITICAL (Tier 1)</strong></div>
                    </div>
                  </div>

                  {/* Document Vault */}
                  <div className="space-y-2">
                    <span className="text-[10px] font-mono uppercase text-muted font-bold flex items-center gap-1.5">
                      <FileText className="w-3.5 h-3.5 text-primary" />
                      Customer Document Vault (OCR Inspected)
                    </span>

                    <div className="space-y-1.5">
                      <div className="p-2.5 rounded bg-surface-muted border border-border flex justify-between items-center text-[11px]">
                        <div>
                          <div className="font-semibold text-ink">Passport_Scan_Bout_V.pdf</div>
                          <div className="text-[10px] text-muted font-mono">Russian Fed • Exp: May 2028 • Flagged</div>
                        </div>
                        <span className="px-2 py-0.5 rounded bg-critical/20 text-critical text-[10px] font-bold font-mono">Sanctioned</span>
                      </div>

                      <div className="p-2.5 rounded bg-surface-muted border border-border flex justify-between items-center text-[11px]">
                        <div>
                          <div className="font-semibold text-ink">Trade_License_Dubai_2026.pdf</div>
                          <div className="text-[10px] text-muted font-mono">TL-DXB-889120 • Commercial Trader</div>
                        </div>
                        <span className="px-2 py-0.5 rounded bg-accent/20 text-accent text-[10px] font-bold font-mono">Verified</span>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          ) : null}
        </div>

        {/* Right Column: Intercept Dossier, 4-Eyes Adjudication, Categorized Notes (4 cols) */}
        <div className="lg:col-span-4 space-y-4">
          
          {/* Intercept Dossier Summary Card */}
          {caseDetail && (
            <div className="bg-surface rounded-xl border border-border p-4 space-y-3">
              <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink pb-2 border-b border-border flex items-center justify-between">
                <span className="flex items-center gap-2">
                  <Briefcase className="w-4 h-4 text-primary" />
                  Intercept Dossier
                </span>
                <span className="text-[11px] font-mono font-bold text-ink">
                  {caseDetail.transactionDetails.amount.toLocaleString()} {caseDetail.transactionDetails.currency}
                </span>
              </h2>

              <div className="space-y-2 text-xs">
                <div className="p-2.5 rounded bg-surface-muted border border-border space-y-1 text-[11px]">
                  <div className="flex justify-between">
                    <span className="text-muted">Transaction Ref:</span>
                    <span className="font-mono text-ink">{caseDetail.transactionDetails.transactionId}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted">Originator:</span>
                    <span className="font-semibold text-ink">Rashid International FZE</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted">Target Beneficiary:</span>
                    <span className="font-semibold text-critical truncate max-w-[180px]">{caseDetail.primarySubjectName}</span>
                  </div>
                  <div className="flex justify-between pt-1 border-t border-border/60">
                    <span className="text-muted">SLA Due Date:</span>
                    <span className="font-mono text-warning font-semibold">68 Hours Remaining</span>
                  </div>
                </div>
              </div>

              {/* 4-Eyes Adjudication Actions */}
              <div className="pt-2 border-t border-border space-y-2">
                <div className="text-[10px] font-mono uppercase tracking-wider text-muted font-semibold flex justify-between items-center">
                  <span>Adjudication Governance</span>
                  {currentRole === 'MLRO' || currentRole === 'SUPER_ADMIN' ? (
                    <span className="text-accent text-[10px] font-bold">MLRO Authorized</span>
                  ) : (
                    <span className="text-warning text-[10px] font-bold">Analyst (Escalate / EDD)</span>
                  )}
                </div>

                {isLockedByOther ? (
                  <div className="p-2.5 rounded bg-critical/10 border border-critical/30 text-critical text-xs font-mono text-center">
                    Case is locked by {caseDetail.lockedByUserName}. Action buttons disabled.
                  </div>
                ) : currentRole === 'MLRO' || currentRole === 'SUPER_ADMIN' ? (
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      onClick={() => {
                        setResolveType('TruePositiveReported');
                        setModalType('RESOLVE');
                      }}
                      className="py-2 px-2.5 rounded bg-critical/20 text-critical border border-critical/40 hover:bg-critical/30 transition text-xs font-semibold flex items-center justify-center gap-1.5"
                    >
                      <AlertTriangle className="w-3.5 h-3.5" />
                      <span>Confirm Hit (STR)</span>
                    </button>
                    <button
                      onClick={() => {
                        setResolveType('FalsePositive');
                        setModalType('RESOLVE');
                      }}
                      className="py-2 px-2.5 rounded bg-accent/20 text-accent border border-accent/40 hover:bg-accent/30 transition text-xs font-semibold flex items-center justify-center gap-1.5"
                    >
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>Dismiss (False +)</span>
                    </button>
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      onClick={() => setModalType('EDD_CHECKLIST')}
                      className="py-2 px-2 rounded bg-surface-muted text-ink border border-border hover:bg-surface-hover transition text-xs font-semibold flex items-center justify-center gap-1"
                    >
                      <CheckSquare className="w-3.5 h-3.5 text-primary" />
                      <span>Initiate EDD</span>
                    </button>
                    <button
                      onClick={handleEscalateToMlro}
                      className="py-2 px-2 rounded bg-warning/20 text-warning border border-warning/40 hover:bg-warning/30 transition text-xs font-semibold flex items-center justify-center gap-1"
                    >
                      <Send className="w-3.5 h-3.5" />
                      <span>Escalate MLRO</span>
                    </button>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Categorized Notes & Chronological Activity Stream */}
          {caseDetail && (
            <div className="bg-surface rounded-xl border border-border p-4 space-y-3">
              <div className="flex justify-between items-center pb-2 border-b border-border">
                <span className="font-mono text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
                  <MessageSquare className="w-4 h-4 text-primary" />
                  Chronological Activity ({combinedTimeline.length})
                </span>
                <button
                  onClick={() => setShowAuditLogsInTimeline(!showAuditLogsInTimeline)}
                  className="text-[10px] font-mono text-muted hover:text-ink transition flex items-center gap-1"
                >
                  <History className="w-3 h-3" />
                  <span>{showAuditLogsInTimeline ? 'Hide Audit' : 'Show Audit'}</span>
                </button>
              </div>

              {/* Activity Timeline Stream */}
              <div className="max-h-64 overflow-y-auto space-y-2 pr-1">
                {combinedTimeline.map((item) => (
                  <div
                    key={item.id}
                    className={`p-2.5 rounded border text-xs space-y-1 ${
                      item.noteType === 'SUPERVISOR'
                        ? 'bg-secondary/10 border-secondary/40'
                        : item.noteType === 'RATIONALE'
                        ? 'bg-warning/10 border-warning/40'
                        : item.category === 'AUDIT'
                        ? 'bg-canvas-subtle border-border/60 text-muted'
                        : 'bg-surface-muted border-border'
                    }`}
                  >
                    <div className="flex justify-between items-center text-[10px] font-mono">
                      <div className="flex items-center gap-1.5 truncate">
                        <span className="font-bold text-ink">{item.author}</span>
                        <span className={`px-1.5 py-0.2 rounded text-[9px] font-semibold ${
                          item.noteType === 'SUPERVISOR'
                            ? 'bg-secondary/20 text-secondary'
                            : item.noteType === 'RATIONALE'
                            ? 'bg-warning/20 text-warning'
                            : item.category === 'AUDIT'
                            ? 'bg-surface-hover text-muted'
                            : 'bg-primary/20 text-primary'
                        }`}>
                          {item.noteType || item.role}
                        </span>
                      </div>
                      <span className="text-muted">{new Date(item.timestampUtc).toLocaleTimeString()}</span>
                    </div>
                    <p className="text-ink text-[11px] leading-relaxed">{item.content}</p>
                  </div>
                ))}
              </div>

              {/* Categorized Note Submission Form */}
              <form onSubmit={handleAddComment} className="pt-2 border-t border-border space-y-2">
                <div className="flex items-center gap-1.5 text-[10px] font-mono">
                  <span className="text-muted">Type:</span>
                  {(['INVESTIGATION', 'SUPERVISOR', 'RATIONALE'] as NoteCategory[]).map(cat => (
                    <button
                      key={cat}
                      type="button"
                      onClick={() => setSelectedNoteCategory(cat)}
                      className={`px-2 py-0.5 rounded font-semibold transition ${
                        selectedNoteCategory === cat
                          ? cat === 'SUPERVISOR' ? 'bg-secondary text-white' : cat === 'RATIONALE' ? 'bg-warning text-black font-bold' : 'bg-primary text-white'
                          : 'bg-surface-muted text-muted hover:text-ink'
                      }`}
                    >
                      {cat === 'INVESTIGATION' ? 'Investigation' : cat === 'SUPERVISOR' ? 'Supervisor' : 'Rationale'}
                    </button>
                  ))}
                </div>

                <div className="flex gap-2">
                  <input
                    type="text"
                    disabled={isLockedByOther}
                    placeholder={isLockedByOther ? "Case locked by another user" : `Add ${selectedNoteCategory.toLowerCase()} note...`}
                    value={newComment}
                    onChange={(e) => setNewComment(e.target.value)}
                    className="flex-1 bg-canvas-subtle border border-border rounded px-3 py-1.5 text-xs text-ink focus:border-primary focus:outline-none disabled:opacity-50"
                  />
                  <button
                    type="submit"
                    disabled={isLockedByOther || !newComment.trim()}
                    className="px-3 py-1.5 rounded bg-primary text-white hover:bg-primary-hover transition text-xs font-semibold flex items-center gap-1 disabled:opacity-50"
                  >
                    <Send className="w-3.5 h-3.5" />
                  </button>
                </div>
              </form>
            </div>
          )}
        </div>
      </div>

      {/* 3. Modals */}

      {/* Modal: AI STR Narrative Copilot (Editable) */}
      {modalType === 'AI_NARRATIVE' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/50 backdrop-blur-sm p-4">
          <div className="bg-surface border border-border rounded-xl max-w-3xl w-full p-6 space-y-4 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="font-mono font-bold text-base text-ink flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-secondary" />
                FATF 5-Paragraph STR Narrative Copilot
              </h3>
              <button onClick={() => setModalType(null)} className="text-muted hover:text-ink">
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-muted">
              You can edit the synthesized narrative below before copying or applying directly to statutory case rationale.
            </p>

            <textarea
              rows={12}
              value={editableNarrative}
              onChange={(e) => setEditableNarrative(e.target.value)}
              className="w-full bg-canvas-subtle border border-border rounded-lg p-3.5 text-xs font-mono text-ink focus:border-primary focus:outline-none leading-relaxed"
            />

            <div className="flex flex-wrap justify-between items-center gap-3 pt-2 border-t border-border">
              <button
                onClick={() => copyToClipboard(editableNarrative)}
                className="px-3 py-2 rounded bg-surface-muted border border-border hover:bg-surface-hover text-xs font-semibold flex items-center gap-1.5 text-ink transition"
              >
                {copiedSuccess ? <Check className="w-4 h-4 text-accent" /> : <Copy className="w-4 h-4 text-muted" />}
                <span>{copiedSuccess ? 'Copied to Clipboard' : 'Copy Narrative'}</span>
              </button>

              <div className="flex gap-2">
                <button
                  onClick={() => setModalType(null)}
                  className="px-4 py-2 rounded bg-surface-hover text-ink text-xs font-semibold"
                >
                  Cancel
                </button>
                <button
                  onClick={handleApplyNarrativeToRationale}
                  className="px-4 py-2 rounded bg-primary text-white text-xs font-bold font-mono hover:bg-primary-hover flex items-center gap-1.5"
                >
                  <CheckCircle2 className="w-4 h-4" />
                  <span>Apply as Statutory Rationale</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal: goAML XML Export (1-Click Download) */}
      {modalType === 'GOAML' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/50 backdrop-blur-sm p-4">
          <div className="bg-surface border border-border rounded-xl max-w-3xl w-full p-6 space-y-4 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="font-mono font-bold text-base text-ink flex items-center gap-2">
                <FileCode className="w-5 h-5 text-accent" />
                UAE Financial Intelligence Unit (goAML XML Export)
              </h3>
              <button onClick={() => setModalType(null)} className="text-muted hover:text-ink">
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            <pre className="p-4 rounded-lg bg-canvas-subtle border border-border text-xs font-mono text-ink overflow-x-auto max-h-96 whitespace-pre-wrap">
              {modalContent}
            </pre>

            <div className="flex justify-between items-center pt-2 border-t border-border">
              <button
                onClick={() => copyToClipboard(modalContent)}
                className="px-3 py-2 rounded bg-surface-muted border border-border hover:bg-surface-hover text-xs font-semibold flex items-center gap-1.5 text-ink"
              >
                {copiedSuccess ? <Check className="w-4 h-4 text-accent" /> : <Copy className="w-4 h-4 text-muted" />}
                <span>{copiedSuccess ? 'Copied XML' : 'Copy XML'}</span>
              </button>

              <div className="flex gap-2">
                <button
                  onClick={() => setModalType(null)}
                  className="px-4 py-2 rounded bg-surface-hover text-ink text-xs font-semibold"
                >
                  Close
                </button>
                <button
                  onClick={handleDownloadGoAmlXml}
                  className="px-4 py-2 rounded bg-accent text-white text-xs font-bold font-mono hover:bg-accent/80 flex items-center gap-1.5 shadow"
                >
                  <Download className="w-4 h-4" />
                  <span>Download .xml File</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Case Resolution (with Whitelist on Dismissal) */}
      {modalType === 'RESOLVE' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/50 backdrop-blur-sm p-4">
          <div className="bg-surface border border-border rounded-xl max-w-xl w-full p-6 space-y-4 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="font-mono font-bold text-base text-ink flex items-center gap-2">
                <AlertTriangle className={`w-5 h-5 ${resolveType === 'FalsePositive' ? 'text-accent' : 'text-critical'}`} />
                <span>Statutory Adjudication: {resolveType === 'FalsePositive' ? 'Dismiss False Positive' : 'Confirm Hit (STR Freeze)'}</span>
              </h3>
              <button onClick={() => setModalType(null)} className="text-muted hover:text-ink">
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-4 text-xs">
              <div>
                <label className="text-muted block mb-1 font-semibold">Statutory Case Rationale</label>
                <textarea
                  rows={4}
                  required
                  placeholder="Provide detailed statutory rationale for this adjudication decision..."
                  value={resolveRationale}
                  onChange={(e) => setResolveRationale(e.target.value)}
                  className="w-full bg-canvas-subtle border border-border rounded-lg p-3 text-ink font-mono focus:border-primary focus:outline-none"
                />
              </div>

              {/* Direct Whitelist-on-Dismissal Toggle for False Positives */}
              {resolveType === 'FalsePositive' && (
                <div className="p-3 bg-surface-muted border border-border rounded-lg space-y-1.5">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={autoWhitelist}
                      onChange={(e) => setAutoWhitelist(e.target.checked)}
                      className="w-4 h-4 text-primary rounded border-border focus:ring-primary"
                    />
                    <span className="font-bold text-ink text-xs">Auto-provision Whitelist Exemption</span>
                  </label>
                  <p className="text-[11px] text-muted pl-6">
                    Exempts "{caseDetail?.primarySubjectName}" from recurring alerts for 12 months under CBUAE Rule 4.2.
                  </p>
                </div>
              )}

              <div className="flex justify-end gap-3 pt-2 border-t border-border">
                <button
                  onClick={() => setModalType(null)}
                  className="px-4 py-2 rounded bg-surface-hover text-ink text-xs font-semibold"
                >
                  Cancel
                </button>
                <button
                  onClick={handleConfirmResolution}
                  disabled={!resolveRationale.trim()}
                  className="px-4 py-2 rounded bg-primary text-white font-bold hover:bg-primary-hover font-mono text-xs disabled:opacity-50"
                >
                  Submit Statutory Adjudication
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Enhanced Due Diligence (EDD) Checklist */}
      {modalType === 'EDD_CHECKLIST' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/50 backdrop-blur-sm p-4">
          <div className="bg-surface border border-border rounded-xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
            <div className="flex justify-between items-center pb-3 border-b border-border">
              <h3 className="font-mono font-bold text-base text-ink flex items-center gap-2">
                <CheckSquare className="w-5 h-5 text-primary" />
                Initiate Enhanced Due Diligence (EDD)
              </h3>
              <button onClick={() => setModalType(null)} className="text-muted hover:text-ink">
                <XCircle className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-muted">
              Mark verified items before transitioning this case into active investigation:
            </p>

            <div className="space-y-2.5 text-xs bg-canvas-subtle p-3.5 rounded-lg border border-border">
              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={eddChecklist.sourceOfFunds}
                  onChange={(e) => setEddChecklist(prev => ({ ...prev, sourceOfFunds: e.target.checked }))}
                  className="w-4 h-4 text-primary rounded"
                />
                <span className="font-semibold text-ink">Source of Funds & Wealth Verified</span>
              </label>

              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={eddChecklist.commercialInvoices}
                  onChange={(e) => setEddChecklist(prev => ({ ...prev, commercialInvoices: e.target.checked }))}
                  className="w-4 h-4 text-primary rounded"
                />
                <span className="font-semibold text-ink">Commercial Contracts & Invoices Inspected</span>
              </label>

              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={eddChecklist.uboAttestation}
                  onChange={(e) => setEddChecklist(prev => ({ ...prev, uboAttestation: e.target.checked }))}
                  className="w-4 h-4 text-primary rounded"
                />
                <span className="font-semibold text-ink">Ultimate Beneficial Ownership (UBO) Attested (&gt;25%)</span>
              </label>

              <label className="flex items-center gap-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={eddChecklist.adverseMediaRecheck}
                  onChange={(e) => setEddChecklist(prev => ({ ...prev, adverseMediaRecheck: e.target.checked }))}
                  className="w-4 h-4 text-primary rounded"
                />
                <span className="font-semibold text-ink">Adverse Media Negative News Screened</span>
              </label>
            </div>

            <div className="flex justify-end gap-3 pt-2 border-t border-border">
              <button
                onClick={() => setModalType(null)}
                className="px-4 py-2 rounded bg-surface-hover text-ink text-xs font-semibold"
              >
                Cancel
              </button>
              <button
                onClick={handleConfirmEddTransition}
                className="px-4 py-2 rounded bg-primary text-white font-bold font-mono text-xs hover:bg-primary-hover"
              >
                Confirm & Transition to UnderReview
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};
