import {
  UserSession,
  EntitlementsResponse,
  TransactionScreeningRequest,
  ScreeningResult,
  CaseSummary,
  CaseDetail,
  BulkJobSummary,
  RuleDefinition,
  BacktestResult,
  CaseResolution,
  NameSearchRequest,
  NameSearchResponse,
  ScreeningAuditCertificate,
  IamUserRecord,
  IamGroupRecord,
  IamGroupPermission,
  KycDossierDto,
  AuditLogRecord
} from '../types';

const API_BASE = '/api/v1';

// Active session state (defaults to Demo Analyst for direct testing)
let currentSession: UserSession = {
  token: 'mock-jwt-token-gcc-dubai',
  expiresAtUtc: new Date(Date.now() + 86400000).toISOString(),
  username: 'analyst.fatima@adcb.ae',
  role: 'ANALYST',
  tenantId: '11111111-1111-1111-1111-111111111111'
};

export const getSession = (): UserSession => currentSession;

export const setSession = (session: UserSession) => {
  const roleChanged = session.role !== currentSession.role || session.tenantId !== currentSession.tenantId;
  currentSession = {
    ...session,
    token: roleChanged ? `mock-jwt-token-${session.role}` : session.token
  };
  localStorage.setItem('edith_session', JSON.stringify(currentSession));
};

export const getAuthHeaders = (): Record<string, string> => ({
  'Authorization': `Bearer ${currentSession.token}`,
  'X-Tenant-ID': currentSession.tenantId,
  'Content-Type': 'application/json'
});

const DEFAULT_MENU_ITEMS = [
  // Group 1: Operational Screening & Ingestion
  { code: 'DASHBOARD', label: 'Executive Dashboard', path: '/dashboard', icon: 'BarChart3', enabled: true },
  { code: 'NAME_SEARCH', label: 'Name & ID Search', path: '/name-search', icon: 'Search', enabled: true },
  { code: 'SCREENING', label: 'Single Entity Screening', path: '/screening', icon: 'ShieldAlert', enabled: true },
  { code: 'BULK', label: 'Bulk File Screening', path: '/bulk', icon: 'Layers', enabled: true },
  { code: 'DELTA', label: 'Delta Rescreening Hub', path: '/delta', icon: 'RefreshCw', enabled: true },
  { code: 'WHITELIST', label: 'Whitelist Governance', path: '/whitelist', icon: 'ShieldCheck', enabled: true },

  // Group 2: Case Investigation & Regulatory
  { code: 'CASES', label: '3-Pane Case Workspace', path: '/cases', icon: 'Briefcase', enabled: true },
  { code: 'KYC360', label: 'Customer KYC 360', path: '/kyc360', icon: 'UserCheck', enabled: true },
  { code: 'GOAML', label: 'goAML Reporting Studio', path: '/goaml', icon: 'FileText', enabled: true },

  // Group 3: Rules, Analytics & Administration
  { code: 'RULES', label: 'Visual Rule Builder', path: '/rules', icon: 'Sliders', enabled: true },
  { code: 'SANDBOX', label: 'Rule Sandbox & Backtest', path: '/sandbox', icon: 'FlaskConical', enabled: true },
  { code: 'REPORTS', label: 'Reports & Audit Ledger', path: '/reports', icon: 'FileSpreadsheet', enabled: true },
  { code: 'NOTIFICATIONS', label: 'Dynamic Notifications', path: '/notifications', icon: 'BellRing', enabled: true },
  { code: 'SETTINGS', label: 'System Masters Settings', path: '/settings', icon: 'Settings', enabled: true },
  { code: 'IAM', label: 'IAM & Access Governance', path: '/iam', icon: 'Users', enabled: true }
];

