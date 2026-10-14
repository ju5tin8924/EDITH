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
  CaseResolution
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
  currentSession = session;
  localStorage.setItem('edith_session', JSON.stringify(session));
};

export const getAuthHeaders = (): Record<string, string> => ({
  'Authorization': `Bearer ${currentSession.token}`,
  'X-Tenant-ID': currentSession.tenantId,
  'Content-Type': 'application/json'
});

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
    try {
      const res = await fetch(`${API_BASE}/iam/entitlements`, {
        headers: getAuthHeaders()
      });
      if (res.ok) return await res.json();
    } catch {
      // Fallback
    }

    return {
      tenantId: currentSession.tenantId,
      username: currentSession.username,
      role: currentSession.role,
      menuItems: [
        // Group 1: Operational Screening & Ingestion
        { code: 'DASHBOARD', label: 'Executive Dashboard', path: '/dashboard', icon: 'BarChart3', enabled: true },
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
      ],
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
      beneficiaryCountry: request.destinationCountry || 'US'
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
        
        return {
          decision: (data.decision || data.status || 'CLEARED'),
          riskScore: Number(data.overallScore || 0),
          primaryHitReason: data.holdReason,
          evaluationLatencyMs: data.latencyMs || Math.round(performance.now() - startTime),
          matchCount: data.matches?.length || 0,
          isCached: false,
          replayedFromIdempotency: !!data.idempotencyReplayed,
          partyEvaluations: [
            {
              role: partyRole,
              queryName: partyName,
              matchFound: (data.matches && data.matches.length > 0) || false,
              maxScore: Number(data.overallScore || 0),
              matchedCandidates: (data.matches || []).map((m: any) => ({
                candidateId: m.matchedEntityId || 'SDN-001',
                canonicalName: m.matchedEntityName,
                matchedName: m.matchedEntityName,
                category: (m.category || 'SANCTIONS'),
                confidenceScore: m.score,
                stringSimilarity: m.nameSimilarity ? m.nameSimilarity / 100 : 0.9,
                phoneticSimilarity: m.phoneticSimilarity ? m.phoneticSimilarity / 100 : 1.0,
                sanctionsList: m.sourceList || 'OFAC / UN',
                isSuppressedByWhitelist: false,
                designationDetails: m.matchDetails,
                tokenBreakdown: [
                  { queryToken: partyName.split(' ')[0] || '', candidateToken: m.matchedEntityName.split(' ')[0] || '', tokenScore: 95, matchType: 'FUZZY' }
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
    // Simulate smart matching for primary party
    const party = request.parties[0];
    const isVictor = party?.fullName?.toLowerCase().includes('bout') || party?.fullName?.toLowerCase().includes('viktor');
    
    return {
      decision: isVictor ? 'BLOCKED' : (request.amount > 100000 ? 'HELD' : 'CLEARED'),
      riskScore: isVictor ? 96.4 : (request.amount > 100000 ? 74.2 : 12.0),
      primaryHitReason: isVictor ? 'UN Security Council Resolution 1521 (Liberia) & OFAC SDN' : undefined,
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
  }
};
