import React, { useState } from 'react';
import { 
  BellRing, 
  Send, 
  CheckCircle2, 
  MessageSquare, 
  Mail, 
  Smartphone, 
  ShieldAlert, 
  ToggleLeft, 
  ToggleRight
} from 'lucide-react';

interface NotificationRule {
  id: string;
  name: string;
  triggerEvent: string;
  channels: ('TEAMS' | 'EMAIL' | 'SMS' | 'WEBHOOK')[];
  shiftAware: boolean;
  active: boolean;
}

const MOCK_RULES: NotificationRule[] = [
  {
    id: 'notif-01',
    name: 'Statutory Freeze Emergency Alert',
    triggerEvent: 'Transaction Frozen (OFAC / UN Hit)',
    channels: ['TEAMS', 'SMS', 'WEBHOOK'],
    shiftAware: true,
    active: true
  },
  {
    id: 'notif-02',
    name: 'SLA Escalation (72h EDD Limit Near)',
    triggerEvent: 'Case Inactivity > 48 Hours',
    channels: ['TEAMS', 'EMAIL'],
    shiftAware: true,
    active: true
  },
  {
    id: 'notif-03',
    name: 'Nightly Delta Rescreening Digest',
    triggerEvent: 'Delta Batch Processing Finished',
    channels: ['EMAIL', 'WEBHOOK'],
    shiftAware: false,
    active: true
  }
];

