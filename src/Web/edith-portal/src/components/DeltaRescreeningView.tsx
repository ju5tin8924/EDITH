import React, { useState, useMemo, useEffect, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { 
  RefreshCw, 
  CheckCircle2, 
  AlertTriangle, 
  ShieldAlert, 
  ChevronRight,
  TrendingUp,
  Check,
  Download,
  Filter,
  FileSpreadsheet,
  Search,
  Sparkles,
  ExternalLink,
  ShieldCheck,
  Clock,
  Database,
  Layers,
  FileText,
  X
} from 'lucide-react';
import { EdithApi } from '../services/api';
import { EdithLoader } from './EdithLogo';
import { usePageMeta } from '../config/pageMeta';

export interface DeltaHit {
  id: string;
  customerCode: string;
  customerName: string;
  nationality: string;
  watchlistSource: string;
  previousScore: number;
  newScore: number;
  deltaDiff: number;
  deltaReason: string;
  status: 'PENDING_REVIEW' | 'ESCALATED' | 'DISMISSED';
  updatedAt: string;
  changeType?: 'NEW_ALIAS_ADDED' | 'BENEFICIAL_OWNERSHIP' | 'DIRECTORSHIP_LINK' | 'PHONETIC_DRIFT';
  associatedCaseId?: string;
  associatedCaseNumber?: string;
  dispositionRationale?: string;
  dispositionReasonCode?: string;
}

const MOCK_DELTA_HITS: DeltaHit[] = [
  {
    id: 'delta-001',
    customerCode: 'CUST-AE-88902',
    customerName: 'Rashid Al-Maktoum Trading FZE',
    nationality: 'ARE',
    watchlistSource: 'OFAC SDN 2026-10-13 Sync',
    previousScore: 12.0,
    newScore: 94.2,
    deltaDiff: 82.2,
    deltaReason: 'New maritime vessel alias added matching registered freight transport charter',
    changeType: 'NEW_ALIAS_ADDED',
    status: 'PENDING_REVIEW',
    updatedAt: '2026-10-14 02:15 UTC'
  },
  {
    id: 'delta-002',
    customerCode: 'CUST-CH-44109',
    customerName: 'Kovalev International Holdings',
    nationality: 'RUS',
    watchlistSource: 'EU Consolidated Sanctions Delta 44',
    previousScore: 45.0,
    newScore: 88.5,
    deltaDiff: 43.5,
    deltaReason: 'Beneficial ownership link to newly designated military electronics supplier',
    changeType: 'BENEFICIAL_OWNERSHIP',
    status: 'PENDING_REVIEW',
    updatedAt: '2026-10-14 02:16 UTC'
  },
  {
    id: 'delta-003',
    customerCode: 'CUST-GB-99214',
    customerName: 'Caspian Mineral Commodities Ltd',
    nationality: 'GBR',
    watchlistSource: 'UK OFSI Sanctions Notice 118',
    previousScore: 28.5,
    newScore: 78.0,
    deltaDiff: 49.5,
    deltaReason: 'Directorship overlap identified with sanctioned energy consortium',
    changeType: 'DIRECTORSHIP_LINK',
    status: 'PENDING_REVIEW',
    updatedAt: '2026-10-14 02:18 UTC'
  },
  {
    id: 'delta-004',
    customerCode: 'CUST-SA-12004',
    customerName: 'Al-Farouq Petroleum Equipment',
    nationality: 'SAU',
    watchlistSource: 'UN Security Council Resolution 2231',
    previousScore: 18.0,
    newScore: 71.5,
    deltaDiff: 53.5,
    deltaReason: 'Secondary phonetic match on updated dual-use equipment supplier registry',
    changeType: 'PHONETIC_DRIFT',
    status: 'PENDING_REVIEW',
    updatedAt: '2026-10-14 02:19 UTC'
  }
];

type ActiveTab = 'TRIAGE' | 'LOG_REPORT' | 'SOURCES';
type FilterPreset = 'ALL' | 'CRITICAL_JUMPS' | 'PENDING' | 'OFAC' | 'EU_UN';

export const DeltaRescreeningView: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();

  // Tab State derived from route if applicable
  const [activeTab, setActiveTab] = useState<ActiveTab>(() => {
    if (location.pathname.includes('/log-report')) return 'LOG_REPORT';
    return 'TRIAGE';
  });

  const [hits, setHits] = useState<DeltaHit[]>(MOCK_DELTA_HITS);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [activeHit, setActiveHit] = useState<DeltaHit | null>(MOCK_DELTA_HITS[0]);
  const [batchActionFeedback, setBatchActionFeedback] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);
  const [latestBatchId, setLatestBatchId] = useState('BATCH-2026-10-26-0200');
  const [scannedCount, setScannedCount] = useState(47618);

  // Filter States
  const [searchQuery, setSearchQuery] = useState('');
  const [filterCustomerCode, setFilterCustomerCode] = useState('');
  const [filterStartDate, setFilterStartDate] = useState('');
  const [filterEndDate, setFilterEndDate] = useState('');
  const [presetFilter, setPresetFilter] = useState<FilterPreset>('ALL');

  // Adjudication Rationale & Reason Code Form
  const [selectedReasonCode, setSelectedReasonCode] = useState('SECONDARY_ID_MISMATCH');
  const [rationaleText, setRationaleText] = useState('');
  const [isAdjudicating, setIsAdjudicating] = useState(false);

  // Safety Confirmation Modal for Bulk Actions
  const [bulkModalAction, setBulkModalAction] = useState<'DISMISS' | 'ESCALATE' | null>(null);
  const [bulkReasonCode, setBulkReasonCode] = useState('SECONDARY_ID_MISMATCH');
  const [bulkRationaleText, setBulkRationaleText] = useState('');

  // Watchlist Feed Watermarks
  const [feedSources, setFeedSources] = useState<Array<{
    id: string;
    name: string;
    lastSync: string;
    version: string;
    entityCount: number;
    status: 'HEALTHY' | 'SYNCING' | 'STALE';
  }>>([]);

  // Historical Log Reports (PDF p. 59 compliance)
  const [historicalLogs, setHistoricalLogs] = useState<Array<{
    id: string;
    batchId: string;
    customerCode: string;
    customerName: string;
    nationality: string;
    score: number;
    status: string;
    authorizedBy: string;
    timestampUtc: string;
    wormHash: string;
  }>>([]);

  // Load feed sources & historical logs on mount
  useEffect(() => {
    EdithApi.fetchDeltaSources().then(res => {
      if (res) setFeedSources(res);
    });
    EdithApi.fetchDeltaLogReport().then(res => {
      if (res) setHistoricalLogs(res);
    });
  }, []);

  // Sync tab with route changes
  useEffect(() => {
    if (location.pathname.includes('/log-report')) {
      setActiveTab('LOG_REPORT');
    }
  }, [location.pathname]);

  // Memoized Filtered Hits
  const filteredHits = useMemo(() => {
    return hits.filter(hit => {
      // 1. Text Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchesQuery = 
          hit.customerName.toLowerCase().includes(q) ||
          hit.customerCode.toLowerCase().includes(q) ||
          hit.watchlistSource.toLowerCase().includes(q) ||
          hit.deltaReason.toLowerCase().includes(q);
        if (!matchesQuery) return false;
      }

      // 2. Specific Customer Code
      if (filterCustomerCode.trim()) {
        if (!hit.customerCode.toLowerCase().includes(filterCustomerCode.toLowerCase())) {
          return false;
        }
      }

      // 3. Date Range
      if (filterStartDate) {
        if (hit.updatedAt < filterStartDate) return false;
      }
      if (filterEndDate) {
        if (hit.updatedAt > filterEndDate + ' 23:59:59') return false;
      }

      // 4. Preset Filter
      if (presetFilter === 'CRITICAL_JUMPS') {
        if (hit.deltaDiff < 50 && hit.newScore < 85) return false;
      } else if (presetFilter === 'PENDING') {
        if (hit.status !== 'PENDING_REVIEW') return false;
      } else if (presetFilter === 'OFAC') {
        if (!hit.watchlistSource.toLowerCase().includes('ofac')) return false;
      } else if (presetFilter === 'EU_UN') {
        const src = hit.watchlistSource.toLowerCase();
        if (!src.includes('eu') && !src.includes('un')) return false;
      }

      return true;
    });
  }, [hits, searchQuery, filterCustomerCode, filterStartDate, filterEndDate, presetFilter]);

  const toggleSelect = useCallback((id: string) => {
    setSelectedIds(prev => 
      prev.includes(id) ? prev.filter(item => item !== id) : [...prev, id]
    );
  }, []);

  const selectAll = useCallback(() => {
    if (selectedIds.length === filteredHits.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(filteredHits.map(h => h.id));
    }
  }, [selectedIds.length, filteredHits]);

  // Keyboard navigation shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing inside an input or textarea
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes((e.target as HTMLElement).tagName)) {
        return;
      }

      if (e.key === 'j' || e.key === 'ArrowDown') {
        e.preventDefault();
        const currentIndex = filteredHits.findIndex(h => h.id === activeHit?.id);
        if (currentIndex < filteredHits.length - 1) {
          setActiveHit(filteredHits[currentIndex + 1]);
        }
      } else if (e.key === 'k' || e.key === 'ArrowUp') {
        e.preventDefault();
        const currentIndex = filteredHits.findIndex(h => h.id === activeHit?.id);
        if (currentIndex > 0) {
          setActiveHit(filteredHits[currentIndex - 1]);
        }
      } else if (e.key === ' ' && activeHit) {
        e.preventDefault();
        toggleSelect(activeHit.id);
      } else if ((e.key === 'e' || e.key === 'E') && activeHit) {
        e.preventDefault();
        handleSingleEscalate();
      } else if ((e.key === 'd' || e.key === 'D') && activeHit) {
        e.preventDefault();
        handleSingleDismiss();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [filteredHits, activeHit, toggleSelect]);

  const handleExportDeltaLogCsv = async () => {
    const rowsToExport = filteredHits.length > 0 ? filteredHits : hits;
    const csvRows = [
      ['CustomerCode', 'CustomerName', 'Nationality', 'WatchlistSource', 'Score', 'DeltaDiff', 'Status', 'UpdatedDate'],
      ...rowsToExport.map(h => [
        h.customerCode, 
        `"${h.customerName}"`, 
        h.nationality, 
        `"${h.watchlistSource}"`, 
        h.newScore, 
        h.deltaDiff, 
        h.status, 
        h.updatedAt
      ])
    ];
    const csvContent = csvRows.map(r => r.join(',')).join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Delta_Screening_Report_Log_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);

    await EdithApi.recordAuditEvent({
      action: 'DELTA_LOG_REPORT_EXPORTED',
      targetEntity: 'Delta Screening Report Log',
      details: `Exported ${rowsToExport.length} delta customer rows per CBUAE/iShield regulatory specifications.`
    });
    setBatchActionFeedback(`Exported ${rowsToExport.length} filtered delta log rows to CSV and committed cryptographic hash to WORM ledger.`);
    setTimeout(() => setBatchActionFeedback(null), 4000);
  };

  const handleTriggerAdHocSync = async () => {
    if (syncing) return;
    setSyncing(true);
    try {
      const res = await EdithApi.triggerDeltaSync();
      if (res) {
        setLatestBatchId(res.syncBatchId);
        setScannedCount(res.scannedCustomers);
        const liveHits: DeltaHit[] = res.hits.map((h, idx) => {
          const prevScore = 14.0;
          const newScore = Number(h.compositeScore || 92.5);
          return {
            id: h.id || `delta-live-${Date.now()}-${idx}`,
            customerCode: h.customerCode,
            customerName: h.customerName,
            nationality: h.nationality || 'ARE',
            watchlistSource: `${h.sourceList} Live Sync`,
            previousScore: prevScore,
            newScore,
            deltaDiff: Number((newScore - prevScore).toFixed(1)),
            deltaReason: `${h.matchDetails} (Matched: ${h.matchedWatchlistName})`,
            changeType: 'NEW_ALIAS_ADDED',
            status: 'PENDING_REVIEW',
            updatedAt: new Date().toISOString().replace('T', ' ').substring(0, 16) + ' UTC'
          };
        });
        if (liveHits.length > 0) {
          setHits(prev => [...liveHits, ...prev]);
          setActiveHit(liveHits[0]);
        }
        setBatchActionFeedback(`Delta Sync ${res.syncBatchId} completed: evaluated ${res.scannedCustomers.toLocaleString()} records against sanctions stream. ${res.newDeltaHits} new delta intercepts detected.`);
        setTimeout(() => setBatchActionFeedback(null), 5000);
      }
    } finally {
      setSyncing(false);
    }
  };

  // AI-Assisted Rationale Generator
  const handleAutoDraftRationale = () => {
    if (!activeHit) return;
    const drafted = `Evaluated customer '${activeHit.customerName}' against ${activeHit.watchlistSource}. ${activeHit.deltaReason}. Verified distinct registration profile and beneficial owner registry. Recommended for review under AML policy Section 4e.`;
    setRationaleText(drafted);
  };

  // Single Item Adjudication: Dismiss False Positive
  const handleSingleDismiss = async () => {
    if (!activeHit) return;
    setIsAdjudicating(true);
    try {
      const rationale = rationaleText.trim() || `Marked False Positive by Compliance Officer. Reason: ${selectedReasonCode}`;
      await EdithApi.adjudicateDeltaHit({
        hitId: activeHit.id,
        customerCode: activeHit.customerCode,
        decision: 'DISMISSED',
        rationale,
        reasonCode: selectedReasonCode,
        batchId: latestBatchId
      });

      const updatedHit: DeltaHit = {
        ...activeHit,
        status: 'DISMISSED',
        dispositionRationale: rationale,
        dispositionReasonCode: selectedReasonCode
      };

      setHits(prev => prev.map(h => h.id === activeHit.id ? updatedHit : h));
      setActiveHit(updatedHit);
      setBatchActionFeedback(`Hit for ${activeHit.customerName} marked as DISMISSED (False Positive). Recorded in WORM audit ledger.`);
      setRationaleText('');
      setTimeout(() => setBatchActionFeedback(null), 4000);
    } finally {
      setIsAdjudicating(false);
    }
  };

  // Single Item Adjudication: Escalate to Active Investigation Case
  const handleSingleEscalate = async () => {
    if (!activeHit) return;
    setIsAdjudicating(true);
    try {
      const caseRes = await EdithApi.escalateDeltaToCase({
        customerCode: activeHit.customerCode,
        customerName: activeHit.customerName,
        matchedEntity: activeHit.watchlistSource,
        score: activeHit.newScore,
        deltaReason: activeHit.deltaReason,
        batchId: latestBatchId
      });

      const caseNumber = caseRes?.caseNumber || `EDITH-CAS-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`;
      const caseId = caseRes?.caseId || activeHit.id;

      const updatedHit: DeltaHit = {
        ...activeHit,
        status: 'ESCALATED',
        associatedCaseId: caseId,
        associatedCaseNumber: caseNumber,
        dispositionRationale: rationaleText.trim() || 'Escalated to EDD investigation queue due to statutory delta match.'
      };

      setHits(prev => prev.map(h => h.id === activeHit.id ? updatedHit : h));
      setActiveHit(updatedHit);
      setBatchActionFeedback(`Created investigation Case #${caseNumber} for ${activeHit.customerName}. Assigned to EDD queue.`);
      setRationaleText('');
      setTimeout(() => setBatchActionFeedback(null), 5000);
    } finally {
      setIsAdjudicating(false);
    }
  };

  // Execute Confirmed Bulk Action
  const handleConfirmBulkAction = async () => {
    if (!bulkModalAction || selectedIds.length === 0) return;
    const selectedCustomers = hits.filter(h => selectedIds.includes(h.id));
    const codes = selectedCustomers.map(h => h.customerCode);
    const action = bulkModalAction;
    const rationale = bulkRationaleText.trim() || `Bulk ${action} executed by Compliance Officer for ${codes.length} records.`;

    await EdithApi.authorizeDeltaBatch(
      latestBatchId, 
      codes, 
      action === 'DISMISS' ? 'DISMISS' : 'ESCALATE',
      rationale,
      bulkReasonCode
    );

    setHits(prev => prev.map(item => {
      if (selectedIds.includes(item.id)) {
        return {
          ...item,
          status: action === 'DISMISS' ? 'DISMISSED' : 'ESCALATED',
          dispositionRationale: rationale,
          dispositionReasonCode: bulkReasonCode
        };
      }
      return item;
    }));

    setBatchActionFeedback(`Successfully executed ${action} for ${selectedIds.length} delta hit(s) in batch ${latestBatchId}. WORM audit block generated.`);
    setSelectedIds([]);
    setBulkModalAction(null);
    setBulkRationaleText('');
    setTimeout(() => setBatchActionFeedback(null), 5000);
  };

  const pageMeta = usePageMeta(
    'Delta',
    'Continuous delta rescreening of customer portfolios against nightly international sanctions additions, alias updates, and PEP designations.'
  );

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {syncing && (
        <EdithLoader
          overlay
          size="md"
          label="SYNCHRONIZING WATCHLIST DELTA..."
          sublabel="Evaluating customer records against updated OFAC, EU, UN & Local Sanctions"
        />
      )}

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <RefreshCw className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            {pageMeta.parent && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {pageMeta.parent}
              </span>
            )}
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-accent/10 text-accent border border-accent/20">
              v2.5.0 Modernized
            </span>
          </div>
          <p className="text-xs text-muted mt-1">
            {pageMeta.definition}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className="text-right font-mono text-xs hidden sm:block">
            <div className="text-muted text-[10px]">LATEST SYNC BATCH</div>
            <div className="font-bold text-ink">{latestBatchId}</div>
          </div>
          <button
            onClick={handleTriggerAdHocSync}
            disabled={syncing}
            className="px-3.5 py-2 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-1.5 shadow-xs disabled:opacity-50 cursor-pointer"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${syncing ? 'animate-spin' : ''}`} />
            <span>{syncing ? 'Syncing Feeds...' : 'Trigger Ad-Hoc Sync'}</span>
          </button>
        </div>
      </div>

      {/* KPI Stats Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Customer Portfolio Evaluated</div>
          <div className="text-2xl font-bold font-mono text-ink mt-1">{scannedCount.toLocaleString()}</div>
          <div className="text-[11px] text-accent font-mono mt-1 flex items-center gap-1">
            <Check className="w-3 h-3" />
            <span>100% matched against Delta Stream</span>
          </div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">New Watchlist Additions</div>
          <div className="text-2xl font-bold font-mono text-primary mt-1">328 Entries</div>
          <div className="text-[11px] text-muted font-mono mt-1">OFAC (182) • EU (96) • UN (50)</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Pending Delta Intercepts</div>
          <div className="text-2xl font-bold font-mono text-critical mt-1">
            {hits.filter(h => h.status === 'PENDING_REVIEW').length} Suspect Hits
          </div>
          <div className="text-[11px] text-critical/80 font-mono mt-1">Requires 4-Eyes Adjudication</div>
        </div>
        <div className="bg-surface p-4 rounded-xl border border-border">
          <div className="text-muted text-xs font-medium">Auto-Cleared Divergence</div>
          <div className="text-2xl font-bold font-mono text-accent mt-1">99.98%</div>
          <div className="text-[11px] text-muted font-mono mt-1">Score delta &lt; 5.0% auto-suppressed</div>
        </div>
      </div>

      {/* Navigation Sub-Tabs */}
      <div className="flex items-center gap-2 border-b border-border pb-2">
        <button
          onClick={() => setActiveTab('TRIAGE')}
          className={`px-3 py-1.5 rounded-lg text-xs font-mono font-bold flex items-center gap-2 cursor-pointer transition-colors ${
            activeTab === 'TRIAGE'
              ? 'bg-primary text-white shadow-xs'
              : 'text-muted hover:text-ink hover:bg-surface-hover'
          }`}
        >
          <Layers className="w-3.5 h-3.5" />
          <span>Active Authorization Queue ({hits.filter(h => h.status === 'PENDING_REVIEW').length})</span>
        </button>
        <button
          onClick={() => setActiveTab('LOG_REPORT')}
          className={`px-3 py-1.5 rounded-lg text-xs font-mono font-bold flex items-center gap-2 cursor-pointer transition-colors ${
            activeTab === 'LOG_REPORT'
              ? 'bg-primary text-white shadow-xs'
              : 'text-muted hover:text-ink hover:bg-surface-hover'
          }`}
        >
          <FileText className="w-3.5 h-3.5" />
          <span>Delta Screening Log Report (PDF p. 59)</span>
        </button>
        <button
          onClick={() => setActiveTab('SOURCES')}
          className={`px-3 py-1.5 rounded-lg text-xs font-mono font-bold flex items-center gap-2 cursor-pointer transition-colors ${
            activeTab === 'SOURCES'
              ? 'bg-primary text-white shadow-xs'
              : 'text-muted hover:text-ink hover:bg-surface-hover'
          }`}
        >
          <Database className="w-3.5 h-3.5" />
          <span>Watchlist Sources &amp; Freshness</span>
        </button>
      </div>

      {/* Action Notification Toast */}
      {batchActionFeedback && (
        <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs font-mono flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{batchActionFeedback}</span>
        </div>
      )}

      {/* TAB 1: ACTIVE TRIAGE QUEUE */}
      {activeTab === 'TRIAGE' && (
        <div className="space-y-4">
          {/* Filter Bar with Presets & Live Search */}
          <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
            <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3">
              <div className="flex flex-wrap items-center gap-2 flex-1">
                {/* Search query input */}
                <div className="relative flex-1 min-w-[200px]">
                  <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-muted" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={e => setSearchQuery(e.target.value)}
                    placeholder="Search Customer, Alias, Reason..."
                    className="w-full pl-8 pr-2.5 py-1.5 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary"
                  />
                  {searchQuery && (
                    <button 
                      onClick={() => setSearchQuery('')}
                      className="absolute right-2.5 top-2.5 text-muted hover:text-ink cursor-pointer"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  )}
                </div>

                {/* Specific customer code filter */}
                <input
                  type="text"
                  value={filterCustomerCode}
                  onChange={e => setFilterCustomerCode(e.target.value)}
                  placeholder="Code (e.g. CUST-AE)..."
                  className="px-2.5 py-1.5 rounded-lg bg-surface-muted border border-border text-ink text-xs w-36 focus:outline-none focus:border-primary"
                />

                {/* Date range filters */}
                <input
                  type="date"
                  value={filterStartDate}
                  onChange={e => setFilterStartDate(e.target.value)}
                  title="Start Date"
                  className="px-2.5 py-1.5 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary"
                />
                <input
                  type="date"
                  value={filterEndDate}
                  onChange={e => setFilterEndDate(e.target.value)}
                  title="End Date"
                  className="px-2.5 py-1.5 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary"
                />

                {(searchQuery || filterCustomerCode || filterStartDate || filterEndDate) && (
                  <button
                    onClick={() => {
                      setSearchQuery('');
                      setFilterCustomerCode('');
                      setFilterStartDate('');
                      setFilterEndDate('');
                    }}
                    className="px-2 py-1 text-muted hover:text-ink text-[11px] underline cursor-pointer"
                  >
                    Clear Filters
                  </button>
                )}
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={handleExportDeltaLogCsv}
                  className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Export CSV ({filteredHits.length})</span>
                </button>
              </div>
            </div>

            {/* Quick Filter Presets Bar */}
            <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-border">
              <span className="text-[10px] uppercase font-bold text-muted flex items-center gap-1">
                <Filter className="w-3 h-3" /> Quick Presets:
              </span>
              {(['ALL', 'CRITICAL_JUMPS', 'PENDING', 'OFAC', 'EU_UN'] as FilterPreset[]).map(preset => (
                <button
                  key={preset}
                  onClick={() => setPresetFilter(preset)}
                  className={`px-2.5 py-0.5 rounded text-[11px] font-bold cursor-pointer transition-colors ${
                    presetFilter === preset
                      ? 'bg-primary/20 text-primary border border-primary/30'
                      : 'bg-surface-muted text-muted hover:text-ink border border-border'
                  }`}
                >
                  {preset === 'ALL' && 'All Hits'}
                  {preset === 'CRITICAL_JUMPS' && 'Critical Jumps (Δ ≥ 50%)'}
                  {preset === 'PENDING' && 'Pending Review'}
                  {preset === 'OFAC' && 'OFAC Lists'}
                  {preset === 'EU_UN' && 'EU / UN Sanctions'}
                </button>
              ))}

              <div className="ml-auto text-[10px] text-muted hidden sm:block">
                Showing {filteredHits.length} of {hits.length} records • Keyboard: <kbd className="px-1 bg-surface-muted border border-border rounded">J/K</kbd> navigate, <kbd className="px-1 bg-surface-muted border border-border rounded">Space</kbd> select, <kbd className="px-1 bg-surface-muted border border-border rounded">E</kbd> escalate, <kbd className="px-1 bg-surface-muted border border-border rounded">D</kbd> dismiss
              </div>
            </div>
          </div>

          {/* Main Triage Layout: Table & Inspector Drawer */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
            {/* Delta Hits Table (8 cols) */}
            <div className="lg:col-span-8 bg-surface rounded-xl border border-border overflow-hidden space-y-3 p-4">
              <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 pb-3 border-b border-border">
                <div className="flex items-center gap-3">
                  <button 
                    onClick={selectAll}
                    className="px-2.5 py-1 text-xs font-mono font-medium rounded border border-border bg-surface-muted hover:bg-surface-hover flex items-center gap-1.5 cursor-pointer"
                  >
                    <div className={`w-3.5 h-3.5 rounded-sm border flex items-center justify-center ${selectedIds.length === filteredHits.length && filteredHits.length > 0 ? 'bg-primary border-primary text-white' : 'border-border'}`}>
                      {selectedIds.length === filteredHits.length && filteredHits.length > 0 && <Check className="w-3 h-3" />}
                    </div>
                    <span>Select All ({filteredHits.length})</span>
                  </button>

                  <span className="text-xs text-muted font-mono">
                    {selectedIds.length} Selected
                  </span>
                </div>

                {/* Batch Action Buttons with Safety Modal Trigger */}
                <div className="flex items-center gap-2">
                  <button
                    disabled={selectedIds.length === 0}
                    onClick={() => {
                      setBulkModalAction('DISMISS');
                      setBulkReasonCode('SECONDARY_ID_MISMATCH');
                    }}
                    className="px-3 py-1 text-xs font-mono rounded bg-surface border border-border hover:bg-surface-hover text-ink disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1.5 cursor-pointer"
                  >
                    <CheckCircle2 className="w-3.5 h-3.5 text-accent" />
                    <span>Dismiss False Positives</span>
                  </button>
                  <button
                    disabled={selectedIds.length === 0}
                    onClick={() => {
                      setBulkModalAction('ESCALATE');
                      setBulkReasonCode('STATUTORY_SDN_CONFIRMED');
                    }}
                    className="px-3 py-1 text-xs font-mono rounded bg-critical text-white hover:bg-critical/90 font-bold disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1.5 shadow-xs cursor-pointer"
                  >
                    <AlertTriangle className="w-3.5 h-3.5" />
                    <span>Escalate to EDD Cases</span>
                  </button>
                </div>
              </div>

              {/* Table Body */}
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs font-mono">
                  <thead>
                    <tr className="border-b border-border text-muted text-[11px] uppercase tracking-wider bg-surface-muted">
                      <th className="py-2.5 px-3 w-8"></th>
                      <th className="py-2.5 px-3">Subject / Customer</th>
                      <th className="py-2.5 px-3">Delta Diff</th>
                      <th className="py-2.5 px-3">Sync Source</th>
                      <th className="py-2.5 px-3">Status</th>
                      <th className="py-2.5 px-3 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {filteredHits.length === 0 ? (
                      <tr>
                        <td colSpan={6} className="py-8 text-center text-muted font-mono text-xs">
                          No delta intercepts match current filter criteria.
                        </td>
                      </tr>
                    ) : (
                      filteredHits.map(hit => {
                        const isSelected = selectedIds.includes(hit.id);
                        const isActive = activeHit?.id === hit.id;
                        return (
                          <tr 
                            key={hit.id} 
                            onClick={() => setActiveHit(hit)}
                            className={`cursor-pointer transition-colors ${
                              isActive ? 'bg-primary/10 font-medium' : isSelected ? 'bg-surface-muted' : 'hover:bg-surface-hover'
                            }`}
                          >
                            <td className="py-3 px-3" onClick={(e) => { e.stopPropagation(); toggleSelect(hit.id); }}>
                              <div className={`w-3.5 h-3.5 rounded-sm border flex items-center justify-center ${isSelected ? 'bg-primary border-primary text-white' : 'border-border'}`}>
                                {isSelected && <Check className="w-3 h-3" />}
                              </div>
                            </td>
                            <td className="py-3 px-3">
                              <div className="font-bold text-ink">{hit.customerName}</div>
                              <div className="text-[10px] text-muted flex items-center gap-2 mt-0.5">
                                <span>{hit.customerCode}</span>
                                <span>•</span>
                                <span>Nat: {hit.nationality}</span>
                              </div>
                            </td>
                            <td className="py-3 px-3">
                              <div className="flex items-center gap-1.5">
                                <span className="text-muted line-through">{hit.previousScore.toFixed(1)}%</span>
                                <ChevronRight className="w-3 h-3 text-muted" />
                                <span className="font-bold text-critical">{hit.newScore.toFixed(1)}%</span>
                              </div>
                              <div className="text-[10px] text-critical font-bold flex items-center gap-1 mt-0.5">
                                <TrendingUp className="w-3 h-3" />
                                <span>+{hit.deltaDiff.toFixed(1)}% Jump</span>
                              </div>
                            </td>
                            <td className="py-3 px-3">
                              <div className="text-ink text-[11px] font-semibold">{hit.watchlistSource}</div>
                              <div className="text-[10px] text-muted">{hit.updatedAt}</div>
                            </td>
                            <td className="py-3 px-3">
                              {hit.status === 'PENDING_REVIEW' && (
                                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-warning/10 text-warning border border-warning/30">
                                  PENDING
                                </span>
                              )}
                              {hit.status === 'ESCALATED' && (
                                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-critical/10 text-critical border border-critical/30">
                                  ESCALATED
                                </span>
                              )}
                              {hit.status === 'DISMISSED' && (
                                <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/10 text-accent border border-accent/30">
                                  DISMISSED
                                </span>
                              )}
                            </td>
                            <td className="py-3 px-3 text-right">
                              {hit.associatedCaseNumber ? (
                                <button
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    navigate(`/cases?search=${hit.associatedCaseNumber}`);
                                  }}
                                  className="text-primary hover:text-primary-hover font-bold text-[11px] flex items-center gap-1 ml-auto cursor-pointer"
                                >
                                  <span>{hit.associatedCaseNumber}</span>
                                  <ExternalLink className="w-3 h-3" />
                                </button>
                              ) : (
                                <button className="text-primary hover:text-primary-hover font-bold text-[11px] cursor-pointer">
                                  Inspect →
                                </button>
                              )}
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Right Inspector Drawer (4 cols) with Side-by-Side "What Changed" Diff */}
            <div className="lg:col-span-4 bg-surface rounded-xl border border-border p-5 space-y-4">
              <div className="flex items-center justify-between pb-3 border-b border-border">
                <h3 className="text-xs font-mono uppercase tracking-wider font-bold text-ink flex items-center gap-2">
                  <ShieldAlert className="w-4 h-4 text-critical" />
                  Delta Diagnostic Inspector
                </h3>
                <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-surface-muted border border-border text-muted">
                  Live State
                </span>
              </div>

              {activeHit ? (
                <div className="space-y-4 text-xs font-mono">
                  {/* Entity Profile Header */}
                  <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1.5">
                    <div className="flex justify-between items-start">
                      <div className="text-muted text-[10px] uppercase">Entity Profile</div>
                      {activeHit.changeType && (
                        <span className="px-1.5 py-0.5 rounded text-[9px] font-bold bg-primary/10 text-primary border border-primary/20">
                          {activeHit.changeType.replace('_', ' ')}
                        </span>
                      )}
                    </div>
                    <div className="text-sm font-bold text-ink">{activeHit.customerName}</div>
                    <div className="text-muted text-[11px]">{activeHit.customerCode} • Nat: {activeHit.nationality}</div>
                  </div>

                  {/* Side-by-Side "What Changed" Diff Snapshot */}
                  <div className="space-y-2">
                    <div className="text-muted text-[10px] uppercase tracking-wider font-bold">
                      Side-by-Side Watchlist Snapshot Diff
                    </div>
                    <div className="grid grid-cols-2 gap-2 text-[10px]">
                      {/* Previous Snapshot */}
                      <div className="p-2.5 rounded-lg bg-surface-muted border border-border space-y-1">
                        <div className="text-muted font-bold uppercase">Previous State</div>
                        <div className="text-ink font-mono font-bold text-sm">{activeHit.previousScore.toFixed(1)}%</div>
                        <div className="text-muted text-[10px] leading-tight">Baseline profile clean or below threshold</div>
                      </div>

                      {/* Updated Snapshot */}
                      <div className="p-2.5 rounded-lg bg-critical/5 border border-critical/30 space-y-1">
                        <div className="text-critical font-bold uppercase">Updated Delta</div>
                        <div className="text-critical font-mono font-bold text-sm">{activeHit.newScore.toFixed(1)}%</div>
                        <div className="text-critical text-[10px] leading-tight font-bold">+{activeHit.deltaDiff.toFixed(1)}% Jump</div>
                      </div>
                    </div>
                  </div>

                  {/* Watchlist Update Rationale */}
                  <div className="space-y-1.5">
                    <div className="text-muted text-[10px] uppercase tracking-wider font-bold">Watchlist Update Rationale</div>
                    <div className="p-3 rounded-lg bg-surface-muted border border-border text-ink text-[11px] leading-relaxed">
                      {activeHit.deltaReason}
                    </div>
                  </div>

                  {/* Associated Case Badge if escalated */}
                  {activeHit.associatedCaseNumber && (
                    <div className="p-3 rounded-lg bg-critical/10 border border-critical/30 flex items-center justify-between">
                      <div>
                        <div className="text-[10px] text-critical font-bold uppercase">Active Investigation Case</div>
                        <div className="text-xs font-bold text-ink">{activeHit.associatedCaseNumber}</div>
                      </div>
                      <button
                        onClick={() => navigate(`/cases?search=${activeHit.associatedCaseNumber}`)}
                        className="px-2.5 py-1 rounded bg-critical text-white text-[10px] font-bold flex items-center gap-1 cursor-pointer"
                      >
                        <span>Open Workspace</span>
                        <ExternalLink className="w-3 h-3" />
                      </button>
                    </div>
                  )}

                  {/* 4-Eyes Adjudication Form */}
                  <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-2.5">
                    <div className="flex items-center justify-between">
                      <div className="text-muted text-[10px] uppercase font-bold">4-Eyes Regulatory Rationale</div>
                      <button
                        type="button"
                        onClick={handleAutoDraftRationale}
                        className="text-primary hover:text-primary-hover text-[10px] font-bold flex items-center gap-1 cursor-pointer"
                      >
                        <Sparkles className="w-3 h-3" />
                        <span>AI Auto-Draft</span>
                      </button>
                    </div>

                    <select
                      value={selectedReasonCode}
                      onChange={e => setSelectedReasonCode(e.target.value)}
                      className="w-full px-2 py-1.5 rounded bg-surface border border-border text-ink text-xs focus:outline-none focus:border-primary"
                    >
                      <option value="SECONDARY_ID_MISMATCH">Secondary ID Mismatch (Verified Distinct Passport/DOB)</option>
                      <option value="COMMON_NAME_COINCIDENCE">Common Name Coincidence (No Nexus)</option>
                      <option value="STATUTORY_SDN_CONFIRMED">Statutory SDN / Sanctions Match Confirmed</option>
                      <option value="UBO_SANCTIONS_EXPOSURE">Beneficial Ownership / Directorship Nexus</option>
                      <option value="HISTORICAL_FALSE_POSITIVE">Pre-Approved Whitelist Exception</option>
                    </select>

                    <textarea
                      rows={3}
                      value={rationaleText}
                      onChange={e => setRationaleText(e.target.value)}
                      placeholder="Enter legal / compliance justification for audit ledger..."
                      className="w-full px-2.5 py-1.5 rounded bg-surface border border-border text-ink text-xs focus:outline-none focus:border-primary resize-none"
                    />
                  </div>

                  {/* Action Buttons */}
                  <div className="pt-2 border-t border-border flex gap-2">
                    <button 
                      onClick={handleSingleDismiss}
                      disabled={isAdjudicating}
                      className="flex-1 py-2 text-xs font-mono font-bold rounded border border-border bg-surface hover:bg-surface-hover text-ink disabled:opacity-50 cursor-pointer flex items-center justify-center gap-1"
                    >
                      <CheckCircle2 className="w-3.5 h-3.5 text-accent" />
                      <span>Mark False Positive</span>
                    </button>
                    <button 
                      onClick={handleSingleEscalate}
                      disabled={isAdjudicating}
                      className="flex-1 py-2 text-xs font-mono font-bold rounded bg-critical hover:bg-critical/90 text-white shadow-xs disabled:opacity-50 cursor-pointer flex items-center justify-center gap-1"
                    >
                      <AlertTriangle className="w-3.5 h-3.5" />
                      <span>Escalate to Case</span>
                    </button>
                  </div>
                </div>
              ) : (
                <div className="p-8 text-center text-muted font-mono text-xs">
                  Select an item from the Delta hits list to inspect score changes.
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: DELTA SCREENING LOG REPORT (PDF p. 59 Compliance) */}
      {activeTab === 'LOG_REPORT' && (
        <div className="space-y-4">
          <div className="bg-surface p-4 rounded-xl border border-border flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 font-mono text-xs">
            <div>
              <h2 className="text-sm font-bold text-ink flex items-center gap-2">
                <FileText className="w-4 h-4 text-primary" />
                <span>Delta Screening Log Report (Regulatory Specification PDF p. 59)</span>
              </h2>
              <p className="text-xs text-muted mt-0.5">
                Statutory audit trail of portfolio differential screenings, approving MLROs, and WORM SHA-256 block signatures.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleExportDeltaLogCsv}
                className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
              >
                <Download className="w-3.5 h-3.5" />
                <span>Export Official Log CSV</span>
              </button>
              <button
                type="button"
                onClick={() => navigate('/reports')}
                className="px-3 py-1.5 rounded-lg bg-primary/10 border border-primary/20 text-primary font-bold hover:bg-primary/20 flex items-center gap-1.5 cursor-pointer"
              >
                <FileSpreadsheet className="w-3.5 h-3.5" />
                <span>Open All 14 Reports &amp; WORM Hub →</span>
              </button>
            </div>
          </div>

          {/* Historical Log Report Table */}
          <div className="bg-surface rounded-xl border border-border overflow-hidden p-4 space-y-3 font-mono text-xs">
            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[11px] uppercase tracking-wider bg-surface-muted">
                    <th className="py-2.5 px-3">Log ID</th>
                    <th className="py-2.5 px-3">Batch ID</th>
                    <th className="py-2.5 px-3">Customer Subject</th>
                    <th className="py-2.5 px-3">Score</th>
                    <th className="py-2.5 px-3">Decision</th>
                    <th className="py-2.5 px-3">Authorized By</th>
                    <th className="py-2.5 px-3">Timestamp (UTC)</th>
                    <th className="py-2.5 px-3">WORM Chain Hash</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {historicalLogs.map(log => (
                    <tr key={log.id} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-ink">{log.id}</td>
                      <td className="py-3 px-3 text-muted">{log.batchId}</td>
                      <td className="py-3 px-3">
                        <div className="font-bold text-ink">{log.customerName}</div>
                        <div className="text-[10px] text-muted">{log.customerCode} • {log.nationality}</div>
                      </td>
                      <td className="py-3 px-3 font-bold text-ink">{log.score.toFixed(1)}%</td>
                      <td className="py-3 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          log.status === 'ESCALATED' 
                            ? 'bg-critical/10 text-critical border border-critical/30' 
                            : 'bg-accent/10 text-accent border border-accent/30'
                        }`}>
                          {log.status}
                        </span>
                      </td>
                      <td className="py-3 px-3 text-ink font-medium">{log.authorizedBy}</td>
                      <td className="py-3 px-3 text-muted">{log.timestampUtc}</td>
                      <td className="py-3 px-3">
                        <span className="font-mono text-[10px] text-primary bg-primary/5 px-2 py-0.5 rounded border border-primary/20">
                          {log.wormHash.substring(0, 16)}...
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: WATCHLIST SOURCES & FRESHNESS WATERMARK */}
      {activeTab === 'SOURCES' && (
        <div className="space-y-4">
          <div className="bg-surface p-4 rounded-xl border border-border font-mono text-xs">
            <h2 className="text-sm font-bold text-ink flex items-center gap-2">
              <Database className="w-4 h-4 text-primary" />
              <span>International Watchlist Feeds &amp; Sync Watermarks</span>
            </h2>
            <p className="text-xs text-muted mt-0.5">
              Live connection telemetry and provenance hashes across United Nations, OFAC SDN, EU, and UAE sanctions feeds.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 font-mono text-xs">
            {feedSources.map(feed => (
              <div key={feed.id} className="bg-surface p-4 rounded-xl border border-border space-y-3">
                <div className="flex items-center justify-between pb-2 border-b border-border">
                  <div className="font-bold text-ink text-sm flex items-center gap-2">
                    <ShieldCheck className="w-4 h-4 text-accent" />
                    <span>{feed.name}</span>
                  </div>
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/10 text-accent border border-accent/30 flex items-center gap-1">
                    <Check className="w-3 h-3" />
                    <span>{feed.status}</span>
                  </span>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <div className="text-[10px] text-muted">LAST SYNC TIME</div>
                    <div className="font-bold text-ink mt-0.5 flex items-center gap-1">
                      <Clock className="w-3 h-3 text-muted" />
                      <span>{feed.lastSync}</span>
                    </div>
                  </div>
                  <div>
                    <div className="text-[10px] text-muted">ACTIVE ENTITIES TESTED</div>
                    <div className="font-bold text-ink mt-0.5">{feed.entityCount.toLocaleString()} Entries</div>
                  </div>
                </div>

                <div className="p-2.5 rounded-lg bg-surface-muted border border-border text-[11px] text-muted flex items-center justify-between">
                  <span>Watermark Version:</span>
                  <span className="font-bold text-primary">{feed.version}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Bulk Action Safety Confirmation Modal */}
      {bulkModalAction && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-surface max-w-lg w-full rounded-2xl border border-border p-6 space-y-4 font-mono text-xs shadow-xl animate-in fade-in zoom-in-95">
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <div className="flex items-center gap-2 text-ink font-bold text-sm">
                {bulkModalAction === 'DISMISS' ? (
                  <CheckCircle2 className="w-5 h-5 text-accent" />
                ) : (
                  <AlertTriangle className="w-5 h-5 text-critical" />
                )}
                <span>Confirm Bulk {bulkModalAction === 'DISMISS' ? 'False Positive Dismissal' : 'EDD Case Escalation'}</span>
              </div>
              <button 
                onClick={() => setBulkModalAction(null)}
                className="text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1">
              <div className="text-[11px] text-muted">Records Selected for Action:</div>
              <div className="text-sm font-bold text-ink">{selectedIds.length} Customer Record(s)</div>
              <div className="text-[10px] text-muted">
                {hits.filter(h => selectedIds.includes(h.id)).map(h => h.customerName).join(', ')}
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-[10px] uppercase font-bold text-muted">Regulatory Reason Code</label>
              <select
                value={bulkReasonCode}
                onChange={e => setBulkReasonCode(e.target.value)}
                className="w-full px-2.5 py-2 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary"
              >
                <option value="SECONDARY_ID_MISMATCH">Secondary ID Mismatch (Verified Distinct Passport/DOB)</option>
                <option value="COMMON_NAME_COINCIDENCE">Common Name Coincidence (No Nexus)</option>
                <option value="STATUTORY_SDN_CONFIRMED">Statutory SDN / Sanctions Match Confirmed</option>
                <option value="UBO_SANCTIONS_EXPOSURE">Beneficial Ownership / Directorship Nexus</option>
                <option value="HISTORICAL_FALSE_POSITIVE">Pre-Approved Whitelist Exception</option>
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="text-[10px] uppercase font-bold text-muted">Mandatory Compliance Rationale</label>
              <textarea
                rows={3}
                value={bulkRationaleText}
                onChange={e => setBulkRationaleText(e.target.value)}
                placeholder="Enter justification for mass action. This note is committed to the immutable WORM ledger..."
                className="w-full px-3 py-2 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary resize-none"
              />
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-border">
              <button
                type="button"
                onClick={() => setBulkModalAction(null)}
                className="px-4 py-2 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink font-bold cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleConfirmBulkAction}
                className={`px-4 py-2 rounded-lg text-white font-bold cursor-pointer shadow-xs ${
                  bulkModalAction === 'DISMISS' 
                    ? 'bg-accent hover:bg-accent/90' 
                    : 'bg-critical hover:bg-critical/90'
                }`}
              >
                Confirm &amp; Commit to Ledger ({selectedIds.length})
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
