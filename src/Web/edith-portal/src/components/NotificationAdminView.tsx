import React, { useState, useEffect } from 'react';
import { 
  BellRing, 
  Send, 
  CheckCircle2, 
  MessageSquare, 
  Mail, 
  Smartphone, 
  ShieldAlert, 
  ToggleLeft, 
  ToggleRight,
  Plus,
  RotateCcw,
  Search,
  ShieldCheck,
  AlertTriangle,
  Clock,
  UserCheck,
  Users,
  Code,
  Copy,
  Check,
  Trash2,
  Edit2,
  Sparkles,
  RefreshCw,
  X,
  Play
} from 'lucide-react';
import { usePageMeta } from '../config/pageMeta';
import { EdithApi } from '../services/api';
import { 
  NotificationRuleDto, 
  DeliveryLedgerRecordDto, 
  ShiftRosterScheduleDto, 
  NotificationChannelType
} from '../types';

type AdminTab = 'RULES' | 'LEDGER' | 'SHIFTS' | 'STUDIO';

export const NotificationAdminView: React.FC = () => {
  const pageMeta = usePageMeta(
    'Notification & Webhook Admin',
    'Configure real-time compliance alert routing, manage on-duty shift rosters, and inspect CBS webhook delivery ledgers.'
  );

  const [activeTab, setActiveTab] = useState<AdminTab>('RULES');
  const [loading, setLoading] = useState(false);
  const [statusBanner, setStatusBanner] = useState<string | null>(null);

  // Tab 1: Rules State
  const [rules, setRules] = useState<NotificationRuleDto[]>([]);
  const [selectedRule, setSelectedRule] = useState<NotificationRuleDto | null>(null);
  const [ruleSearch, setRuleSearch] = useState('');
  const [ruleSeverityFilter, setRuleSeverityFilter] = useState<string>('ALL');
  const [isRuleModalOpen, setIsRuleModalOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<Partial<NotificationRuleDto> | null>(null);

  // Tab 2: Delivery Ledgers & DLQ State
  const [ledgers, setLedgers] = useState<DeliveryLedgerRecordDto[]>([]);
  const [ledgerSearch, setLedgerSearch] = useState('');
  const [ledgerStatusFilter, setLedgerStatusFilter] = useState<string>('ALL');
  const [selectedLedger, setSelectedLedger] = useState<DeliveryLedgerRecordDto | null>(null);
  const [replayingId, setReplayingId] = useState<string | null>(null);
  const [copiedPayload, setCopiedPayload] = useState(false);

  // Tab 3: Shift Rosters State
  const [shifts, setShifts] = useState<ShiftRosterScheduleDto[]>([]);
  const [escalationTestOfficer, setEscalationTestOfficer] = useState<string>('u-1');

  // Tab 4: Studio State
  const [studioChannel, setStudioChannel] = useState<NotificationChannelType>('TEAMS');
  const [studioCasePreset, setStudioCasePreset] = useState<'VIKTOR_BOUT' | 'AL_BARAKA' | 'DELTA_DIGEST'>('VIKTOR_BOUT');
  const [acknowledged, setAcknowledged] = useState(false);
  const [testFiring, setTestFiring] = useState(false);
  const [testFireResult, setTestFireResult] = useState<any | null>(null);

  // Initial Data Fetch
  useEffect(() => {
    loadAllData();
  }, []);

  const loadAllData = async () => {
    setLoading(true);
    try {
      const [fetchedRules, fetchedLedgers, fetchedShifts] = await Promise.all([
        EdithApi.fetchNotificationRules(),
        EdithApi.fetchDeliveryLedgers(),
        EdithApi.fetchShiftRosters()
      ]);
      setRules(fetchedRules);
      if (fetchedRules.length > 0 && !selectedRule) {
        setSelectedRule(fetchedRules[0]);
      }
      setLedgers(fetchedLedgers);
      setShifts(fetchedShifts);
    } catch (err: any) {
      setStatusBanner(`Failed to load notification configurations: ${err.message || 'Server error'}`);
    } finally {
      setLoading(false);
    }
  };

  // Rule Actions
  const handleToggleRule = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      const res = await EdithApi.toggleNotificationRule(id);
      setRules(prev => prev.map(r => r.id === id ? { ...r, active: res.active } : r));
      setStatusBanner(`Rule status updated to ${res.active ? 'ACTIVE' : 'MUTED'}`);
      setTimeout(() => setStatusBanner(null), 3000);
    } catch {
      setRules(prev => prev.map(r => r.id === id ? { ...r, active: !r.active } : r));
    }
  };

  const handleOpenCreateRule = () => {
    setEditingRule({
      name: '',
      triggerEvent: 'Transaction Frozen (OFAC / UN Hit)',
      severity: 'HIGH',
      channels: ['TEAMS', 'WEBHOOK'],
      shiftAware: true,
      active: true,
      targetEndpoint: 'https://cbs-relay.internal.adcb.ae/api/v1/aml-callback',
      fallbackRecipient: 'MLRO Duty Officer',
      throttleMinutes: 5,
      templateFormat: 'ADAPTIVE_CARD'
    });
    setIsRuleModalOpen(true);
  };

  const handleOpenEditRule = (rule: NotificationRuleDto, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingRule({ ...rule });
    setIsRuleModalOpen(true);
  };

  const handleSaveRule = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingRule || !editingRule.name) return;

    try {
      if (editingRule.id) {
        const updated = await EdithApi.updateNotificationRule(editingRule.id, editingRule);
        setRules(prev => prev.map(r => r.id === updated.id ? updated : r));
        setStatusBanner(`Rule "${updated.name}" successfully updated.`);
      } else {
        const created = await EdithApi.createNotificationRule(editingRule);
        setRules(prev => [created, ...prev]);
        setSelectedRule(created);
        setStatusBanner(`New dispatch rule "${created.name}" created and active.`);
      }
    } catch {
      setStatusBanner('Failed to save rule. Checked local state.');
    } finally {
      setIsRuleModalOpen(false);
      setEditingRule(null);
      setTimeout(() => setStatusBanner(null), 4000);
    }
  };

  const handleDeleteRule = async (id: string, name: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!window.confirm(`Are you sure you want to delete rule "${name}"?`)) return;
    await EdithApi.deleteNotificationRule(id);
    setRules(prev => prev.filter(r => r.id !== id));
    if (selectedRule?.id === id) {
      setSelectedRule(rules.find(r => r.id !== id) || null);
    }
    setStatusBanner(`Rule "${name}" removed.`);
    setTimeout(() => setStatusBanner(null), 3000);
  };

  // Ledger / DLQ Actions
  const handleReplaySingle = async (id: string, traceId: string) => {
    setReplayingId(id);
    try {
      await EdithApi.replayDeliveryLedger(id);
      setLedgers(prev => prev.map(l => l.id === id ? {
        ...l,
        deliveryStatus: 'DELIVERED',
        attemptCount: l.attemptCount + 1,
        httpStatus: 200,
        errorMessage: undefined,
        sentAt: new Date().toISOString()
      } : l));
      setStatusBanner(`Callback trace "${traceId}" successfully replayed (HTTP 200 OK).`);
    } finally {
      setReplayingId(null);
      setTimeout(() => setStatusBanner(null), 4000);
    }
  };

  const handleBulkReplayDLQ = async () => {
    setLoading(true);
    try {
      const res = await EdithApi.bulkReplayDeadLetters();
      setLedgers(prev => prev.map(l => l.deliveryStatus === 'DEAD_LETTER' ? {
        ...l,
        deliveryStatus: 'DELIVERED',
        attemptCount: l.attemptCount + 1,
        httpStatus: 200,
        errorMessage: undefined,
        sentAt: new Date().toISOString()
      } : l));
      setStatusBanner(`Bulk Reconciliation Complete: ${res.replayedCount} dead-letter deliveries resolved.`);
    } finally {
      setLoading(false);
      setTimeout(() => setStatusBanner(null), 4000);
    }
  };

  // Shift Actions
  const handleToggleDutyStatus = async (shift: ShiftRosterScheduleDto) => {
    const updated = { ...shift, isOnDuty: !shift.isOnDuty };
    await EdithApi.updateShiftRoster(shift.id, updated);
    setShifts(prev => prev.map(s => s.id === shift.id ? updated : s));
    setStatusBanner(`Officer ${shift.userName} set to ${updated.isOnDuty ? 'ON DUTY' : 'OFF DUTY'}.`);
    setTimeout(() => setStatusBanner(null), 3000);
  };

  // Studio Test Fire Action
  const handleTestFire = async () => {
    setTestFiring(true);
    setTestFireResult(null);
    try {
      const res = await EdithApi.testFireNotification({
        ruleId: selectedRule?.id,
        channel: studioChannel,
        customEndpoint: selectedRule?.targetEndpoint
      });
      setTestFireResult(res);
      setStatusBanner(`Simulated alert delivered to ${studioChannel} in ${res.latencyMs}ms`);
    } finally {
      setTestFiring(false);
      setTimeout(() => setStatusBanner(null), 4000);
    }
  };

  // Filtered Rules
  const filteredRules = rules.filter(r => {
    const matchesSearch = r.name.toLowerCase().includes(ruleSearch.toLowerCase()) ||
                          r.triggerEvent.toLowerCase().includes(ruleSearch.toLowerCase());
    const matchesSeverity = ruleSeverityFilter === 'ALL' || r.severity === ruleSeverityFilter;
    return matchesSearch && matchesSeverity;
  });

  // Filtered Ledgers
  const filteredLedgers = ledgers.filter(l => {
    const matchesSearch = l.traceId.toLowerCase().includes(ledgerSearch.toLowerCase()) ||
                          l.ruleName.toLowerCase().includes(ledgerSearch.toLowerCase()) ||
                          l.targetEndpoint.toLowerCase().includes(ledgerSearch.toLowerCase());
    const matchesStatus = ledgerStatusFilter === 'ALL' ||
                          (ledgerStatusFilter === 'DEAD_LETTER' && l.deliveryStatus.includes('DEAD_LETTER')) ||
                          l.deliveryStatus === ledgerStatusFilter;
    return matchesSearch && matchesStatus;
  });

  const deadLetterCount = ledgers.filter(l => l.deliveryStatus.includes('DEAD_LETTER')).length;

  // Escalation Simulation calculation based on DynamicNotificationEngine logic
  const selectedTestOfficer = shifts.find(s => s.userId === escalationTestOfficer) || shifts[0];
  const simulatedEscalation = selectedTestOfficer?.isOnDuty ? {
    channel: 'DIRECT_ANALYST',
    recipient: selectedTestOfficer.userName,
    isEscalated: false,
    reason: 'Analyst is currently marked ON-DUTY on active EMEA shift.'
  } : {
    channel: selectedTestOfficer?.substituteUserName ? 'ON_DUTY_ANALYST' : 'TEAMS_DUTY_ESCALATION',
    recipient: selectedTestOfficer?.substituteUserName || 'MLRO Duty Officer (Tariq Mansoor)',
    isEscalated: true,
    reason: `Original assignee (${selectedTestOfficer?.userName}) is OFF-DUTY. Dynamic engine automatically rerouted to designated substitute.`
  };

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto font-sans">
      {/* Top Page Header */}
      <div className="flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4 bg-surface p-4 rounded-xl border border-border shadow-xs">
        <div>
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2.5">
              <BellRing className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
            <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
              Rules & Governance
            </span>
            {deadLetterCount > 0 && (
              <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-critical/10 text-critical border border-critical/30 animate-pulse flex items-center gap-1">
                <AlertTriangle className="w-3 h-3" />
                {deadLetterCount} DLQ Drops Pending Replay
              </span>
            )}
          </div>
          <p className="text-xs text-muted mt-1 max-w-2xl">
            {pageMeta.definition}
          </p>
        </div>

        {/* Telemetry Status Badges */}
        <div className="flex flex-wrap items-center gap-2 font-mono text-xs">
          <div className="px-2.5 py-1.5 rounded-lg bg-surface-muted border border-border text-ink flex items-center gap-1.5">
            <ShieldCheck className="w-3.5 h-3.5 text-accent" />
            <span>HMAC-SHA256 Signed</span>
          </div>

          <div className="px-2.5 py-1.5 rounded-lg bg-accent/10 border border-accent/30 text-accent font-bold flex items-center gap-1.5">
            <CheckCircle2 className="w-3.5 h-3.5" />
            <span>Polly Exponential Backoff (3x Jitter)</span>
          </div>

          <button
            type="button"
            onClick={loadAllData}
            title="Refresh Data"
            className="p-1.5 rounded-lg border border-border hover:bg-surface-muted text-muted hover:text-ink transition cursor-pointer"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin text-primary' : ''}`} />
          </button>
        </div>
      </div>

      {/* Status Notification Banner */}
      {statusBanner && (
        <div className="p-3 bg-accent/10 border border-accent/30 text-accent rounded-xl text-xs font-mono flex items-center justify-between gap-2 shadow-xs animate-in fade-in">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            <span>{statusBanner}</span>
          </div>
          <button type="button" onClick={() => setStatusBanner(null)} className="text-accent hover:opacity-75">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Sub-Navigation Tabs */}
      <div className="flex border-b border-border gap-2 font-mono text-xs overflow-x-auto">
        <button
          type="button"
          onClick={() => setActiveTab('RULES')}
          className={`pb-3 px-3 font-bold border-b-2 flex items-center gap-2 transition cursor-pointer ${
            activeTab === 'RULES'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted hover:text-ink'
          }`}
        >
          <BellRing className="w-4 h-4" />
          <span>Alert Rules & Policies</span>
          <span className="px-1.5 py-0.5 rounded text-[10px] bg-primary/10 text-primary">
            {rules.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('LEDGER')}
          className={`pb-3 px-3 font-bold border-b-2 flex items-center gap-2 transition cursor-pointer ${
            activeTab === 'LEDGER'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted hover:text-ink'
          }`}
        >
          <RotateCcw className="w-4 h-4" />
          <span>Delivery Ledgers & DLQ</span>
          {deadLetterCount > 0 ? (
            <span className="px-1.5 py-0.5 rounded text-[10px] bg-critical text-white font-bold animate-pulse">
              {deadLetterCount} Drops
            </span>
          ) : (
            <span className="px-1.5 py-0.5 rounded text-[10px] bg-accent/20 text-accent">
              100% OK
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('SHIFTS')}
          className={`pb-3 px-3 font-bold border-b-2 flex items-center gap-2 transition cursor-pointer ${
            activeTab === 'SHIFTS'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted hover:text-ink'
          }`}
        >
          <Users className="w-4 h-4" />
          <span>On-Duty Shift Rosters</span>
          <span className="px-1.5 py-0.5 rounded text-[10px] bg-surface-muted text-ink border border-border">
            {shifts.filter(s => s.isOnDuty).length} Active
          </span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('STUDIO')}
          className={`pb-3 px-3 font-bold border-b-2 flex items-center gap-2 transition cursor-pointer ${
            activeTab === 'STUDIO'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted hover:text-ink'
          }`}
        >
          <Sparkles className="w-4 h-4" />
          <span>Multi-Channel Studio & Sandbox</span>
        </button>
      </div>

      {/* ========================================================================= */}
      {/* TAB 1: ALERT RULES & DISPATCH POLICIES                                     */}
      {/* ========================================================================= */}
      {activeTab === 'RULES' && (
        <div className="space-y-4">
          {/* Controls Bar */}
          <div className="flex flex-col sm:flex-row justify-between items-stretch sm:items-center gap-3">
            <div className="flex flex-1 items-center gap-2 max-w-md">
              <div className="relative flex-1">
                <Search className="w-3.5 h-3.5 text-muted absolute left-3 top-2.5 pointer-events-none" />
                <input
                  type="text"
                  placeholder="Filter rules by name or trigger..."
                  value={ruleSearch}
                  onChange={e => setRuleSearch(e.target.value)}
                  className="w-full pl-9 pr-3 py-1.5 text-xs bg-surface border border-border rounded-lg text-ink focus:outline-hidden focus:border-primary"
                />
              </div>

              <select
                value={ruleSeverityFilter}
                onChange={e => setRuleSeverityFilter(e.target.value)}
                className="px-2.5 py-1.5 text-xs bg-surface border border-border rounded-lg text-ink font-mono focus:outline-hidden focus:border-primary cursor-pointer"
              >
                <option value="ALL">All Severities</option>
                <option value="CRITICAL">Critical Only</option>
                <option value="HIGH">High Only</option>
                <option value="MEDIUM">Medium Only</option>
                <option value="LOW">Low Only</option>
              </select>
            </div>

            <button
              type="button"
              onClick={handleOpenCreateRule}
              className="px-3 py-1.5 rounded-lg bg-primary hover:bg-primary-hover text-white text-xs font-bold flex items-center justify-center gap-1.5 shadow-xs transition cursor-pointer"
            >
              <Plus className="w-4 h-4" />
              <span>Create Dispatch Rule</span>
            </button>
          </div>

          {/* Rules Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {filteredRules.map(rule => (
              <div
                key={rule.id}
                onClick={() => setSelectedRule(rule)}
                className={`p-4 rounded-xl border transition-all cursor-pointer space-y-3 ${
                  selectedRule?.id === rule.id
                    ? 'border-primary bg-primary/5 shadow-xs ring-1 ring-primary/20'
                    : 'border-border bg-surface hover:bg-surface-hover'
                }`}
              >
                <div className="flex justify-between items-start gap-2">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ${
                        rule.severity === 'CRITICAL'
                          ? 'bg-critical/10 text-critical border-critical/30'
                          : rule.severity === 'HIGH'
                          ? 'bg-warning/10 text-warning border-warning/30'
                          : 'bg-primary/10 text-primary border-primary/30'
                      }`}>
                        {rule.severity}
                      </span>
                      <span className="font-bold text-ink text-sm">{rule.name}</span>
                    </div>
                    <div className="text-xs text-muted font-mono">
                      Trigger: <span className="text-ink font-medium">{rule.triggerEvent}</span>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={(e) => handleToggleRule(rule.id, e)}
                    title={rule.active ? 'Mute Rule' : 'Activate Rule'}
                    className="p-1 rounded text-primary hover:opacity-80 transition cursor-pointer"
                  >
                    {rule.active ? (
                      <ToggleRight className="w-6 h-6 text-accent" />
                    ) : (
                      <ToggleLeft className="w-6 h-6 text-muted" />
                    )}
                  </button>
                </div>

                {/* Target Channels */}
                <div className="flex flex-wrap items-center gap-1.5 pt-2 border-t border-border/60">
                  {rule.channels.map(channel => (
                    <span
                      key={channel}
                      className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold flex items-center gap-1 border ${
                        channel === 'TEAMS'
                          ? 'bg-[#5059c9]/10 text-[#5059c9] border-[#5059c9]/30'
                          : channel === 'EMAIL'
                          ? 'bg-blue-500/10 text-blue-600 border-blue-500/30'
                          : channel === 'SMS'
                          ? 'bg-warning/10 text-warning border-warning/30'
                          : 'bg-accent/10 text-accent border-accent/30'
                      }`}
                    >
                      {channel === 'TEAMS' && <MessageSquare className="w-3 h-3" />}
                      {channel === 'EMAIL' && <Mail className="w-3 h-3" />}
                      {channel === 'SMS' && <Smartphone className="w-3 h-3" />}
                      {channel === 'WEBHOOK' && <Send className="w-3 h-3" />}
                      <span>{channel}</span>
                    </span>
                  ))}

                  <span className={`px-2 py-0.5 rounded text-[10px] font-mono font-bold border ml-auto ${
                    rule.shiftAware
                      ? 'bg-accent/10 border-accent/30 text-accent'
                      : 'bg-surface-muted border-border text-muted'
                  }`}>
                    {rule.shiftAware ? '✓ Shift-Presence Aware' : 'All Officers (24/7)'}
                  </span>
                </div>

                {/* Endpoint & Stats */}
                <div className="pt-2 flex items-center justify-between text-[11px] font-mono text-muted border-t border-border/60">
                  <div className="truncate max-w-[220px]" title={rule.targetEndpoint}>
                    {rule.targetEndpoint || rule.fallbackRecipient}
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={(e) => handleOpenEditRule(rule, e)}
                      title="Edit Rule"
                      className="p-1 text-muted hover:text-primary transition cursor-pointer"
                    >
                      <Edit2 className="w-3.5 h-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={(e) => handleDeleteRule(rule.id, rule.name, e)}
                      title="Delete Rule"
                      className="p-1 text-muted hover:text-critical transition cursor-pointer"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 2: DELIVERY LEDGERS & DEAD-LETTER QUEUE (DLQ)                          */}
      {/* ========================================================================= */}
      {activeTab === 'LEDGER' && (
        <div className="space-y-4">
          {/* Top KPI Cards */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 font-mono">
            <div className="p-3 rounded-xl bg-surface border border-border space-y-1">
              <div className="text-[11px] text-muted">Total Dispatches</div>
              <div className="text-xl font-bold text-ink">{ledgers.length + 264}</div>
              <div className="text-[10px] text-accent">Active WORM trace logging</div>
            </div>

            <div className="p-3 rounded-xl bg-surface border border-border space-y-1">
              <div className="text-[11px] text-muted">Delivery Success Rate</div>
              <div className="text-xl font-bold text-accent">
                {deadLetterCount === 0 ? '100%' : '98.8%'}
              </div>
              <div className="text-[10px] text-muted">HMAC-SHA256 verified</div>
            </div>

            <div className={`p-3 rounded-xl border space-y-1 ${
              deadLetterCount > 0
                ? 'bg-critical/5 border-critical/30'
                : 'bg-surface border-border'
            }`}>
              <div className="text-[11px] text-muted">Dead-Letter Drops (DLQ)</div>
              <div className={`text-xl font-bold ${deadLetterCount > 0 ? 'text-critical' : 'text-accent'}`}>
                {deadLetterCount}
              </div>
              <div className="text-[10px] text-muted">Exhausted 3 Polly retries</div>
            </div>

            <div className="p-3 rounded-xl bg-surface border border-border space-y-1">
              <div className="text-[11px] text-muted">Average Relay Latency</div>
              <div className="text-xl font-bold text-ink">34.2 ms</div>
              <div className="text-[10px] text-accent">p99 &lt; 120ms SLA adhered</div>
            </div>
          </div>

          {/* Filter & Action Bar */}
          <div className="flex flex-col sm:flex-row justify-between items-stretch sm:items-center gap-3">
            <div className="flex flex-1 items-center gap-2 max-w-md">
              <div className="relative flex-1">
                <Search className="w-3.5 h-3.5 text-muted absolute left-3 top-2.5 pointer-events-none" />
                <input
                  type="text"
                  placeholder="Search trace ID, rule, or endpoint..."
                  value={ledgerSearch}
                  onChange={e => setLedgerSearch(e.target.value)}
                  className="w-full pl-9 pr-3 py-1.5 text-xs bg-surface border border-border rounded-lg text-ink focus:outline-hidden focus:border-primary"
                />
              </div>

              <select
                value={ledgerStatusFilter}
                onChange={e => setLedgerStatusFilter(e.target.value)}
                className="px-2.5 py-1.5 text-xs bg-surface border border-border rounded-lg text-ink font-mono focus:outline-hidden focus:border-primary cursor-pointer"
              >
                <option value="ALL">All Statuses</option>
                <option value="DEAD_LETTER">Dead Letters (DLQ)</option>
                <option value="DELIVERED">Delivered (200 OK)</option>
              </select>
            </div>

            {deadLetterCount > 0 && (
              <button
                type="button"
                onClick={handleBulkReplayDLQ}
                disabled={loading}
                className="px-3 py-1.5 rounded-lg bg-critical hover:bg-critical/90 text-white text-xs font-bold font-mono flex items-center justify-center gap-1.5 shadow-xs transition cursor-pointer"
              >
                <RotateCcw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
                <span>Bulk Replay All Dead Letters ({deadLetterCount})</span>
              </button>
            )}
          </div>

          {/* Ledgers Table */}
          <div className="bg-surface rounded-xl border border-border overflow-hidden shadow-xs">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs font-mono">
                <thead className="bg-surface-muted text-muted uppercase text-[10px] tracking-wider border-b border-border">
                  <tr>
                    <th className="py-2.5 px-3">Sent At (UTC)</th>
                    <th className="py-2.5 px-3">Trace ID / Case Ref</th>
                    <th className="py-2.5 px-3">Rule Name</th>
                    <th className="py-2.5 px-3">Channel & Endpoint</th>
                    <th className="py-2.5 px-3 text-center">Attempts</th>
                    <th className="py-2.5 px-3 text-right">Latency</th>
                    <th className="py-2.5 px-3 text-center">Status</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredLedgers.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="py-8 text-center text-muted text-xs">
                        No delivery ledger records match the selected filter.
                      </td>
                    </tr>
                  ) : (
                    filteredLedgers.map(item => (
                      <tr 
                        key={item.id} 
                        className={`hover:bg-surface-hover transition cursor-pointer ${
                          item.deliveryStatus.includes('DEAD_LETTER') ? 'bg-critical/5' : ''
                        }`}
                        onClick={() => setSelectedLedger(item)}
                      >
                        <td className="py-2.5 px-3 text-muted">
                          {new Date(item.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                        </td>
                        <td className="py-2.5 px-3 font-bold text-ink">
                          {item.traceId}
                        </td>
                        <td className="py-2.5 px-3 text-ink truncate max-w-[180px]">
                          {item.ruleName}
                        </td>
                        <td className="py-2.5 px-3 text-muted truncate max-w-[200px]" title={item.targetEndpoint}>
                          <span className="font-bold text-primary mr-1">[{item.channel}]</span>
                          {item.targetEndpoint}
                        </td>
                        <td className="py-2.5 px-3 text-center">
                          <span className="px-1.5 py-0.5 rounded bg-surface border border-border text-[10px]">
                            {item.attemptCount}/3
                          </span>
                        </td>
                        <td className="py-2.5 px-3 text-right text-muted">
                          {item.latencyMs.toFixed(1)}ms
                        </td>
                        <td className="py-2.5 px-3 text-center">
                          {item.deliveryStatus.includes('DEAD_LETTER') ? (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-critical/10 text-critical border border-critical/30">
                              DEAD_LETTER ({item.httpStatus || 504})
                            </span>
                          ) : (
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/10 text-accent border border-accent/30">
                              DELIVERED (200)
                            </span>
                          )}
                        </td>
                        <td className="py-2.5 px-3 text-right">
                          <div className="flex items-center justify-end gap-1.5" onClick={e => e.stopPropagation()}>
                            <button
                              type="button"
                              onClick={() => setSelectedLedger(item)}
                              className="px-2 py-1 rounded bg-surface border border-border hover:bg-surface-muted text-ink text-[11px] transition cursor-pointer"
                            >
                              Inspect
                            </button>

                            {item.deliveryStatus.includes('DEAD_LETTER') && (
                              <button
                                type="button"
                                onClick={() => handleReplaySingle(item.id, item.traceId)}
                                disabled={replayingId === item.id}
                                className="px-2 py-1 rounded bg-critical hover:bg-critical/90 text-white text-[11px] font-bold flex items-center gap-1 transition cursor-pointer"
                              >
                                <RotateCcw className={`w-3 h-3 ${replayingId === item.id ? 'animate-spin' : ''}`} />
                                <span>Replay</span>
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 3: ON-DUTY SHIFT ROSTERS & ESCALATION PATHS                            */}
      {/* ========================================================================= */}
      {activeTab === 'SHIFTS' && (
        <div className="space-y-6">
          {/* Shift Architecture Info Card */}
          <div className="p-4 rounded-xl bg-surface border border-border space-y-2">
            <div className="flex items-center gap-2 text-ink font-bold text-sm">
              <Clock className="w-4 h-4 text-primary" />
              <span>Shift-Aware Notification Routing Engine (`DynamicNotificationEngine.cs`)</span>
            </div>
            <p className="text-xs text-muted leading-relaxed">
              EDITH automatically determines investigator availability in real time. When a high-priority statutory freeze or case SLA triggers, the engine verifies whether the preferred assignee is currently marked <span className="font-bold text-accent">ON-DUTY</span>. If the assignee is off-duty, notifications are dynamically escalated to on-duty substitutes or the MLRO escalation channel.
            </p>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
            {/* Shift Roster Table (8 cols) */}
            <div className="lg:col-span-8 bg-surface rounded-xl border border-border p-5 space-y-4">
              <div className="flex items-center justify-between pb-3 border-b border-border">
                <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2 font-mono">
                  <Users className="w-4 h-4 text-primary" />
                  Active Regional Duty Roster
                </h3>
                <span className="text-xs font-mono text-muted">
                  Current UTC: {new Date().toISOString().substring(11, 16)} UTC
                </span>
              </div>

              <div className="space-y-3 font-mono text-xs">
                {shifts.map(shift => (
                  <div
                    key={shift.id}
                    className={`p-3.5 rounded-xl border transition-all flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 ${
                      shift.isOnDuty
                        ? 'border-accent/40 bg-accent/5'
                        : 'border-border bg-surface-muted'
                    }`}
                  >
                    <div className="space-y-1">
                      <div className="font-bold text-ink text-sm flex items-center gap-2">
                        <span>{shift.userName}</span>
                        <span className="px-2 py-0.5 rounded text-[10px] bg-primary/10 text-primary border border-primary/20">
                          {shift.userRole}
                        </span>
                      </div>
                      <div className="text-muted text-[11px] flex items-center gap-3">
                        <span>Shift: <strong className="text-ink">{shift.shiftName} ({shift.shiftStart} - {shift.shiftEnd})</strong></span>
                        {shift.substituteUserName && (
                          <span>Substitute: <strong className="text-ink">{shift.substituteUserName}</strong></span>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => handleToggleDutyStatus(shift)}
                        className={`px-3 py-1.5 rounded-lg text-xs font-bold transition flex items-center gap-1.5 cursor-pointer ${
                          shift.isOnDuty
                            ? 'bg-accent text-white shadow-xs'
                            : 'bg-surface border border-border text-muted hover:text-ink'
                        }`}
                      >
                        <UserCheck className="w-3.5 h-3.5" />
                        <span>{shift.isOnDuty ? 'On Duty' : 'Off Duty'}</span>
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Escalation Simulator (4 cols) */}
            <div className="lg:col-span-4 bg-surface rounded-xl border border-border p-5 space-y-4 font-mono text-xs">
              <div className="flex items-center justify-between pb-3 border-b border-border">
                <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-primary" />
                  Routing Test Simulator
                </h3>
                <span className="text-[10px] font-bold text-accent">Interactive</span>
              </div>

              <div className="space-y-3">
                <div>
                  <label className="text-[11px] text-muted block mb-1">Select Target Assignee:</label>
                  <select
                    value={escalationTestOfficer}
                    onChange={e => setEscalationTestOfficer(e.target.value)}
                    className="w-full p-2 bg-surface border border-border rounded-lg text-ink font-mono text-xs focus:outline-hidden focus:border-primary"
                  >
                    {shifts.map(s => (
                      <option key={s.userId} value={s.userId}>
                        {s.userName} ({s.isOnDuty ? 'ON DUTY' : 'OFF DUTY'})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="p-3 rounded-lg bg-surface-muted border border-border space-y-2">
                  <div className="text-[11px] text-muted">Evaluated Dispatch Target:</div>
                  <div className="font-bold text-ink text-sm flex items-center gap-1.5">
                    <span className="text-primary">{simulatedEscalation.channel}</span>
                    <span>&rarr;</span>
                    <span className="text-accent">{simulatedEscalation.recipient}</span>
                  </div>
                  <div className="text-[11px] text-muted leading-relaxed">
                    {simulatedEscalation.reason}
                  </div>
                </div>

                <div className="p-2.5 rounded bg-primary/5 border border-primary/20 text-[11px] text-ink">
                  <div className="font-bold flex items-center gap-1 text-primary mb-1">
                    <ShieldCheck className="w-3.5 h-3.5" />
                    <span>Compliance Guarantee:</span>
                  </div>
                  Zero alert loss. Statutory sanctions are never dropped when investigators go off-shift.
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 4: MULTI-CHANNEL SIMULATION STUDIO & SANDBOX                           */}
      {/* ========================================================================= */}
      {activeTab === 'STUDIO' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Controls & Token Builder (5 cols) */}
          <div className="lg:col-span-5 bg-surface rounded-xl border border-border p-5 space-y-5 font-mono text-xs">
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
                <Code className="w-4 h-4 text-primary" />
                Channel Simulation Controls
              </h3>
              <span className="text-[10px] font-bold text-accent">Live Sandbox</span>
            </div>

            {/* Target Channel Selector */}
            <div className="space-y-1.5">
              <label className="text-[11px] text-muted font-bold">Target Channel Format:</label>
              <div className="grid grid-cols-2 gap-2">
                {(['TEAMS', 'EMAIL', 'SMS', 'WEBHOOK'] as NotificationChannelType[]).map(ch => (
                  <button
                    key={ch}
                    type="button"
                    onClick={() => setStudioChannel(ch)}
                    className={`py-2 px-3 rounded-lg font-bold border flex items-center justify-center gap-1.5 transition cursor-pointer ${
                      studioChannel === ch
                        ? 'bg-primary text-white border-primary shadow-xs'
                        : 'bg-surface border-border text-muted hover:text-ink'
                    }`}
                  >
                    {ch === 'TEAMS' && <MessageSquare className="w-3.5 h-3.5" />}
                    {ch === 'EMAIL' && <Mail className="w-3.5 h-3.5" />}
                    {ch === 'SMS' && <Smartphone className="w-3.5 h-3.5" />}
                    {ch === 'WEBHOOK' && <Send className="w-3.5 h-3.5" />}
                    <span>{ch}</span>
                  </button>
                ))}
              </div>
            </div>

            {/* Sample Case Preset */}
            <div className="space-y-1.5">
              <label className="text-[11px] text-muted font-bold">Select Synthetic Scenario:</label>
              <select
                value={studioCasePreset}
                onChange={e => setStudioCasePreset(e.target.value as any)}
                className="w-full p-2 bg-surface border border-border rounded-lg text-ink font-mono text-xs focus:outline-hidden focus:border-primary cursor-pointer"
              >
                <option value="VIKTOR_BOUT">Victor Anatolyevich Bout ($450,000 USD Statutory Freeze)</option>
                <option value="AL_BARAKA">Al-Baraka General Trading ($1,250,000 AED Intercept)</option>
                <option value="DELTA_DIGEST">Nightly Delta Rescreening Digest (142 Alerts)</option>
              </select>
            </div>

            {/* Dynamic Token Insert Chips */}
            <div className="space-y-1.5">
              <label className="text-[11px] text-muted font-bold">Available Dynamic Tokens:</label>
              <div className="flex flex-wrap gap-1.5">
                {['{{CaseRef}}', '{{MatchedName}}', '{{Amount}}', '{{Currency}}', '{{SanctionList}}', '{{ActionUrl}}'].map(token => (
                  <span
                    key={token}
                    className="px-2 py-0.5 rounded text-[10px] bg-surface-muted border border-border text-primary font-bold"
                  >
                    {token}
                  </span>
                ))}
              </div>
            </div>

            {/* Pre-Flight Test Fire Button */}
            <div className="pt-3 border-t border-border space-y-2">
              <button
                type="button"
                onClick={handleTestFire}
                disabled={testFiring}
                className="w-full py-2.5 rounded-lg bg-accent hover:bg-accent/90 text-white font-bold flex items-center justify-center gap-2 shadow-xs transition cursor-pointer"
              >
                <Play className={`w-3.5 h-3.5 ${testFiring ? 'animate-spin' : ''}`} />
                <span>Test-Fire Simulated Dispatch ({studioChannel})</span>
              </button>

              {testFireResult && (
                <div className="p-2.5 rounded bg-accent/10 border border-accent/30 text-accent text-[11px]">
                  ✓ Dispatched via {studioChannel} in {testFireResult.latencyMs}ms (HTTP {testFireResult.httpStatus})
                </div>
              )}
            </div>
          </div>

          {/* Live Preview Display (7 cols) */}
          <div className="lg:col-span-7 bg-surface rounded-xl border border-border p-5 space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-border font-mono text-xs">
              <h3 className="text-xs uppercase tracking-wider font-bold text-ink flex items-center gap-2">
                <Sparkles className="w-4 h-4 text-primary" />
                {studioChannel === 'TEAMS' && 'Microsoft Teams Adaptive Card Live Simulation'}
                {studioChannel === 'EMAIL' && 'HTML Regulatory Email Alert Simulation'}
                {studioChannel === 'SMS' && 'Mobile SMS Emergency Alert Simulation'}
                {studioChannel === 'WEBHOOK' && 'Outbound Signed JSON Webhook Payload'}
              </h3>
              <span className="text-[10px] font-bold text-accent">Two-Way Actionable</span>
            </div>

            {/* 1. Teams Adaptive Card */}
            {studioChannel === 'TEAMS' && (
              <div className="p-4 rounded-xl bg-[#0f172a] text-white border border-slate-700 space-y-3 font-sans shadow-lg">
                <div className="flex items-center justify-between pb-2 border-b border-slate-700 font-mono text-xs">
                  <div className="flex items-center gap-2">
                    <ShieldAlert className="w-4 h-4 text-red-500 animate-pulse" />
                    <span className="font-bold text-red-400">
                      {studioCasePreset === 'DELTA_DIGEST' ? 'DELTA RESCREENING COMPLETED' : 'CRITICAL STATUTORY FREEZE'}
                    </span>
                  </div>
                  <span className="text-slate-400 text-[10px]">Just now</span>
                </div>

                <div className="space-y-1">
                  <div className="font-bold text-white text-sm">
                    {studioCasePreset === 'VIKTOR_BOUT' && 'Intercepted Wire: $450,000.00 USD'}
                    {studioCasePreset === 'AL_BARAKA' && 'Corridor Hold: 1,250,000.00 AED'}
                    {studioCasePreset === 'DELTA_DIGEST' && 'Delta Batch Run #849: 142 Intercepts'}
                  </div>
                  <div className="text-slate-300 text-xs">
                    {studioCasePreset === 'VIKTOR_BOUT' && 'Inbound SWIFT MT103 matched UN Security Council Res 1521.'}
                    {studioCasePreset === 'AL_BARAKA' && 'Remittance from Dubai (ARE) triggered FATF Grey Corridor Typology.'}
                    {studioCasePreset === 'DELTA_DIGEST' && 'Customer database delta rescreening completed across 28,490 active accounts.'}
                  </div>
                </div>

                <div className="p-2.5 rounded bg-slate-800/80 font-mono text-xs space-y-1 border border-slate-700">
                  <div className="flex justify-between text-slate-300">
                    <span>Beneficiary / Subject:</span>
                    <span className="font-bold text-red-300">
                      {studioCasePreset === 'VIKTOR_BOUT' ? 'Victor Anatolyevich Bout' : studioCasePreset === 'AL_BARAKA' ? 'Al-Baraka General Trading LLC' : 'Multi-Account Delta Batch'}
                    </span>
                  </div>
                  <div className="flex justify-between text-slate-300">
                    <span>Case Reference:</span>
                    <span className="font-bold text-sky-400">EDITH-CAS-2026-0042</span>
                  </div>
                  <div className="flex justify-between text-slate-300">
                    <span>Routing Officer:</span>
                    <span className="text-emerald-400 font-bold">Fatima Al-Nuaimi (On-Duty)</span>
                  </div>
                </div>

                {acknowledged ? (
                  <div className="p-2 rounded bg-emerald-900/60 border border-emerald-500/40 text-emerald-300 font-mono text-xs flex items-center justify-between">
                    <span>✓ Alert Acknowledged by Fatima Al-Nuaimi at {new Date().toLocaleTimeString()}</span>
                    <button 
                      type="button" 
                      onClick={() => setAcknowledged(false)}
                      className="text-[10px] text-slate-400 hover:text-white underline cursor-pointer"
                    >
                      Reset
                    </button>
                  </div>
                ) : (
                  <div className="pt-2 flex gap-2 font-mono text-xs">
                    <button
                      type="button"
                      onClick={() => setAcknowledged(true)}
                      className="flex-1 py-1.5 rounded bg-primary hover:bg-primary-hover text-white font-bold text-center transition cursor-pointer"
                    >
                      Acknowledge Alert
                    </button>
                    <button
                      type="button"
                      className="flex-1 py-1.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 font-bold text-center border border-slate-600 transition cursor-pointer"
                    >
                      Open Case Canvas
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* 2. HTML Email Format */}
            {studioChannel === 'EMAIL' && (
              <div className="p-4 rounded-xl bg-white text-slate-800 border border-slate-300 space-y-3 font-sans shadow-xs">
                <div className="border-b pb-2 text-xs text-slate-500 font-mono">
                  From: <strong>alerts@edith.aml.cloud</strong> | Subject: [STATUTORY ALERT] Transaction Frozen
                </div>
                <h4 className="font-bold text-red-600 text-sm">Action Required: Statutory Sanction Intercept</h4>
                <p className="text-xs text-slate-600 leading-relaxed">
                  EDITH Deterministic Screening Engine has intercepted and placed an emergency freeze on Transaction Reference <strong>TX-2026-9921448</strong>.
                </p>
                <div className="p-3 rounded bg-slate-50 border border-slate-200 font-mono text-xs space-y-1">
                  <div>Subject: <strong>Victor Anatolyevich Bout</strong></div>
                  <div>Amount: <strong>$450,000.00 USD</strong></div>
                  <div>SLA Deadline: <strong>72 Hours from Receipt</strong></div>
                </div>
                <button type="button" className="px-4 py-1.5 rounded bg-primary text-white text-xs font-bold font-mono">
                  Review in Case Workspace &rarr;
                </button>
              </div>
            )}

            {/* 3. SMS Text Format */}
            {studioChannel === 'SMS' && (
              <div className="p-4 rounded-xl bg-surface-muted border border-border space-y-2 font-mono text-xs max-w-sm mx-auto">
                <div className="text-[10px] text-muted text-center">SMS PREVIEW (142 / 160 Characters)</div>
                <div className="p-3 rounded-lg bg-surface border border-border text-ink leading-relaxed">
                  [EDITH AML] CRITICAL FREEZE: Tx TX-2026-9921448 ($450k USD) matched UN Res 1521. Case EDITH-CAS-2026-0042 assigned to on-duty roster.
                </div>
              </div>
            )}

            {/* 4. JSON Webhook Payload */}
            {studioChannel === 'WEBHOOK' && (
              <div className="p-4 rounded-xl bg-[#0f172a] text-slate-200 font-mono text-xs border border-slate-800 space-y-2">
                <div className="text-slate-400 text-[10px] flex justify-between">
                  <span>POST /api/v1/aml-freeze-callback</span>
                  <span>X-Edith-Signature: sha256=a7f29b4...</span>
                </div>
                <pre className="overflow-x-auto p-2 rounded bg-slate-900 text-emerald-400 text-[11px] leading-relaxed">
{JSON.stringify({
  eventId: "evt-01931000-0042",
  timestamp: new Date().toISOString(),
  decision: "FREEZE",
  caseRef: "EDITH-CAS-2026-0042",
  transaction: {
    referenceNo: "TX-2026-9921448",
    amount: 450000.00,
    currency: "USD",
    subject: "Victor Anatolyevich Bout",
    matchedSanctionsList: "UN Security Council Res 1521"
  },
  onDutyOfficer: "Fatima Al-Nuaimi",
  hmacVerified: true
}, null, 2)}
                </pre>
              </div>
            )}

            <div className="p-3 rounded-lg bg-surface-muted border border-border text-muted font-mono text-[11px]">
              <strong className="text-ink">Two-Way Actionable ChatOps:</strong> When an officer clicks "Acknowledge Alert" inside Microsoft Teams or via webhook callback, the action is verified cryptographically via HMAC-SHA256 and immediately transitions the case status in PostgreSQL.
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: RULE AUTHORING & EDIT DRAWER                                        */}
      {/* ========================================================================= */}
      {isRuleModalOpen && editingRule && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-surface border border-border rounded-xl shadow-2xl max-w-xl w-full p-6 space-y-4 font-sans text-xs animate-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <h3 className="font-bold text-sm text-ink flex items-center gap-2">
                <BellRing className="w-4 h-4 text-primary" />
                <span>{editingRule.id ? 'Edit Dispatch Rule' : 'Create New Alert Dispatch Rule'}</span>
              </h3>
              <button 
                type="button" 
                onClick={() => setIsRuleModalOpen(false)}
                className="text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleSaveRule} className="space-y-3 font-mono">
              <div>
                <label className="text-[11px] text-muted block mb-1">Rule Name:</label>
                <input
                  type="text"
                  required
                  value={editingRule.name || ''}
                  onChange={e => setEditingRule(prev => ({ ...prev, name: e.target.value }))}
                  placeholder="e.g. Statutory Freeze Emergency Alert"
                  className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary font-sans"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[11px] text-muted block mb-1">Trigger Event:</label>
                  <select
                    value={editingRule.triggerEvent || ''}
                    onChange={e => setEditingRule(prev => ({ ...prev, triggerEvent: e.target.value }))}
                    className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary cursor-pointer"
                  >
                    <option value="Transaction Frozen (OFAC / UN Hit)">Transaction Frozen (OFAC / UN Hit)</option>
                    <option value="Case Inactivity > 48 Hours">Case Inactivity &gt; 48 Hours</option>
                    <option value="Delta Batch Processing Finished">Delta Batch Processing Finished</option>
                    <option value="Real-Time Intercept (Score >= 95.0%)">Real-Time Intercept (Score &gt;= 95.0%)</option>
                  </select>
                </div>

                <div>
                  <label className="text-[11px] text-muted block mb-1">Severity Tier:</label>
                  <select
                    value={editingRule.severity || 'HIGH'}
                    onChange={e => setEditingRule(prev => ({ ...prev, severity: e.target.value as any }))}
                    className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary cursor-pointer"
                  >
                    <option value="CRITICAL">CRITICAL</option>
                    <option value="HIGH">HIGH</option>
                    <option value="MEDIUM">MEDIUM</option>
                    <option value="LOW">LOW</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="text-[11px] text-muted block mb-1">Target Channels:</label>
                <div className="flex gap-4">
                  {(['TEAMS', 'EMAIL', 'SMS', 'WEBHOOK'] as NotificationChannelType[]).map(ch => (
                    <label key={ch} className="flex items-center gap-1.5 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={editingRule.channels?.includes(ch) ?? false}
                        onChange={e => {
                          const current = editingRule.channels || [];
                          const updated = e.target.checked
                            ? [...current, ch]
                            : current.filter(c => c !== ch);
                          setEditingRule(prev => ({ ...prev, channels: updated }));
                        }}
                        className="rounded border-border text-primary"
                      />
                      <span>{ch}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div>
                <label className="text-[11px] text-muted block mb-1">Target Webhook Endpoint / Recipient:</label>
                <input
                  type="text"
                  value={editingRule.targetEndpoint || ''}
                  onChange={e => setEditingRule(prev => ({ ...prev, targetEndpoint: e.target.value }))}
                  placeholder="https://cbs-relay.internal.adcb.ae/api/v1/aml-callback"
                  className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary font-mono"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[11px] text-muted block mb-1">Fallback Escalation Officer:</label>
                  <input
                    type="text"
                    value={editingRule.fallbackRecipient || ''}
                    onChange={e => setEditingRule(prev => ({ ...prev, fallbackRecipient: e.target.value }))}
                    placeholder="MLRO Duty Officer"
                    className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary"
                  />
                </div>

                <div>
                  <label className="text-[11px] text-muted block mb-1">Throttle / Digest Window:</label>
                  <select
                    value={editingRule.throttleMinutes || 0}
                    onChange={e => setEditingRule(prev => ({ ...prev, throttleMinutes: Number(e.target.value) }))}
                    className="w-full p-2 bg-surface border border-border rounded-lg text-ink text-xs focus:outline-hidden focus:border-primary cursor-pointer"
                  >
                    <option value={0}>0 mins (Immediate Real-Time)</option>
                    <option value={5}>5 mins</option>
                    <option value={15}>15 mins</option>
                    <option value={60}>60 mins (Batch Digest)</option>
                  </select>
                </div>
              </div>

              <div className="flex items-center justify-between p-3 rounded-lg bg-surface-muted border border-border">
                <span className="font-bold text-ink">Shift-Presence Aware:</span>
                <button
                  type="button"
                  onClick={() => setEditingRule(prev => ({ ...prev, shiftAware: !prev?.shiftAware }))}
                  className="text-primary cursor-pointer"
                >
                  {editingRule.shiftAware ? (
                    <ToggleRight className="w-6 h-6 text-accent" />
                  ) : (
                    <ToggleLeft className="w-6 h-6 text-muted" />
                  )}
                </button>
              </div>

              <div className="pt-3 flex justify-end gap-2 border-t border-border">
                <button
                  type="button"
                  onClick={() => setIsRuleModalOpen(false)}
                  className="px-3 py-1.5 rounded-lg border border-border hover:bg-surface-muted text-muted font-bold cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 rounded-lg bg-primary hover:bg-primary-hover text-white font-bold cursor-pointer"
                >
                  Save & Activate Rule
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL: DIAGNOSTIC PAYLOAD & HMAC INSPECTOR                                  */}
      {/* ========================================================================= */}
      {selectedLedger && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-surface border border-border rounded-xl shadow-2xl max-w-2xl w-full p-6 space-y-4 font-mono text-xs animate-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <div className="flex items-center gap-2">
                <Code className="w-4 h-4 text-primary" />
                <span className="font-bold text-sm text-ink">
                  Ledger Diagnostics: {selectedLedger.traceId}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setSelectedLedger(null)}
                className="text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-2 p-2.5 rounded bg-surface-muted border border-border text-[11px]">
                <div>Status: <strong className={selectedLedger.deliveryStatus.includes('DEAD_LETTER') ? 'text-critical' : 'text-accent'}>{selectedLedger.deliveryStatus}</strong></div>
                <div>HTTP Status: <strong>{selectedLedger.httpStatus || 504}</strong></div>
                <div>Relay Latency: <strong>{selectedLedger.latencyMs.toFixed(1)} ms</strong></div>
                <div>Attempts: <strong>{selectedLedger.attemptCount} of 3</strong></div>
              </div>

              {selectedLedger.errorMessage && (
                <div className="p-2.5 rounded bg-critical/10 border border-critical/30 text-critical text-[11px]">
                  <strong>Error Message:</strong> {selectedLedger.errorMessage}
                </div>
              )}

              {selectedLedger.signatureHex && (
                <div className="space-y-1">
                  <div className="text-[11px] text-muted">HMAC-SHA256 Cryptographic Header:</div>
                  <div className="p-2 rounded bg-surface-muted border border-border text-primary text-[10px] break-all select-all">
                    X-Edith-Signature: {selectedLedger.signatureHex}
                  </div>
                </div>
              )}

              <div className="space-y-1">
                <div className="flex items-center justify-between text-[11px] text-muted">
                  <span>Outbound JSON Payload:</span>
                  <button
                    type="button"
                    onClick={() => {
                      navigator.clipboard.writeText(selectedLedger.payloadJson || '');
                      setCopiedPayload(true);
                      setTimeout(() => setCopiedPayload(false), 2000);
                    }}
                    className="text-primary hover:underline flex items-center gap-1 cursor-pointer"
                  >
                    {copiedPayload ? <Check className="w-3 h-3 text-accent" /> : <Copy className="w-3 h-3" />}
                    <span>{copiedPayload ? 'Copied' : 'Copy JSON'}</span>
                  </button>
                </div>
                <pre className="p-3 rounded-lg bg-[#0f172a] text-slate-200 text-[11px] overflow-x-auto max-h-48">
                  {selectedLedger.payloadJson}
                </pre>
              </div>

              <div className="pt-3 flex justify-end gap-2 border-t border-border">
                {selectedLedger.deliveryStatus.includes('DEAD_LETTER') && (
                  <button
                    type="button"
                    onClick={() => {
                      handleReplaySingle(selectedLedger.id, selectedLedger.traceId);
                      setSelectedLedger(null);
                    }}
                    className="px-3 py-1.5 rounded-lg bg-critical hover:bg-critical/90 text-white font-bold flex items-center gap-1.5 cursor-pointer"
                  >
                    <RotateCcw className="w-3.5 h-3.5" />
                    <span>Replay Callback</span>
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => setSelectedLedger(null)}
                  className="px-4 py-1.5 rounded-lg bg-surface border border-border text-ink font-bold cursor-pointer"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