export const NotificationAdminView: React.FC = () => {
  const [rules, setRules] = useState<NotificationRule[]>(MOCK_RULES);
  const [activeRule, setActiveRule] = useState<NotificationRule>(MOCK_RULES[0]);

  const toggleRuleActive = (id: string) => {
    setRules(prev => prev.map(r => r.id === id ? { ...r, active: !r.active } : r));
  };

  const toggleShiftAware = (id: string) => {
    setRules(prev => prev.map(r => r.id === id ? { ...r, shiftAware: !r.shiftAware } : r));
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              PAGE 10 // MOD-NOTIF-DYN
            </span>
            <span className="text-[10px] font-mono text-muted">Multi-Channel Relay & Teams Integration</span>
          </div>
          <h1 className="text-xl font-bold text-ink flex items-center gap-2.5 mt-1">
            <BellRing className="w-5 h-5 text-primary" />
            Dynamic Notification & Webhook Relay Administration
          </h1>
          <p className="text-xs text-muted mt-1">
            Configure shift-aware alert routing, interactive Microsoft Teams Adaptive Cards, and cryptographically signed Core Banking callbacks.
          </p>
        </div>

        <div className="flex items-center gap-2 font-mono text-xs">
          <div className="px-3 py-1.5 rounded-lg bg-accent/10 border border-accent/30 text-accent font-bold flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4" />
            <span>Webhook Relay: 100% Delivery (0 Drops)</span>
          </div>
        </div>
      </div>

      {/* Main Grid: Rules Left, Teams Preview Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Rules Config List (7 cols) */}
        <div className="lg:col-span-7 bg-surface rounded-xl border border-border p-5 space-y-4 font-mono text-xs">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <BellRing className="w-4 h-4 text-primary" />
              Configured Alert Dispatches
            </h3>
            <span className="text-[10px] text-muted">{rules.length} Active Rules</span>
          </div>

          <div className="space-y-3">
            {rules.map(rule => (
              <div 
                key={rule.id}
                onClick={() => setActiveRule(rule)}
                className={`p-4 rounded-xl border transition-all cursor-pointer ${
                  activeRule.id === rule.id
                    ? 'border-primary bg-primary/5 shadow-xs'
                    : 'border-border bg-surface-muted hover:bg-surface-hover'
                }`}
              >
                <div className="flex justify-between items-start">
                  <div>
                    <div className="font-bold text-ink text-sm">{rule.name}</div>
                    <div className="text-[11px] text-muted mt-0.5">Trigger: {rule.triggerEvent}</div>
                  </div>

                  <button 
                    onClick={(e) => { e.stopPropagation(); toggleRuleActive(rule.id); }}
                    className="text-primary"
                  >
                    {rule.active ? (
                      <ToggleRight className="w-6 h-6 text-accent" />
                    ) : (
                      <ToggleLeft className="w-6 h-6 text-muted" />
                    )}
                  </button>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 pt-3 mt-3 border-t border-border/60">
                  <div className="flex items-center gap-1.5">
                    {rule.channels.includes('TEAMS') && (
                      <span className="px-2 py-0.5 rounded text-[10px] bg-primary text-white font-bold flex items-center gap-1">
                        <MessageSquare className="w-3 h-3" /> Teams
                      </span>
                    )}
                    {rule.channels.includes('EMAIL') && (
                      <span className="px-2 py-0.5 rounded text-[10px] bg-surface border border-border text-ink font-bold flex items-center gap-1">
                        <Mail className="w-3 h-3 text-primary" /> Email
                      </span>
                    )}
                    {rule.channels.includes('SMS') && (
                      <span className="px-2 py-0.5 rounded text-[10px] bg-surface border border-border text-ink font-bold flex items-center gap-1">
                        <Smartphone className="w-3 h-3 text-warning" /> SMS
                      </span>
                    )}
                    {rule.channels.includes('WEBHOOK') && (
                      <span className="px-2 py-0.5 rounded text-[10px] bg-accent/20 border border-accent/40 text-accent font-bold flex items-center gap-1">
                        <Send className="w-3 h-3" /> Webhook
                      </span>
                    )}
                  </div>

                  <button
                    onClick={(e) => { e.stopPropagation(); toggleShiftAware(rule.id); }}
                    className={`px-2 py-0.5 rounded text-[10px] font-bold border transition-colors ${
                      rule.shiftAware 
                        ? 'bg-accent/10 border-accent/30 text-accent' 
                        : 'bg-surface border-border text-muted'
                    }`}
                  >
                    {rule.shiftAware ? '✓ Shift-Presence Active' : 'All Officers (24/7)'}
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Adaptive Card Live Preview (5 cols) */}
        <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-4 font-mono text-xs">
          <div className="flex items-center justify-between pb-3 border-b border-border">
            <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
              <MessageSquare className="w-4 h-4 text-primary" />
              Microsoft Teams Adaptive Card Preview
            </h3>
            <span className="text-[10px] font-bold text-accent">Live Simulation</span>
          </div>

          <div className="p-4 rounded-xl bg-[#0f172a] text-white border border-slate-700 space-y-3 font-sans shadow-lg">
            <div className="flex items-center justify-between pb-2 border-b border-slate-700 font-mono text-xs">
              <div className="flex items-center gap-2">
                <ShieldAlert className="w-4 h-4 text-critical animate-pulse" />
                <span className="font-bold text-red-400">CRITICAL STATUTORY FREEZE</span>
              </div>
              <span className="text-slate-400 text-[10px]">Just now</span>
            </div>

            <div className="space-y-1">
              <div className="font-bold text-white text-sm">Intercepted Wire: $450,000.00 USD</div>
              <div className="text-slate-300 text-xs">
                Inbound SWIFT MT103 from Dubai (ARE) to Zurich (CHE) matched UN Security Council Res 1521.
              </div>
            </div>

            <div className="p-2.5 rounded bg-slate-800/80 font-mono text-xs space-y-1 border border-slate-700">
              <div className="flex justify-between text-slate-300">
                <span>Beneficiary Subject:</span>
                <span className="font-bold text-red-300">Victor Anatolyevich Bout</span>
              </div>
              <div className="flex justify-between text-slate-300">
                <span>Case Reference:</span>
                <span className="font-bold text-primary-light">EDITH-CAS-2026-0042</span>
              </div>
              <div className="flex justify-between text-slate-300">
                <span>Routing Officer:</span>
                <span className="text-emerald-400 font-bold">Fatima Al-Nuaimi (On-Duty)</span>
              </div>
            </div>

            <div className="pt-2 flex gap-2 font-mono text-xs">
              <button className="flex-1 py-1.5 rounded bg-primary hover:bg-primary-hover text-white font-bold text-center">
                Acknowledge Alert
              </button>
              <button className="flex-1 py-1.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 font-bold text-center border border-slate-600">
                Open Case Canvas
              </button>
            </div>
          </div>

          <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-1 font-mono text-[11px] text-muted">
            <div className="text-ink font-bold">Two-Way Actionable Webhook Support:</div>
            <div>
              When an officer clicks `Acknowledge Alert` inside Microsoft Teams, the callback cryptographically verifies via HMAC and transitions the case in PostgreSQL without requiring browser login.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
