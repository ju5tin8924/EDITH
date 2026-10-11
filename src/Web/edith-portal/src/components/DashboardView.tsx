import React from 'react';
import { 
  BarChart3, 
  Activity, 
  ShieldCheck, 
  ShieldAlert, 
  Clock, 
  Globe, 
  TrendingUp
} from 'lucide-react';

export const DashboardView: React.FC = () => {
  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Title */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <h1 className="text-xl font-bold text-white flex items-center gap-2.5">
            <BarChart3 className="w-6 h-6 text-primary" />
            Executive AML Screening & Intelligence Overview
          </h1>
          <p className="text-xs text-slate-400 mt-1">
            Real-time throughput metrics, latency SLA adherence, and threat vector distribution across GCC corridors.
          </p>
        </div>

        <div className="flex items-center gap-2 text-xs font-mono">
          <div className="px-3 py-1.5 rounded-lg bg-emerald-950/60 border border-emerald-800/40 text-emerald-300 flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
            All 5 Microservices Operational
          </div>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Metric 1 */}
        <div className="bg-surface p-4 rounded-xl border border-border space-y-2">
          <div className="flex justify-between items-center text-slate-400 text-xs font-medium">
            <span>Screened Today</span>
            <Activity className="w-4 h-4 text-primary" />
          </div>
          <div className="text-2xl font-bold font-mono text-white">2,418,920</div>
          <div className="flex items-center gap-1.5 text-[11px] text-accent font-mono">
            <TrendingUp className="w-3.5 h-3.5" />
            <span>+14.2% vs yesterday peak</span>
          </div>
        </div>

        {/* Metric 2 */}
        <div className="bg-surface p-4 rounded-xl border border-border space-y-2">
          <div className="flex justify-between items-center text-slate-400 text-xs font-medium">
            <span>Auto-Clear Rate</span>
            <ShieldCheck className="w-4 h-4 text-accent" />
          </div>
          <div className="text-2xl font-bold font-mono text-accent">98.42%</div>
          <div className="text-[11px] text-slate-400 font-mono">
            1.58% Held for EDD Review
          </div>
        </div>

        {/* Metric 3 */}
        <div className="bg-surface p-4 rounded-xl border border-border space-y-2">
          <div className="flex justify-between items-center text-slate-400 text-xs font-medium">
            <span>P99 Engine Latency</span>
            <Clock className="w-4 h-4 text-primary" />
          </div>
          <div className="text-2xl font-bold font-mono text-primary">8.4 ms</div>
          <div className="text-[11px] text-slate-400 font-mono">
            Estimated Target: &lt;20.0 ms
          </div>
        </div>

        {/* Metric 4 */}
        <div className="bg-surface p-4 rounded-xl border border-border space-y-2">
          <div className="flex justify-between items-center text-slate-400 text-xs font-medium">
            <span>Active Investigation Cases</span>
            <ShieldAlert className="w-4 h-4 text-critical" />
          </div>
          <div className="text-2xl font-bold font-mono text-critical">42 Open</div>
          <div className="text-[11px] text-critical/80 font-mono">
            3 Critical • 12 High • 27 Medium
          </div>
        </div>
      </div>

      {/* Middle Grid: Corridors & Hit Vectors */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Threat Distribution (7 cols) */}
        <div className="lg:col-span-7 bg-surface rounded-xl border border-border p-5 space-y-4">
          <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-slate-300 pb-2 border-b border-border flex items-center justify-between">
            <span className="flex items-center gap-2">
              <Globe className="w-4 h-4 text-primary" />
              High-Risk Corridors & FATF Monitored Intercepts
            </span>
            <span className="text-[11px] font-mono text-slate-500">Live 24h</span>
          </h2>

          <div className="space-y-3">
            {[
              { corridor: 'UAE (AE) -> Iran (IR)', risk: 'CRITICAL', volume: 'AED 14.8M', hits: 18, pct: 85 },
              { corridor: 'UAE (AE) -> Syria (SY)', risk: 'CRITICAL', volume: 'AED 6.2M', hits: 9, pct: 60 },
              { corridor: 'UAE (AE) -> Russia (RU)', risk: 'HIGH', volume: 'USD 82.5M', hits: 34, pct: 75 },
              { corridor: 'UAE (AE) -> North Korea (KP)', risk: 'CRITICAL', volume: 'AED 0.00', hits: 0, pct: 10 },
              { corridor: 'UAE (AE) -> Myanmar (MM)', risk: 'HIGH', volume: 'AED 1.1M', hits: 3, pct: 30 }
            ].map((item, idx) => (
              <div key={idx} className="p-3 rounded-lg bg-slate-900 border border-border space-y-2 text-xs">
                <div className="flex justify-between items-center">
                  <span className="font-semibold text-slate-200">{item.corridor}</span>
                  <div className="flex items-center gap-2 font-mono">
                    <span className="text-slate-400">{item.volume}</span>
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${
                      item.risk === 'CRITICAL' ? 'bg-critical/20 text-critical' : 'bg-warning/20 text-warning'
                    }`}>
                      {item.hits} Hits ({item.risk})
                    </span>
                  </div>
                </div>
                <div className="w-full bg-slate-950 rounded-full h-1.5 overflow-hidden">
                  <div
                    className={`h-full rounded-full ${item.risk === 'CRITICAL' ? 'bg-critical' : 'bg-warning'}`}
                    style={{ width: `${item.pct}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Watchlist Hit Types (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-4">
          <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-slate-300 pb-2 border-b border-border">
            Screening Hits by Designation Category
          </h2>

          <div className="space-y-4 text-xs">
            <div className="p-3.5 rounded-lg bg-slate-900 border border-border space-y-2">
              <div className="flex justify-between font-mono">
                <span className="text-slate-300 font-semibold">1. Statutory Sanctions (UN / OFAC / UAE)</span>
                <span className="text-critical font-bold">62%</span>
              </div>
              <div className="w-full bg-slate-950 rounded-full h-2">
                <div className="h-full bg-critical rounded-full" style={{ width: '62%' }} />
              </div>
              <span className="text-[10px] text-slate-500 font-mono">Zero false tolerance. Immediate statutory block.</span>
            </div>

            <div className="p-3.5 rounded-lg bg-slate-900 border border-border space-y-2">
              <div className="flex justify-between font-mono">
                <span className="text-slate-300 font-semibold">2. PEP (Politically Exposed Persons)</span>
                <span className="text-amber-400 font-bold">24%</span>
              </div>
              <div className="w-full bg-slate-950 rounded-full h-2">
                <div className="h-full bg-amber-400 rounded-full" style={{ width: '24%' }} />
              </div>
              <span className="text-[10px] text-slate-500 font-mono">Tier 1-3 PEPs subject to senior MLRO sign-off.</span>
            </div>

            <div className="p-3.5 rounded-lg bg-slate-900 border border-border space-y-2">
              <div className="flex justify-between font-mono">
                <span className="text-slate-300 font-semibold">3. Adverse Media & Law Enforcement</span>
                <span className="text-primary-light font-bold">14%</span>
              </div>
              <div className="w-full bg-slate-950 rounded-full h-2">
                <div className="h-full bg-primary-light rounded-full" style={{ width: '14%' }} />
              </div>
              <span className="text-[10px] text-slate-500 font-mono">Reputational and financial crime flags.</span>
            </div>
          </div>
        </div>
      </div>

      {/* Live Event Stream from Kafka */}
      <div className="bg-surface rounded-xl border border-border p-5 space-y-4">
        <h2 className="text-xs font-mono uppercase tracking-wider font-semibold text-slate-300 pb-2 border-b border-border flex items-center justify-between">
          <span className="flex items-center gap-2">
            <Activity className="w-4 h-4 text-accent" />
            Live Event Stream (Topic: edith.tx.held.v1)
          </span>
          <span className="text-[11px] font-mono text-slate-500">Kafka 3.9 KRaft Consumer</span>
        </h2>

        <div className="space-y-2 font-mono text-xs">
          {[
            { id: 'EVT-901', time: '14:22:01', tx: 'TX-2026-9921448', amount: 'USD 450,000', party: 'Victor Bout', score: 96.4, action: 'HELD_FOR_CASE' },
            { id: 'EVT-902', time: '14:21:49', tx: 'TX-2026-9921447', amount: 'AED 850,000', party: 'Caspian Petrochem', score: 88.2, action: 'HELD_FOR_CASE' },
            { id: 'EVT-903', time: '14:21:12', tx: 'TX-2026-9921446', amount: 'AED 3,200', party: 'Salem Mohammed', score: 12.0, action: 'AUTO_CLEARED' }
          ].map((evt) => (
            <div key={evt.id} className="p-2.5 rounded bg-slate-900 border border-border/80 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className="text-slate-500 text-[11px]">{evt.time}</span>
                <span className="text-slate-300 font-bold">{evt.tx}</span>
                <span className="text-slate-400">({evt.amount})</span>
                <span className="text-slate-200">Party: "{evt.party}"</span>
              </div>
              <div className="flex items-center gap-2">
                <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                  evt.score > 80 ? 'bg-critical/20 text-critical' : 'bg-accent/20 text-accent'
                }`}>
                  Risk {evt.score}
                </span>
                <span className="text-slate-400 text-[11px]">{evt.action}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
