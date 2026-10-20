import React, { useState, useRef, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Search,
  ShieldAlert,
  CheckCircle2,
  Fingerprint,
  GitBranch,
  Globe,
  FileWarning,
  Users,
  ChevronDown,
  ChevronUp,
  ArrowRight,
  Sparkles,
  BookOpen,
  X,
  RotateCcw,
  Copy,
  Check,
  Building,
  User,
  ShieldCheck,
  AlertTriangle,
  RefreshCw,
  FileText,
  PlusCircle,
  Download,
  Clock,
  Columns,
  Layers,
  ClipboardList,
  Sliders
} from 'lucide-react';
import { NameSearchMode, NameSearchResponse, NameSearchMatch, ScreeningAuditCertificate } from '../types';
import { EdithApi } from '../services/api';

interface RecentSearchRecord {
  name: string;
  idNumber: string;
  birthYear: string;
  mode: NameSearchMode;
  verdict: 'CLEARED' | 'HELD' | 'BLOCKED';
  score: number;
  matchCount: number;
  timestamp: string;
}

interface BatchScreeningItem {
  inputLine: string;
  detectedMode: NameSearchMode;
  verdict: 'CLEARED' | 'HELD' | 'BLOCKED';
  topHit?: string;
  topList?: string;
  score: number;
  matchCount: number;
  latencyMs: number;
  rawResponse: NameSearchResponse;
}

const STORAGE_KEY_RECENT = 'edith_recent_searches_v1';

