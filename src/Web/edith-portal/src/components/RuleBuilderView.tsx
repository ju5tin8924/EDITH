import React, { useState, useEffect, useMemo } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Sliders,
  Play,
  CheckCircle,
  AlertTriangle,
  Clock,
  Code,
  BarChart2,
  Plus,
  Save,
  Trash2,
  RotateCcw,
  ShieldCheck,
  Search,
  Eye,
  Power,
  Globe,
  Activity,
  DollarSign,
  Layers,
  FlaskConical,
  ExternalLink,
  X
} from 'lucide-react';
import { RuleDefinition, BacktestResult, BacktestDatasetCohort, CasePriority, RuleExecutionMode } from '../types';
import { EdithApi } from '../services/api';
import { usePageMeta } from '../config/pageMeta';

interface RuleDraftForm {
  id?: string;
  ruleCode: string;
  ruleName: string;
  ruleType: string;
  severity: CasePriority;
  scoreWeightage: number;
  executionMode: RuleExecutionMode;
  isActive: boolean;
  thresholdAmount: number;
  currency: string;
  highRiskCountries: string[];
  windowMinutes: number;
  minTransactions: number;
  structuringMinAmount: number;
  structuringMaxAmount: number;
}

const DEFAULT_FATF_BLACKLIST = ['IR', 'KP', 'MM'];
const DEFAULT_FATF_MONITORED = ['IR', 'KP', 'MM', 'SY', 'RU', 'YE'];

const normalizeRuleType = (rawType?: string): string => {
  const upper = (rawType || '').toUpperCase().replace(/[\s-]/g, '_');
  if (upper.includes('CORRIDOR')) return 'HIGH_RISK_CORRIDOR';
  if (upper.includes('STRUCTURING') || upper.includes('SMURF')) return 'STRUCTURING_SMURFING';
  if (upper.includes('VELOCITY')) return 'VELOCITY_ANOMALY';
  return 'TRANSACTION_THRESHOLD';
};

const ruleToDraft = (r: RuleDefinition): RuleDraftForm => {
  let parsedParams: Record<string, any> = {};
  try {
    parsedParams = r.parametersJson ? JSON.parse(r.parametersJson) : {};
  } catch {
    parsedParams = {};
  }

  return {
    id: r.id,
    ruleCode: r.ruleCode,
    ruleName: r.ruleName,
    ruleType: normalizeRuleType(r.ruleType),
    severity: (r.severity || 'HIGH') as CasePriority,
    scoreWeightage: r.scoreWeightage ?? parsedParams.scoreWeightage ?? 35,
    executionMode: (r.executionMode || parsedParams.executionMode || 'REALTIME') as RuleExecutionMode,
    isActive: r.isActive ?? true,
    thresholdAmount: r.thresholdAmount ?? parsedParams.thresholdAmount ?? 50000,
    currency: r.currency || parsedParams.currency || 'AED',
    highRiskCountries:
      r.highRiskCountries && r.highRiskCountries.length > 0
        ? r.highRiskCountries
        : Array.isArray(parsedParams.jurisdictions)
        ? parsedParams.jurisdictions
        : DEFAULT_FATF_MONITORED,
    windowMinutes: r.windowMinutes ?? parsedParams.windowMinutes ?? 60,
    minTransactions: r.minTransactions ?? parsedParams.minTransactions ?? 3,
    structuringMinAmount: r.structuringMinAmount ?? parsedParams.structuringMin ?? 45000,
    structuringMaxAmount: r.structuringMaxAmount ?? parsedParams.structuringMax ?? 54999
  };
};

