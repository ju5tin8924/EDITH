import React, { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  FlaskConical,
  Play,
  RotateCcw,
  Sliders,
  BarChart,
  ArrowRight,
  CheckCircle,
  AlertTriangle,
  Lock,
  Unlock,
  ShieldCheck,
  Eye,
  ExternalLink
} from 'lucide-react';
import { EdithLoader } from './EdithLogo';
import { EdithApi } from '../services/api';
import { BacktestDatasetCohort, SandboxSimulationResult } from '../types';
import { usePageMeta } from '../config/pageMeta';

type WeightKey = 'name' | 'nationality' | 'dob' | 'id';

export const RuleSandboxView: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const linkedRuleCode = searchParams.get('rule');

  // Slider states
  const [nameWeight, setNameWeight] = useState(60);
  const [nationalityWeight, setNationalityWeight] = useState(10);
  const [dobWeight, setDobWeight] = useState(15);
  const [idWeight, setIdWeight] = useState(15);
  const [minThreshold, setMinThreshold] = useState(80);

  // Baseline reference from System Masters
  const [baselineWeights, setBaselineWeights] = useState({
    nameWeightage: 60,
    nationalityWeightage: 10,
    dobWeightage: 15,
    idWeightage: 15,
    defaultThreshold: 80
  });

  // UX: Auto-balance to 100% & per-slider lock pins
  const [autoBalance, setAutoBalance] = useState(true);
  const [lockedSliders, setLockedSliders] = useState<Record<WeightKey, boolean>>({
    name: false,
    nationality: false,
    dob: false,
    id: false
  });

  // Simulation & Cohort states
  const [datasetCohort, setDatasetCohort] = useState<BacktestDatasetCohort>('STANDARD_MIX');
  const [simResult, setSimResult] = useState<SandboxSimulationResult | null>(null);
  const [entityFilter, setEntityFilter] = useState<'ALL' | 'NEWLY_CAPTURED' | 'NEWLY_SUPPRESSED' | 'STR_CONFIRMED'>('ALL');
  const [isRunning, setIsRunning] = useState(false);
  const [isPromoting, setIsPromoting] = useState(false);
  const [promoteBanner, setPromoteBanner] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const pageMeta = usePageMeta(
    'Rule Sandbox',
    'Simulate composite screening weightage matrices against historical transaction archives before promoting to active runtime.'
  );

  useEffect(() => {
    EdithApi.fetchMasters()
      .then((m) => {
        if (m?.scoringWeights) {
          const loaded = {
            nameWeightage: m.scoringWeights.nameWeightage,
            nationalityWeightage: m.scoringWeights.nationalityWeightage,
            dobWeightage: m.scoringWeights.dobWeightage,
            idWeightage: m.scoringWeights.idWeightage,
            defaultThreshold: m.scoringWeights.defaultThreshold
          };
          setBaselineWeights(loaded);
          setNameWeight(loaded.nameWeightage);
          setNationalityWeight(loaded.nationalityWeightage);
          setDobWeight(loaded.dobWeightage);
          setIdWeight(loaded.idWeightage);
          setMinThreshold(loaded.defaultThreshold);
          executePortfolioSimulation(
            loaded.nameWeightage,
            loaded.nationalityWeightage,
            loaded.dobWeightage,
            loaded.idWeightage,
            loaded.defaultThreshold,
            datasetCohort,
            loaded
          );
        } else {
          executePortfolioSimulation(60, 10, 15, 15, 80, datasetCohort, baselineWeights);
        }
      })
      .catch(() => {
        executePortfolioSimulation(60, 10, 15, 15, 80, datasetCohort, baselineWeights);
      });
  }, []);

  const totalWeight = nameWeight + nationalityWeight + dobWeight + idWeight;
  const isWeightValid = totalWeight === 100;

  const toggleLock = (key: WeightKey) => {
    setLockedSliders((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const handleWeightChange = (changedKey: WeightKey, rawValue: number) => {
    if (lockedSliders[changedKey]) return;

    const current: Record<WeightKey, number> = {
      name: nameWeight,
      nationality: nationalityWeight,
      dob: dobWeight,
      id: idWeight
    };

    if (!autoBalance) {
      current[changedKey] = rawValue;
      setNameWeight(current.name);
      setNationalityWeight(current.nationality);
      setDobWeight(current.dob);
      setIdWeight(current.id);
      return;
    }

    const otherKeys = (['name', 'nationality', 'dob', 'id'] as WeightKey[]).filter(
      (k) => k !== changedKey && !lockedSliders[k]
    );
    if (otherKeys.length === 0) return;

    const lockedSum = (['name', 'nationality', 'dob', 'id'] as WeightKey[])
      .filter((k) => k !== changedKey && lockedSliders[k])
      .reduce((acc, k) => acc + current[k], 0);

    const maxAllowed = Math.max(0, 100 - lockedSum);
    const clampedNew = Math.min(rawValue, maxAllowed);
    const remainderToDistribute = 100 - lockedSum - clampedNew;

    const currentOthersSum = otherKeys.reduce((acc, k) => acc + current[k], 0);
    const next: Record<WeightKey, number> = { ...current, [changedKey]: clampedNew };

    let allocated = 0;
    otherKeys.forEach((k, idx) => {
      if (idx === otherKeys.length - 1) {
        next[k] = Math.max(0, remainderToDistribute - allocated);
      } else {
        const share =
          currentOthersSum > 0
            ? Math.round((current[k] / currentOthersSum) * remainderToDistribute)
            : Math.round(remainderToDistribute / otherKeys.length);
        const bounded = Math.max(0, Math.min(remainderToDistribute - allocated, share));
        next[k] = bounded;
        allocated += bounded;
      }
    });

    setNameWeight(next.name);
    setNationalityWeight(next.nationality);
    setDobWeight(next.dob);
    setIdWeight(next.id);
  };

  const normalizeWeightsTo100 = () => {
    const sum = nameWeight + nationalityWeight + dobWeight + idWeight;
    if (sum === 100 || sum === 0) {
      if (sum === 0) {
        setNameWeight(60);
        setNationalityWeight(10);
        setDobWeight(15);
        setIdWeight(15);
      }
      return;
    }
    const n = Math.round((nameWeight / sum) * 100);
    const nat = Math.round((nationalityWeight / sum) * 100);
    const dob = Math.round((dobWeight / sum) * 100);
    const id = Math.max(0, 100 - (n + nat + dob));
    setNameWeight(n);
    setNationalityWeight(nat);
    setDobWeight(dob);
    setIdWeight(id);
  };

  const executePortfolioSimulation = async (
    n = nameWeight,
    nat = nationalityWeight,
    dob = dobWeight,
    id = idWeight,
    thresh = minThreshold,
    cohort = datasetCohort,
    baseRef = baselineWeights
  ) => {
    setIsRunning(true);
    setPromoteBanner(null);
    try {
      const res = await EdithApi.simulateSandboxPortfolio({
        nameWeightage: n,
        nationalityWeightage: nat,
        dobWeightage: dob,
        idWeightage: id,
        minThreshold: thresh,
        datasetCohort: cohort,
        baselineWeights: baseRef
      });
      setSimResult(res);
    } finally {
      setIsRunning(false);
    }
  };

  const resetDefaults = () => {
    setNameWeight(baselineWeights.nameWeightage);
    setNationalityWeight(baselineWeights.nationalityWeightage);
    setDobWeight(baselineWeights.dobWeightage);
    setIdWeight(baselineWeights.idWeightage);
    setMinThreshold(baselineWeights.defaultThreshold);
    setPromoteBanner(null);
    executePortfolioSimulation(
      baselineWeights.nameWeightage,
      baselineWeights.nationalityWeightage,
      baselineWeights.dobWeightage,
      baselineWeights.idWeightage,
      baselineWeights.defaultThreshold,
      datasetCohort,
      baselineWeights
    );
  };

  const handleStageShadowConfig = async () => {
    if (!isWeightValid) return;
    setIsPromoting(true);
    setPromoteBanner(null);
    try {
      await EdithApi.recordAuditEvent({
        action: 'SCORING_WEIGHTS_SHADOW_STAGED',
        targetEntity: 'Rule Sandbox Shadow Trial',
        details: `Staged 7-day shadow trial: Name=${nameWeight}%, Nat=${nationalityWeight}%, DOB=${dobWeight}%, ID=${idWeight}%, Threshold=${minThreshold}% (Projected Alerts=${simResult?.projectedAlerts ?? 0}, STR Recall=${simResult?.truePositivesRetained ?? 18}/${simResult?.totalHistoricalStrs ?? 18}).`
      });
      setPromoteBanner({
        type: 'success',
        message: `Staged candidate matrix in 7-Day SHADOW observation mode (Name ${nameWeight}% / Nat ${nationalityWeight}% / DOB ${dobWeight}% / ID ${idWeight}% @ ${minThreshold}% cut-off). Logged to SHA-256 WORM Audit Ledger.`
      });
    } catch (err: any) {
      setPromoteBanner({
        type: 'error',
        message: err?.message || 'Failed to stage shadow configuration.'
      });
    } finally {
      setIsPromoting(false);
    }
  };

  const handlePromoteConfig = async () => {
    if (!isWeightValid) return;
    setIsPromoting(true);
    setPromoteBanner(null);
    try {
      const res = await EdithApi.updateScoringWeights({
        nameWeightage: nameWeight,
        nationalityWeightage: nationalityWeight,
        dobWeightage: dobWeight,
        idWeightage: idWeight,
        defaultThreshold: minThreshold
      });
      const nextBaseline = {
        nameWeightage: nameWeight,
        nationalityWeightage: nationalityWeight,
        dobWeightage: dobWeight,
        idWeightage: idWeight,
        defaultThreshold: minThreshold
      };
      setBaselineWeights(nextBaseline);
      setPromoteBanner({
        type: 'success',
        message: `${res.message} Recorded to SHA-256 WORM Audit Ledger (SCORING_WEIGHTS_UPDATE).`
      });
    } catch (err: any) {
      setPromoteBanner({
        type: 'error',
        message: err?.message || 'Failed to promote scoring weights.'
      });
    } finally {
      setIsPromoting(false);
    }
  };

  const scoreBands = simResult?.scoreBands || [
    { band: '90 - 100% (High Confidence Hits)', count: 92, pct: 18, color: 'bg-critical' },
    { band: '80 - 89% (Actionable Case Threshold)', count: 328, pct: 52, color: 'bg-warning' },
    { band: '70 - 79% (Suppressed Gray Zone)', count: 340, pct: 75, color: 'bg-primary' },
    { band: '50 - 69% (Auto-Cleared Noise)', count: 1820, pct: 100, color: 'bg-slate-400' }
  ];

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {isRunning && (
        <EdithLoader
          overlay
          size="md"
          label="RUNNING PORTFOLIO SIMULATION..."
          sublabel="Evaluating 50,000 archive records across composite weights & active AML rules"
        />
      )}

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <FlaskConical className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            {pageMeta.parent && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {pageMeta.parent}
              </span>
            )}
            {linkedRuleCode && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-accent/15 text-accent border border-accent/30">
                Linked Rule: {linkedRuleCode}
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1">{pageMeta.definition}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() =>
              navigate(`/aml/rules${linkedRuleCode ? `?rule=${encodeURIComponent(linkedRuleCode)}` : ''}`)
            }
            className="px-3 py-1.5 rounded-lg border border-border bg-canvas-subtle hover:bg-surface-muted text-ink text-xs font-mono flex items-center gap-1.5"
          >
            <Sliders className="w-3.5 h-3.5 text-primary" />
            <span>Customize Rules</span>
          </button>

          <select
            value={datasetCohort}
            onChange={(e) => {
              const next = e.target.value as BacktestDatasetCohort;
              setDatasetCohort(next);
              executePortfolioSimulation(
                nameWeight,
                nationalityWeight,
                dobWeight,
                idWeight,
                minThreshold,
                next,
                baselineWeights
              );
            }}
            aria-label="Simulation Dataset Cohort"
            className="px-2.5 py-1.5 rounded-lg border border-border bg-canvas-subtle text-ink text-xs font-mono font-semibold focus:outline-none focus:border-primary"
          >
            <option value="STANDARD_MIX">Q3 2026 Standard Archive (50k)</option>
            <option value="HIGH_RISK_CORRIDOR_SURGE">FATF High-Risk Corridor Surge</option>
            <option value="SMURFING_RING_ATTACK">Smurfing / Split-Deposit Surge</option>
            <option value="VELOCITY_BURST_STRESS">Rapid Velocity Burst Stress</option>
          </select>

          <button
            onClick={resetDefaults}
            className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink text-xs font-mono flex items-center gap-1.5"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset Baseline</span>
          </button>

          <button
            onClick={() => executePortfolioSimulation()}
            disabled={!isWeightValid || isRunning}
            className="px-4 py-1.5 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary-hover flex items-center gap-2 shadow-xs disabled:opacity-50"
          >
            <Play className="w-3.5 h-3.5" />
            <span>{isRunning ? 'Replaying 50k Records...' : 'Execute Backtest'}</span>
          </button>
        </div>
      </div>

      {/* Weight Error Banner if sum != 100 */}
      {!isWeightValid && (
        <div className="p-3 bg-critical/10 border border-critical text-critical rounded-xl font-mono text-xs flex justify-between items-center">
          <span>Total weight must equal exactly 100%. Current sum: {totalWeight}%</span>
          <button
            type="button"
            onClick={normalizeWeightsTo100}
            className="px-3 py-1 rounded-lg bg-critical text-white font-bold hover:opacity-90"
          >
            Normalize to 100%
          </button>
        </div>
      )}

      {/* Promotion Confirmation Banner */}
      {promoteBanner && (
        <div
          className={`p-3 rounded-xl border font-mono text-xs flex items-center gap-2 ${
            promoteBanner.type === 'success'
              ? 'bg-accent/10 border-accent/40 text-accent'
              : 'bg-critical/10 border-critical/40 text-critical'
          }`}
        >
          {promoteBanner.type === 'success' ? (
            <CheckCircle className="w-4 h-4 shrink-0" />
          ) : (
            <AlertTriangle className="w-4 h-4 shrink-0" />
          )}
          <span>{promoteBanner.message}</span>
        </div>
      )}

      {/* Main Grid: Sliders Left, Simulation Metrics Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Sliders Workbench (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-5 font-mono text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2 pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <Sliders className="w-4 h-4 text-primary" />
              Scoring Weightage Matrix
            </h3>
            <div className="flex items-center gap-2">
              <label className="flex items-center gap-1.5 text-[10px] text-muted cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={autoBalance}
                  onChange={(e) => setAutoBalance(e.target.checked)}
                  className="accent-primary rounded"
                />
                <span>Auto-Balance 100%</span>
              </label>
              <span
                className={`text-[10px] font-bold px-2 py-0.5 rounded ${
                  isWeightValid ? 'bg-accent/20 text-accent' : 'bg-critical/20 text-critical'
                }`}
              >
                Sum: {totalWeight}%
              </span>
            </div>
          </div>

          {/* Slider 1: Name Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">1. Name Similarity Weightage</span>
              <div className="flex items-center gap-2">
                <span className="text-[10px] text-muted">Base: {baselineWeights.nameWeightage}%</span>
                <span className="font-bold text-primary">{nameWeight}%</span>
                <button
                  type="button"
                  onClick={() => toggleLock('name')}
                  title={lockedSliders.name ? 'Unlock slider' : 'Lock slider during auto-balance'}
                  className={`p-1 rounded border ${
                    lockedSliders.name
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border text-muted hover:text-ink'
                  }`}
                >
                  {lockedSliders.name ? <Lock className="w-3 h-3" /> : <Unlock className="w-3 h-3" />}
                </button>
              </div>
            </div>
            <input
              type="range"
              min="20"
              max="90"
              disabled={lockedSliders.name}
              value={nameWeight}
              onChange={(e) => handleWeightChange('name', Number(e.target.value))}
              className="w-full accent-primary disabled:opacity-50"
            />
            <div className="text-[10px] text-muted">Double Metaphone + Jaro-Winkler multi-token alignment</div>
          </div>

          {/* Slider 2: Nationality Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">2. Nationality &amp; Residence Weightage</span>
              <div className="flex items-center gap-2">
                <span className="text-[10px] text-muted">Base: {baselineWeights.nationalityWeightage}%</span>
                <span className="font-bold text-primary">{nationalityWeight}%</span>
                <button
                  type="button"
                  onClick={() => toggleLock('nationality')}
                  title={lockedSliders.nationality ? 'Unlock slider' : 'Lock slider during auto-balance'}
                  className={`p-1 rounded border ${
                    lockedSliders.nationality
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border text-muted hover:text-ink'
                  }`}
                >
                  {lockedSliders.nationality ? <Lock className="w-3 h-3" /> : <Unlock className="w-3 h-3" />}
                </button>
              </div>
            </div>
            <input
              type="range"
              min="0"
              max="45"
              disabled={lockedSliders.nationality}
              value={nationalityWeight}
              onChange={(e) => handleWeightChange('nationality', Number(e.target.value))}
              className="w-full accent-primary disabled:opacity-50"
            />
            <div className="text-[10px] text-muted">FATF high-risk country matrix and passport origin match</div>
          </div>

          {/* Slider 3: Date of Birth Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">3. Date of Birth (DOB) Weightage</span>
              <div className="flex items-center gap-2">
                <span className="text-[10px] text-muted">Base: {baselineWeights.dobWeightage}%</span>
                <span className="font-bold text-primary">{dobWeight}%</span>
                <button
                  type="button"
                  onClick={() => toggleLock('dob')}
                  title={lockedSliders.dob ? 'Unlock slider' : 'Lock slider during auto-balance'}
                  className={`p-1 rounded border ${
                    lockedSliders.dob
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border text-muted hover:text-ink'
                  }`}
                >
                  {lockedSliders.dob ? <Lock className="w-3 h-3" /> : <Unlock className="w-3 h-3" />}
                </button>
              </div>
            </div>
            <input
              type="range"
              min="0"
              max="45"
              disabled={lockedSliders.dob}
              value={dobWeight}
              onChange={(e) => handleWeightChange('dob', Number(e.target.value))}
              className="w-full accent-primary disabled:opacity-50"
            />
            <div className="text-[10px] text-muted">Year, month, day exactness with ±1 year fuzzy grace tolerance</div>
          </div>

          {/* Slider 4: National ID Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">4. National ID &amp; Passport Weightage</span>
              <div className="flex items-center gap-2">
                <span className="text-[10px] text-muted">Base: {baselineWeights.idWeightage}%</span>
                <span className="font-bold text-primary">{idWeight}%</span>
                <button
                  type="button"
                  onClick={() => toggleLock('id')}
                  title={lockedSliders.id ? 'Unlock slider' : 'Lock slider during auto-balance'}
                  className={`p-1 rounded border ${
                    lockedSliders.id
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border text-muted hover:text-ink'
                  }`}
                >
                  {lockedSliders.id ? <Lock className="w-3 h-3" /> : <Unlock className="w-3 h-3" />}
                </button>
              </div>
            </div>
            <input
              type="range"
              min="0"
              max="45"
              disabled={lockedSliders.id}
              value={idWeight}
              onChange={(e) => handleWeightChange('id', Number(e.target.value))}
              className="w-full accent-primary disabled:opacity-50"
            />
            <div className="text-[10px] text-muted">National identification and passport number verification</div>
          </div>

          {/* Slider 5: Threshold Score */}
          <div className="pt-3 border-t border-border space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-critical">Cut-off Alert Threshold</span>
              <div className="flex items-center gap-2">
                <span className="text-[10px] text-muted">Base: {baselineWeights.defaultThreshold}%</span>
                <span className="font-bold text-critical text-sm">{minThreshold}%</span>
              </div>
            </div>
            <input
              type="range"
              min="60"
              max="95"
              value={minThreshold}
              onChange={(e) => setMinThreshold(Number(e.target.value))}
              className="w-full accent-critical"
            />
            <div className="text-[10px] text-muted">
              Composite scores &gt;= {minThreshold}% generate statutory case alerts
            </div>
          </div>
        </div>

        {/* Simulation Analytics & Histogram (7 cols) */}
        <div className="lg:col-span-7 bg-surface rounded-xl border border-border p-5 space-y-5 font-mono text-xs">
          <div className="flex flex-wrap items-center justify-between gap-2 pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <BarChart className="w-4 h-4 text-primary" />
              Replay Projection Against {(simResult?.evaluatedRecords ?? 50000).toLocaleString()} Transactions
            </h3>
            <span className="text-[10px] text-accent font-bold">
              Active Rules Evaluated: {simResult?.activePortfolioRulesEvaluated ?? 6} • Cohort: {datasetCohort}
            </span>
          </div>

          {/* 4 Key Metrics */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">Projected Alerts</div>
              <div className="text-xl font-bold text-ink mt-0.5">
                {(simResult?.projectedAlerts ?? 420).toLocaleString()}
              </div>
              <div className="text-[10px] text-muted mt-0.5">
                vs {(simResult?.baselineAlerts ?? 820).toLocaleString()} baseline (
                <span
                  className={
                    (simResult?.netAlertDelta ?? -400) <= 0 ? 'text-accent font-bold' : 'text-warning font-bold'
                  }
                >
                  {(simResult?.netAlertDelta ?? -400) > 0
                    ? `+${simResult?.netAlertDelta}`
                    : simResult?.netAlertDelta ?? -400}
                </span>
                )
              </div>
            </div>

            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">FP Alert Reduction</div>
              <div className="text-xl font-bold text-accent mt-0.5">
                {(simResult?.fpReductionPercent ?? 36.6) >= 0
                  ? `-${simResult?.fpReductionPercent ?? 36.6}%`
                  : `+${Math.abs(simResult?.fpReductionPercent ?? 0)}%`}
              </div>
              <div className="text-[10px] text-accent mt-0.5">
                {simResult?.falsePositivesSuppressed ?? 300} noise hits cleared
              </div>
            </div>

            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">Ground-Truth STR Recall</div>
              <div className="text-xl font-bold text-ink mt-0.5">
                {simResult?.truePositivesRetained ?? 18}/{simResult?.totalHistoricalStrs ?? 18}
              </div>
              <div
                className={`text-[10px] mt-0.5 font-semibold ${
                  (simResult?.truePositivesRetained ?? 18) < (simResult?.totalHistoricalStrs ?? 18)
                    ? 'text-critical'
                    : 'text-accent'
                }`}
              >
                {(simResult?.truePositivesRetained ?? 18) < (simResult?.totalHistoricalStrs ?? 18)
                  ? 'Warning: STR loss detected'
                  : '100% statutory STR retention'}
              </div>
            </div>

            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">Estimated P99 Latency</div>
              <div className="text-xl font-bold text-primary mt-0.5">
                {simResult?.estimatedP99LatencyMs ?? 8.4} ms
              </div>
              <div className="text-[10px] text-muted mt-0.5">
                +{simResult?.newlyCapturedAlerts ?? 0} new / -{simResult?.newlySuppressedAlerts ?? 0} dropped
              </div>
            </div>
          </div>

          {/* Score Band Histogram */}
          <div className="space-y-2.5">
            <div className="text-muted text-[10px] uppercase font-bold">
              Dynamic Score Distribution Histogram
            </div>
            <div className="space-y-2">
              {scoreBands.map((item) => (
                <div key={item.band} className="space-y-1">
                  <div className="flex justify-between text-[11px]">
                    <span className="text-ink">{item.band}</span>
                    <span className="text-muted font-bold">{item.count.toLocaleString()} items</span>
                  </div>
                  <div className="w-full h-2 rounded-full bg-border overflow-hidden">
                    <div className={`h-full ${item.color} transition-all duration-300`} style={{ width: `${item.pct}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Interactive Cut-off Threshold Sensitivity Curve */}
          {simResult?.thresholdSensitivity && simResult.thresholdSensitivity.length > 0 && (
            <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-[10px] uppercase font-bold text-ink">
                  Cut-off Threshold Sensitivity Curve (Click step to calibrate)
                </span>
                <span className="text-[10px] text-muted">Projected Alerts vs. STR Recall</span>
              </div>
              <div className="grid grid-cols-5 gap-2">
                {simResult.thresholdSensitivity.map((step) => (
                  <button
                    key={step.threshold}
                    type="button"
                    onClick={() => {
                      setMinThreshold(step.threshold);
                      executePortfolioSimulation(
                        nameWeight,
                        nationalityWeight,
                        dobWeight,
                        idWeight,
                        step.threshold,
                        datasetCohort,
                        baselineWeights
                      );
                    }}
                    className={`p-2 rounded border text-left transition cursor-pointer ${
                      step.isSelected
                        ? 'border-primary bg-primary/10 shadow-2xs'
                        : 'border-border bg-surface hover:border-primary/40'
                    }`}
                  >
                    <div className="text-[10px] font-bold text-ink">{step.threshold}% Cut-off</div>
                    <div className="text-xs font-bold text-primary mt-0.5">
                      {step.projectedAlerts.toLocaleString()}
                    </div>
                    <div
                      className={`text-[9px] font-semibold mt-0.5 ${
                        step.strRecallCount < step.totalStrCount ? 'text-critical' : 'text-accent'
                      }`}
                    >
                      STRs: {step.strRecallCount}/{step.totalStrCount}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Sample Impacted Counterparties Delta Table */}
          {simResult?.sampleImpactedEntities && simResult.sampleImpactedEntities.length > 0 && (
            <div className="space-y-2">
              <div className="flex flex-wrap justify-between items-center gap-2">
                <span className="text-[10px] uppercase font-bold text-muted flex items-center gap-1.5">
                  <Eye className="w-3.5 h-3.5 text-primary" />
                  <span>Sample Impacted Counterparties (Baseline vs. Candidate Matrix)</span>
                </span>
                <div className="flex flex-wrap gap-1">
                  {(
                    [
                      { id: 'ALL', label: 'All' },
                      { id: 'NEWLY_CAPTURED', label: '+ Captured' },
                      { id: 'NEWLY_SUPPRESSED', label: '- Suppressed' },
                      { id: 'STR_CONFIRMED', label: 'STR Confirmed' }
                    ] as const
                  ).map((f) => (
                    <button
                      key={f.id}
                      type="button"
                      onClick={() => setEntityFilter(f.id)}
                      className={`px-2 py-0.5 rounded text-[10px] font-bold transition ${
                        entityFilter === f.id
                          ? 'bg-primary text-white'
                          : 'bg-surface-muted text-muted hover:text-ink border border-border'
                      }`}
                    >
                      {f.label}
                    </button>
                  ))}
                </div>
              </div>

              <div className="overflow-x-auto rounded-lg border border-border bg-surface-muted max-h-52">
                <table className="w-full text-left font-mono text-[11px]">
                  <thead className="bg-surface text-muted border-b border-border sticky top-0">
                    <tr>
                      <th className="py-1.5 px-2.5">Entity / Corridor</th>
                      <th className="py-1.5 px-2.5">Base → Sim Score</th>
                      <th className="py-1.5 px-2.5">Name / ID</th>
                      <th className="py-1.5 px-2.5">Delta &amp; Truth</th>
                      <th className="py-1.5 px-2.5 text-right">Inspect</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {simResult.sampleImpactedEntities
                      .filter((e) => {
                        if (entityFilter === 'NEWLY_CAPTURED') return e.deltaStatus === 'NEWLY_CAPTURED';
                        if (entityFilter === 'NEWLY_SUPPRESSED') return e.deltaStatus === 'NEWLY_SUPPRESSED';
                        if (entityFilter === 'STR_CONFIRMED') return e.groundTruth === 'STR_CONFIRMED';
                        return true;
                      })
                      .map((ent) => (
                        <tr key={ent.referenceId} className="hover:bg-surface/70">
                          <td className="py-1.5 px-2.5">
                            <div className="font-bold text-ink truncate max-w-[180px]">{ent.entityName}</div>
                            <div className="text-[10px] text-muted">
                              {ent.referenceId} • {ent.corridor}
                            </div>
                          </td>
                          <td className="py-1.5 px-2.5 whitespace-nowrap">
                            <span className="text-muted">{ent.baselineScore}%</span>
                            <span className="mx-1 text-muted">→</span>
                            <span className="font-bold text-primary">{ent.simulatedScore}%</span>
                          </td>
                          <td className="py-1.5 px-2.5 whitespace-nowrap text-muted">
                            N:{ent.nameMatchScore}% / 2nd:{ent.secondaryIdScore}%
                          </td>
                          <td className="py-1.5 px-2.5 whitespace-nowrap">
                            <div className="flex items-center gap-1">
                              <span
                                className={`px-1.5 py-0.5 rounded text-[9px] font-bold ${
                                  ent.deltaStatus === 'NEWLY_CAPTURED'
                                    ? 'bg-warning/20 text-warning'
                                    : ent.deltaStatus === 'NEWLY_SUPPRESSED'
                                    ? 'bg-primary/15 text-primary'
                                    : 'bg-accent/15 text-accent'
                                }`}
                              >
                                {ent.deltaStatus}
                              </span>
                              <span
                                className={`px-1.5 py-0.5 rounded text-[9px] font-bold ${
                                  ent.groundTruth === 'STR_CONFIRMED'
                                    ? 'bg-critical/20 text-critical'
                                    : 'bg-slate-200 text-slate-700'
                                }`}
                              >
                                {ent.groundTruth}
                              </span>
                            </div>
                          </td>
                          <td className="py-1.5 px-2.5 text-right whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() =>
                                navigate(`/kyc360?customer=${encodeURIComponent(ent.entityName.split(' #')[0])}`)
                              }
                              className="px-2 py-0.5 rounded border border-border bg-surface hover:border-primary text-primary text-[10px] font-bold inline-flex items-center gap-1"
                            >
                              <span>KYC</span>
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

          {/* Deploy Action */}
          <div className="pt-4 border-t border-border flex flex-wrap justify-between items-center gap-3">
            <div className="text-muted text-[11px]">
              Stage in 7-day shadow observation or promote directly to cluster active runtime:
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleStageShadowConfig}
                disabled={!isWeightValid || isPromoting}
                className="px-3.5 py-2 rounded-lg border border-warning/40 bg-warning/15 text-warning font-bold hover:bg-warning/25 flex items-center gap-1.5 disabled:opacity-50"
              >
                <ShieldCheck className="w-4 h-4" />
                <span>Stage 7-Day Shadow</span>
              </button>
              <button
                type="button"
                onClick={handlePromoteConfig}
                disabled={!isWeightValid || isPromoting}
                className="px-4 py-2 rounded-lg bg-accent text-white font-bold hover:bg-accent-hover shadow-xs flex items-center gap-1.5 disabled:opacity-50"
              >
                <span>{isPromoting ? 'Promoting...' : 'Promote to Active Config'}</span>
                <ArrowRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