export const NameSearchView: React.FC = () => {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [idNumber, setIdNumber] = useState('');
  const [birthYear, setBirthYear] = useState<string>('');
  const [searchMode, setSearchMode] = useState<NameSearchMode>('FUZZY');
  const [minScore, setMinScore] = useState<number>(65);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [response, setResponse] = useState<NameSearchResponse | null>(null);

  // Client-Side "What-If" Sensitivity Thresholding
  const [clientThreshold, setClientThreshold] = useState<number>(65);

  // PEP Tiering & Adverse Media Disaggregation Tabs
  const [activeTierTab, setActiveTierTab] = useState<'ALL' | 'SANCTIONS' | 'PEP' | 'ADVERSE_MEDIA'>('ALL');

  // Track the query that produced the currently rendered response
  const [lastExecutedQuery, setLastExecutedQuery] = useState<{
    name: string;
    idNumber: string;
    mode: NameSearchMode;
  } | null>(null);

  // Filters for results
  const [entityTypeFilter, setEntityTypeFilter] = useState<'ALL' | 'INDIVIDUAL' | 'CORPORATE'>('ALL');
  const [listFilter, setListFilter] = useState<string>('ALL');

  // Copy feedback state
  const [copiedAuditNote, setCopiedAuditNote] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  // Interactive Panels: Diagram and Side-by-Side Diff
  const [activeDiagramEntityId, setActiveDiagramEntityId] = useState<string | null>(null);
  const [activeDiffEntityId, setActiveDiffEntityId] = useState<string | null>(null);
  const [showOverviewDiagram, setShowOverviewDiagram] = useState(false);

  // 1-Click Whitelist Exception Modal
  const [whitelistModalMatch, setWhitelistModalMatch] = useState<NameSearchMatch | null>(null);
  const [whitelistReason, setWhitelistReason] = useState('Divergent Year of Birth / Secondary National ID discrepancy verified.');
  const [whitelistScope, setWhitelistScope] = useState('EXACT');
  const [whitelistSubmitting, setWhitelistSubmitting] = useState(false);

  // Regulatory Screening Certificate Modal
  const [certificateData, setCertificateData] = useState<ScreeningAuditCertificate | null>(null);
  const [certifying, setCertifying] = useState(false);

  // Recent Searches State
  const [recentSearches, setRecentSearches] = useState<RecentSearchRecord[]>(() => {
    try {
      const stored = sessionStorage.getItem(STORAGE_KEY_RECENT);
      return stored ? JSON.parse(stored) : [];
    } catch {
      return [];
    }
  });

  // Batch Clipboard Paste Screening Mode
  const [isBatchPasteOpen, setIsBatchPasteOpen] = useState(false);
  const [batchInputText, setBatchInputText] = useState(
    'VIKTOR ANATOLYEVICH BOUT\nAl-Baraka General Trading LLC\nTariq Mansoor Al-Aziz\nUS-OFAC-9812\nDubai Retail Holdings LLC'
  );
  const [batchLoading, setBatchLoading] = useState(false);
  const [batchResults, setBatchResults] = useState<BatchScreeningItem[]>([]);

  const nameInputRef = useRef<HTMLInputElement>(null);

  const isQueryDirty = useMemo(() => {
    if (!lastExecutedQuery) return false;
    return (
      name.trim() !== lastExecutedQuery.name.trim() ||
      idNumber.trim() !== lastExecutedQuery.idNumber.trim() ||
      searchMode !== lastExecutedQuery.mode
    );
  }, [name, idNumber, searchMode, lastExecutedQuery]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        handleReset();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const saveRecentSearch = (
    qName: string,
    qId: string,
    qBy: string,
    qMode: NameSearchMode,
    res: NameSearchResponse
  ) => {
    const verdict: 'CLEARED' | 'HELD' | 'BLOCKED' =
      res.totalMatches === 0
        ? 'CLEARED'
        : res.matches.some(m => m.isBlocked)
        ? 'BLOCKED'
        : 'HELD';

    const record: RecentSearchRecord = {
      name: qName,
      idNumber: qId,
      birthYear: qBy,
      mode: qMode,
      verdict,
      score: res.matches[0]?.score || 0,
      matchCount: res.totalMatches,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    };

    setRecentSearches(prev => {
      const filtered = prev.filter(
        r => !(r.name === qName && r.idNumber === qId && r.mode === qMode)
      );
      const updated = [record, ...filtered].slice(0, 5);
      try {
        sessionStorage.setItem(STORAGE_KEY_RECENT, JSON.stringify(updated));
      } catch {}
      return updated;
    });
  };

  const executeSearch = async (
    overrideName?: string,
    overrideId?: string,
    overrideMode?: NameSearchMode,
    overrideBirthYear?: string
  ) => {
    const qName = overrideName !== undefined ? overrideName : name;
    const qId = overrideId !== undefined ? overrideId : idNumber;
    const qMode = overrideMode !== undefined ? overrideMode : searchMode;
    const qBy = overrideBirthYear !== undefined ? overrideBirthYear : birthYear;

    if (!qName.trim() && !qId.trim()) {
      setError('Please enter a full/partial name or an ID/Passport number to screen.');
      return;
    }

    setLoading(true);
    setError(null);
    setActiveDiagramEntityId(null);
    setActiveDiffEntityId(null);
    setCopiedAuditNote(false);

    try {
      const parsedYear = qBy.trim() ? parseInt(qBy.trim(), 10) : undefined;
      const res = await EdithApi.searchNameAndId({
        name: qName,
        idNumber: qId,
        birthYear: isNaN(parsedYear as number) ? undefined : parsedYear,
        searchMode: qMode,
        minScore,
        maxResults: 30
      });
      setResponse(res);
      setClientThreshold(minScore);
      setLastExecutedQuery({
        name: qName,
        idNumber: qId,
        mode: qMode
      });
      saveRecentSearch(qName, qId, qBy, qMode, res);
    } catch (err: any) {
      setError(err?.message || 'Screening search request failed.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    executeSearch();
  };

  const handleReset = () => {
    setName('');
    setIdNumber('');
    setBirthYear('');
    setResponse(null);
    setLastExecutedQuery(null);
    setError(null);
    setActiveDiagramEntityId(null);
    setActiveDiffEntityId(null);
    setCopiedAuditNote(false);
    setEntityTypeFilter('ALL');
    setListFilter('ALL');
    setActiveTierTab('ALL');
    if (nameInputRef.current) {
      nameInputRef.current.focus();
    }
  };

  const applyPreset = (presetName: string, presetId: string, mode: NameSearchMode, presetYear: string = '') => {
    setName(presetName);
    setIdNumber(presetId);
    setBirthYear(presetYear);
    setSearchMode(mode);
    executeSearch(presetName, presetId, mode, presetYear);
  };

  const handleModeChange = (newMode: NameSearchMode) => {
    setSearchMode(newMode);
    if (response && (name.trim() || idNumber.trim())) {
      executeSearch(name, idNumber, newMode);
    }
  };

  const toggleMatchDiagram = (key: string) => {
    setActiveDiagramEntityId(prev => (prev === key ? null : key));
  };

  const toggleDiffInspector = (key: string) => {
    setActiveDiffEntityId(prev => (prev === key ? null : key));
  };

  // 1-Click Intercept & Persistent Case Creation
  const handleInterceptCase = async (match: NameSearchMatch) => {
    try {
      const created = await EdithApi.createInterceptCase({
        beneficiaryName: match.fullName,
        riskScore: match.score,
        triggeredList: match.sanctionsList,
        holdReason: `1-Click Direct Intercept from Universal Screening (${match.sanctionsAuthority})`
      });
      if (created) {
        setStatusMessage(`Provisioned Case ${created.caseNumber} in 3-Pane Investigation Workspace.`);
        setTimeout(() => {
          navigate('/cases');
        }, 1200);
      }
    } catch {
      setStatusMessage('Case creation failed. Please check network connectivity.');
    }
  };

  // 1-Click Whitelist Submission
  const handleWhitelistSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!whitelistModalMatch) return;
    setWhitelistSubmitting(true);
    try {
      const expiresAt = new Date();
      expiresAt.setFullYear(expiresAt.getFullYear() + 1);
      await EdithApi.createWhitelist({
        entityIdentifier: whitelistModalMatch.externalId || whitelistModalMatch.fullName,
        matchScope: whitelistScope,
        reason: whitelistReason,
        expiresAt: expiresAt.toISOString()
      });
      setStatusMessage(`Added ${whitelistModalMatch.fullName} to 12-Month Whitelist Suppression ledger.`);
      setWhitelistModalMatch(null);
      setTimeout(() => setStatusMessage(null), 4000);
    } catch {
      setStatusMessage('Failed to register whitelist exception.');
    } finally {
      setWhitelistSubmitting(false);
    }
  };

  // Export Cryptographically Signed WORM Certificate
  const handleExportCertificate = async () => {
    if (!response) return;
    setCertifying(true);
    try {
      const cert = await EdithApi.generateScreeningAuditCertificate({
        subjectName: response.queryName || response.queryId || 'Single Entity',
        subjectId: response.queryId || undefined,
        screeningType: 'SINGLE_ENTITY',
        decision: response.totalMatches > 0 ? 'HELD' : 'CLEARED',
        overallScore: response.matches[0]?.score || 0,
        latencyMs: response.latencyMs,
        matchCount: response.totalMatches
      });
      setCertificateData(cert);
    } catch {
      setStatusMessage('Certificate generation failed.');
    } finally {
      setCertifying(false);
    }
  };

  // Batch Clipboard Paste Screening Execution
  const handleExecuteBatch = async () => {
    const lines = batchInputText
      .split('\n')
      .map(l => l.trim())
      .filter(l => l.length > 0)
      .slice(0, 10);

    if (lines.length === 0) return;

    setBatchLoading(true);
    try {
      const promises = lines.map(async line => {
        const isId = /^(OFAC|WC|UN|TL|AE|CH|[A-Z]{2,4}-)/i.test(line);
        const mode: NameSearchMode = isId ? 'EXACT_NAME_AND_ID' : 'FUZZY';
        try {
          const res = await EdithApi.searchNameAndId({
            name: isId ? '' : line,
            idNumber: isId ? line : '',
            searchMode: mode,
            minScore: 65,
            maxResults: 5
          });

          const verdict: 'CLEARED' | 'HELD' | 'BLOCKED' =
            res.totalMatches === 0
              ? 'CLEARED'
              : res.matches.some(m => m.isBlocked)
              ? 'BLOCKED'
              : 'HELD';

          const topMatch = res.matches[0];
          return {
            inputLine: line,
            detectedMode: mode,
            verdict,
            topHit: topMatch?.fullName,
            topList: topMatch?.sanctionsList,
            score: topMatch?.score || 0,
            matchCount: res.totalMatches,
            latencyMs: res.latencyMs,
            rawResponse: res
          } as BatchScreeningItem;
        } catch {
          return {
            inputLine: line,
            detectedMode: mode,
            verdict: 'CLEARED',
            score: 0,
            matchCount: 0,
            latencyMs: 0,
            rawResponse: {
              queryName: line,
              normalizedQueryName: line,
              searchMode: mode,
              totalMatches: 0,
              latencyMs: 0,
              matches: [],
              evaluatedAt: new Date().toISOString()
            }
          } as BatchScreeningItem;
        }
      });

      const results = await Promise.all(promises);
      setBatchResults(results);
    } finally {
      setBatchLoading(false);
    }
  };

  const copyAuditNote = () => {
    if (!response) return;
    const auditText = `[EDITH AML CLEARANCE AUDIT NOTE]
Timestamp: ${new Date().toISOString()}
Search Mode: ${response.searchMode}
Screened Name: "${response.queryName || 'N/A'}"
Screened ID / Passport: "${response.queryId || 'N/A'}"
Engine Verdict: ${response.totalMatches === 0 ? 'CLEAN / 0 MATCHES IDENTIFIED' : `${response.totalMatches} POTENTIAL MATCHES FOUND`}
Checked Registries: OFAC SDN, UN Consolidated, EU Financial Sanctions, UK HMT, CBUAE Local Terrorist List, World-Check PEP.
Engine Latency: ${response.latencyMs} ms
Compliance Status: ${response.totalMatches === 0 ? 'PASS (Auto-Cleared)' : 'HELD (Investigation Required)'}`;

    navigator.clipboard.writeText(auditText);
    setCopiedAuditNote(true);
    setTimeout(() => setCopiedAuditNote(false), 3000);
  };

  const getListBadgeStyle = (list: string) => {
    if (list.includes('OFAC')) return 'bg-critical/15 text-critical border-critical/40';
    if (list.includes('UN')) return 'bg-warning/15 text-warning border-warning/40';
    if (list.includes('PEP')) return 'bg-primary/15 text-primary border-primary/40';
    if (list.includes('WORLD_CHECK')) return 'bg-purple-500/15 text-purple-700 border-purple-400/40';
    if (list.includes('CBUAE') || list.includes('UAE')) return 'bg-red-700/15 text-red-700 border-red-500/40';
    return 'bg-accent/15 text-accent border-accent/40';
  };

  // PEP Tiering Counts based on client threshold
  const tierCounts = useMemo(() => {
    if (!response || !response.matches) return { all: 0, sanctions: 0, pep: 0, media: 0 };
    const overThreshold = response.matches.filter(m => m.score >= clientThreshold);
    return {
      all: overThreshold.length,
      sanctions: overThreshold.filter(m => m.category === 'SANCTIONS').length,
      pep: overThreshold.filter(m => m.category === 'PEP' || m.sanctionsList.includes('PEP')).length,
      media: overThreshold.filter(m => m.category === 'ADVERSE_MEDIA').length
    };
  }, [response, clientThreshold]);

  // Client-Side "What-If" Sensitivity & Tier Filtered Matches
  const filteredMatches = useMemo(() => {
    if (!response || !response.matches) return [];
    return response.matches.filter(m => {
      // 1. Client Sensitivity Threshold
      if (m.score < clientThreshold) return false;

      // 2. PEP & Disaggregation Tiers
      if (activeTierTab === 'SANCTIONS' && m.category !== 'SANCTIONS') return false;
      if (activeTierTab === 'PEP' && m.category !== 'PEP' && !m.sanctionsList.includes('PEP')) return false;
      if (activeTierTab === 'ADVERSE_MEDIA' && m.category !== 'ADVERSE_MEDIA') return false;

      // 3. Entity Type Filter
      if (entityTypeFilter !== 'ALL' && m.entityType !== entityTypeFilter) {
        return false;
      }

      // 4. List Filter
      if (listFilter !== 'ALL' && !m.sanctionsList.toUpperCase().includes(listFilter)) {
        return false;
      }

      return true;
    });
  }, [response, clientThreshold, activeTierTab, entityTypeFilter, listFilter]);

  // Simulated Verdict under What-If Threshold
  const simulatedVerdict = useMemo(() => {
    if (!response) return 'CLEARED';
    if (filteredMatches.length === 0) return 'CLEARED';
    if (filteredMatches.some(m => m.isBlocked)) return 'BLOCKED';
    return 'HELD';
  }, [response, filteredMatches]);

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Top Header Banner */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-surface p-5 rounded-xl border border-border shadow-2xs">
        <div>
          <div className="flex items-center gap-2.5">
            <Search className="w-6 h-6 text-primary" />
            <h1 className="text-xl font-bold text-ink">
              Universal Single Entity Sanctions & PEP Screening
            </h1>
            <span className="px-2 py-0.5 text-[10px] font-mono font-bold uppercase rounded-full bg-accent/15 text-accent border border-accent/30 flex items-center gap-1">
              <Users className="w-3 h-3" />
              All Users Accessible
            </span>
          </div>
          <p className="text-xs text-muted mt-1">
            Deterministic search across 47,600+ live PostgreSQL & OpenSearch records with 1-Click Intercept, Whitelist Suppression, and WORM Proof Certificates.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          <button
            type="button"
            onClick={() => setIsBatchPasteOpen(prev => !prev)}
            className={`px-3 py-2 text-xs font-mono font-bold rounded-lg border transition flex items-center gap-1.5 ${
              isBatchPasteOpen
                ? 'bg-primary text-white border-primary shadow-xs'
                : 'bg-surface-muted text-ink border-border hover:bg-border/40'
            }`}
            title="Screen up to 10 entities/passports in rapid parallel batch"
          >
            <ClipboardList className="w-3.5 h-3.5" />
            <span>Multi-Line Batch Paste</span>
          </button>

          {(response || name || idNumber) && (
            <button
              type="button"
              onClick={handleReset}
              className="px-3 py-2 text-xs font-mono font-bold rounded-lg bg-surface-muted text-ink border border-border hover:bg-border/40 transition flex items-center gap-1.5"
              title="Clear all fields and reset for next entity (Esc)"
            >
              <RotateCcw className="w-3.5 h-3.5 text-muted" />
              <span>Clear Search</span>
              <span className="text-[10px] px-1 py-0.2 rounded bg-surface border border-border text-muted">Esc</span>
            </button>
          )}

          <button
            type="button"
            onClick={() => setShowOverviewDiagram(prev => !prev)}
            className="px-3 py-2 text-xs font-mono font-bold rounded-lg bg-primary/10 text-primary border border-primary/30 hover:bg-primary/20 transition flex items-center gap-2"
          >
            <GitBranch className="w-4 h-4" />
            {showOverviewDiagram ? 'Hide Search Engine Diagram' : 'View Search Engine Diagram'}
          </button>
        </div>
      </div>

      {statusMessage && (
        <div className="p-3 rounded-lg bg-accent/15 border border-accent/30 text-accent text-xs font-mono flex items-center justify-between animate-in fade-in duration-150">
          <div className="flex items-center gap-2">
            <Check className="w-4 h-4" />
            <span>{statusMessage}</span>
          </div>
          <button type="button" onClick={() => setStatusMessage(null)}>
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* UX 3: Session Recent Searches Breadcrumb Bar */}
      {recentSearches.length > 0 && (
        <div className="p-3 bg-surface rounded-xl border border-border flex items-center justify-between gap-3 flex-wrap">
          <div className="flex items-center gap-2 flex-wrap text-xs font-mono">
            <span className="text-muted flex items-center gap-1.5 font-bold uppercase text-[10px]">
              <Clock className="w-3.5 h-3.5 text-primary" /> Recent Searches:
            </span>
            {recentSearches.map((rec, rIdx) => (
              <button
                key={rIdx}
                type="button"
                onClick={() => {
                  setName(rec.name);
                  setIdNumber(rec.idNumber);
                  setBirthYear(rec.birthYear);
                  setSearchMode(rec.mode);
                  executeSearch(rec.name, rec.idNumber, rec.mode, rec.birthYear);
                }}
                className="px-2.5 py-1 rounded-md bg-surface-muted hover:bg-border/40 border border-border flex items-center gap-1.5 transition text-xs"
                title={`Re-run search for "${rec.name || rec.idNumber}" (${rec.mode})`}
              >
                <span className="font-semibold text-ink truncate max-w-[150px]">
                  {rec.name || rec.idNumber}
                </span>
                <span className={`px-1.5 py-0.2 rounded text-[10px] font-bold ${
                  rec.verdict === 'BLOCKED'
                    ? 'bg-critical/20 text-critical'
                    : rec.verdict === 'HELD'
                    ? 'bg-warning/20 text-warning'
                    : 'bg-accent/20 text-accent'
                }`}>
                  {rec.verdict} ({rec.matchCount})
                </span>
                <span className="text-[10px] text-muted">{rec.timestamp}</span>
              </button>
            ))}
          </div>

          <button
            type="button"
            onClick={() => {
              setRecentSearches([]);
              sessionStorage.removeItem(STORAGE_KEY_RECENT);
            }}
            className="text-muted hover:text-ink text-[11px] underline font-mono shrink-0"
            title="Clear recent searches history"
          >
            Clear History
          </button>
        </div>
      )}

      {/* Engine 7: Batch Clipboard Paste Screening Panel */}
      {isBatchPasteOpen && (
        <div className="bg-surface rounded-xl border-2 border-primary/30 p-5 space-y-4 shadow-sm animate-in fade-in duration-150 font-mono text-xs">
          <div className="flex items-center justify-between border-b border-border pb-3">
            <div className="flex items-center gap-2">
              <ClipboardList className="w-4 h-4 text-primary" />
              <h2 className="text-xs uppercase tracking-wider font-bold text-ink">
                Multi-Line Batch Clipboard Screening (Parallel Engine)
              </h2>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-[10px] text-muted">Max 10 rows per batch</span>
              <button
                type="button"
                onClick={() => setIsBatchPasteOpen(false)}
                className="text-muted hover:text-ink"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-muted block text-[11px]">
              Paste names, passports, or trade licenses (one entry per line):
            </label>
            <textarea
              rows={5}
              value={batchInputText}
              onChange={e => setBatchInputText(e.target.value)}
              placeholder="VIKTOR ANATOLYEVICH BOUT&#10;Al-Baraka General Trading LLC&#10;Tariq Mansoor Al-Aziz&#10;US-OFAC-9812"
              className="w-full p-3 rounded-lg border border-border bg-background text-ink font-mono text-xs focus:outline-none focus:border-primary"
            />
          </div>

          <div className="flex justify-between items-center pt-2">
            <div className="text-[11px] text-muted">
              Auto-detects Exact Name, Fuzzy Phonetic, or Identifier search based on row format.
            </div>
            <button
              type="button"
              onClick={handleExecuteBatch}
              disabled={batchLoading}
              className="px-4 py-2 rounded-lg bg-primary text-white font-bold hover:bg-primary/90 flex items-center gap-1.5 shadow-xs transition"
            >
              {batchLoading ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  <span>Screening {batchInputText.split('\n').filter(l => l.trim()).length} Rows...</span>
                </>
              ) : (
                <>
                  <Search className="w-3.5 h-3.5" />
                  <span>Run Multi-Line Screening</span>
                </>
              )}
            </button>
          </div>

          {/* Batch Results Matrix */}
          {batchResults.length > 0 && (
            <div className="pt-3 border-t border-border space-y-2">
              <h3 className="font-bold text-ink uppercase text-[11px]">
                Batch Screening Triage Matrix ({batchResults.length} Queried)
              </h3>
              <div className="overflow-x-auto border border-border rounded-lg">
                <table className="w-full text-left text-xs">
                  <thead className="bg-surface-muted border-b border-border text-[10px] uppercase text-muted">
                    <tr>
                      <th className="p-2.5">#</th>
                      <th className="p-2.5">Input Query</th>
                      <th className="p-2.5">Detected Mode</th>
                      <th className="p-2.5">Verdict</th>
                      <th className="p-2.5">Top Matched Record</th>
                      <th className="p-2.5">Score</th>
                      <th className="p-2.5">Latency</th>
                      <th className="p-2.5 text-right">Inspect</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border font-mono">
                    {batchResults.map((bItem, bIdx) => (
                      <tr key={bIdx} className="hover:bg-surface-muted/40 transition">
                        <td className="p-2.5 font-bold text-muted">{bIdx + 1}</td>
                        <td className="p-2.5 font-semibold text-ink">{bItem.inputLine}</td>
                        <td className="p-2.5 text-[11px] text-muted">{bItem.detectedMode}</td>
                        <td className="p-2.5">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            bItem.verdict === 'BLOCKED'
                              ? 'bg-critical/20 text-critical'
                              : bItem.verdict === 'HELD'
                              ? 'bg-warning/20 text-warning'
                              : 'bg-accent/20 text-accent'
                          }`}>
                            {bItem.verdict} ({bItem.matchCount})
                          </span>
                        </td>
                        <td className="p-2.5 text-[11px] text-ink">
                          {bItem.topHit ? (
                            <div>
                              <span className="font-bold">{bItem.topHit}</span>
                              <span className="text-muted text-[10px] ml-1">({bItem.topList})</span>
                            </div>
                          ) : (
                            <span className="text-accent italic">Clean / Zero adverse hits</span>
                          )}
                        </td>
                        <td className="p-2.5 font-bold">
                          {bItem.score > 0 ? (
                            <span className={bItem.score >= 85 ? 'text-critical' : 'text-warning'}>
                              {bItem.score.toFixed(1)}%
                            </span>
                          ) : (
                            <span className="text-muted">0%</span>
                          )}
                        </td>
                        <td className="p-2.5 text-muted text-[11px]">{bItem.latencyMs}ms</td>
                        <td className="p-2.5 text-right">
                          <button
                            type="button"
                            onClick={() => {
                              setName(bItem.inputLine);
                              setSearchMode(bItem.detectedMode);
                              setResponse(bItem.rawResponse);
                              setClientThreshold(minScore);
                              setIsBatchPasteOpen(false);
                            }}
                            className="px-2 py-1 rounded bg-surface border border-border hover:bg-border/30 text-[10px] font-bold text-primary"
                          >
                            Inspect
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* On-Click Architecture & Search Modes Diagram */}
      {showOverviewDiagram && (
        <div className="bg-surface rounded-xl border-2 border-primary/30 p-5 space-y-4 shadow-md animate-in fade-in duration-150">
          <div className="flex items-center justify-between border-b border-border pb-3">
            <div className="flex items-center gap-2">
              <GitBranch className="w-4 h-4 text-primary" />
              <h2 className="text-xs font-mono uppercase tracking-wider font-bold text-ink">
                Interactive Architecture Diagram — How the Single Entity Engine Works
              </h2>
            </div>
            <button
              type="button"
              onClick={() => setShowOverviewDiagram(false)}
              className="text-muted hover:text-ink text-xs font-mono"
            >
              Close Diagram
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-3 text-xs font-mono">
            <div className="p-3.5 rounded-lg bg-surface-muted border border-border space-y-1.5">
              <div className="font-bold text-primary flex items-center gap-1.5">
                <span className="w-5 h-5 rounded-full bg-primary/10 flex items-center justify-center text-[10px]">1</span>
                Query Normalization
              </div>
              <p className="text-[11px] text-muted leading-relaxed">
                Strips Arabic ligatures, Cyrillic diacritics, and corporate legal noise suffixes (LLC, FZE, Corp, GmbH).
              </p>
            </div>

            <div className="p-3.5 rounded-lg bg-surface-muted border border-border space-y-1.5">
              <div className="font-bold text-primary flex items-center gap-1.5">
                <span className="w-5 h-5 rounded-full bg-primary/10 flex items-center justify-center text-[10px]">2</span>
                Phonetic & DisMax Recall
              </div>
              <p className="text-[11px] text-muted leading-relaxed">
                Executes OpenSearch 2.12 Double Metaphone and edge n-gram queries with in-memory deterministic fallback.
              </p>
            </div>

            <div className="p-3.5 rounded-lg bg-surface-muted border border-border space-y-1.5">
              <div className="font-bold text-primary flex items-center gap-1.5">
                <span className="w-5 h-5 rounded-full bg-primary/10 flex items-center justify-center text-[10px]">3</span>
                Dual String Metric Scoring
              </div>
              <p className="text-[11px] text-muted leading-relaxed">
                Calculates Jaro-Winkler (prefix match) and Damerau-Levenshtein (transposition penalty) token similarities.
              </p>
            </div>

            <div className="p-3.5 rounded-lg bg-surface-muted border border-border space-y-1.5">
              <div className="font-bold text-critical flex items-center gap-1.5">
                <span className="w-5 h-5 rounded-full bg-critical/10 flex items-center justify-center text-[10px]">4</span>
                Statutory Freeze & EDD
              </div>
              <p className="text-[11px] text-muted leading-relaxed">
                Composite threshold evaluation: 100% Exact Freeze, 75%+ EDD Hold, &lt;75% Auto-Release with WORM audit log.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Main Single Entity Search Box Form */}
      <form onSubmit={handleSubmit} className="bg-surface rounded-xl border border-border p-5 space-y-4 shadow-2xs">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-border">
          <div className="flex items-center gap-2">
            <span className="text-xs font-mono font-bold uppercase tracking-wider text-muted">
              Select Search Mode:
            </span>
            <div className="flex flex-wrap items-center gap-1.5">
              {(['EXACT', 'FUZZY', 'EXACT_NAME_AND_ID'] as NameSearchMode[]).map((mode) => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => handleModeChange(mode)}
                  className={`px-3 py-1 rounded-md text-xs font-mono font-bold transition ${
                    searchMode === mode
                      ? 'bg-primary text-white shadow-xs'
                      : 'bg-surface-muted text-muted hover:text-ink border border-border'
                  }`}
                >
                  {mode === 'EXACT' && 'Exact (100%)'}
                  {mode === 'FUZZY' && 'Fuzzy & Phonetic'}
                  {mode === 'EXACT_NAME_AND_ID' && 'Dual-Factor Name + ID'}
                </button>
              ))}
            </div>
          </div>

          <div className="text-[11px] text-muted font-mono">
            {searchMode === 'EXACT' && 'Deterministic alias equality. Zero false positive tolerance.'}
            {searchMode === 'FUZZY' && 'Jaro-Winkler + Levenshtein + Double Metaphone phonetic matching.'}
            {searchMode === 'EXACT_NAME_AND_ID' && 'Validates Official ID / Passport / Trade License.'}
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-end">
          <div className="md:col-span-4">
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-ink">
                Individual / Legal Entity Name
              </label>
              {name && (
                <button
                  type="button"
                  onClick={() => {
                    setName('');
                    if (nameInputRef.current) nameInputRef.current.focus();
                  }}
                  className="text-[10px] text-muted hover:text-critical flex items-center gap-0.5"
                >
                  <X className="w-3 h-3" />
                  Clear
                </button>
              )}
            </div>
            <div className="relative">
              <Search className="w-4 h-4 text-muted absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                ref={nameInputRef}
                type="text"
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder="e.g. Viktor Bout, Tariq Aziz..."
                className="w-full pl-9 pr-8 py-2 text-sm rounded-lg border border-border bg-background text-ink focus:outline-none focus:border-primary font-medium"
              />
            </div>
          </div>

          <div className="md:col-span-3">
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-ink">
                Sanctions UID / Passport / ID
              </label>
            </div>
            <div className="relative">
              <Fingerprint className="w-4 h-4 text-muted absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={idNumber}
                onChange={e => setIdNumber(e.target.value)}
                placeholder="e.g. OFAC-9812, WC-6211256..."
                className="w-full pl-9 pr-8 py-2 text-sm rounded-lg border border-border bg-background text-ink focus:outline-none focus:border-primary font-mono"
              />
            </div>
          </div>

          <div className="md:col-span-2">
            <label className="text-xs font-semibold text-ink block mb-1">
              Birth Year (YOB)
            </label>
            <input
              type="number"
              placeholder="e.g. 1967"
              value={birthYear}
              onChange={e => setBirthYear(e.target.value)}
              className="w-full px-3 py-2 text-sm rounded-lg border border-border bg-background text-ink focus:outline-none focus:border-primary font-mono"
            />
          </div>

          {searchMode === 'FUZZY' ? (
            <div className="md:col-span-1">
              <label className="block text-[11px] font-mono text-muted mb-1 flex items-center justify-between">
                <span>Min</span>
                <span className="font-bold text-ink">{minScore}%</span>
              </label>
              <input
                type="range"
                min={50}
                max={95}
                step={5}
                value={minScore}
                onChange={e => setMinScore(Number(e.target.value))}
                className="w-full accent-primary cursor-pointer"
              />
            </div>
          ) : (
            <div className="md:col-span-1 text-center">
              <label className="block text-[11px] font-mono text-muted mb-1">Threshold</label>
              <span className="text-[10px] font-mono px-2 py-1.5 rounded bg-surface-muted border border-border text-muted block">
                Strict 100%
              </span>
            </div>
          )}

          <div className="md:col-span-2 flex items-center gap-2">
            <button
              type="button"
              onClick={handleReset}
              className="py-2.5 px-3 rounded-lg bg-surface-muted hover:bg-border text-ink font-semibold text-xs transition flex items-center justify-center gap-1.5 border border-border shrink-0"
              title="Clear all fields and reset for next entity (Esc)"
            >
              <RotateCcw className="w-4 h-4 text-muted" />
              <span>Clear</span>
            </button>

            <button
              type="submit"
              disabled={loading}
              className="flex-1 py-2.5 px-3 rounded-lg bg-primary text-white font-semibold text-xs uppercase tracking-wider hover:bg-primary/90 transition flex items-center justify-center gap-2 shadow-sm disabled:opacity-50"
            >
              {loading ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  <span>Searching...</span>
                </>
              ) : (
                <>
                  <Search className="w-4 h-4" />
                  <span>Search</span>
                </>
              )}
            </button>
          </div>
        </div>

        {isQueryDirty && response && (
          <div className="p-2.5 rounded-lg bg-warning/10 border border-warning/30 text-warning text-xs flex items-center justify-between animate-in fade-in duration-100">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>
                Search inputs modified. You are viewing results for previous query: <strong>"{lastExecutedQuery?.name || lastExecutedQuery?.idNumber}"</strong>.
              </span>
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleReset}
                className="px-2 py-0.5 rounded bg-surface text-ink text-[11px] border border-border hover:bg-border/30"
              >
                Clear Previous
              </button>
              <button
                type="button"
                onClick={() => executeSearch()}
                className="px-2.5 py-0.5 rounded bg-primary text-white text-[11px] font-bold hover:bg-primary/90"
              >
                Run Updated Search
              </button>
            </div>
          </div>
        )}

        <div className="pt-2 border-t border-border space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-muted font-mono text-[11px] font-bold uppercase">
              1-Click Test Scenarios:
            </span>
          </div>

          <div className="flex flex-wrap items-center gap-2 text-xs">
            <button
              type="button"
              onClick={() => applyPreset('VIKTOR ANATOLYEVICH BOUT', '', 'EXACT', '1967')}
              className="px-2.5 py-1 rounded-md bg-critical/10 text-critical border border-critical/30 hover:bg-critical/20 font-mono text-[11px] transition flex items-center gap-1"
            >
              <FileWarning className="w-3 h-3" />
              1. Exact Sanction Hit: "Viktor Bout" (1967)
            </button>

            <button
              type="button"
              onClick={() => applyPreset('Victor Bout', '', 'FUZZY', '1968')}
              className="px-2.5 py-1 rounded-md bg-primary/10 text-primary border border-primary/25 hover:bg-primary/20 font-mono text-[11px] transition flex items-center gap-1"
            >
              <Sparkles className="w-3 h-3" />
              2. Fuzzy Typo & YOB ±1yr: "Victor Bout"
            </button>

            <button
              type="button"
              onClick={() => applyPreset('Viktor Anatolyevich Bout', 'OFAC-9812', 'EXACT_NAME_AND_ID', '1967')}
              className="px-2.5 py-1 rounded-md bg-warning/15 text-warning border border-warning/30 hover:bg-warning/25 font-mono text-[11px] transition flex items-center gap-1"
            >
              <Fingerprint className="w-3 h-3" />
              3. Dual-Factor: "Viktor Bout" + "OFAC-9812"
            </button>

            <button
              type="button"
              onClick={() => applyPreset('Fatima Salem Al-Nuaimi', '', 'EXACT', '1985')}
              className="px-2.5 py-1 rounded-md bg-accent/15 text-accent border border-accent/30 hover:bg-accent/25 font-mono text-[11px] transition flex items-center gap-1"
            >
              <CheckCircle2 className="w-3 h-3" />
              4. Clean Entity: "Fatima Al-Nuaimi" (0 Hits)
            </button>

            <button
              type="button"
              onClick={() => applyPreset('Mansoor Mohammed Al-Marzooqi', '', 'FUZZY', '1975')}
              className="px-2.5 py-1 rounded-md bg-purple-500/10 text-purple-700 border border-purple-400/30 hover:bg-purple-500/20 font-mono text-[11px] transition flex items-center gap-1"
            >
              <Users className="w-3 h-3" />
              5. PEP Registry: "Mansoor Al-Marzooqi"
            </button>

            <button
              type="button"
              onClick={() => applyPreset('Al-Quds Logistics LLC', 'TL-DXB-889120', 'EXACT_NAME_AND_ID')}
              className="px-2.5 py-1 rounded-md bg-surface-muted text-ink border border-border hover:bg-border/40 font-mono text-[11px] transition flex items-center gap-1"
            >
              <Building className="w-3 h-3" />
              6. Corporate & Trade Lic: "Al-Quds Logistics"
            </button>
          </div>
        </div>
      </form>

      {error && (
        <div className="p-4 rounded-xl bg-critical/10 border border-critical text-critical text-xs font-medium flex items-center justify-between">
          <div className="flex items-center gap-2">
            <ShieldAlert className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
          <button
            type="button"
            onClick={() => setError(null)}
            className="text-critical hover:text-ink p-1"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Results Section */}
      {response && (
        <div className="space-y-4 animate-in fade-in duration-150">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 bg-surface p-4 rounded-xl border border-border shadow-2xs">
            <div className="flex items-center gap-3 flex-wrap">
              <span className={`px-3 py-1 rounded-md font-mono font-bold text-xs ${
                simulatedVerdict === 'BLOCKED'
                  ? 'bg-critical/15 text-critical border border-critical/30'
                  : simulatedVerdict === 'HELD'
                  ? 'bg-warning/15 text-warning border border-warning/30'
                  : 'bg-accent/15 text-accent border border-accent/30'
              }`}>
                {simulatedVerdict === 'CLEARED'
                  ? '0 MATCHES (CLEAN / PASS)'
                  : `${filteredMatches.length} MATCHES (${simulatedVerdict})`}
              </span>

              <div className="text-xs text-muted font-mono">
                Mode: <strong className="text-ink">{response.searchMode}</strong>
                {response.normalizedQueryName && (
                  <> • Query: <strong className="text-primary">"{response.normalizedQueryName}"</strong></>
                )}
                {response.queryId && (
                  <> • ID Filter: <strong className="text-primary">"{response.queryId}"</strong></>
                )}
                {response.queryBirthYear && (
                  <> • YOB: <strong className="text-primary">{response.queryBirthYear}</strong></>
                )}
                <span className="ml-2 text-primary font-bold">({response.latencyMs}ms)</span>
              </div>
            </div>

            {/* Engine 5: Client-Side "What-If" Sensitivity Slider */}
            <div className="flex items-center gap-4 flex-wrap">
              <div className="flex items-center gap-2 px-3 py-1 rounded-lg bg-surface-muted border border-border">
                <Sliders className="w-3.5 h-3.5 text-primary shrink-0" />
                <span className="text-[10px] font-mono text-muted uppercase font-bold">
                  What-If Threshold:
                </span>
                <input
                  type="range"
                  min={50}
                  max={95}
                  step={5}
                  value={clientThreshold}
                  onChange={e => setClientThreshold(Number(e.target.value))}
                  className="w-20 accent-primary cursor-pointer"
                  title="Simulate sensitivity filter in real time"
                />
                <span className="text-xs font-mono font-bold text-primary w-8 text-right">
                  {clientThreshold}%
                </span>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={handleExportCertificate}
                  disabled={certifying}
                  className="px-3 py-1.5 rounded-lg bg-surface-muted text-ink border border-border hover:bg-border/40 font-mono text-xs font-bold transition flex items-center gap-1.5"
                  title="Generate cryptographically signed WORM regulatory screening certificate"
                >
                  <FileText className="w-3.5 h-3.5 text-primary" />
                  <span>{certifying ? 'Signing...' : 'WORM Certificate'}</span>
                </button>

                <button
                  type="button"
                  onClick={copyAuditNote}
                  className="px-3 py-1.5 rounded-lg bg-surface-muted text-ink border border-border hover:bg-border/40 font-mono text-xs font-bold transition flex items-center gap-1.5"
                >
                  {copiedAuditNote ? <Check className="w-3.5 h-3.5 text-accent" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{copiedAuditNote ? 'Copied' : 'Audit Note'}</span>
                </button>

                <button
                  type="button"
                  onClick={handleReset}
                  className="px-3 py-1.5 rounded-lg bg-primary text-white font-mono text-xs font-bold hover:bg-primary/90 transition flex items-center gap-1.5 shadow-xs"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                  <span>Screen Next</span>
                </button>
              </div>
            </div>
          </div>

          {/* Engine 6: PEP Tiering & Adverse Media Disaggregation Tab Bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 bg-surface p-3 rounded-xl border border-border">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="text-muted font-mono text-[11px] font-bold uppercase flex items-center gap-1 mr-1">
                <Layers className="w-3.5 h-3.5 text-primary" /> HIT TIERS:
              </span>

              <button
                type="button"
                onClick={() => setActiveTierTab('ALL')}
                className={`px-3 py-1 rounded-lg text-xs font-mono font-bold transition flex items-center gap-1.5 ${
                  activeTierTab === 'ALL'
                    ? 'bg-primary text-white shadow-xs'
                    : 'bg-surface-muted text-muted hover:text-ink border border-border'
                }`}
              >
                <span>All Hits</span>
                <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-black/20 text-white font-bold">
                  {tierCounts.all}
                </span>
              </button>

              <button
                type="button"
                onClick={() => setActiveTierTab('SANCTIONS')}
                className={`px-3 py-1 rounded-lg text-xs font-mono font-bold transition flex items-center gap-1.5 ${
                  activeTierTab === 'SANCTIONS'
                    ? 'bg-critical text-white shadow-xs'
                    : 'bg-surface-muted text-muted hover:text-critical border border-border'
                }`}
              >
                <FileWarning className="w-3 h-3" />
                <span>Tier 1: Sanctions</span>
                <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-black/20 text-white font-bold">
                  {tierCounts.sanctions}
                </span>
              </button>

              <button
                type="button"
                onClick={() => setActiveTierTab('PEP')}
                className={`px-3 py-1 rounded-lg text-xs font-mono font-bold transition flex items-center gap-1.5 ${
                  activeTierTab === 'PEP'
                    ? 'bg-purple-600 text-white shadow-xs'
                    : 'bg-surface-muted text-muted hover:text-purple-600 border border-border'
                }`}
              >
                <Users className="w-3 h-3" />
                <span>Tier 2: PEP & Reg. EDD</span>
                <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-black/20 text-white font-bold">
                  {tierCounts.pep}
                </span>
              </button>

              <button
                type="button"
                onClick={() => setActiveTierTab('ADVERSE_MEDIA')}
                className={`px-3 py-1 rounded-lg text-xs font-mono font-bold transition flex items-center gap-1.5 ${
                  activeTierTab === 'ADVERSE_MEDIA'
                    ? 'bg-warning text-ink shadow-xs'
                    : 'bg-surface-muted text-muted hover:text-warning border border-border'
                }`}
              >
                <Globe className="w-3 h-3" />
                <span>Tier 3: Adverse Media</span>
                <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-black/20 text-ink font-bold">
                  {tierCounts.media}
                </span>
              </button>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-[11px] font-mono text-muted uppercase">Entity:</span>
              <select
                value={entityTypeFilter}
                onChange={e => setEntityTypeFilter(e.target.value as any)}
                className="px-2.5 py-1 text-xs font-mono rounded-lg border border-border bg-background text-ink focus:outline-none"
              >
                <option value="ALL">All Types</option>
                <option value="INDIVIDUAL">Individuals Only</option>
                <option value="CORPORATE">Corporate Entities Only</option>
              </select>
            </div>
          </div>

          {filteredMatches.length === 0 ? (
            <div className="bg-surface rounded-xl border border-border p-8 text-center space-y-3">
              <div className="w-12 h-12 rounded-full bg-accent/10 border border-accent/30 text-accent flex items-center justify-center mx-auto">
                <CheckCircle2 className="w-6 h-6" />
              </div>
              <h3 className="text-base font-bold text-ink">
                Zero Adverse Matches Above Threshold ({clientThreshold}%)
              </h3>
              <p className="text-xs text-muted max-w-md mx-auto">
                No sanctions hits, PEP designations, or adverse records exceeded the active sensitivity threshold. Entity cleared for straight-through clearing.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {filteredMatches.map((match: NameSearchMatch, index: number) => {
                const cardKey = `${match.externalId}-${match.fullName}-${index}`;
                const isDiagramOpen = activeDiagramEntityId === cardKey;
                const isDiffOpen = activeDiffEntityId === cardKey;
                const isPep = match.category === 'PEP' || match.sanctionsList.includes('PEP');
                const isBorderline = match.score >= 70 && match.score <= 88;

                return (
                  <div
                    key={cardKey}
                    className="bg-surface rounded-xl border border-border overflow-hidden shadow-2xs hover:border-primary/40 transition"
                  >
                    <div className="p-5 flex flex-col lg:flex-row justify-between gap-4 border-b border-border/60">
                      <div className="space-y-2">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="px-2 py-0.5 rounded text-[11px] font-mono font-bold bg-primary/10 text-primary border border-primary/25">
                            #{index + 1}
                          </span>
                          <h3 className="text-base font-bold text-ink font-mono">
                            {match.fullName}
                          </h3>
                          <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-surface-muted border border-border text-ink">
                            UID: {match.externalId}
                          </span>
                          <span className={`px-2.5 py-0.5 rounded text-[10px] font-mono font-bold border ${getListBadgeStyle(match.sanctionsList)}`}>
                            LIST: {match.sanctionsList}
                          </span>
                          <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold ${
                            match.isBlocked
                              ? 'bg-critical text-white'
                              : isPep
                              ? 'bg-purple-600 text-white'
                              : 'bg-warning/20 text-warning border border-warning/40'
                          }`}>
                            {match.isBlocked
                              ? 'STATUTORY BLOCK / FREEZE'
                              : isPep
                              ? 'PEP HIT — EDD REQUIRED'
                              : 'EDD REVIEW REQUIRED'}
                          </span>

                          {/* Secondary Identifier Quick Badges */}
                          {match.yobDelta !== undefined && match.yobDelta !== null && (
                            <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ${
                              match.yobDelta === 0
                                ? 'bg-critical/15 text-critical border-critical/30'
                                : match.yobDelta <= 2
                                ? 'bg-warning/15 text-warning border-warning/30'
                                : 'bg-accent/15 text-accent border-accent/30'
                            }`}>
                              YOB Delta: {match.yobDelta === 0 ? 'Exact Year Match' : `±${match.yobDelta} yrs`}
                            </span>
                          )}

                          {match.isIdMatched && (
                            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-critical/20 text-critical border border-critical/40 flex items-center gap-1">
                              <Fingerprint className="w-3 h-3" />
                              Passport / ID Verified
                            </span>
                          )}
                        </div>

                        <div className="flex flex-wrap items-center gap-4 text-xs text-muted font-mono">
                          <span className="flex items-center gap-1">
                            {match.entityType === 'CORPORATE' ? <Building className="w-3.5 h-3.5 text-muted" /> : <User className="w-3.5 h-3.5 text-muted" />}
                            Type: <strong className="text-ink">{match.entityType}</strong>
                          </span>
                          <span>Category: <strong className={isPep ? 'text-purple-600' : 'text-critical'}>{match.category}</strong></span>
                          {match.nationality && <span>Nationality: <strong className="text-ink">{match.nationality}</strong></span>}
                          {match.dob && <span>DOB: <strong className="text-ink">{match.dob}</strong></span>}
                          {match.aliasName && <span>Aliases: <strong className="text-ink">{match.aliasName}</strong></span>}
                        </div>
                      </div>

                      <div className="flex flex-col sm:flex-row items-end sm:items-center gap-3 shrink-0">
                        <div className="text-right font-mono">
                          <div className="text-[10px] uppercase text-muted">Match Confidence</div>
                          <div className={`text-xl font-bold ${
                            match.score >= 90 ? 'text-critical' : match.score >= 75 ? 'text-warning' : 'text-primary'
                          }`}>
                            {match.score.toFixed(1)}%
                          </div>
                          <div className="text-[10px] text-muted">
                            Str: {match.stringSimilarity}% • Phon: {match.phoneticSimilarity}%
                          </div>
                        </div>

                        {/* Action Buttons: Explain Diagram, Side-by-Side Diff, 1-Click Intercept, Add Whitelist */}
                        <div className="flex items-center gap-2 flex-wrap">
                          <button
                            type="button"
                            onClick={() => toggleDiffInspector(cardKey)}
                            className={`px-3 py-2 rounded-lg text-xs font-mono font-bold border transition flex items-center gap-1.5 ${
                              isDiffOpen
                                ? 'bg-primary text-white border-primary shadow-xs'
                                : 'bg-surface-muted text-ink border-border hover:bg-border/40'
                            }`}
                            title="Side-by-side comparative inspection between query and record"
                          >
                            <Columns className="w-3.5 h-3.5" />
                            <span>{isDiffOpen ? 'Hide Diff' : 'Side-by-Side Diff'}</span>
                          </button>

                          <button
                            type="button"
                            onClick={() => toggleMatchDiagram(cardKey)}
                            className={`px-3 py-2 rounded-lg text-xs font-mono font-bold border transition flex items-center gap-1.5 ${
                              isDiagramOpen
                                ? 'bg-primary text-white border-primary shadow-xs'
                                : 'bg-primary/10 text-primary border-primary/30 hover:bg-primary/20'
                            }`}
                          >
                            <GitBranch className="w-3.5 h-3.5" />
                            <span>{isDiagramOpen ? 'Hide' : 'Explain'}</span>
                            {isDiagramOpen ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                          </button>

                          <button
                            type="button"
                            onClick={() => handleInterceptCase(match)}
                            className="px-3 py-2 rounded-lg text-xs font-mono font-bold bg-critical/15 text-critical border border-critical/30 hover:bg-critical/25 transition flex items-center gap-1.5"
                            title="Provision persistent AML case in 3-Pane workspace"
                          >
                            <FileWarning className="w-3.5 h-3.5" />
                            <span>1-Click Intercept</span>
                          </button>

                          {isBorderline && (
                            <button
                              type="button"
                              onClick={() => setWhitelistModalMatch(match)}
                              className="px-3 py-2 rounded-lg text-xs font-mono font-bold bg-accent/15 text-accent border border-accent/30 hover:bg-accent/25 transition flex items-center gap-1.5"
                              title="1-Click False-Positive Exception Whitelist Approval"
                            >
                              <PlusCircle className="w-3.5 h-3.5" />
                              <span>Whitelist Exemption</span>
                            </button>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* UX 4: Side-by-Side Visual Diff Inspector */}
                    {isDiffOpen && (
                      <div className="p-5 bg-surface-muted/80 border-b border-border space-y-3 font-mono text-xs animate-in fade-in duration-150">
                        <div className="flex items-center justify-between">
                          <div className="font-bold text-ink uppercase flex items-center gap-2">
                            <Columns className="w-4 h-4 text-primary" />
                            <span>Side-by-Side Field Alignment & Discrepancy Matrix</span>
                          </div>
                          <span className="text-[10px] text-muted">Record: {match.externalId}</span>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          {/* Left Column: Query Input */}
                          <div className="p-3.5 rounded-lg bg-surface border border-border space-y-2">
                            <div className="text-[10px] font-bold uppercase text-primary border-b border-border pb-1">
                              Search Query Parameters
                            </div>
                            <div className="space-y-1.5">
                              <div>
                                <span className="text-muted text-[11px] block">Full Name:</span>
                                <span className="font-bold text-ink">{response.queryName || 'N/A'}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">ID / Passport Number:</span>
                                <span className="font-bold text-ink">{response.queryId || 'None Specified'}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">Birth Year (YOB):</span>
                                <span className="font-bold text-ink">{response.queryBirthYear || 'Unspecified'}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">Screening Mode:</span>
                                <span className="font-bold text-ink">{response.searchMode}</span>
                              </div>
                            </div>
                          </div>

                          {/* Right Column: Matched Sanctions Profile */}
                          <div className="p-3.5 rounded-lg bg-surface border border-border space-y-2">
                            <div className="text-[10px] font-bold uppercase text-critical border-b border-border pb-1">
                              Watchlist Record ({match.sanctionsList})
                            </div>
                            <div className="space-y-1.5">
                              <div>
                                <span className="text-muted text-[11px] block">Canonical Name:</span>
                                <span className="font-bold text-ink">{match.fullName}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">Target Identifier:</span>
                                <span className="font-bold text-ink">{match.externalId} {match.idDetails ? `(${match.idDetails})` : ''}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">Official DOB / YOB:</span>
                                <span className="font-bold text-ink">{match.dob || 'N/A'}</span>
                              </div>
                              <div>
                                <span className="text-muted text-[11px] block">Nationality / Jurisdiction:</span>
                                <span className="font-bold text-ink">{match.nationality || 'N/A'}</span>
                              </div>
                            </div>
                          </div>
                        </div>

                        {/* Alignment Chips */}
                        <div className="p-2.5 rounded-lg bg-surface border border-border flex items-center justify-between gap-2 flex-wrap">
                          <span className="text-[10px] font-bold uppercase text-muted">Field Alignment:</span>
                          <div className="flex items-center gap-2 flex-wrap text-[11px]">
                            <span className="px-2 py-0.5 rounded bg-primary/10 text-primary border border-primary/30 font-bold">
                              Name: {match.score >= 90 ? 'High Confidence' : `${match.score.toFixed(1)}% Similar`}
                            </span>
                            <span className={`px-2 py-0.5 rounded font-bold border ${
                              match.yobDelta === 0
                                ? 'bg-accent/20 text-accent border-accent/40'
                                : match.yobDelta && match.yobDelta <= 2
                                ? 'bg-warning/20 text-warning border-warning/40'
                                : 'bg-surface-muted text-muted border-border'
                            }`}>
                              DOB/YOB: {match.yobDelta !== undefined ? `Delta ±${match.yobDelta} yrs` : 'Not Compared'}
                            </span>
                            <span className={`px-2 py-0.5 rounded font-bold border ${
                              match.isIdMatched
                                ? 'bg-critical/20 text-critical border-critical/40'
                                : 'bg-surface-muted text-muted border-border'
                            }`}>
                              Identifier: {match.isIdMatched ? 'Exact Verified' : 'No Direct ID Match'}
                            </span>
                          </div>
                        </div>
                      </div>
                    )}

                    <div className="p-5 bg-background/60 grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
                      <div className="p-3.5 rounded-lg bg-surface border border-border space-y-1.5">
                        <div className="flex items-center gap-1.5 text-[10px] font-mono uppercase tracking-wider text-critical font-bold">
                          <Globe className="w-3.5 h-3.5" />
                          <span>Which Sanctions List & Why Listed</span>
                        </div>
                        <div className="font-bold text-ink">{match.sanctionsAuthority}</div>
                        <p className="text-muted leading-relaxed">{match.whySanctioned}</p>
                        {match.idDetails && (
                          <div className="pt-1 text-[11px] font-mono text-ink">
                            <strong>ID / Designation Record:</strong> {match.idDetails}
                          </div>
                        )}
                      </div>

                      <div className="p-3.5 rounded-lg bg-surface border border-border space-y-1.5">
                        <div className="flex items-center gap-1.5 text-[10px] font-mono uppercase tracking-wider text-primary font-bold">
                          <BookOpen className="w-3.5 h-3.5" />
                          <span>Why This Record Matched ({match.matchModeUsed})</span>
                        </div>
                        <div className="font-bold text-ink">{match.technicalBreakdown}</div>
                        <p className="text-muted leading-relaxed">{match.whyMatched}</p>
                      </div>
                    </div>

                    {isDiagramOpen && (
                      <div className="p-5 bg-[#f4f6fb] border-t-2 border-primary/30 space-y-4 animate-in fade-in duration-150">
                        <div className="flex items-center justify-between">
                          <div className="text-xs font-mono font-bold uppercase text-primary flex items-center gap-2">
                            <GitBranch className="w-4 h-4" />
                            <span>Step-by-Step Sanctions Attribution & Match Decision Diagram</span>
                          </div>
                          <span className="text-[10px] font-mono text-muted">
                            Interactive Trace • {match.externalId}
                          </span>
                        </div>

                        <div className="grid grid-cols-1 lg:grid-cols-7 gap-2 items-center text-xs font-mono">
                          <div className="lg:col-span-1 p-3 rounded-xl bg-surface border border-border shadow-2xs space-y-1">
                            <div className="text-[9px] uppercase text-muted font-bold">Step 1: Input</div>
                            <div className="font-bold text-ink truncate" title={response.queryName || response.queryId}>
                              "{response.queryName || response.queryId}"
                            </div>
                            <div className="text-[10px] text-primary">
                              Mode: {match.matchModeUsed}
                            </div>
                          </div>

                          <div className="hidden lg:flex justify-center text-primary">
                            <ArrowRight className="w-5 h-5" />
                          </div>

                          <div className="lg:col-span-1 p-3 rounded-xl bg-surface border border-border shadow-2xs space-y-1">
                            <div className="text-[9px] uppercase text-muted font-bold">Step 2: Normalized</div>
                            <div className="font-bold text-ink truncate" title={match.normalizedTargetName}>
                              "{match.normalizedTargetName}"
                            </div>
                            <div className="text-[10px] text-muted">
                              UID: {match.externalId}
                            </div>
                          </div>

                          <div className="hidden lg:flex justify-center text-primary">
                            <ArrowRight className="w-5 h-5" />
                          </div>

                          <div className="lg:col-span-1 p-3 rounded-xl bg-surface border border-primary/40 shadow-2xs space-y-1">
                            <div className="text-[9px] uppercase text-primary font-bold">Step 3: Scoring</div>
                            <div className="font-bold text-ink">
                              Composite: {match.score.toFixed(1)}%
                            </div>
                            <div className="text-[10px] text-muted">
                              String {match.stringSimilarity}% | Phonetic {match.phoneticSimilarity}%
                            </div>
                          </div>

                          <div className="hidden lg:flex justify-center text-primary">
                            <ArrowRight className="w-5 h-5" />
                          </div>

                          <div className={`lg:col-span-1 p-3 rounded-xl border shadow-2xs space-y-1 ${
                            match.isBlocked ? 'bg-critical/10 border-critical/40' : isPep ? 'bg-purple-500/10 border-purple-400/40' : 'bg-warning/10 border-warning/40'
                          }`}>
                            <div className="text-[9px] uppercase font-bold flex items-center gap-1 text-ink">
                              <FileWarning className="w-3 h-3" />
                              Step 4: Action
                            </div>
                            <div className="font-bold text-ink truncate" title={match.sanctionsList}>
                              {match.sanctionsList}
                            </div>
                            <div className="text-[10px] font-semibold text-critical">
                              {match.isBlocked ? 'Statutory Freeze' : isPep ? 'PEP EDD Sign-off' : 'EDD Escalation'}
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}

              <div className="flex items-center justify-between p-4 bg-surface rounded-xl border border-border">
                <div className="text-xs text-muted font-mono">
                  Finished reviewing entity? Reset all inputs to screen the next customer.
                </div>
                <button
                  type="button"
                  onClick={handleReset}
                  className="px-4 py-2 rounded-lg bg-primary hover:bg-primary/90 text-white font-semibold text-xs flex items-center gap-2 shadow-xs transition"
                >
                  <RotateCcw className="w-4 h-4" />
                  <span>Clear & Screen Next Entity</span>
                  <ArrowRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* 1-Click Whitelist Exception Modal */}
      {whitelistModalMatch && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-50 p-4 font-mono">
          <div className="bg-surface rounded-xl border border-border shadow-xl max-w-lg w-full p-6 space-y-4 animate-in fade-in duration-150">
            <div className="flex justify-between items-center border-b border-border pb-3">
              <div className="flex items-center gap-2">
                <ShieldCheck className="w-5 h-5 text-accent" />
                <h3 className="text-sm font-bold text-ink">
                  1-Click False-Positive Whitelist Approval
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setWhitelistModalMatch(null)}
                className="text-muted hover:text-ink"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleWhitelistSubmit} className="space-y-3 text-xs">
              <div className="p-3 bg-surface-muted rounded-lg border border-border space-y-1">
                <div className="text-muted text-[11px]">Target Entity & UID:</div>
                <div className="font-bold text-ink">{whitelistModalMatch.fullName} ({whitelistModalMatch.externalId})</div>
                <div className="text-[11px] text-primary">Score: {whitelistModalMatch.score.toFixed(1)}% ({whitelistModalMatch.sanctionsList})</div>
              </div>

              <div>
                <label className="text-muted block mb-1">Match Scope</label>
                <select
                  value={whitelistScope}
                  onChange={e => setWhitelistScope(e.target.value)}
                  className="w-full bg-surface-muted border border-border rounded px-3 py-1.5 text-ink focus:outline-none focus:border-primary"
                >
                  <option value="EXACT">EXACT (Exact Name/Identifier suppression)</option>
                  <option value="FUZZY">FUZZY (Suppresses variations within score margin)</option>
                  <option value="CUSTOMER_REF">CUSTOMER_REF (Account-level exemption)</option>
                </select>
              </div>

              <div>
                <label className="text-muted block mb-1">Statutory Rationale for 12-Month Exemption *</label>
                <textarea
                  required
                  rows={3}
                  value={whitelistReason}
                  onChange={e => setWhitelistReason(e.target.value)}
                  className="w-full bg-surface-muted border border-border rounded p-2.5 text-ink focus:outline-none focus:border-primary"
                />
              </div>

              <div className="p-2.5 rounded bg-surface-muted border border-border text-[11px] text-muted">
                <strong>MLRO Policy Note:</strong> Pass 5 bypass policy will suppress alerts for 12 months. Requires annual MLRO recertification under FATF Rec. 11.
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-border">
                <button
                  type="button"
                  onClick={() => setWhitelistModalMatch(null)}
                  className="px-3 py-1.5 rounded bg-surface-muted text-muted hover:text-ink text-xs"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={whitelistSubmitting}
                  className="px-4 py-1.5 rounded bg-accent text-white font-bold text-xs hover:bg-accent/90 flex items-center gap-1.5 shadow-xs"
                >
                  <Check className="w-3.5 h-3.5" />
                  <span>{whitelistSubmitting ? 'Registering...' : 'Approve 12-Mo Whitelist'}</span>
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Regulatory Screening Certificate Proof Modal */}
      {certificateData && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-xs flex items-center justify-center z-50 p-4 font-mono">
          <div className="bg-surface rounded-xl border border-border shadow-2xl max-w-2xl w-full p-6 space-y-4 animate-in fade-in duration-150">
            <div className="flex justify-between items-center border-b border-border pb-3">
              <div className="flex items-center gap-2">
                <FileText className="w-5 h-5 text-primary" />
                <h3 className="text-sm font-bold text-ink">
                  Regulatory Screening Audit Certificate (WORM Proof)
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setCertificateData(null)}
                className="text-muted hover:text-ink"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-4 bg-background rounded-lg border border-border space-y-3 text-xs leading-relaxed">
              <div className="flex justify-between items-center border-b border-border/80 pb-2">
                <div>
                  <span className="text-[10px] text-muted">CERTIFICATE ID:</span>
                  <div className="font-bold text-primary">{certificateData.certificateId}</div>
                </div>
                <div className="text-right">
                  <span className="text-[10px] text-muted">TIMESTAMP (UTC):</span>
                  <div className="font-bold text-ink">{certificateData.issuedAtUtc}</div>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <span className="text-muted text-[11px]">Subject Screened:</span>
                  <div className="font-bold text-ink">{certificateData.subjectName}</div>
                </div>
                <div>
                  <span className="text-muted text-[11px]">Decision & Score:</span>
                  <div className={`font-bold ${certificateData.decision === 'HELD' ? 'text-critical' : 'text-accent'}`}>
                    {certificateData.decision} ({certificateData.overallScore.toFixed(1)}%)
                  </div>
                </div>
              </div>

              <div>
                <span className="text-muted text-[11px]">5-Pass Logs & Latency ({certificateData.latencyMs}ms):</span>
                <div className="mt-1 space-y-1">
                  {certificateData.passLogs.map(p => (
                    <div key={p.passNumber} className="flex justify-between text-[11px] p-1 rounded bg-surface-muted border border-border">
                      <span className="font-bold text-primary">Pass {p.passNumber}: {p.matchType}</span>
                      <span className="text-muted">{p.details}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="p-2.5 rounded bg-surface-muted border border-border space-y-1">
                <span className="text-[10px] text-muted uppercase font-bold">Cryptographic WORM Proof (SHA-256):</span>
                <div className="font-mono text-[10px] text-accent break-all">
                  {certificateData.wormBlockHashSha256}
                </div>
              </div>

              <p className="text-[10px] text-muted italic">
                {certificateData.regulatoryAttestation}
              </p>
            </div>

            <div className="flex justify-end gap-2 pt-2 border-t border-border">
              <button
                type="button"
                onClick={() => {
                  const blob = new Blob([JSON.stringify(certificateData, null, 2)], { type: 'application/json' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = `${certificateData.certificateId}.json`;
                  a.click();
                  URL.revokeObjectURL(url);
                }}
                className="px-3.5 py-1.5 rounded bg-surface-muted text-ink border border-border hover:bg-border/30 text-xs font-bold flex items-center gap-1.5"
              >
                <Download className="w-3.5 h-3.5" />
                <span>Download JSON Proof</span>
              </button>
              <button
                type="button"
                onClick={() => window.print()}
                className="px-4 py-1.5 rounded bg-primary text-white text-xs font-bold hover:bg-primary/90 flex items-center gap-1.5 shadow-xs"
              >
                <FileText className="w-3.5 h-3.5" />
                <span>Print Certificate</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