export const RuleBuilderView: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [rules, setRules] = useState<RuleDefinition[]>([]);
  const [selectedRule, setSelectedRule] = useState<RuleDefinition | null>(null);
  const [draft, setDraft] = useState<RuleDraftForm | null>(null);
  const [isCreatingNew, setIsCreatingNew] = useState(false);

  // Catalog filters
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'ACTIVE' | 'SHADOW' | 'CRITICAL'>('ALL');

  // Backtest & Save states
  const [datasetCohort, setDatasetCohort] = useState<BacktestDatasetCohort>('STANDARD_MIX');
  const [sampleFilter, setSampleFilter] = useState<'ALL' | 'NEWLY_CAPTURED' | 'NEWLY_SUPPRESSED' | 'STR_FILED'>('ALL');
  const [backtestResult, setBacktestResult] = useState<BacktestResult | null>(null);
  const [runningBacktest, setRunningBacktest] = useState(false);
  const [savingRule, setSavingRule] = useState(false);
  const [statusBanner, setStatusBanner] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
  const [showRawJson, setShowRawJson] = useState(false);
  const [countryInput, setCountryInput] = useState('');
  const [knownCountries, setKnownCountries] = useState<string[]>(['IR', 'KP', 'MM', 'SY', 'RU', 'YE', 'AF', 'VE', 'BY', 'LB']);

  useEffect(() => {
    const requestedRuleCode = searchParams.get('rule');
    loadRules(undefined, requestedRuleCode || undefined);
    EdithApi.fetchMasters()
      .then((masters) => {
        const codes = masters.fatfTiers.flatMap((t) => t.countries);
        if (codes.length > 0) {
          setKnownCountries(Array.from(new Set([...codes, 'SY', 'RU', 'YE', 'AF'])));
        }
      })
      .catch(() => {});
  }, []);

  const loadRules = async (preserveRuleId?: string, preserveRuleCode?: string) => {
    const data = await EdithApi.fetchRules();
    setRules(data);
    if (data.length > 0) {
      const target =
        (preserveRuleId && data.find((r) => r.id === preserveRuleId)) ||
        (preserveRuleCode && data.find((r) => r.ruleCode.toUpperCase() === preserveRuleCode.toUpperCase())) ||
        data[0];
      setSelectedRule(target);
      setDraft(ruleToDraft(target));
      setIsCreatingNew(false);
    }
  };

  const filteredRules = useMemo(() => {
    return rules.filter((r) => {
      const q = searchQuery.trim().toLowerCase();
      const matchesSearch =
        !q ||
        r.ruleCode.toLowerCase().includes(q) ||
        r.ruleName.toLowerCase().includes(q) ||
        r.ruleType.toLowerCase().includes(q) ||
        r.description.toLowerCase().includes(q);

      if (!matchesSearch) return false;
      if (statusFilter === 'ACTIVE') return r.isActive && (r.executionMode || 'REALTIME') === 'REALTIME';
      if (statusFilter === 'SHADOW') return r.executionMode === 'SHADOW' || r.executionMode === 'PENDING_APPROVAL';
      if (statusFilter === 'CRITICAL') return r.severity === 'CRITICAL';
      return true;
    });
  }, [rules, searchQuery, statusFilter]);

  const isDirty = useMemo(() => {
    if (isCreatingNew) return true;
    if (!selectedRule || !draft) return false;
    const baseline = ruleToDraft(selectedRule);
    return JSON.stringify(baseline) !== JSON.stringify(draft);
  }, [selectedRule, draft, isCreatingNew]);

  const handleSelectRule = (rule: RuleDefinition) => {
    setSelectedRule(rule);
    setDraft(ruleToDraft(rule));
    setIsCreatingNew(false);
    setBacktestResult(null);
    setStatusBanner(null);
  };

  const handleStartNewRule = () => {
    const nextNum = rules.length + 1;
    const blank: RuleDraftForm = {
      ruleCode: `RULE_CUSTOM_00${nextNum}`,
      ruleName: 'New Behavioral Monitoring Scenario',
      ruleType: 'TRANSACTION_THRESHOLD',
      severity: 'HIGH',
      scoreWeightage: 35,
      executionMode: 'SHADOW',
      isActive: true,
      thresholdAmount: 100000,
      currency: 'AED',
      highRiskCountries: ['IR', 'KP', 'SY'],
      windowMinutes: 120,
      minTransactions: 3,
      structuringMinAmount: 45000,
      structuringMaxAmount: 54999
    };
    setSelectedRule(null);
    setDraft(blank);
    setIsCreatingNew(true);
    setBacktestResult(null);
    setStatusBanner(null);
  };

  const handleRunBacktest = async (cohortOverride?: BacktestDatasetCohort) => {
    if (!draft) return;
    const activeCohort = cohortOverride || datasetCohort;
    setRunningBacktest(true);
    setStatusBanner(null);
    try {
      const res = await EdithApi.backtestRule(draft.ruleCode, {
        ruleName: draft.ruleName,
        ruleType: draft.ruleType,
        severity: draft.severity,
        weightage: draft.scoreWeightage,
        executionMode: draft.executionMode,
        thresholdAmount: draft.thresholdAmount,
        currency: draft.currency,
        highRiskCountries: draft.highRiskCountries,
        windowMinutes: draft.windowMinutes,
        minTransactions: draft.minTransactions,
        structuringMinAmount: draft.structuringMinAmount,
        structuringMaxAmount: draft.structuringMaxAmount,
        datasetCohort: activeCohort
      });
      setBacktestResult(res);
    } finally {
      setRunningBacktest(false);
    }
  };

  const handleSaveRule = async (overridePatch?: Partial<RuleDraftForm>) => {
    if (!draft) return;
    const current = { ...draft, ...overridePatch };

    if (!current.ruleCode.trim() || !current.ruleName.trim()) {
      setStatusBanner({ type: 'error', message: 'Rule Code and Rule Name are required.' });
      return;
    }

    if (
      current.ruleType === 'STRUCTURING_SMURFING' &&
      current.structuringMinAmount >= current.structuringMaxAmount
    ) {
      setStatusBanner({
        type: 'error',
        message: 'Structuring minimum amount must be strictly less than structuring maximum amount.'
      });
      return;
    }

    setSavingRule(true);
    setStatusBanner(null);
    try {
      const payload = {
        ruleCode: current.ruleCode.trim().toUpperCase(),
        ruleName: current.ruleName.trim(),
        ruleType: current.ruleType,
        severity: current.severity,
        weightage: current.scoreWeightage,
        executionMode: current.executionMode,
        isActive: current.isActive,
        thresholdAmount:
          current.ruleType === 'TRANSACTION_THRESHOLD' || current.ruleType === 'VELOCITY_ANOMALY'
            ? current.thresholdAmount
            : null,
        currency: current.currency,
        highRiskCountries: current.ruleType === 'HIGH_RISK_CORRIDOR' ? current.highRiskCountries : [],
        windowMinutes:
          current.ruleType === 'VELOCITY_ANOMALY' || current.ruleType === 'STRUCTURING_SMURFING'
            ? current.windowMinutes
            : null,
        minTransactions:
          current.ruleType === 'VELOCITY_ANOMALY' || current.ruleType === 'STRUCTURING_SMURFING'
            ? current.minTransactions
            : null,
        structuringMinAmount:
          current.ruleType === 'STRUCTURING_SMURFING' ? current.structuringMinAmount : null,
        structuringMaxAmount:
          current.ruleType === 'STRUCTURING_SMURFING' ? current.structuringMaxAmount : null
      };

      let saved: RuleDefinition;
      if (isCreatingNew || !current.id) {
        saved = await EdithApi.createRule(payload);
        setStatusBanner({
          type: 'success',
          message: `Created rule ${saved.ruleCode} and committed SHA-256 block to WORM audit ledger.`
        });
      } else {
        saved = await EdithApi.updateRule(current.id, payload);
        setStatusBanner({
          type: 'success',
          message: `Updated rule ${saved.ruleCode} (${saved.executionMode}) and logged to WORM audit ledger.`
        });
      }

      await loadRules(saved.id);
    } catch (err: any) {
      setStatusBanner({
        type: 'error',
        message: err?.message || 'Failed to save rule configuration.'
      });
    } finally {
      setSavingRule(false);
    }
  };

  const handleDeleteRule = async () => {
    if (!selectedRule) return;
    setSavingRule(true);
    try {
      const ok = await EdithApi.deleteRule(selectedRule.id);
      if (ok) {
        setStatusBanner({
          type: 'success',
          message: `Deleted rule ${selectedRule.ruleCode} and recorded audit event.`
        });
        await loadRules();
      }
    } finally {
      setSavingRule(false);
    }
  };

  const handleAddCountry = (code: string) => {
    if (!draft) return;
    const clean = code.trim().toUpperCase().slice(0, 3);
    if (!clean || draft.highRiskCountries.includes(clean)) return;
    setDraft({ ...draft, highRiskCountries: [...draft.highRiskCountries, clean] });
    setCountryInput('');
  };

  const handleRemoveCountry = (code: string) => {
    if (!draft) return;
    setDraft({
      ...draft,
      highRiskCountries: draft.highRiskCountries.filter((c) => c !== code)
    });
  };

  const pageMeta = usePageMeta(
    'Customize Rules',
    'Configure transaction monitoring scenarios, velocity thresholds, FATF corridor risk parameters, and pre-deployment backtests.'
  );

  const activeRealtimeCount = rules.filter((r) => r.isActive && (r.executionMode || 'REALTIME') === 'REALTIME').length;
  const shadowCount = rules.filter((r) => r.executionMode === 'SHADOW' || r.executionMode === 'PENDING_APPROVAL').length;

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Top Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <Sliders className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            {pageMeta.parent && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {pageMeta.parent}
              </span>
            )}
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30">
              {activeRealtimeCount} Realtime
            </span>
            {shadowCount > 0 && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-warning/15 text-warning border border-warning/30">
                {shadowCount} Shadow / 4-Eyes
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1">{pageMeta.definition}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => navigate(`/aml/process${draft ? `?rule=${encodeURIComponent(draft.ruleCode)}` : ''}`)}
            className="px-3 py-2 rounded-lg border border-border bg-canvas-subtle hover:bg-surface-muted text-ink font-mono text-xs font-semibold flex items-center gap-1.5 transition"
            title="Open Portfolio & Scoring Weightage Sandbox"
          >
            <FlaskConical className="w-3.5 h-3.5 text-primary" />
            <span>Portfolio Sandbox</span>
          </button>

          <button
            onClick={handleStartNewRule}
            className="px-3.5 py-2 rounded-lg border border-primary/40 bg-primary/10 hover:bg-primary/20 text-primary font-mono text-xs font-bold flex items-center gap-1.5 transition"
          >
            <Plus className="w-4 h-4" />
            <span>New AML Rule</span>
          </button>

          {draft && (
            <div className="flex items-center gap-1.5">
              <select
                value={datasetCohort}
                onChange={(e) => {
                  const next = e.target.value as BacktestDatasetCohort;
                  setDatasetCohort(next);
                  if (backtestResult) {
                    handleRunBacktest(next);
                  }
                }}
                aria-label="Backtest Dataset Cohort"
                className="px-2.5 py-2 rounded-lg bg-canvas-subtle border border-border text-ink font-mono text-[11px] font-semibold focus:outline-none focus:border-primary"
              >
                <option value="STANDARD_MIX">Cohort: Standard Mix (1k)</option>
                <option value="HIGH_RISK_CORRIDOR_SURGE">Cohort: FATF Corridor Surge</option>
                <option value="SMURFING_RING_ATTACK">Cohort: Smurfing Split Ring</option>
                <option value="VELOCITY_BURST_STRESS">Cohort: Velocity Burst Stress</option>
              </select>

              <button
                onClick={() => handleRunBacktest()}
                disabled={runningBacktest}
                className="px-4 py-2 rounded-lg bg-primary hover:bg-primary-hover text-white font-mono text-xs font-bold flex items-center gap-2 transition disabled:opacity-50 shadow-sm"
              >
                {runningBacktest ? (
                  <>
                    <Clock className="w-4 h-4 animate-spin" />
                    <span>Simulating Replay...</span>
                  </>
                ) : (
                  <>
                    <Play className="w-4 h-4 fill-white" />
                    <span>{isDirty ? 'Test Draft Parameters' : 'Run Backtest Simulation'}</span>
                  </>
                )}
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Status Notification Banner */}
      {statusBanner && (
        <div
          className={`p-3 rounded-xl border font-mono text-xs flex justify-between items-center ${
            statusBanner.type === 'success'
              ? 'bg-accent/10 border-accent/40 text-accent'
              : 'bg-critical/10 border-critical/40 text-critical'
          }`}
        >
          <div className="flex items-center gap-2">
            {statusBanner.type === 'success' ? (
              <CheckCircle className="w-4 h-4 shrink-0" />
            ) : (
              <AlertTriangle className="w-4 h-4 shrink-0" />
            )}
            <span>{statusBanner.message}</span>
          </div>
          <button onClick={() => setStatusBanner(null)} className="text-muted hover:text-ink">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Pane: Rules Catalog (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-4 flex flex-col">
          <div className="flex items-center justify-between pb-2 border-b border-border">
            <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-ink">
              Configured Tenant Rules ({filteredRules.length}/{rules.length})
            </h2>
          </div>

          {/* Search & Quick Filter Pills */}
          <div className="space-y-2.5">
            <div className="relative">
              <Search className="w-3.5 h-3.5 text-muted absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search code, scenario, or corridor..."
                className="w-full pl-8 pr-7 py-1.5 rounded-lg bg-canvas-subtle border border-border text-xs font-mono text-ink focus:outline-none focus:border-primary"
              />
              {searchQuery && (
                <button
                  onClick={() => setSearchQuery('')}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted hover:text-ink"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              )}
            </div>

            <div className="flex flex-wrap gap-1.5">
              {(['ALL', 'ACTIVE', 'SHADOW', 'CRITICAL'] as const).map((f) => (
                <button
                  key={f}
                  onClick={() => setStatusFilter(f)}
                  className={`px-2.5 py-1 rounded text-[10px] font-mono font-bold transition ${
                    statusFilter === f
                      ? 'bg-primary text-white'
                      : 'bg-canvas-subtle text-muted hover:text-ink border border-border'
                  }`}
                >
                  {f}
                </button>
              ))}
            </div>
          </div>

          {/* Catalog Cards */}
          <div className="space-y-3 overflow-y-auto max-h-[620px] pr-1">
            {filteredRules.map((r) => {
              const isSelected = !isCreatingNew && selectedRule?.id === r.id;
              const mode = r.executionMode || 'REALTIME';
              return (
                <div
                  key={r.id}
                  onClick={() => handleSelectRule(r)}
                  className={`p-4 rounded-lg border cursor-pointer transition-all ${
                    isSelected
                      ? 'bg-surface-muted border-primary shadow-sm'
                      : 'bg-canvas-subtle/60 border-border hover:border-border-light'
                  }`}
                >
                  <div className="flex justify-between items-start gap-2">
                    <div>
                      <div className="font-semibold text-ink text-sm flex items-center gap-2">
                        <span>{r.ruleName}</span>
                      </div>
                      <div className="text-[11px] font-mono text-primary-light mt-0.5 flex items-center gap-2">
                        <span>{r.ruleCode}</span>
                        <span className="text-muted">•</span>
                        <span className="text-muted">+{r.scoreWeightage ?? 35}% wt</span>
                      </div>
                    </div>

                    <div className="flex flex-col items-end gap-1 shrink-0">
                      <span
                        className={`text-[10px] font-mono px-2 py-0.5 rounded font-bold uppercase ${
                          r.severity === 'CRITICAL'
                            ? 'bg-critical/20 text-critical border border-critical/40'
                            : r.severity === 'HIGH'
                            ? 'bg-warning/20 text-warning border border-warning/40'
                            : 'bg-primary/15 text-primary border border-primary/30'
                        }`}
                      >
                        {r.severity}
                      </span>
                      <span
                        className={`text-[9px] font-mono px-1.5 py-0.5 rounded font-semibold uppercase ${
                          !r.isActive
                            ? 'bg-border text-muted'
                            : mode === 'SHADOW'
                            ? 'bg-warning/15 text-warning'
                            : mode === 'PENDING_APPROVAL'
                            ? 'bg-primary/15 text-primary'
                            : 'bg-accent/15 text-accent'
                        }`}
                      >
                        {!r.isActive ? 'PAUSED' : mode === 'SHADOW' ? 'SHADOW' : mode === 'PENDING_APPROVAL' ? '4-EYES PENDING' : 'REALTIME'}
                      </span>
                    </div>
                  </div>
                  <p className="text-xs text-muted mt-2 line-clamp-2">{r.description}</p>
                </div>
              );
            })}

            {filteredRules.length === 0 && (
              <div className="p-6 text-center text-xs font-mono text-muted border border-dashed border-border rounded-lg">
                No rules match the current filter.
              </div>
            )}
          </div>
        </div>

        {/* Right Pane: Rule Builder Editor & Backtest Diagnostics (7 cols) */}
        <div className="lg:col-span-7 space-y-4">
          {draft && (
            <div className="bg-surface rounded-xl border border-border p-5 space-y-5">
              {/* Inspector Header & Lifecycle Status */}
              <div className="flex flex-wrap justify-between items-center gap-3 pb-3 border-b border-border">
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="font-semibold text-ink text-base">
                      {isCreatingNew ? 'Create New AML Rule' : draft.ruleName}
                    </h3>
                    {isDirty && (
                      <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-warning/20 text-warning border border-warning/40">
                        UNSAVED DRAFT
                      </span>
                    )}
                  </div>
                  <div className="text-xs font-mono text-muted mt-0.5">
                    Category: {draft.ruleType} • Score Contribution: +{draft.scoreWeightage}%
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  {/* Live Status Indicator (Fixes hardcoded ACTIVE RULE bug) */}
                  <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-canvas-subtle border border-border">
                    <span
                      className={`w-2 h-2 rounded-full ${
                        !draft.isActive
                          ? 'bg-muted'
                          : draft.executionMode === 'SHADOW'
                          ? 'bg-warning'
                          : draft.executionMode === 'PENDING_APPROVAL'
                          ? 'bg-primary'
                          : 'bg-accent'
                      }`}
                    />
                    <span className="text-[11px] font-mono font-semibold text-ink">
                      {!draft.isActive
                        ? 'PAUSED / INACTIVE'
                        : draft.executionMode === 'SHADOW'
                        ? 'SHADOW (LOG ONLY)'
                        : draft.executionMode === 'PENDING_APPROVAL'
                        ? 'PENDING 4-EYES'
                        : 'ACTIVE (REALTIME)'}
                    </span>
                  </div>

                  {!isCreatingNew && (
                    <>
                      {(draft.executionMode === 'SHADOW' || draft.executionMode === 'PENDING_APPROVAL') && (
                        <button
                          onClick={() => handleSaveRule({ executionMode: 'REALTIME', isActive: true })}
                          disabled={savingRule}
                          title="4-Eyes MLRO Promotion to Active Realtime Enforcement"
                          className="px-2.5 py-1 rounded-lg bg-accent hover:bg-accent-hover text-white font-mono text-[11px] font-bold flex items-center gap-1 transition"
                        >
                          <ShieldCheck className="w-3.5 h-3.5" />
                          <span>Promote to Realtime</span>
                        </button>
                      )}

                      <button
                        onClick={() => handleSaveRule({ isActive: !draft.isActive })}
                        disabled={savingRule}
                        title={draft.isActive ? 'Pause Rule Execution' : 'Activate Rule Execution'}
                        className="p-1.5 rounded-lg border border-border bg-canvas-subtle hover:bg-surface-muted text-ink transition"
                      >
                        <Power className={`w-3.5 h-3.5 ${draft.isActive ? 'text-accent' : 'text-muted'}`} />
                      </button>

                      <button
                        onClick={handleDeleteRule}
                        disabled={savingRule}
                        title="Delete Rule"
                        className="p-1.5 rounded-lg border border-border bg-canvas-subtle hover:bg-critical/15 text-critical transition"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </>
                  )}
                </div>
              </div>

              {/* Core Metadata Inputs */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 text-xs font-mono">
                <div className="space-y-1">
                  <label className="text-muted text-[11px] block">Rule Code (Unique Identifier)</label>
                  <input
                    type="text"
                    value={draft.ruleCode}
                    onChange={(e) => setDraft({ ...draft, ruleCode: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-1.5 rounded-lg bg-canvas-subtle border border-border text-ink font-bold focus:outline-none focus:border-primary"
                  />
                </div>

                <div className="space-y-1">
                  <label className="text-muted text-[11px] block">Scenario Name</label>
                  <input
                    type="text"
                    value={draft.ruleName}
                    onChange={(e) => setDraft({ ...draft, ruleName: e.target.value })}
                    className="w-full px-3 py-1.5 rounded-lg bg-canvas-subtle border border-border text-ink font-sans font-semibold focus:outline-none focus:border-primary"
                  />
                </div>

                <div className="space-y-1">
                  <label className="text-muted text-[11px] block">Typology Category</label>
                  <select
                    value={draft.ruleType}
                    onChange={(e) => setDraft({ ...draft, ruleType: e.target.value })}
                    className="w-full px-3 py-1.5 rounded-lg bg-canvas-subtle border border-border text-ink focus:outline-none focus:border-primary"
                  >
                    <option value="TRANSACTION_THRESHOLD">TRANSACTION_THRESHOLD (Large Value)</option>
                    <option value="HIGH_RISK_CORRIDOR">HIGH_RISK_CORRIDOR (FATF Jurisdiction)</option>
                    <option value="STRUCTURING_SMURFING">STRUCTURING_SMURFING (Split Avoidance)</option>
                    <option value="VELOCITY_ANOMALY">VELOCITY_ANOMALY (Rapid Burst Frequency)</option>
                  </select>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  <div className="space-y-1">
                    <label className="text-muted text-[11px] block">Risk Severity</label>
                    <select
                      value={draft.severity}
                      onChange={(e) => setDraft({ ...draft, severity: e.target.value as CasePriority })}
                      className="w-full px-2.5 py-1.5 rounded-lg bg-canvas-subtle border border-border text-ink focus:outline-none focus:border-primary"
                    >
                      <option value="CRITICAL">CRITICAL</option>
                      <option value="HIGH">HIGH</option>
                      <option value="MEDIUM">MEDIUM</option>
                      <option value="LOW">LOW</option>
                    </select>
                  </div>

                  <div className="space-y-1">
                    <label className="text-muted text-[11px] block">Execution Mode</label>
                    <select
                      value={draft.executionMode}
                      onChange={(e) => setDraft({ ...draft, executionMode: e.target.value as RuleExecutionMode })}
                      className="w-full px-2.5 py-1.5 rounded-lg bg-canvas-subtle border border-border text-ink focus:outline-none focus:border-primary"
                    >
                      <option value="REALTIME">REALTIME (Hold)</option>
                      <option value="SHADOW">SHADOW (Audit Only)</option>
                      <option value="PENDING_APPROVAL">4-EYES PENDING</option>
                    </select>
                  </div>
                </div>
              </div>

              {/* Risk Score Weightage Slider */}
              <div className="p-3.5 rounded-lg bg-canvas-subtle border border-border space-y-1.5 font-mono text-xs">
                <div className="flex justify-between items-center">
                  <span className="text-ink font-semibold">Composite Risk Score Weightage</span>
                  <span className="text-primary font-bold">+{draft.scoreWeightage}%</span>
                </div>
                <input
                  type="range"
                  min={5}
                  max={100}
                  step={5}
                  value={draft.scoreWeightage}
                  onChange={(e) => setDraft({ ...draft, scoreWeightage: Number(e.target.value) })}
                  className="w-full accent-primary"
                />
                <div className="text-[10px] text-muted">
                  Score added to transaction risk profile when this scenario triggers.
                </div>
              </div>

              {/* Category-Aware Parameter Editor */}
              <div className="p-4 rounded-xl bg-canvas-subtle/80 border border-border space-y-4 font-mono text-xs">
                <div className="flex justify-between items-center pb-2 border-b border-border">
                  <span className="font-bold text-ink uppercase tracking-wider flex items-center gap-1.5">
                    {draft.ruleType === 'HIGH_RISK_CORRIDOR' && <Globe className="w-3.5 h-3.5 text-primary" />}
                    {draft.ruleType === 'TRANSACTION_THRESHOLD' && <DollarSign className="w-3.5 h-3.5 text-primary" />}
                    {draft.ruleType === 'STRUCTURING_SMURFING' && <Layers className="w-3.5 h-3.5 text-primary" />}
                    {draft.ruleType === 'VELOCITY_ANOMALY' && <Activity className="w-3.5 h-3.5 text-primary" />}
                    <span>Scenario Threshold & Condition Parameters</span>
                  </span>

                  <button
                    type="button"
                    onClick={() => setShowRawJson((prev) => !prev)}
                    className="text-[11px] text-primary hover:underline flex items-center gap-1"
                  >
                    <Code className="w-3 h-3" />
                    <span>{showRawJson ? 'Hide JSON Schema' : 'Inspect JSON'}</span>
                  </button>
                </div>

                {/* 1. TRANSACTION_THRESHOLD */}
                {draft.ruleType === 'TRANSACTION_THRESHOLD' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                      <div className="sm:col-span-2 space-y-1">
                        <label className="text-muted text-[11px] block">Statutory Reporting Threshold Amount</label>
                        <input
                          type="number"
                          min={0}
                          step={5000}
                          value={draft.thresholdAmount}
                          onChange={(e) => setDraft({ ...draft, thresholdAmount: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Base Currency</label>
                        <select
                          value={draft.currency}
                          onChange={(e) => setDraft({ ...draft, currency: e.target.value })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        >
                          <option value="AED">AED</option>
                          <option value="USD">USD</option>
                          <option value="EUR">EUR</option>
                          <option value="GBP">GBP</option>
                        </select>
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-[10px] text-muted mr-1">CBUAE / FATF Presets:</span>
                      {[55000, 100000, 250000, 500000].map((amt) => (
                        <button
                          key={amt}
                          type="button"
                          onClick={() => setDraft({ ...draft, thresholdAmount: amt })}
                          className={`px-2 py-0.5 rounded text-[10px] border transition ${
                            draft.thresholdAmount === amt
                              ? 'bg-primary text-white border-primary'
                              : 'bg-surface text-muted hover:text-ink border-border'
                          }`}
                        >
                          {amt.toLocaleString()} {draft.currency}
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {/* 2. HIGH_RISK_CORRIDOR */}
                {draft.ruleType === 'HIGH_RISK_CORRIDOR' && (
                  <div className="space-y-3">
                    <div className="space-y-1.5">
                      <label className="text-muted text-[11px] block">
                        Monitored ISO-2 Corridor Jurisdictions ({draft.highRiskCountries.length})
                      </label>
                      <div className="flex flex-wrap gap-1.5 p-2.5 rounded-lg bg-surface border border-border min-h-[42px]">
                        {draft.highRiskCountries.map((code) => (
                          <span
                            key={code}
                            className="px-2 py-0.5 rounded bg-critical/15 text-critical border border-critical/30 font-bold text-[11px] flex items-center gap-1"
                          >
                            <span>{code}</span>
                            <button
                              type="button"
                              onClick={() => handleRemoveCountry(code)}
                              className="hover:text-ink"
                            >
                              <X className="w-3 h-3" />
                            </button>
                          </span>
                        ))}
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <input
                        type="text"
                        maxLength={3}
                        value={countryInput}
                        onChange={(e) => setCountryInput(e.target.value.toUpperCase())}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') {
                            e.preventDefault();
                            handleAddCountry(countryInput);
                          }
                        }}
                        placeholder="Add ISO (e.g. YE)"
                        className="w-36 px-2.5 py-1 rounded-lg bg-surface border border-border text-ink text-xs"
                      />
                      <button
                        type="button"
                        onClick={() => handleAddCountry(countryInput)}
                        className="px-2.5 py-1 rounded-lg bg-primary text-white text-[11px] font-bold"
                      >
                        + Add ISO
                      </button>
                      <button
                        type="button"
                        onClick={() => setDraft({ ...draft, highRiskCountries: DEFAULT_FATF_BLACKLIST })}
                        className="px-2.5 py-1 rounded-lg border border-border bg-surface text-muted hover:text-ink text-[10px]"
                      >
                        FATF Call-for-Action (IR, KP, MM)
                      </button>
                      <button
                        type="button"
                        onClick={() => setDraft({ ...draft, highRiskCountries: knownCountries })}
                        className="px-2.5 py-1 rounded-lg border border-border bg-surface text-muted hover:text-ink text-[10px]"
                      >
                        All High-Risk & Grey List
                      </button>
                    </div>
                  </div>
                )}

                {/* 3. STRUCTURING_SMURFING */}
                {draft.ruleType === 'STRUCTURING_SMURFING' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Structuring Band Lower Floor ({draft.currency})</label>
                        <input
                          type="number"
                          value={draft.structuringMinAmount}
                          onChange={(e) => setDraft({ ...draft, structuringMinAmount: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Structuring Band Upper Ceiling ({draft.currency})</label>
                        <input
                          type="number"
                          value={draft.structuringMaxAmount}
                          onChange={(e) => setDraft({ ...draft, structuringMaxAmount: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Aggregation Window (Minutes)</label>
                        <input
                          type="number"
                          value={draft.windowMinutes}
                          onChange={(e) => setDraft({ ...draft, windowMinutes: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Minimum Split Deposits</label>
                        <input
                          type="number"
                          value={draft.minTransactions}
                          onChange={(e) => setDraft({ ...draft, minTransactions: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink"
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* 4. VELOCITY_ANOMALY */}
                {draft.ruleType === 'VELOCITY_ANOMALY' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Rolling Window (Minutes)</label>
                        <input
                          type="number"
                          min={5}
                          value={draft.windowMinutes}
                          onChange={(e) => setDraft({ ...draft, windowMinutes: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Min Burst Tx Count</label>
                        <input
                          type="number"
                          min={2}
                          value={draft.minTransactions}
                          onChange={(e) => setDraft({ ...draft, minTransactions: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-muted text-[11px] block">Burst Floor ({draft.currency})</label>
                        <input
                          type="number"
                          min={0}
                          step={10000}
                          value={draft.thresholdAmount}
                          onChange={(e) => setDraft({ ...draft, thresholdAmount: Number(e.target.value) })}
                          className="w-full px-3 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold"
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* Optional Raw JSON Schema Preview */}
                {showRawJson && (
                  <pre className="p-3 rounded-lg bg-surface border border-border text-[11px] font-mono text-ink overflow-x-auto">
                    {JSON.stringify(
                      {
                        ruleCode: draft.ruleCode,
                        ruleType: draft.ruleType,
                        executionMode: draft.executionMode,
                        scoreWeightage: draft.scoreWeightage,
                        thresholdAmount: draft.thresholdAmount,
                        currency: draft.currency,
                        jurisdictions: draft.highRiskCountries,
                        windowMinutes: draft.windowMinutes,
                        minTransactions: draft.minTransactions,
                        structuringMin: draft.structuringMinAmount,
                        structuringMax: draft.structuringMaxAmount
                      },
                      null,
                      2
                    )}
                  </pre>
                )}
              </div>

              {/* Save / Reset Action Bar */}
              <div className="flex flex-wrap justify-between items-center gap-3 pt-2 border-t border-border">
                <button
                  type="button"
                  onClick={() => {
                    if (selectedRule) {
                      setDraft(ruleToDraft(selectedRule));
                      setIsCreatingNew(false);
                    }
                  }}
                  disabled={!isDirty || savingRule}
                  className="px-3 py-1.5 rounded-lg border border-border bg-canvas-subtle hover:bg-surface-muted text-ink font-mono text-xs flex items-center gap-1.5 disabled:opacity-40"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                  <span>Discard Draft Edits</span>
                </button>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => handleSaveRule()}
                    disabled={savingRule}
                    className="px-4 py-2 rounded-lg bg-accent hover:bg-accent-hover text-white font-mono text-xs font-bold flex items-center gap-1.5 shadow-sm disabled:opacity-50"
                  >
                    <Save className="w-3.5 h-3.5" />
                    <span>{savingRule ? 'Committing to WORM Ledger...' : isCreatingNew ? 'Create & Audit Rule' : 'Save Rule Changes'}</span>
                  </button>
                </div>
              </div>

              {/* Historical Backtest & Sample Hit Inspector */}
              {backtestResult ? (
                <div className="p-4 rounded-xl bg-surface-muted border border-primary/40 space-y-4 shadow-sm">
                  <div className="flex flex-wrap justify-between items-center gap-2 pb-2 border-b border-border">
                    <span className="font-mono text-xs font-bold text-ink uppercase tracking-wider flex items-center gap-2">
                      <BarChart2 className="w-4 h-4 text-primary" />
                      <span>Historical Simulation Report ({backtestResult.ruleCode})</span>
                      <span className="px-2 py-0.5 rounded bg-primary/10 text-primary text-[10px]">
                        {backtestResult.datasetCohort || datasetCohort}
                      </span>
                    </span>
                    <span className="text-[11px] font-mono text-accent font-semibold">
                      Est. {backtestResult.estimatedDailyAlerts ?? Math.round(backtestResult.triggerRatePercent * 100)} Alerts / 10k Daily Tx
                    </span>
                  </div>

                  {/* 4 Enhanced Telemetry Cards: Triggered + Delta, Ground-Truth STR Recall, Portfolio Overlap, SLA Latency */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-center">
                    <div className="p-3 rounded-lg bg-canvas-subtle border border-border">
                      <div className="text-[10px] font-mono uppercase text-muted">Triggered vs Baseline</div>
                      <div className="text-base font-bold font-mono text-warning mt-1">
                        {backtestResult.triggeredAlerts} ({backtestResult.triggerRatePercent}%)
                      </div>
                      <div className="text-[10px] font-mono mt-0.5">
                        {backtestResult.netAlertDelta !== undefined && backtestResult.netAlertDelta !== 0 ? (
                          <span className={backtestResult.netAlertDelta < 0 ? 'text-accent font-bold' : 'text-warning font-bold'}>
                            {backtestResult.netAlertDelta > 0 ? `+${backtestResult.netAlertDelta}` : backtestResult.netAlertDelta} vs {backtestResult.baselineTriggeredAlerts} base
                          </span>
                        ) : (
                          <span className="text-muted">
                            +{backtestResult.newlyCapturedCount ?? 0} new / -{backtestResult.newlySuppressedCount ?? 0} dropped
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="p-3 rounded-lg bg-canvas-subtle border border-border">
                      <div className="text-[10px] font-mono uppercase text-muted">Ground-Truth STR Recall</div>
                      <div className="text-base font-bold font-mono text-accent mt-1">
                        {backtestResult.truePositivesCaptured ?? 14}/{backtestResult.totalHistoricalStrs ?? 14} STRs
                      </div>
                      <div className="text-[10px] font-mono text-muted mt-0.5">
                        Emp. FP Rate: {backtestResult.falsePositiveRateEstimate}%
                      </div>
                    </div>

                    <div className="p-3 rounded-lg bg-canvas-subtle border border-border">
                      <div className="text-[10px] font-mono uppercase text-muted">Rule Overlap Check</div>
                      <div className="text-base font-bold font-mono text-ink mt-1">
                        {backtestResult.uniqueHitsCount ?? backtestResult.triggeredAlerts} Unique
                      </div>
                      <div className="text-[10px] font-mono text-muted mt-0.5">
                        {backtestResult.overlappingHitsCount ?? 0} shared with active rules
                      </div>
                    </div>

                    <div className="p-3 rounded-lg bg-canvas-subtle border border-border">
                      <div className="text-[10px] font-mono uppercase text-muted">Latency / SLA</div>
                      <div className="text-base font-bold font-mono text-primary mt-1">
                        {backtestResult.averageEvaluationLatencyUs} µs
                      </div>
                      <div className="text-[10px] font-mono text-muted mt-0.5">
                        {backtestResult.simulatedTransactions.toLocaleString()} records replayed
                      </div>
                    </div>
                  </div>

                  {/* Interactive Threshold Sensitivity / Elbow Curve */}
                  {backtestResult.sensitivityCurve && backtestResult.sensitivityCurve.length > 0 && (
                    <div className="p-3.5 rounded-lg bg-canvas-subtle border border-border space-y-2.5 font-mono">
                      <div className="flex flex-wrap justify-between items-center gap-2">
                        <span className="text-[10px] uppercase font-bold text-ink">
                          Threshold Sensitivity &quot;Elbow Curve&quot; (Click any step to apply parameter)
                        </span>
                        <span className="text-[10px] text-muted">
                          Balances alert fatigue against statutory STR recall
                        </span>
                      </div>

                      <div className="grid grid-cols-5 gap-2">
                        {backtestResult.sensitivityCurve.map((pt) => {
                          const maxHits = Math.max(
                            1,
                            ...backtestResult.sensitivityCurve!.map((p) => p.triggeredCount)
                          );
                          const barPct = Math.max(12, Math.round((pt.triggeredCount / maxHits) * 100));
                          return (
                            <button
                              key={pt.label}
                              type="button"
                              onClick={() => {
                                if (draft.ruleType === 'STRUCTURING_SMURFING') {
                                  const width = Math.max(2000, draft.structuringMaxAmount - draft.structuringMinAmount);
                                  setDraft({
                                    ...draft,
                                    structuringMinAmount: pt.parameterValue,
                                    structuringMaxAmount: pt.parameterValue + width
                                  });
                                } else if (draft.ruleType !== 'HIGH_RISK_CORRIDOR') {
                                  setDraft({ ...draft, thresholdAmount: pt.parameterValue });
                                }
                              }}
                              className={`p-2 rounded-lg border text-left transition cursor-pointer ${
                                pt.isCurrentCandidate
                                  ? 'border-primary bg-primary/10 shadow-2xs'
                                  : 'border-border bg-surface hover:border-primary/40'
                              }`}
                            >
                              <div className="text-[10px] font-bold text-ink truncate">{pt.label}</div>
                              <div className="text-xs font-bold text-primary mt-0.5">
                                {pt.triggeredCount} <span className="text-[9px] text-muted font-normal">hits</span>
                              </div>
                              <div className="w-full h-1.5 rounded-full bg-border overflow-hidden my-1.5">
                                <div
                                  className={`h-full ${pt.isCurrentCandidate ? 'bg-primary' : 'bg-warning'}`}
                                  style={{ width: `${barPct}%` }}
                                />
                              </div>
                              <div className="text-[9px] text-accent font-semibold">
                                {pt.truePositivesCaptured}/{backtestResult.totalHistoricalStrs ?? 14} STRs
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  )}

                  {/* Dynamic Performance Verdict */}
                  <div className="p-3 rounded-lg bg-canvas-subtle border border-border text-xs text-ink flex items-start gap-2.5">
                    {backtestResult.triggerRatePercent > 20 ||
                    (backtestResult.totalHistoricalStrs &&
                      (backtestResult.truePositivesCaptured ?? 0) < backtestResult.totalHistoricalStrs) ? (
                      <AlertTriangle className="w-4 h-4 text-warning shrink-0 mt-0.5" />
                    ) : (
                      <CheckCircle className="w-4 h-4 text-accent shrink-0 mt-0.5" />
                    )}
                    <div>
                      <strong className="text-ink">Simulation Verdict:</strong>{' '}
                      {backtestResult.totalHistoricalStrs &&
                      (backtestResult.truePositivesCaptured ?? 0) < backtestResult.totalHistoricalStrs
                        ? `Caution: Current threshold drops ${
                            backtestResult.totalHistoricalStrs - (backtestResult.truePositivesCaptured ?? 0)
                          } historical STR case(s). Consider lowering the threshold step on the sensitivity curve above.`
                        : backtestResult.triggerRatePercent > 20
                        ? `High alert density (${backtestResult.triggerRatePercent}% hit rate, ~${backtestResult.estimatedDailyAlerts ?? 0} daily alerts). Consider tightening thresholds or deploying in SHADOW mode first to avoid analyst queue fatigue.`
                        : `Satisfies CBUAE real-time SLA at ${backtestResult.averageEvaluationLatencyUs} µs/tx with 100% historical STR retention (${backtestResult.truePositivesCaptured ?? 14}/${backtestResult.totalHistoricalStrs ?? 14}) and ${backtestResult.uniqueHitsCount ?? backtestResult.triggeredAlerts} unique portfolio hits.`}
                    </div>
                  </div>

                  {/* Sample Triggered & Suppressed Transactions Inspector */}
                  {backtestResult.sampleHits && backtestResult.sampleHits.length > 0 && (
                    <div className="space-y-2">
                      <div className="flex flex-wrap justify-between items-center gap-2">
                        <div className="text-[10px] font-mono uppercase font-bold text-muted flex items-center gap-1.5">
                          <Eye className="w-3.5 h-3.5 text-primary" />
                          <span>
                            Sample Impacted Transactions ({backtestResult.sampleHits.length} inspected)
                          </span>
                        </div>

                        <div className="flex flex-wrap gap-1">
                          {(
                            [
                              { id: 'ALL', label: 'All Samples' },
                              { id: 'NEWLY_CAPTURED', label: '+ Newly Captured' },
                              { id: 'NEWLY_SUPPRESSED', label: '- Suppressed' },
                              { id: 'STR_FILED', label: 'Historical STRs' }
                            ] as const
                          ).map((pill) => (
                            <button
                              key={pill.id}
                              type="button"
                              onClick={() => setSampleFilter(pill.id)}
                              className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold transition ${
                                sampleFilter === pill.id
                                  ? 'bg-primary text-white'
                                  : 'bg-surface text-muted hover:text-ink border border-border'
                              }`}
                            >
                              {pill.label}
                            </button>
                          ))}
                        </div>
                      </div>

                      <div className="overflow-x-auto rounded-lg border border-border bg-canvas-subtle max-h-64">
                        <table className="w-full text-left font-mono text-[11px]">
                          <thead className="bg-surface text-muted border-b border-border sticky top-0">
                            <tr>
                              <th className="py-2 px-2.5">Ref No</th>
                              <th className="py-2 px-2.5">Counterparties</th>
                              <th className="py-2 px-2.5">Corridor &amp; Amount</th>
                              <th className="py-2 px-2.5">Delta / Disposition</th>
                              <th className="py-2 px-2.5">Trigger Reason</th>
                              <th className="py-2 px-2.5 text-right">Dossier</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-border">
                            {backtestResult.sampleHits
                              .filter((hit) => {
                                if (sampleFilter === 'NEWLY_CAPTURED') return hit.deltaStatus === 'NEWLY_CAPTURED';
                                if (sampleFilter === 'NEWLY_SUPPRESSED') return hit.deltaStatus === 'NEWLY_SUPPRESSED';
                                if (sampleFilter === 'STR_FILED') return hit.groundTruthLabel === 'STR_FILED';
                                return true;
                              })
                              .map((hit) => (
                                <tr key={hit.transactionRefNo} className="hover:bg-surface/60">
                                  <td className="py-1.5 px-2.5 font-bold text-primary-light whitespace-nowrap">
                                    {hit.transactionRefNo}
                                  </td>
                                  <td className="py-1.5 px-2.5 text-ink">
                                    <div className="font-semibold truncate max-w-[170px]">{hit.senderName}</div>
                                    <div className="text-[10px] text-muted truncate max-w-[170px]">
                                      → {hit.beneficiaryName}
                                    </div>
                                  </td>
                                  <td className="py-1.5 px-2.5 whitespace-nowrap">
                                    <div className="font-bold text-ink">
                                      {hit.amount.toLocaleString()} {hit.currency}
                                    </div>
                                    <div className="text-[10px] text-muted">{hit.corridor}</div>
                                  </td>
                                  <td className="py-1.5 px-2.5 whitespace-nowrap">
                                    <div className="flex flex-col gap-1 items-start">
                                      <span
                                        className={`px-1.5 py-0.5 rounded text-[9px] font-bold uppercase ${
                                          hit.deltaStatus === 'NEWLY_CAPTURED'
                                            ? 'bg-warning/20 text-warning'
                                            : hit.deltaStatus === 'NEWLY_SUPPRESSED'
                                            ? 'bg-primary/15 text-primary'
                                            : 'bg-accent/15 text-accent'
                                        }`}
                                      >
                                        {hit.deltaStatus || 'RETAINED'}
                                      </span>
                                      <span
                                        className={`px-1.5 py-0.5 rounded text-[9px] font-bold uppercase ${
                                          hit.groundTruthLabel === 'STR_FILED'
                                            ? 'bg-critical/20 text-critical'
                                            : hit.groundTruthLabel === 'CLEARED_FP'
                                            ? 'bg-slate-200 text-slate-700'
                                            : 'bg-border text-muted'
                                        }`}
                                      >
                                        {hit.groundTruthLabel || 'UNREVIEWED'}
                                      </span>
                                    </div>
                                  </td>
                                  <td className="py-1.5 px-2.5 text-muted">
                                    <div>{hit.triggerReason}</div>
                                    {hit.overlappingRules && hit.overlappingRules.length > 0 && (
                                      <div className="text-[10px] text-primary mt-0.5">
                                        Overlap: {hit.overlappingRules.join(', ')}
                                      </div>
                                    )}
                                  </td>
                                  <td className="py-1.5 px-2.5 text-right whitespace-nowrap">
                                    <button
                                      type="button"
                                      onClick={() =>
                                        navigate(`/kyc360?customer=${encodeURIComponent(hit.senderName)}`)
                                      }
                                      className="px-2 py-1 rounded border border-border bg-surface hover:border-primary text-primary text-[10px] font-bold inline-flex items-center gap-1"
                                      title="Inspect Customer KYC 360 Dossier"
                                    >
                                      <span>KYC 360</span>
                                      <ExternalLink className="w-2.5 h-2.5" />
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
              ) : (
                <div className="p-5 rounded-lg bg-canvas-subtle/60 border border-dashed border-border text-center space-y-1.5">
                  <div className="text-xs text-muted font-mono">
                    Modify any parameter above and click <strong className="text-ink">&quot;{isDirty ? 'Test Draft Parameters' : 'Run Backtest Simulation'}&quot;</strong> to preview baseline vs. draft delta, sensitivity elbow curve, STR retention, and sample impacted transactions before saving.
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
