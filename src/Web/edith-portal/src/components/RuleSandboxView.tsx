import React, { useState } from 'react';
import { 
  FlaskConical, 
  Play, 
  RotateCcw, 
  Sliders, 
  BarChart, 
  ArrowRight
} from 'lucide-react';

export const RuleSandboxView: React.FC = () => {
  // Slider states
  const [nameWeight, setNameWeight] = useState(60);
  const [nationalityWeight, setNationalityWeight] = useState(10);
  const [dobWeight, setDobWeight] = useState(15);
  const [idWeight, setIdWeight] = useState(15);
  const [minThreshold, setMinThreshold] = useState(80);

  const [isRunning, setIsRunning] = useState(false);

  // Dynamic calculations based on slider values
  const totalWeight = nameWeight + nationalityWeight + dobWeight + idWeight;
  const isWeightValid = totalWeight === 100;

  // Simulated metrics
  const simulatedAlerts = Math.round(520 * (85 / minThreshold) * (nameWeight / 60));
  const fpReduction = ((820 - simulatedAlerts) / 820 * 100).toFixed(1);

  const runSimulation = () => {
    setIsRunning(true);
    setTimeout(() => {
      setIsRunning(false);
    }, 1200);
  };

  const resetDefaults = () => {
    setNameWeight(60);
    setNationalityWeight(10);
    setDobWeight(15);
    setIdWeight(15);
    setMinThreshold(80);
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 11 // MOD-SIM-SAND
            </span>
            <span className="text-[10px] font-mono text-muted">Historical Replay & Weightage Calibration</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <FlaskConical className="w-5 h-5 text-primary" />
            Rule Sandbox & Historical Backtesting Workbench
          </h1>
          <p className="text-xs text-muted mt-1">
            Simulate composite scoring algorithms and phonetic threshold variations against 50,000+ historical banking records to measure false positive reduction.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={resetDefaults}
            className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink text-xs font-mono flex items-center gap-1.5"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset Baseline</span>
          </button>
          <button
            onClick={runSimulation}
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
          <span className="font-bold">Adjust sliders below</span>
        </div>
      )}

      {/* Main Grid: Sliders Left, Simulation Metrics Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Sliders Workbench (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-5 font-mono text-xs">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <Sliders className="w-4 h-4 text-primary" />
              Scoring Weightage Matrix
            </h3>
            <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${isWeightValid ? 'bg-accent/20 text-accent' : 'bg-critical/20 text-critical'}`}>
              Sum: {totalWeight}%
            </span>
          </div>

          {/* Slider 1: Name Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">1. Name Similarity Weightage</span>
              <span className="font-bold text-primary">{nameWeight}%</span>
            </div>
            <input
              type="range"
              min="20"
              max="90"
              value={nameWeight}
              onChange={e => setNameWeight(Number(e.target.value))}
              className="w-full accent-primary"
            />
            <div className="text-[10px] text-muted">Double Metaphone + Jaro-Winkler multi-token alignment</div>
          </div>

          {/* Slider 2: Nationality Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">2. Nationality & Residence Weightage</span>
              <span className="font-bold text-primary">{nationalityWeight}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="40"
              value={nationalityWeight}
              onChange={e => setNationalityWeight(Number(e.target.value))}
              className="w-full accent-primary"
            />
            <div className="text-[10px] text-muted">FATF high-risk country matrix and passport origin match</div>
          </div>

          {/* Slider 3: Date of Birth Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">3. Date of Birth (DOB) Weightage</span>
              <span className="font-bold text-primary">{dobWeight}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="40"
              value={dobWeight}
              onChange={e => setDobWeight(Number(e.target.value))}
              className="w-full accent-primary"
            />
            <div className="text-[10px] text-muted">Year, month, day exactness with ±1 year fuzzy grace tolerance</div>
          </div>

          {/* Slider 4: National ID Weight */}
          <div className="space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-ink">4. National ID & Passport Weightage</span>
              <span className="font-bold text-primary">{idWeight}%</span>
            </div>
            <input
              type="range"
              min="0"
              max="40"
              value={idWeight}
              onChange={e => setIdWeight(Number(e.target.value))}
              className="w-full accent-primary"
            />
            <div className="text-[10px] text-muted">National identification and passport number verification</div>
          </div>

          {/* Slider 5: Threshold Score */}
          <div className="pt-3 border-t border-border space-y-1.5">
            <div className="flex justify-between items-center">
              <span className="font-bold text-critical">Cut-off Alert Threshold</span>
              <span className="font-bold text-critical text-sm">{minThreshold}%</span>
            </div>
            <input
              type="range"
              min="60"
              max="95"
              value={minThreshold}
              onChange={e => setMinThreshold(Number(e.target.value))}
              className="w-full accent-critical"
            />
            <div className="text-[10px] text-muted">Composite scores &gt;= {minThreshold}% generate statutory case alerts</div>
          </div>
        </div>

        {/* Simulation Analytics & Histogram (7 cols) */}
        <div className="lg:col-span-7 bg-surface rounded-xl border border-border p-5 space-y-5 font-mono text-xs">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <BarChart className="w-4 h-4 text-primary" />
              Replay Projection Against 50,000 Transactions
            </h3>
            <span className="text-[10px] text-accent font-bold">Simulated Dataset: Q3 2026 Archive</span>
          </div>

          {/* 3 Metrics */}
          <div className="grid grid-cols-3 gap-3">
            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">Projected Alerts</div>
              <div className="text-xl font-bold text-ink mt-0.5">{simulatedAlerts}</div>
              <div className="text-[10px] text-muted mt-0.5">vs 820 baseline</div>
            </div>
            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">FP Alert Reduction</div>
              <div className="text-xl font-bold text-accent mt-0.5">-{fpReduction}%</div>
              <div className="text-[10px] text-accent mt-0.5">Alert fatigue reduction</div>
            </div>
            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-muted text-[10px]">Estimated P99 Latency</div>
              <div className="text-xl font-bold text-primary mt-0.5">8.4 ms</div>
              <div className="text-[10px] text-muted mt-0.5">Zero regression</div>
            </div>
          </div>

          {/* Score Band Histogram */}
          <div className="space-y-3">
            <div className="text-muted text-[10px] uppercase font-bold">Score Distribution Histogram</div>
            <div className="space-y-2">
              {[
                { band: '90 - 100% (High Confidence Hits)', count: 48, pct: 15, color: 'bg-critical' },
                { band: '80 - 89% (Actionable Case Threshold)', count: 184, pct: 45, color: 'bg-warning' },
                { band: '70 - 79% (Suppressed Gray Zone)', count: 340, pct: 75, color: 'bg-primary' },
                { band: '50 - 69% (Auto-Cleared Noise)', count: 1820, pct: 100, color: 'bg-slate-400' }
              ].map(item => (
                <div key={item.band} className="space-y-1">
                  <div className="flex justify-between text-[11px]">
                    <span className="text-ink">{item.band}</span>
                    <span className="text-muted font-bold">{item.count} items</span>
                  </div>
                  <div className="w-full h-2 rounded-full bg-border overflow-hidden">
                    <div className={`h-full ${item.color}`} style={{ width: `${item.pct}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Deploy Action */}
          <div className="pt-4 border-t border-border flex justify-between items-center">
            <div className="text-muted text-[11px]">
              Ready to promote configuration to cluster active runtime?
            </div>
            <button className="px-4 py-2 rounded-lg bg-accent text-white font-bold hover:bg-accent-hover shadow-xs flex items-center gap-1.5">
              <span>Promote to Active Config</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