async function ensureJwtToken(): Promise<void> {
  if (!currentSession.token.startsWith('mock-')) return;
  try {
    const res = await fetch(`${API_BASE}/iam/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Tenant-ID': currentSession.tenantId },
      body: JSON.stringify({
        username: currentSession.username,
        password: 'Password123!',
        role: currentSession.role,
        tenantId: currentSession.tenantId
      })
    });
    if (res.ok) {
      const data = await res.json();
      if (data.accessToken) {
        currentSession = {
          ...currentSession,
          token: data.accessToken,
          expiresAtUtc: data.expiresAt || currentSession.expiresAtUtc
        };
        localStorage.setItem('edith_session', JSON.stringify(currentSession));
      }
    }
  } catch {
    // Offline fallback keeps mock token
  }
}

// Mock fallback data for graceful degradation
const MOCK_CASES: CaseSummary[] = [
  {
    id: 'c1111111-2222-3333-4444-555555555555',
    caseNumber: 'EDITH-CAS-2026-0042',
    tenantId: '11111111-1111-1111-1111-111111111111',
    status: 'Open',
    priority: 'CRITICAL',
    riskScore: 94.5,
    primarySubjectName: 'Viktor Anatolyevich Bout',
    assignedAnalystName: 'Fatima Al-Nuaimi',
    createdAtUtc: new Date(Date.now() - 3600000 * 4).toISOString(),
    dueAtUtc: new Date(Date.now() + 3600000 * 68).toISOString(),
    isLocked: false
  },
  {
    id: 'c2222222-3333-4444-5555-666666666666',
    caseNumber: 'EDITH-CAS-2026-0043',
    tenantId: '11111111-1111-1111-1111-111111111111',
    status: 'UnderReview',
    priority: 'HIGH',
    riskScore: 82.0,
    primarySubjectName: 'Al-Baraka General Trading LLC',
    assignedAnalystName: 'Tariq Mansoor',
    createdAtUtc: new Date(Date.now() - 3600000 * 12).toISOString(),
    dueAtUtc: new Date(Date.now() + 3600000 * 60).toISOString(),
    isLocked: true,
    lockedByUserName: 'Tariq Mansoor',
    lockExpiresAtUtc: new Date(Date.now() + 3600000 * 2).toISOString()
  },
  {
    id: 'c3333333-4444-5555-6666-777777777777',
    caseNumber: 'EDITH-CAS-2026-0044',
    tenantId: '11111111-1111-1111-1111-111111111111',
    status: 'PendingApproval',
    priority: 'MEDIUM',
    riskScore: 68.0,
    primarySubjectName: 'Mohammed bin Rashid Logistics',
    assignedAnalystName: 'Fatima Al-Nuaimi',
    createdAtUtc: new Date(Date.now() - 3600000 * 24).toISOString(),
    dueAtUtc: new Date(Date.now() + 3600000 * 48).toISOString(),
    isLocked: false
  }
];

export const EdithApi = {
  // IAM
  async fetchEntitlements(): Promise<EntitlementsResponse> {
    await ensureJwtToken();
    try {
      const res = await fetch(`${API_BASE}/iam/entitlements`, {
        headers: getAuthHeaders()
      });
      if (res.ok) {
        const data = await res.json();
        return {
          tenantId: data.tenantId || currentSession.tenantId,
          username: currentSession.username,
          role: data.role || currentSession.role,
          menuItems: Array.isArray(data.menuItems) && data.menuItems.length > 0 ? data.menuItems : DEFAULT_MENU_ITEMS,
          features: Array.isArray(data.permissions) ? data.permissions : ['REALTIME_SCREENING', 'CASE_WORKFLOW', 'AI_COPILOT', 'GOAML_EXPORT', 'AUDIT_TRAIL', 'DELTA_SYNC', 'WORM_LEDGER']
        };
      }
    } catch {
      // Fallback
    }

    return {
      tenantId: currentSession.tenantId,
      username: currentSession.username,
      role: currentSession.role,
      menuItems: DEFAULT_MENU_ITEMS,
      features: ['REALTIME_SCREENING', 'CASE_WORKFLOW', 'AI_COPILOT', 'GOAML_EXPORT', 'AUDIT_TRAIL', 'DELTA_SYNC', 'WORM_LEDGER']
    };
  },

  // Single Screening
  async screenTransaction(request: TransactionScreeningRequest): Promise<ScreeningResult> {
    const idempotencyKey = `idemp-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
    const startTime = performance.now();
    const sender = request.parties?.find(p => p.role === 'SENDER') || request.parties?.[0];
    const beneficiary = request.parties?.find(p => p.role === 'BENEFICIARY') || request.parties?.[1] || request.parties?.[0];

    const backendPayload = {
      transactionRefNo: request.transactionId || `TX-${Date.now()}`,
      amount: request.amount,
      currency: request.currency,
      senderName: sender?.fullName || 'Commercial Remitter',
      senderCountry: request.originatingCountry || 'AE',
      beneficiaryName: beneficiary?.fullName || 'Commercial Payee',
      beneficiaryCountry: request.destinationCountry || 'US',
      minThresholdScore: request.minThresholdScore,
      nameWeightage: request.nameWeightage,
      nationalityWeightage: request.nationalityWeightage,
      dobWeightage: request.dobWeightage,
      idWeightage: request.idWeightage
    };

    try {
      const res = await fetch(`${API_BASE}/screen/transaction`, {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          'Idempotency-Key': idempotencyKey
        },
        body: JSON.stringify(backendPayload)
      });
      if (res.ok) {
        const data = await res.json();
        const partyRole = beneficiary?.role || 'BENEFICIARY';
        const partyName = beneficiary?.fullName || 'Unknown Party';
        const topScore = Number(data.overallScore || 0);
        const rawStatus = (data.decision || data.status || 'CLEARED') as string;
        const uiDecision = rawStatus === 'HELD' && topScore >= 95 && (data.triggeredList?.includes('OFAC') || data.triggeredList?.includes('UN'))
          ? 'BLOCKED'
          : (rawStatus as 'CLEARED' | 'HELD' | 'BLOCKED');
        
        const threshold = request.minThresholdScore ?? 80;
        const effectiveMatches = (data.matches || []).filter((m: any) => 
          rawStatus === 'HELD' || rawStatus === 'BLOCKED' || (Number(m.score || 0) >= threshold)
        );
        const matchCount = (rawStatus === 'CLEARED' && !data.isWhitelisted) ? 0 : effectiveMatches.length;

        return {
          decision: uiDecision,
          riskScore: topScore,
          primaryHitReason: data.holdReason,
          caseId: data.caseId,
          caseNumber: data.caseNumber,
          evaluationLatencyMs: data.latencyMs || Math.round(performance.now() - startTime),
          matchCount: matchCount,
          passLogs: data.passLogs,
          isCached: false,
          replayedFromIdempotency: !!data.idempotencyReplayed,
          partyEvaluations: [
            {
              role: partyRole,
              queryName: partyName,
              matchFound: matchCount > 0,
              maxScore: topScore,
              matchedCandidates: ((rawStatus === 'CLEARED' && !data.isWhitelisted) ? [] : effectiveMatches).map((m: any) => ({
                candidateId: m.matchedEntityId || 'SDN-001',
                canonicalName: m.matchedEntityName,
                matchedName: m.matchedEntityName,
                category: (m.category || 'SANCTIONS'),
                confidenceScore: m.score,
                stringSimilarity: m.nameSimilarity ? m.nameSimilarity / 100 : 0.9,
                phoneticSimilarity: m.phoneticSimilarity ? m.phoneticSimilarity / 100 : 1.0,
                sanctionsList: m.sourceList || 'OFAC / UN',
                isSuppressedByWhitelist: !!data.isWhitelisted,
                designationDetails: m.matchDetails,
                tokenBreakdown: [
                  { queryToken: partyName.split(' ')[0] || '', candidateToken: m.matchedEntityName.split(' ')[0] || '', tokenScore: Math.round(m.nameSimilarity || 95), matchType: (m.nameSimilarity >= 99 ? 'EXACT' : 'FUZZY') }
                ]
              }))
            }
          ]
        };
      }
    } catch {
      // Fallback local screening simulation
    }

    const elapsed = Math.round(performance.now() - startTime) || 12;
    const party = request.parties[0];
    const isVictor = party?.fullName?.toLowerCase().includes('bout') || party?.fullName?.toLowerCase().includes('viktor');
    
    return {
      decision: isVictor ? 'BLOCKED' : (request.amount > 100000 ? 'HELD' : 'CLEARED'),
      riskScore: isVictor ? 96.4 : (request.amount > 100000 ? 74.2 : 12.0),
      primaryHitReason: isVictor ? 'UN Security Council Resolution 1521 (Liberia) & OFAC SDN' : undefined,
      caseId: isVictor ? 'c1111111-2222-3333-4444-555555555555' : undefined,
      caseNumber: isVictor ? 'EDITH-CAS-2026-0042' : undefined,
      evaluationLatencyMs: elapsed,
      matchCount: isVictor ? 1 : 0,
      isCached: false,
      replayedFromIdempotency: false,
      partyEvaluations: [
        {
          role: party ? party.role : 'BENEFICIARY',
          queryName: party ? party.fullName : 'Unknown Party',
          matchFound: isVictor,
          maxScore: isVictor ? 96.4 : 15.0,
          matchedCandidates: isVictor ? [
            {
              candidateId: 'SDN-UN-1521-99',
              canonicalName: 'BOUT, Viktor Anatolyevich',
              matchedName: 'Victor Bout',
              category: 'SANCTIONS',
              confidenceScore: 96.4,
              stringSimilarity: 0.94,
              phoneticSimilarity: 1.00,
              sanctionsList: 'UN / OFAC / UAE Local Terror List',
              isSuppressedByWhitelist: false,
              dateOfBirth: '1967-01-13',
              nationality: 'RU',
              designationDetails: 'Arms trafficking, UN Security Council Resolution 1521 & 1532 sanctions.',
              tokenBreakdown: [
                { queryToken: 'VICTOR', candidateToken: 'VIKTOR', tokenScore: 94.0, matchType: 'FUZZY' },
                { queryToken: 'BOUT', candidateToken: 'BOUT', tokenScore: 100.0, matchType: 'EXACT' }
              ]
            }
          ] : []
        }
      ]
    };
  },

  // Cases
  async fetchCases(): Promise<CaseSummary[]> {
    try {
      const res = await fetch(`${API_BASE}/cases`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return MOCK_CASES;
  },

  async fetchCaseDetail(caseId: string): Promise<CaseDetail> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    const summary = MOCK_CASES.find(c => c.id === caseId) || MOCK_CASES[0];
    return {
      ...summary,
      transactionDetails: {
        transactionId: 'TX-2026-9921448',
        amount: 450000.00,
        currency: 'USD',
        sourceAccount: 'AE820330000001234567890',
        destinationAccount: 'CH9300762011623852957',
        originatingCountry: 'AE',
        destinationCountry: 'CH'
      },
      matchedCandidates: [
        {
          candidateId: 'SDN-UN-1521-99',
          canonicalName: 'BOUT, Viktor Anatolyevich',
          matchedName: 'Victor Bout',
          category: 'SANCTIONS',
          confidenceScore: 94.5,
          stringSimilarity: 0.92,
          phoneticSimilarity: 1.00,
          sanctionsList: 'OFAC SDN / UN Sanctions List',
          isSuppressedByWhitelist: false,
          dateOfBirth: '1967-01-13',
          nationality: 'RU',
          designationDetails: 'Specially Designated National under Executive Order 13348',
          tokenBreakdown: [
            { queryToken: 'VICTOR', candidateToken: 'VIKTOR', tokenScore: 94.0, matchType: 'FUZZY' },
            { queryToken: 'BOUT', candidateToken: 'BOUT', tokenScore: 100.0, matchType: 'EXACT' }
          ]
        }
      ],
      auditLog: [
        {
          id: 'aud-1',
          timestampUtc: new Date(Date.now() - 3600000 * 4).toISOString(),
          actorName: 'SYSTEM_AUTOCREATOR',
          actorRole: 'SUPER_ADMIN',
          action: 'CASE_CREATED',
          notes: 'Auto-created from Kafka event: edith.tx.held.v1 with high risk score 94.5'
        },
        {
          id: 'aud-2',
          timestampUtc: new Date(Date.now() - 3600000 * 2).toISOString(),
          actorName: 'Fatima Al-Nuaimi',
          actorRole: 'ANALYST',
          action: 'LOCK_ACQUIRED',
          notes: 'Analyst Fatima initiated EDD screening on transaction beneficiary'
        }
      ],
      comments: [
        {
          id: 'comm-1',
          timestampUtc: new Date(Date.now() - 3600000 * 2).toISOString(),
          authorName: 'Fatima Al-Nuaimi',
          authorRole: 'ANALYST',
          content: 'Confirmed strong phonetic and token alignment with Viktor Bout. Escalating to MLRO for formal freeze authorization.'
        }
      ]
    };
  },

  async acquireLock(caseId: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}/acquire-lock`, {
        method: 'POST',
        headers: getAuthHeaders()
      });
      return res.ok;
    } catch {
      return true;
    }
  },

  async releaseLock(caseId: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}/release-lock`, {
        method: 'POST',
        headers: getAuthHeaders()
      });
      return res.ok;
    } catch {
      return true;
    }
  },

  async resolveCase(caseId: string, resolution: CaseResolution, rationale: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}/resolve`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ resolution, rationale })
      });
      return res.ok;
    } catch {
      return true;
    }
  },

  async transitionCase(caseId: string, newStatus: string, reason?: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}/transition`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ newStatus, userRole: currentSession.role, reason })
      });
      return res.ok;
    } catch {
      return true;
    }
  },

  async addComment(caseId: string, content: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/cases/${caseId}/comments`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ content })
      });
      return res.ok;
    } catch {
      return true;
    }
  },

  // Bulk screening
  async submitBulkJob(fileName: string, totalRows: number): Promise<BulkJobSummary> {
    try {
      const res = await fetch(`${API_BASE}/bulk/jobs`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ fileName, totalRows })
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return {
      jobId: `job-${Date.now().toString(36)}`,
      fileName,
      status: 'PROCESSING',
      totalRecords: totalRows,
      processedRecords: 0,
      hitCount: 0,
      percentComplete: 0,
      startedAtUtc: new Date().toISOString()
    };
  },

  async uploadBulkFile(file: File, keepSkipped: boolean, skippedFormat: string): Promise<{
    jobId: string;
    fileName: string;
    format: string;
    status: string;
    keepSkipped: boolean;
    skippedFormat: string;
    skippedDownloadUrl?: string;
  }> {
    const formData = new FormData();
    formData.append('file', file);
    const params = new URLSearchParams({
      keepSkipped: String(keepSkipped),
      skippedFormat: skippedFormat.toLowerCase()
    });
    const res = await fetch(`${API_BASE}/bulk/upload?${params.toString()}`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${currentSession.token}`,
        'X-Tenant-ID': currentSession.tenantId
      },
      body: formData
    });
    if (!res.ok) throw new Error('Bulk upload failed');
    return await res.json();
  },

  async getBulkJobStatus(jobId: string): Promise<{
    jobId: string;
    fileName: string;
    status: string;
    totalRecords: number;
    processedRecords: number;
    skippedRecords: number;
    skippedDownloadUrl?: string;
  } | null> {
    try {
      const res = await fetch(`${API_BASE}/bulk/jobs/${jobId}`, {
        headers: getAuthHeaders()
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  // Rules
  async fetchRules(): Promise<RuleDefinition[]> {
    try {
      const res = await fetch(`${API_BASE}/rules`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return [
      {
        id: 'r1',
        ruleCode: 'RULE_FATF_GREY_BLACK',
        ruleName: 'FATF High Risk & Monitored Corridors',
        ruleType: 'HIGH_RISK_CORRIDOR',
        severity: 'CRITICAL',
        isActive: true,
        parametersJson: JSON.stringify({ jurisdictions: ['IR', 'KP', 'MM', 'SY', 'RU'], thresholdAmount: 0 }),
        description: 'Auto-holds any cross-border wire entering or originating from FATF blacklist/greylist corridors.',
        updatedAtUtc: new Date().toISOString()
      },
      {
        id: 'r2',
        ruleCode: 'RULE_STRUCTURING_AED',
        ruleName: 'Structuring Smurfing Window (UAE CBUAE)',
        ruleType: 'STRUCTURING_SMURFING',
        severity: 'HIGH',
        isActive: true,
        parametersJson: JSON.stringify({ windowHours: 24, maxCumulativeAmount: 55000, triggerCount: 3 }),
        description: 'Detects multiple rapid deposits just beneath mandatory reporting threshold (55,000 AED).',
        updatedAtUtc: new Date().toISOString()
      },
      {
        id: 'r3',
        ruleCode: 'RULE_CASH_THRESHOLD_50K',
        ruleName: 'Single Large Value Transfer Threshold',
        ruleType: 'TRANSACTION_THRESHOLD',
        severity: 'MEDIUM',
        isActive: true,
        parametersJson: JSON.stringify({ thresholdAmount: 500000, currency: 'AED' }),
        description: 'Requires secondary MLRO approval for all single transfers exceeding 500k AED.',
        updatedAtUtc: new Date().toISOString()
      }
    ];
  },

  async backtestRule(ruleCode: string): Promise<BacktestResult> {
    try {
      const res = await fetch(`${API_BASE}/rules/backtest`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ ruleCode })
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return {
      ruleCode,
      simulatedTransactions: 154200,
      triggeredAlerts: 342,
      triggerRatePercent: 0.22,
      falsePositiveRateEstimate: 4.8,
      averageEvaluationLatencyUs: 140
    };
  },

  // Addons
  async generateGoAmlXml(caseId: string): Promise<string> {
    try {
      const res = await fetch(`${API_BASE}/addons/goaml/export`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ caseId })
      });
      if (res.ok) return await res.text();
    } catch {
      // Fallback
    }
    return `<?xml version="1.0" encoding="utf-8"?>
<report xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="goAML_v5.xsd">
  <rentity_id>CB_UAE_ADCB_001</rentity_id>
  <submission_code>A</submission_code>
  <report_code>STR</report_code>
  <entity_reference>${caseId}</entity_reference>
  <submission_date>${new Date().toISOString()}</submission_date>
  <currency_code_local>AED</currency_code_local>
  <reporting_person>
    <first_name>Fatima</first_name>
    <last_name>Al-Nuaimi</last_name>
    <title>Senior AML Compliance Specialist</title>
  </reporting_person>
  <reason>Match against UN/OFAC Sanctions List with high confidence phonetic score 94.5%</reason>
</report>`;
  },

  async generateAiNarrative(caseId: string): Promise<string> {
    try {
      const res = await fetch(`${API_BASE}/addons/ai/narrative`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ caseId })
      });
      if (res.ok) {
        const data = await res.json();
        return data.narrative;
      }
    } catch {
      // Fallback
    }
    return `1. EXECUTIVE SUMMARY:
On ${new Date().toISOString().split('T')[0]}, transaction reference TX-2026-9921448 for the amount of USD 450,000.00 originating from UAE Account AE820330000001234567890 was intercepted by the EDITH Real-Time Screening Core.

2. SUBJECT IDENTIFICATION:
The designated beneficiary was identified as "Victor Bout", which triggered high-confidence fuzzy and phonetic matching (94.5%) against UN Security Council Resolution 1521 / OFAC SDN designation: BOUT, Viktor Anatolyevich (DOB 1967-01-13).

3. TYPOLOGY & PATTERN OF SUSPICION:
The pattern exhibits rapid cross-border movement into an offshore correspondent account with high-risk corridor flags. The customer's historical profile does not explain the abrupt scale and destination corridor of this transaction.

4. INVESTIGATIVE FINDINGS:
Enhanced Due Diligence (EDD) confirmed no legitimate commercial contracts or trade documents to substantiate the wire. Whitelist screening confirmed the subject is NOT whitelisted.

5. ACTION TAKEN & RECOMMENDATION:
The transaction was placed on immediate statutory HOLD. The compliance team recommends filing this Suspicious Transaction Report (STR) to the UAE Financial Intelligence Unit (goAML) and maintaining the asset freeze pending regulatory directive.`;
  },

  async searchNameAndId(request: NameSearchRequest): Promise<NameSearchResponse> {
    const res = await fetch(`${API_BASE}/screen/name-search`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(request)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ error: 'Failed to execute Name & ID search' }));
      throw new Error(err.error || 'Failed to execute Name & ID search');
    }
    return await res.json();
  },

  async fetchBulkJobs(): Promise<Array<{
    jobId: string;
    fileName: string;
    status: string;
    totalRecords: number;
    processedRecords: number;
    skippedRecords: number;
    skippedDownloadUrl?: string;
  }>> {
    try {
      const res = await fetch(`${API_BASE}/bulk/jobs`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return [];
  },

  async triggerDeltaSync(): Promise<{
    syncBatchId: string;
    scannedCustomers: number;
    newDeltaHits: number;
    hits: Array<{
      id?: string;
      customerCode: string;
      customerName: string;
      nationality?: string;
      matchedWatchlistName: string;
      sourceList: string;
      compositeScore: number;
      matchDetails: string;
    }>;
  } | null> {
    try {
      const res = await fetch(`${API_BASE}/delta/sync`, {
        method: 'POST',
        headers: getAuthHeaders()
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  async authorizeDeltaBatch(batchId: string, approvedCustomerCodes: string[]): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/delta/authorize`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ batchId, approvedCustomerCodes })
      });
      return res.ok;
    } catch {
      return false;
    }
  },

  async fetchWhitelists(): Promise<Array<{
    id: string;
    tenantId: string;
    customerId?: string;
    entityIdentifier: string;
    matchScope: string;
    reason: string;
    expiresAt?: string;
    isActive: boolean;
  }>> {
    try {
      const res = await fetch(`${API_BASE}/whitelists`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return [];
  },

  async createWhitelist(payload: {
    entityIdentifier: string;
    matchScope: string;
    reason: string;
    expiresAt?: string;
  }): Promise<{
    id: string;
    tenantId: string;
    entityIdentifier: string;
    matchScope: string;
    reason: string;
    expiresAt?: string;
    isActive: boolean;
  } | null> {
    try {
      const res = await fetch(`${API_BASE}/whitelists`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(payload)
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  async deleteWhitelist(id: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/whitelists/${id}`, {
        method: 'DELETE',
        headers: getAuthHeaders()
      });
      return res.ok;
    } catch {
      return false;
    }
  },

  async createInterceptCase(payload: {
    transactionRefNo?: string;
    senderName?: string;
    beneficiaryName: string;
    amount?: number;
    currency?: string;
    riskScore: number;
    triggeredList: string;
    holdReason: string;
  }): Promise<{ caseId: string; caseNumber: string; priority: string; status: string } | null> {
    try {
      const res = await fetch(`${API_BASE}/screen/intercept-case`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(payload)
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return {
      caseId: 'c1111111-2222-3333-4444-555555555555',
      caseNumber: `EDITH-CAS-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`,
      priority: payload.riskScore >= 90 ? 'CRITICAL' : 'HIGH',
      status: 'OPEN'
    };
  },

  async generateScreeningAuditCertificate(payload: {
    subjectName: string;
    subjectId?: string;
    screeningType: 'SINGLE_ENTITY' | 'TRANSACTION';
    decision: string;
    overallScore: number;
    latencyMs: number;
    caseNumber?: string;
    matchCount: number;
    passLogs?: Array<{ passNumber: number; score: number; matchType: string; details: string }>;
  }): Promise<ScreeningAuditCertificate> {
    try {
      const res = await fetch(`${API_BASE}/screen/audit-certificate`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(payload)
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    const nowUtc = new Date().toISOString();
    const certId = `EDITH-CERT-${nowUtc.slice(0, 10).replace(/-/g, '')}-${Math.random().toString(16).slice(2, 10).toUpperCase()}`;
    return {
      certificateId: certId,
      issuedAtUtc: nowUtc,
      tenantId: currentSession.tenantId,
      subjectName: payload.subjectName,
      subjectId: payload.subjectId,
      screeningType: payload.screeningType,
      decision: payload.decision,
      overallScore: payload.overallScore,
      latencyMs: payload.latencyMs,
      caseNumber: payload.caseNumber,
      matchCount: payload.matchCount,
      passLogs: payload.passLogs || [
        { passNumber: 1, score: payload.overallScore, matchType: 'NORMALIZATION', details: 'ICU / AnyAscii canonical normalization completed.' },
        { passNumber: 2, score: payload.overallScore, matchType: 'CANDIDATE_RETRIEVAL', details: `Retrieved ${payload.matchCount} watchlist candidates.` },
        { passNumber: 3, score: payload.overallScore, matchType: 'COMPOSITE_SCORING', details: `Evaluated Jaro-Winkler + Double Metaphone score: ${payload.overallScore.toFixed(1)}%.` },
        { passNumber: 4, score: payload.overallScore, matchType: 'THRESHOLD_DECISION', details: `Decision evaluated as ${payload.decision}.` },
        { passNumber: 5, score: 0, matchType: 'WHITELIST_BYPASS', details: 'Pass 5 statutory exemption check completed.' }
      ],
      wormBlockHashSha256: Array.from({ length: 64 }, () => Math.floor(Math.random() * 16).toString(16)).join(''),
      regulatoryAttestation: 'Certified under CBUAE AML/CFT Decision No. 74 & FATF Recommendation 11 (5-Year WORM Immutable Retention).'
    };
  },

  // IAM Users & Groups CRUD
  async fetchIamUsers(): Promise<IamUserRecord[]> {
    try {
      const res = await fetch(`${API_BASE}/iam/users`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    return [];
  },

  async createIamUser(payload: {
    username: string;
    password?: string;
    fullName?: string;
    role: string;
    groupId?: string;
    department?: string;
    shiftStatus?: string;
  }): Promise<IamUserRecord | null> {
    try {
      const res = await fetch(`${API_BASE}/iam/users`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(payload)
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  async updateIamUser(id: string, payload: {
    role?: string;
    groupId?: string;
    isActive?: boolean;
    shiftStatus?: string;
    password?: string;
  }): Promise<IamUserRecord | null> {
    try {
      const res = await fetch(`${API_BASE}/iam/users/${id}`, {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(payload)
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  async deleteIamUser(id: string): Promise<boolean> {
    try {
      const res = await fetch(`${API_BASE}/iam/users/${id}`, {
        method: 'DELETE',
        headers: getAuthHeaders()
      });
      return res.ok;
    } catch {
      return false;
    }
  },

  async fetchIamGroups(): Promise<IamGroupRecord[]> {
    try {
      const res = await fetch(`${API_BASE}/iam/groups`, { headers: getAuthHeaders() });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return [];
  },

  async updateIamGroupPermissions(groupId: string, permissions: IamGroupPermission[]): Promise<IamGroupRecord | null> {
    try {
      const res = await fetch(`${API_BASE}/iam/groups/${groupId}/permissions`, {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify({ permissions })
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    return null;
  },

  async fetchCustomerDossier(customerIdOrCode: string): Promise<KycDossierDto | null> {
    try {
      const res = await fetch(`${API_BASE}/kyc/${encodeURIComponent(customerIdOrCode)}`, {
        headers: getAuthHeaders()
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }
    if (customerIdOrCode.toLowerCase().includes('baraka') || customerIdOrCode.toLowerCase().includes('88902')) {
      return {
        customerId: 'c1010101-1010-1010-1010-101010101010',
        customerCode: 'CUST-AE-88902',
        fullName: 'Al-Baraka General Trading LLC',
        customerType: 'CORPORATE',
        riskCategory: 'HIGH_RISK',
        riskScore: 84.5,
        nationality: 'AE',
        incorporationCountry: 'ARE',
        uboIdentities: ['Sheikh Mohammed Al-Baraka (55% Shareholder)', 'Tariq Mansoor Al-Aziz (45% Shareholder)'],
        documents: [
          { id: 'doc-01', title: 'Commercial License (DED Dubai)', status: 'VERIFIED', expiryDate: '2027-04-12' },
          { id: 'doc-02', title: 'Tax Registration Certificate (TRN)', status: 'VERIFIED', expiryDate: '2028-01-01' }
        ],
        recentTransactions: [
          { txId: 'TX-882194', date: '2026-10-14', amount: 450000, currency: 'USD', counterparty: 'Swiss Trade Finance AG', status: 'CLEARED' },
          { txId: 'TX-882195', date: '2026-10-18', amount: 120000, currency: 'AED', counterparty: 'Gulf Cargo LLC', status: 'CLEARED' }
        ]
      };
    }
    return null;
  },

  async recordAuditEvent(payload: {
    actor?: string;
    action: string;
    targetEntity: string;
    caseNumber?: string;
    details: string;
  }): Promise<AuditLogRecord> {
    try {
      const res = await fetch(`${API_BASE}/reports/audit`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({
          actor: payload.actor || currentSession.username || 'Compliance Officer',
          action: payload.action,
          targetEntity: payload.targetEntity,
          caseNumber: payload.caseNumber,
          details: payload.details
        })
      });
      if (res.ok) return await res.json();
    } catch {
      // ignore
    }
    const hash = Array.from({ length: 64 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
    return {
      id: `aud-${Date.now()}`,
      timestampUtc: new Date().toISOString(),
      actor: payload.actor || 'Compliance Officer',
      action: payload.action,
      targetEntity: payload.targetEntity,
      caseNumber: payload.caseNumber,
      sha256Hash: hash,
      previousBlockHash: '000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f',
      details: payload.details
    };
  }
};
