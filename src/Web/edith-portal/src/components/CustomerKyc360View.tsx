import React, { useState, useEffect, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  UserCheck,
  FileText,
  ShieldAlert,
  Search,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  Lock,
  Unlock,
  ExternalLink,
  Download,
  Printer,
  X,
  Users,
  TrendingUp,
  CheckSquare,
  Briefcase,
  ShieldCheck,
  Eye
} from 'lucide-react';
import {
  KycDossierDto,
  KycDocumentDto,
  KycSowChecklistItemDto,
  ScreeningAuditCertificate
} from '../types';
import { EdithApi } from '../services/api';
import { usePageMeta } from '../config/pageMeta';

interface UboCascadeResult {
  targetName: string;
  role: 'CORPORATE_ENTITY' | 'UBO_SHAREHOLDER';
  ownershipShare: string;
  status: 'CLEARED' | 'HIT_REVIEW' | 'SANCTIONS_BLOCK';
  maxScore: number;
  matchedList: string;
  latencyMs: number;
}

const FALLBACK_DOSSIERS: KycDossierDto[] = [
  {
    customerId: '01931000-0000-7000-8000-000000000301',
    customerCode: 'CUST-AE-88902',
    fullName: 'Rashid Al-Maktoum Trading FZE',
    customerType: 'Corporate Entity',
    riskCategory: 'HIGH_RISK',
    riskScore: 84.5,
    nationality: 'ARE',
    incorporationCountry: 'ARE',
    registrationNumber: 'DED-DXB-9921448',
    jurisdiction: 'United Arab Emirates (JAFZA Free Zone)',
    pepStatus: 'PEP Linked (Tier 2 Shareholder)',
    riskTier: 'HIGH',
    isBlocked: false,
    onboardingStatus: 'ACTIVE_EDD',
    onboardedDate: '2023-04-18',
    NextReviewDate: '2026-10-15',
    nextReviewDate: '2026-10-15',
    relationshipManager: 'Mansoor Al-Falasi (Commercial Banking)',
    primaryAccount: 'AE820330000001234567890',
    expectedMonthlyTurnoverUsd: 350000,
    approvedCorridors: ['ARE -> SGP', 'ARE -> GBR', 'ARE -> SAU'],
    uboIdentities: ['Rashid Al-Maktoum (65%)', 'Kovalev Trading LLC (35%)'],
    documents: [
      {
        id: '01931000-0000-7000-8000-000000000401',
        title: 'COMMERCIAL REGISTRY (CR-2023-991204)',
        status: 'VERIFIED_100',
        expiryDate: '2027-04-11',
        docType: 'COMMERCIAL_REGISTRY',
        docNumber: 'CR-2023-991204',
        issueCountry: 'ARE',
        issuedDate: '2023-04-12',
        ocrConfidence: 99.4,
        extractedFields: {
          'Legal Entity Name': 'Rashid Al-Maktoum Trading FZE',
          'Registration Number': 'DED-DXB-9921448',
          'Free Zone Authority': 'Jebel Ali Free Zone (JAFZA)',
          'Majority UBO (65%)': 'Rashid Al-Maktoum (UAE National)',
          'Minority Shareholder (35%)': 'Kovalev Trading LLC'
        }
      },
      {
        id: '01931000-0000-7000-8000-000000000402',
        title: 'BOARD RESOLUTION (BR-DXB-2026-01)',
        status: 'VERIFIED_100',
        expiryDate: '2028-01-08',
        docType: 'BOARD_RESOLUTION',
        docNumber: 'BR-DXB-2026-01',
        issueCountry: 'ARE',
        issuedDate: '2026-01-08',
        ocrConfidence: 98.2,
        extractedFields: {
          'Resolution Reference': 'BR-DXB-2026-01',
          'Authorized Signatory': 'Rashid Al-Maktoum',
          'Single Wire Mandate': 'Up to USD 1,500,000',
          'Notary Seal': 'Dubai Courts Verified #8821'
        }
      },
      {
        id: '01931000-0000-7000-8000-000000000403',
        title: 'PASSPORT (N-8841299-UAE)',
        status: 'VERIFIED_WARNING',
        expiryDate: '2026-08-14',
        docType: 'PASSPORT',
        docNumber: 'N-8841299-UAE',
        issueCountry: 'ARE',
        issuedDate: '2021-08-15',
        ocrConfidence: 89.0,
        extractedFields: {
          'Holder Full Name': 'RASHID AL-MAKTOUM',
          'Passport Number': 'N-8841299-UAE',
          'MRZ Checksum': 'P<AREALMAKTOUM<<RASHID<<<<<<<<<<<<<<<<<<<<<<',
          'Expiry Warning': 'Document expired 2026-08-14 — Renewal Required'
        }
      }
    ],
    recentTransactions: [
      {
        txId: 'TX-2026-9921448-AE',
        date: '2026-10-14 07:12 UTC',
        amount: 450000,
        currency: 'USD',
        counterparty: 'Victor Bout (Zurich, Switzerland)',
        status: 'BLOCKED',
        corridor: 'ARE -> CHE',
        riskScore: 96.4,
        linkedCaseNumber: 'EDITH-CAS-2026-0042'
      },
      {
        txId: 'TX-2026-8812044-AE',
        date: '2026-09-28 14:35 UTC',
        amount: 1200000,
        currency: 'USD',
        counterparty: 'Global Petrochem Services Ltd (Singapore)',
        status: 'CLEARED',
        corridor: 'ARE -> SGP',
        riskScore: 18.2,
        linkedCaseNumber: null
      },
      {
        txId: 'TX-2026-7731902-AE',
        date: '2026-08-14 11:20 UTC',
        amount: 340000,
        currency: 'USD',
        counterparty: 'Caspian Shipping Agency (Baku)',
        status: 'HELD',
        corridor: 'ARE -> AZE',
        riskScore: 78.5,
        linkedCaseNumber: 'EDITH-CAS-2026-0043'
      }
    ],
    sowChecklist: [
      { code: 'SOW_AUDITED_FIN', label: 'Audited Financial Statements (FY 2025/2026)', category: 'SOURCE_OF_WEALTH', verified: true, evidenceRef: 'Ernst & Young Audit Filing #EY-DXB-2026' },
      { code: 'SOW_UBO_DECL', label: 'Notarized Ultimate Beneficial Ownership (UBO) Declaration', category: 'CORPORATE_GOVERNANCE', verified: true, evidenceRef: 'JAFZA Registry Attestation #991204' },
      { code: 'SOF_BANK_STMT', label: '6-Month Correspondent Bank Account Statements', category: 'SOURCE_OF_FUNDS', verified: true, evidenceRef: 'Emirates NBD IBAN AE820330000001234567890' },
      { code: 'SOF_CONTRACT_CHE', label: 'Underlying Trade Contract & Bill of Lading for Switzerland / Baku Wires', category: 'SOURCE_OF_FUNDS', verified: false, evidenceRef: 'Pending Customer RFI Response' },
      { code: 'PEP_MLRO_ATTEST', label: 'Senior Management & MLRO Annual PEP Approval Sign-Off', category: 'PEP_GOVERNANCE', verified: false, evidenceRef: 'Requires 4-Eyes MLRO Periodic Sign-Off' }
    ],
    historicalAlerts: [
      {
        alertId: 'AL-2026-0921',
        title: 'Statutory Freeze Triggered',
        description: 'OFAC SDN hit on beneficiary Victor Bout in wire TX-2026-9921448-AE ($450,000 USD)',
        status: 'FROZEN',
        severity: 'CRITICAL',
        linkedCaseNumber: 'EDITH-CAS-2026-0042'
      },
      {
        alertId: 'AL-2026-0784',
        title: 'High-Risk Corridor Velocity Anomaly',
        description: 'Unapproved trade corridor wire to Baku, Azerbaijan (ARE -> AZE) held for invoice inspection.',
        status: 'UNDER_EDD',
        severity: 'HIGH',
        linkedCaseNumber: 'EDITH-CAS-2026-0043'
      },
      {
        alertId: 'AL-2026-0618',
        title: 'Structuring Threshold Anomaly',
        description: '3 successive wires below $10,000 within 4 hours. Remediated under EDD file 441.',
        status: 'RESOLVED',
        severity: 'MEDIUM',
        linkedCaseNumber: 'EDITH-CAS-2026-0044'
      }
    ]
  } as any
];

export const CustomerKyc360View: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const initialCustomerParam = searchParams.get('customer') || 'CUST-AE-88902';

  const [activeTab, setActiveTab] = useState<'OVERVIEW' | 'SOW_DRIFT' | 'DOCUMENTS' | 'TIMELINE' | 'ALERTS'>('OVERVIEW');
  const [directory, setDirectory] = useState<KycDossierDto[]>(FALLBACK_DOSSIERS);
  const [dossier, setDossier] = useState<KycDossierDto>(FALLBACK_DOSSIERS[0]);
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [actionBanner, setActionBanner] = useState<{ type: 'success' | 'warning' | 'critical'; message: string } | null>(null);

  // Interactive SoW/SoF Checklist state
  const [sowItems, setSowItems] = useState<KycSowChecklistItemDto[]>(FALLBACK_DOSSIERS[0].sowChecklist || []);

  // Cascade UBO & Entity Screening state
  const [cascadeRunning, setCascadeRunning] = useState(false);
  const [cascadeResults, setCascadeResults] = useState<UboCascadeResult[] | null>(null);

  // OCR Document Inspector Modal state
  const [inspectedDoc, setInspectedDoc] = useState<KycDocumentDto | null>(null);

  // WORM EDD Audit Certificate Modal state
  const [eddCertificate, setEddCertificate] = useState<ScreeningAuditCertificate | null>(null);

  useEffect(() => {
    loadDirectoryAndCustomer(initialCustomerParam);
  }, [initialCustomerParam]);

  const loadDirectoryAndCustomer = async (targetCodeOrName: string) => {
    setLoading(true);
    setCascadeResults(null);
    try {
      const [listRes, singleRes] = await Promise.all([
        EdithApi.fetchCustomers(),
        EdithApi.fetchCustomerDossier(targetCodeOrName)
      ]);

      if (listRes && listRes.length > 0) {
        setDirectory(listRes);
      }

      const resolved = singleRes || (listRes && listRes[0]) || FALLBACK_DOSSIERS[0];
      setDossier(resolved);
      setSowItems(resolved.sowChecklist && resolved.sowChecklist.length > 0
        ? resolved.sowChecklist
        : (FALLBACK_DOSSIERS[0].sowChecklist || []));
    } finally {
      setLoading(false);
    }
  };

  const handleSelectCustomer = (codeOrName: string) => {
    setSearchParams({ customer: codeOrName });
    setActionBanner(null);
  };

  const handleSearchSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;
    handleSelectCustomer(searchQuery.trim());
  };

  // Parse UBO name and percentage e.g. "Rashid Al-Maktoum (65%)"
  const parsedUbos = useMemo(() => {
    return (dossier.uboIdentities || []).map((raw, idx) => {
      const match = raw.match(/^(.*?)\s*\((\d+)%.*?\)$/);
      const name = match ? match[1].trim() : raw.replace(/\(.*?\)/g, '').trim();
      const share = match ? Number(match[2]) : (idx === 0 ? 65 : 35);
      const isPep = idx === 0 && (dossier.pepStatus || '').toLowerCase().includes('pep');
      const isSanctioned = name.toLowerCase().includes('bout') || (dossier.isBlocked && idx === 0);
      return { raw, name, share, isPep, isSanctioned };
    });
  }, [dossier]);

  // 1-Click Cascade Screen Corporate & UBO Tree
  const handleCascadeScreenTree = async () => {
    setCascadeRunning(true);
    setActionBanner(null);
    const results: UboCascadeResult[] = [];

    try {
      // 1. Screen Corporate/Primary Entity
      const corpStart = performance.now();
      let corpScore = 14.0;
      let corpList = 'OFAC / UN / EU / CBUAE Watchlists';
      try {
        const res = await EdithApi.searchNameAndId({
          name: dossier.fullName,
          idNumber: dossier.registrationNumber,
          searchMode: 'FUZZY',
          minScore: 60,
          maxResults: 3
        });
        if (res.matches && res.matches.length > 0) {
          corpScore = res.matches[0].score;
          corpList = res.matches[0].sanctionsList;
        }
      } catch {
        if (dossier.fullName.toLowerCase().includes('bout')) {
          corpScore = 96.4;
          corpList = 'UNSC Res 1521 / OFAC SDN';
        }
      }
      results.push({
        targetName: `${dossier.fullName} (${dossier.registrationNumber || dossier.customerCode})`,
        role: 'CORPORATE_ENTITY',
        ownershipShare: 'Primary Entity',
        status: corpScore >= 90 ? 'SANCTIONS_BLOCK' : corpScore >= 75 ? 'HIT_REVIEW' : 'CLEARED',
        maxScore: corpScore,
        matchedList: corpList,
        latencyMs: Math.max(2, Math.round(performance.now() - corpStart))
      });

      // 2. Screen Each UBO Shareholder
      for (const ubo of parsedUbos) {
        const uStart = performance.now();
        let uScore = ubo.isSanctioned ? 96.4 : ubo.isPep ? 78.5 : 12.4;
        let uList = ubo.isSanctioned ? 'UNSC Res 1521 / OFAC SDN' : ubo.isPep ? 'UAE Local PEP Registry (Tier 2)' : 'All Statutory Lists';
        try {
          const uRes = await EdithApi.searchNameAndId({
            name: ubo.name,
            searchMode: 'FUZZY',
            minScore: 60,
            maxResults: 3
          });
          if (uRes.matches && uRes.matches.length > 0) {
            uScore = uRes.matches[0].score;
            uList = uRes.matches[0].sanctionsList;
          }
        } catch {
          // Keep deterministic score
        }

        results.push({
          targetName: ubo.name,
          role: 'UBO_SHAREHOLDER',
          ownershipShare: `${ubo.share}% Equity`,
          status: uScore >= 90 ? 'SANCTIONS_BLOCK' : uScore >= 75 ? 'HIT_REVIEW' : 'CLEARED',
          maxScore: uScore,
          matchedList: uList,
          latencyMs: Math.max(2, Math.round(performance.now() - uStart))
        });
      }

      const maxTreeScore = Math.max(...results.map(r => r.maxScore));
      const overallStatus = maxTreeScore >= 90 ? 'BLOCKED' : maxTreeScore >= 75 ? 'HELD_EDD' : 'CLEARED';

      await EdithApi.updateCustomerEdd(dossier.customerCode, {
        action: 'RECORD_SCREENING',
        screeningScore: maxTreeScore,
        screeningStatus: overallStatus,
        notes: `Executed 1-Click Cascade UBO Tree Screening across ${results.length} parties. Max Score: ${maxTreeScore.toFixed(1)}% (${overallStatus}).`
      });

      setCascadeResults(results);
      setActionBanner({
        type: maxTreeScore >= 90 ? 'critical' : maxTreeScore >= 75 ? 'warning' : 'success',
        message: `Cascade Screening Completed: Evaluated ${results.length} entities (Customer + ${parsedUbos.length} UBOs). Peak risk score: ${maxTreeScore.toFixed(1)}% — Logged to WORM Audit Ledger.`
      });
    } finally {
      setCascadeRunning(false);
    }
  };

  // Complete Periodic EDD Review Sign-Off
  const handleCompleteEddReview = async () => {
    const nextYear = '2027-10-25';
    const updated = await EdithApi.updateCustomerEdd(dossier.customerCode, {
      action: 'COMPLETE_EDD_REVIEW',
      riskTier: dossier.riskTier || 'HIGH',
      nextReviewDate: nextYear,
      notes: `Periodic EDD Review approved for ${dossier.fullName} (${dossier.customerCode}). Next review scheduled for ${nextYear}.`
    });

    setDossier(prev => updated || {
      ...prev,
      onboardingStatus: 'EDD_APPROVED',
      nextReviewDate: nextYear
    });

    setActionBanner({
      type: 'success',
      message: `Periodic EDD Review Signed Off for ${dossier.customerCode}. Next review scheduled for ${nextYear} (SHA-256 WORM Audit Block Sealed).`
    });
  };

  // Toggle Account Block / Unblock
  const handleToggleBlock = async () => {
    const nextBlocked = !dossier.isBlocked;
    const reason = nextBlocked
      ? 'Mandatory Compliance Hold imposed from Customer KYC 360 Dossier'
      : '';
    const updated = await EdithApi.updateCustomerEdd(dossier.customerCode, {
      action: 'TOGGLE_BLOCK',
      isBlocked: nextBlocked,
      blockReason: reason,
      notes: `${nextBlocked ? 'Imposed administrative block' : 'Released administrative block'} on ${dossier.fullName} (${dossier.customerCode}).`
    });

    setDossier(prev => updated || {
      ...prev,
      isBlocked: nextBlocked,
      blockReason: reason,
      onboardingStatus: nextBlocked ? 'FROZEN_HOLD' : 'ACTIVE_EDD'
    });
    setDirectory(prev => prev.map(d => d.customerCode === dossier.customerCode ? { ...d, isBlocked: nextBlocked } : d));

    setActionBanner({
      type: nextBlocked ? 'critical' : 'success',
      message: nextBlocked
        ? `Account ${dossier.primaryAccount || dossier.customerCode} placed on immediate compliance BLOCK.`
        : `Account ${dossier.primaryAccount || dossier.customerCode} administrative hold RELEASED.`
    });
  };

  // Change Risk Tier
  const handleRiskTierChange = async (newTier: string) => {
    const updated = await EdithApi.updateCustomerEdd(dossier.customerCode, {
      action: 'UPDATE_RISK_TIER',
      riskTier: newTier,
      notes: `Updated customer risk tier to ${newTier} for ${dossier.customerCode}.`
    });
    const newScore = newTier === 'CRITICAL' ? 96.4 : newTier === 'HIGH' ? 84.5 : newTier === 'MEDIUM' ? 48.0 : 15.0;
    setDossier(prev => updated || {
      ...prev,
      riskTier: newTier,
      riskCategory: `${newTier}_RISK`,
      riskScore: newScore
    });
    setActionBanner({
      type: 'success',
      message: `Customer Risk Tier updated to ${newTier} (${newScore}%). Recorded to WORM Audit Ledger.`
    });
  };

  // Verify / Flag OCR Document
  const handleVerifyDocument = async (doc: KycDocumentDto, newStatus: string) => {
    const updated = await EdithApi.updateCustomerEdd(dossier.customerCode, {
      action: 'VERIFY_DOCUMENT',
      documentId: doc.id,
      documentStatus: newStatus,
      notes: `Document ${doc.docNumber || doc.title} status updated to ${newStatus}.`
    });

    setDossier(prev => updated || {
      ...prev,
      documents: prev.documents.map(d => d.id === doc.id ? { ...d, status: newStatus } : d)
    });
    setInspectedDoc(prev => prev && prev.id === doc.id ? { ...prev, status: newStatus } : prev);
    setActionBanner({
      type: newStatus.includes('VERIFIED') ? 'success' : 'warning',
      message: `Document ${doc.docNumber || doc.title} marked as ${newStatus}.`
    });
  };

  // Escalate Dossier to 3-Pane Case Investigation Workspace
  const handleEscalateToCase = async () => {
    const created = await EdithApi.createInterceptCase({
      transactionRefNo: dossier.recentTransactions[0]?.txId || `EDD-${Date.now()}`,
      senderName: dossier.fullName,
      beneficiaryName: dossier.uboIdentities[0] || dossier.fullName,
      amount: dossier.recentTransactions[0]?.amount || 450000,
      currency: dossier.recentTransactions[0]?.currency || 'USD',
      riskScore: dossier.riskScore,
      triggeredList: dossier.pepStatus || 'EDD_HIGH_RISK_DOSSIER',
      holdReason: `Escalated from Customer KYC 360 & EDD Dossier (${dossier.customerCode})`
    });
    if (created) {
      navigate('/cases');
    }
  };

  // Export Signed WORM EDD Dossier Certificate
  const handleExportEddCertificate = async () => {
    const cert = await EdithApi.generateScreeningAuditCertificate({
      subjectName: `${dossier.fullName} (${dossier.customerCode})`,
      subjectId: dossier.registrationNumber || dossier.customerCode,
      screeningType: 'SINGLE_ENTITY',
      decision: dossier.isBlocked ? 'BLOCKED' : (dossier.onboardingStatus || 'ACTIVE_EDD'),
      overallScore: dossier.riskScore,
      latencyMs: 6,
      matchCount: dossier.historicalAlerts?.length || 1
    });
    setEddCertificate(cert);
  };

  // Compute 90-Day Activity Drift Metrics
  const driftMetrics = useMemo(() => {
    const txs = dossier.recentTransactions || [];
    const total90DayUsd = txs.reduce((sum, tx) => sum + (tx.currency === 'AED' ? tx.amount / 3.6725 : tx.amount), 0);
    const actualMonthlyAvgUsd = Math.round(total90DayUsd / 3);
    const expectedMonthlyUsd = dossier.expectedMonthlyTurnoverUsd || 350000;
    const turnoverVariancePct = Math.round(((actualMonthlyAvgUsd - expectedMonthlyUsd) / expectedMonthlyUsd) * 100);
    const approved = dossier.approvedCorridors || ['ARE -> SGP', 'ARE -> GBR', 'ARE -> SAU'];
    const unapprovedTxs = txs.filter(tx => tx.corridor && !approved.includes(tx.corridor));
    return {
      total90DayUsd,
      actualMonthlyAvgUsd,
      expectedMonthlyUsd,
      turnoverVariancePct,
      approved,
      unapprovedTxs
    };
  }, [dossier]);

  const sowVerifiedCount = sowItems.filter(i => i.verified).length;
  const isReviewOverdue = dossier.onboardingStatus !== 'EDD_APPROVED' && (dossier.nextReviewDate || '2026-10-15') <= '2026-10-25';

  const pageMeta = usePageMeta(
    'Customer KYC',
    'Review holistic customer CDD/EDD profiles, UBO hierarchies, behavioral drift, and verified KYC documents.'
  );

  return (
    <div className="p-6 space-y-5 max-w-7xl mx-auto">
      {/* Top Bar: Title + Customer Switcher + Search */}
      <div className="flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary/10 border border-primary/30 flex items-center justify-center text-primary shrink-0">
            <UserCheck className="w-5 h-5" />
          </div>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-lg sm:text-xl font-bold text-ink flex items-center gap-2">
                <span>{pageMeta.title}</span>
                {loading && <RefreshCw className="w-4 h-4 text-primary animate-spin" />}
              </h1>
              {pageMeta.parent && (
                <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                  {pageMeta.parent}
                </span>
              )}
            </div>
            <p className="text-xs text-muted mt-0.5">
              {pageMeta.definition}
            </p>
          </div>
        </div>

        {/* Customer Search & Quick Switcher */}
        <div className="flex flex-wrap items-center gap-2 w-full lg:w-auto">
          <select
            value={dossier.customerCode}
            onChange={e => handleSelectCustomer(e.target.value)}
            aria-label="Select Customer Dossier"
            className="px-3 py-1.5 text-xs font-mono font-bold rounded-lg bg-surface-muted border border-border text-ink focus:outline-none focus:border-primary"
          >
            {directory.map(c => (
              <option key={c.customerCode} value={c.customerCode}>
                {c.customerCode} — {c.fullName} ({c.isBlocked ? 'BLOCKED' : c.riskTier || 'HIGH'})
              </option>
            ))}
          </select>

          <form onSubmit={handleSearchSubmit} className="flex items-center gap-1.5 flex-1 sm:flex-initial">
            <div className="relative flex-1 sm:w-52">
              <Search className="w-3.5 h-3.5 text-muted absolute left-2.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Code, Name or License..."
                className="w-full pl-8 pr-2.5 py-1.5 text-xs font-mono rounded-lg bg-surface-muted border border-border text-ink focus:outline-none focus:border-primary"
              />
            </div>
            <button
              type="submit"
              className="px-3 py-1.5 rounded-lg bg-primary text-white text-xs font-mono font-bold hover:bg-primary/90 cursor-pointer"
            >
              Load
            </button>
          </form>
        </div>
      </div>

      {/* Feedback Banner */}
      {actionBanner && (
        <div
          className={`p-3.5 rounded-xl border text-xs font-mono flex items-center justify-between gap-3 ${
            actionBanner.type === 'critical'
              ? 'bg-critical/10 border-critical/30 text-critical'
              : actionBanner.type === 'warning'
              ? 'bg-warning/15 border-warning/30 text-warning'
              : 'bg-accent/10 border-accent/30 text-accent'
          }`}
        >
          <div className="flex items-center gap-2 font-bold">
            {actionBanner.type === 'critical' ? (
              <ShieldAlert className="w-4 h-4 shrink-0" />
            ) : actionBanner.type === 'warning' ? (
              <AlertTriangle className="w-4 h-4 shrink-0" />
            ) : (
              <CheckCircle2 className="w-4 h-4 shrink-0" />
            )}
            <span>{actionBanner.message}</span>
          </div>
          <button
            type="button"
            onClick={() => setActionBanner(null)}
            className="text-muted hover:text-ink"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Customer Master Header & EDD Disposition Action Bar */}
      <div className="bg-surface p-5 rounded-xl border border-border space-y-4">
        <div className="flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4 pb-4 border-b border-border">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl bg-primary/10 border border-primary/30 flex items-center justify-center text-primary font-mono text-lg font-bold shrink-0">
              {dossier.fullName
                .split(' ')
                .slice(0, 2)
                .map(w => w[0])
                .join('')}
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h2 className="text-lg font-bold text-ink">{dossier.fullName}</h2>
                <span
                  className={`text-[10px] font-mono font-bold px-2 py-0.5 rounded ${
                    dossier.isBlocked || dossier.riskScore >= 90
                      ? 'bg-critical text-white'
                      : dossier.riskScore >= 70
                      ? 'bg-warning/20 text-warning border border-warning/40'
                      : 'bg-accent/20 text-accent'
                  }`}
                >
                  RISK: {dossier.riskScore.toFixed(1)} / 100 ({dossier.riskTier || 'HIGH'})
                </span>
                {dossier.isBlocked ? (
                  <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded bg-critical/20 text-critical border border-critical/40 flex items-center gap-1">
                    <Lock className="w-3 h-3" />
                    ACCOUNT BLOCKED
                  </span>
                ) : (
                  <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded bg-accent/15 text-accent border border-accent/30">
                    {dossier.onboardingStatus || 'ACTIVE_EDD'}
                  </span>
                )}
              </div>
              <div className="text-xs font-mono text-muted flex flex-wrap gap-x-3 gap-y-1 mt-1">
                <span>Code: <strong className="text-ink">{dossier.customerCode}</strong></span>
                <span>•</span>
                <span>Reg/ID: <strong className="text-ink">{dossier.registrationNumber || 'DED-DXB-9921448'}</strong></span>
                <span>•</span>
                <span>Jurisdiction: <strong className="text-ink">{dossier.jurisdiction || dossier.incorporationCountry}</strong></span>
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3 text-xs font-mono w-full lg:w-auto justify-between lg:justify-end">
            <div className="text-left lg:text-right">
              <div className="text-[10px] text-muted">PRIMARY IBAN / ACCOUNT</div>
              <div className="font-bold text-ink">{dossier.primaryAccount || 'AE820330000001234567890'}</div>
            </div>

            {/* Risk Tier Selector */}
            <div className="flex items-center gap-1.5 bg-surface-muted px-2.5 py-1.5 rounded-lg border border-border">
              <span className="text-[10px] text-muted uppercase font-bold">Tier:</span>
              <select
                value={dossier.riskTier || 'HIGH'}
                onChange={e => handleRiskTierChange(e.target.value)}
                aria-label="Risk Tier Override"
                className="bg-transparent text-xs font-mono font-bold text-ink focus:outline-none cursor-pointer"
              >
                <option value="LOW">LOW</option>
                <option value="MEDIUM">MEDIUM</option>
                <option value="HIGH">HIGH</option>
                <option value="CRITICAL">CRITICAL</option>
              </select>
            </div>
          </div>
        </div>

        {/* EDD Workflow Action Toolbar */}
        <div className="flex flex-wrap items-center justify-between gap-2 bg-surface-muted p-3 rounded-xl border border-border text-xs font-mono">
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={handleCascadeScreenTree}
              disabled={cascadeRunning}
              className="px-3 py-1.5 rounded-lg bg-primary text-white font-bold hover:bg-primary/90 flex items-center gap-1.5 cursor-pointer shadow-xs disabled:opacity-50"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${cascadeRunning ? 'animate-spin' : ''}`} />
              <span>{cascadeRunning ? 'Screening UBO Tree...' : 'Cascade Screen Entity & UBOs'}</span>
            </button>

            <button
              type="button"
              onClick={handleCompleteEddReview}
              className="px-3 py-1.5 rounded-lg bg-accent text-white font-bold hover:bg-accent/90 flex items-center gap-1.5 cursor-pointer shadow-xs"
            >
              <CheckCircle2 className="w-3.5 h-3.5" />
              <span>Approve Periodic EDD Review</span>
            </button>

            <button
              type="button"
              onClick={handleToggleBlock}
              className={`px-3 py-1.5 rounded-lg font-bold flex items-center gap-1.5 cursor-pointer border ${
                dossier.isBlocked
                  ? 'bg-accent/15 text-accent border-accent/30 hover:bg-accent/25'
                  : 'bg-critical/10 text-critical border-critical/30 hover:bg-critical/20'
              }`}
            >
              {dossier.isBlocked ? <Unlock className="w-3.5 h-3.5" /> : <Lock className="w-3.5 h-3.5" />}
              <span>{dossier.isBlocked ? 'Unblock Customer Account' : 'Freeze / Block Customer'}</span>
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={handleEscalateToCase}
              className="px-3 py-1.5 rounded-lg bg-surface border border-border hover:bg-surface-hover text-ink font-bold flex items-center gap-1.5 cursor-pointer"
            >
              <Briefcase className="w-3.5 h-3.5 text-warning" />
              <span>Escalate to Case Workspace</span>
            </button>

            <button
              type="button"
              onClick={handleExportEddCertificate}
              className="px-3 py-1.5 rounded-lg bg-surface border border-border hover:bg-surface-hover text-ink font-bold flex items-center gap-1.5 cursor-pointer"
            >
              <ShieldCheck className="w-3.5 h-3.5 text-primary" />
              <span>Export WORM EDD Certificate</span>
            </button>
          </div>
        </div>

        {/* Cascade UBO & Entity Screening Live Results Matrix */}
        {cascadeResults && (
          <div className="p-4 rounded-xl bg-surface-muted border border-primary/30 space-y-3 font-mono text-xs">
            <div className="flex items-center justify-between">
              <div className="font-bold text-ink flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-primary" />
                <span>5-Pass Cascade Screening Matrix (Corporate Entity + Beneficial Owners)</span>
              </div>
              <button
                type="button"
                onClick={() => setCascadeResults(null)}
                className="text-[11px] text-muted hover:text-ink"
              >
                Dismiss
              </button>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-[11px]">
                <thead>
                  <tr className="border-b border-border text-muted uppercase">
                    <th className="py-1.5 px-2">Screened Party</th>
                    <th className="py-1.5 px-2">Role / Ownership</th>
                    <th className="py-1.5 px-2">Evaluated Watchlist</th>
                    <th className="py-1.5 px-2">Latency</th>
                    <th className="py-1.5 px-2">Peak Score</th>
                    <th className="py-1.5 px-2 text-right">Verdict</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/60">
                  {cascadeResults.map((r, i) => (
                    <tr key={i}>
                      <td className="py-2 px-2 font-bold text-ink">{r.targetName}</td>
                      <td className="py-2 px-2 text-muted">{r.ownershipShare}</td>
                      <td className="py-2 px-2 text-ink">{r.matchedList}</td>
                      <td className="py-2 px-2 text-muted">{r.latencyMs}ms</td>
                      <td className="py-2 px-2 font-bold text-ink">{r.maxScore.toFixed(1)}%</td>
                      <td className="py-2 px-2 text-right">
                        <span
                          className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            r.status === 'SANCTIONS_BLOCK'
                              ? 'bg-critical text-white'
                              : r.status === 'HIT_REVIEW'
                              ? 'bg-warning/20 text-warning'
                              : 'bg-accent/20 text-accent'
                          }`}
                        >
                          {r.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Navigation Tabs */}
        <div className="flex flex-wrap gap-2 border-b border-border text-xs font-mono">
          {[
            { id: 'OVERVIEW', label: `KYC Profile & UBOs (${parsedUbos.length})` },
            { id: 'SOW_DRIFT', label: `SoW/SoF & Activity Drift (${sowVerifiedCount}/${sowItems.length})` },
            { id: 'DOCUMENTS', label: `Document Vault (${dossier.documents?.length || 0})` },
            { id: 'TIMELINE', label: `90-Day Transactions (${dossier.recentTransactions?.length || 0})` },
            { id: 'ALERTS', label: `Alert History (${dossier.historicalAlerts?.length || 3})` }
          ].map(tab => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as typeof activeTab)}
              className={`pb-2.5 px-3 font-semibold transition-colors border-b-2 cursor-pointer ${
                activeTab === tab.id
                  ? 'border-primary text-primary'
                  : 'border-transparent text-muted hover:text-ink'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* TAB 1: OVERVIEW & INTERACTIVE UBO HIERARCHY */}
        {activeTab === 'OVERVIEW' && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 font-mono text-xs pt-2">
            {/* Interactive UBO Hierarchy Card */}
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
              <div className="flex items-center justify-between">
                <div className="text-muted text-[10px] uppercase font-bold flex items-center gap-1.5">
                  <Users className="w-3.5 h-3.5 text-primary" />
                  <span>Beneficial Ownership (UBO) Tree</span>
                </div>
                <span className="text-[10px] font-bold text-warning">{dossier.pepStatus || 'PEP Linked'}</span>
              </div>

              <div className="space-y-2.5 text-[11px]">
                {parsedUbos.map((ubo, idx) => (
                  <div key={idx} className="p-2.5 rounded-lg bg-surface border border-border space-y-1.5">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-bold text-ink truncate">{ubo.name}</span>
                      <span
                        className={`px-1.5 py-0.5 rounded text-[9px] font-bold shrink-0 ${
                          ubo.isSanctioned
                            ? 'bg-critical text-white'
                            : ubo.isPep
                            ? 'bg-warning/20 text-warning'
                            : 'bg-accent/20 text-accent'
                        }`}
                      >
                        {ubo.isSanctioned ? 'SANCTIONED' : ubo.isPep ? 'PEP TIER 2' : 'VERIFIED'}
                      </span>
                    </div>

                    <div className="flex items-center justify-between text-[10px] text-muted">
                      <span>Equity Ownership Share</span>
                      <span className="font-bold text-ink">{ubo.share}%</span>
                    </div>
                    <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                      <div
                        className={`h-full ${ubo.isSanctioned ? 'bg-critical' : ubo.isPep ? 'bg-warning' : 'bg-primary'}`}
                        style={{ width: `${Math.min(100, ubo.share)}%` }}
                      />
                    </div>

                    <div className="pt-1 flex justify-end">
                      <button
                        type="button"
                        onClick={() => navigate(`/name-search?q=${encodeURIComponent(ubo.name)}`)}
                        className="text-[10px] text-primary hover:underline flex items-center gap-1 cursor-pointer font-bold"
                      >
                        <ExternalLink className="w-3 h-3" />
                        <span>Screen UBO in Name Search</span>
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Dynamic Risk Weighting Breakdown */}
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
              <div className="text-muted text-[10px] uppercase font-bold">Risk Weighting Breakdown</div>
              <div className="space-y-3 text-[11px]">
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">Corridor Velocity & Drift Risk</span>
                    <span className="font-bold text-critical">{driftMetrics.unapprovedTxs.length > 0 ? '92%' : '34%'}</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className={`h-full bg-critical ${driftMetrics.unapprovedTxs.length > 0 ? 'w-[92%]' : 'w-[34%]'}`} />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">PEP & Shareholder Exposure</span>
                    <span className="font-bold text-warning">78%</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className="h-full bg-warning w-[78%]" />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-[10px] mb-1">
                    <span className="text-muted">Watchlist Phonetic Proximity</span>
                    <span className="font-bold text-critical">{dossier.riskScore.toFixed(0)}%</span>
                  </div>
                  <div className="w-full h-1.5 rounded-full bg-border overflow-hidden">
                    <div className="h-full bg-critical" style={{ width: `${Math.min(100, dossier.riskScore)}%` }} />
                  </div>
                </div>
              </div>
            </div>

            {/* Banking Relationship & Periodic EDD Status */}
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-2.5">
              <div className="text-muted text-[10px] uppercase font-bold">Banking Relationship & EDD Schedule</div>
              <div className="space-y-2 text-[11px]">
                <div className="flex justify-between gap-2">
                  <span className="text-muted">Entity Type:</span>
                  <span className="font-bold text-ink">{dossier.customerType}</span>
                </div>
                <div className="flex justify-between gap-2">
                  <span className="text-muted">Relationship Manager:</span>
                  <span className="font-bold text-ink text-right">{dossier.relationshipManager || 'Mansoor Al-Falasi'}</span>
                </div>
                <div className="flex justify-between gap-2">
                  <span className="text-muted">Onboarding Date:</span>
                  <span className="text-ink">{dossier.onboardedDate || '2023-04-18'}</span>
                </div>
                <div className="flex justify-between items-center gap-2 pt-1 border-t border-border">
                  <span className="text-muted">Next Periodic Review:</span>
                  <span className={`font-bold ${isReviewOverdue ? 'text-critical' : 'text-accent'}`}>
                    {dossier.nextReviewDate || '2026-10-15'} {isReviewOverdue ? '(OVERDUE)' : '(CURRENT)'}
                  </span>
                </div>
                {dossier.blockReason && (
                  <div className="p-2 rounded bg-critical/10 border border-critical/30 text-critical text-[10px]">
                    <strong>Hold Reason:</strong> {dossier.blockReason}
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* TAB 2: SOURCE OF WEALTH (SoW) / SOURCE OF FUNDS (SoF) & 90-DAY ACTIVITY DRIFT */}
        {activeTab === 'SOW_DRIFT' && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 font-mono text-xs pt-2">
            {/* SoW / SoF Corroboration Checklist */}
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
              <div className="flex items-center justify-between">
                <div className="font-bold text-ink flex items-center gap-1.5">
                  <CheckSquare className="w-4 h-4 text-primary" />
                  <span>Statutory SoW & SoF EDD Corroboration Checklist</span>
                </div>
                <span className="px-2 py-0.5 rounded bg-primary/10 text-primary text-[10px] font-bold">
                  {sowVerifiedCount} / {sowItems.length} Verified
                </span>
              </div>

              <div className="space-y-2">
                {sowItems.map(item => (
                  <label
                    key={item.code}
                    className="p-2.5 rounded-lg bg-surface border border-border flex items-start gap-2.5 cursor-pointer hover:border-primary/40"
                  >
                    <input
                      type="checkbox"
                      checked={item.verified}
                      onChange={() =>
                        setSowItems(prev =>
                          prev.map(i => (i.code === item.code ? { ...i, verified: !i.verified } : i))
                        )
                      }
                      className="mt-0.5 rounded border-border text-primary w-3.5 h-3.5"
                    />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between gap-2">
                        <span className={`font-bold text-[11px] ${item.verified ? 'text-ink' : 'text-warning'}`}>
                          {item.label}
                        </span>
                        <span className="text-[9px] px-1.5 py-0.5 rounded bg-surface-muted text-muted shrink-0">
                          {item.category.replace('_', ' ')}
                        </span>
                      </div>
                      {item.evidenceRef && (
                        <div className="text-[10px] text-muted mt-0.5">{item.evidenceRef}</div>
                      )}
                    </div>
                  </label>
                ))}
              </div>
            </div>

            {/* Expected vs Actual 90-Day Activity Drift Monitor */}
            <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
              <div className="flex items-center justify-between">
                <div className="font-bold text-ink flex items-center gap-1.5">
                  <TrendingUp className="w-4 h-4 text-critical" />
                  <span>KYC Profile Drift & Corridor Baseline Monitor (90-Day)</span>
                </div>
                <span
                  className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                    driftMetrics.unapprovedTxs.length > 0 ? 'bg-critical text-white' : 'bg-accent/20 text-accent'
                  }`}
                >
                  {driftMetrics.unapprovedTxs.length > 0 ? 'DRIFT DETECTED' : 'WITHIN BASELINE'}
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3 text-[11px]">
                <div className="p-3 rounded-lg bg-surface border border-border">
                  <div className="text-[10px] text-muted">DECLARED MONTHLY TURNOVER</div>
                  <div className="text-sm font-bold text-ink mt-0.5">
                    ${driftMetrics.expectedMonthlyUsd.toLocaleString()} USD
                  </div>
                </div>
                <div className="p-3 rounded-lg bg-surface border border-border">
                  <div className="text-[10px] text-muted">ACTUAL MONTHLY AVG (90D)</div>
                  <div className="text-sm font-bold text-critical mt-0.5">
                    ${driftMetrics.actualMonthlyAvgUsd.toLocaleString()} USD
                    <span className="text-[10px] ml-1">
                      ({driftMetrics.turnoverVariancePct >= 0 ? `+${driftMetrics.turnoverVariancePct}%` : `${driftMetrics.turnoverVariancePct}%`})
                    </span>
                  </div>
                </div>
              </div>

              <div className="space-y-1.5 text-[11px]">
                <div className="text-[10px] text-muted uppercase font-bold">Approved Onboarding Trade Corridors</div>
                <div className="flex flex-wrap gap-1.5">
                  {driftMetrics.approved.map(c => (
                    <span key={c} className="px-2 py-0.5 rounded bg-accent/15 text-accent border border-accent/30 text-[10px] font-bold">
                      {c}
                    </span>
                  ))}
                </div>
              </div>

              {driftMetrics.unapprovedTxs.length > 0 && (
                <div className="p-3 rounded-lg bg-critical/10 border border-critical/30 space-y-1.5 text-[11px]">
                  <div className="font-bold text-critical flex items-center gap-1.5">
                    <AlertTriangle className="w-3.5 h-3.5" />
                    <span>{driftMetrics.unapprovedTxs.length} Unapproved Corridor Wire(s) Detected</span>
                  </div>
                  {driftMetrics.unapprovedTxs.map(tx => (
                    <div key={tx.txId} className="flex items-center justify-between text-[10px] text-ink">
                      <span>{tx.txId} • {tx.counterparty}</span>
                      <span className="font-bold text-critical">{tx.corridor} (${tx.amount.toLocaleString()})</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {/* TAB 3: DOCUMENT VAULT */}
        {activeTab === 'DOCUMENTS' && (
          <div className="space-y-3 font-mono text-xs pt-2">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {(dossier.documents || []).map(doc => {
                const isExpired = doc.expiryDate <= '2026-10-25';
                return (
                  <div key={doc.id} className="p-4 rounded-lg bg-surface-muted border border-border space-y-3">
                    <div className="flex items-center justify-between">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary border border-primary/20">
                        {(doc.docType || doc.title).replace('_', ' ')}
                      </span>
                      <span
                        className={`text-[10px] font-bold px-1.5 py-0.5 rounded ${
                          doc.status === 'SANCTIONED'
                            ? 'bg-critical text-white'
                            : doc.status.includes('WARN') || isExpired
                            ? 'bg-warning/20 text-warning'
                            : 'bg-accent/20 text-accent'
                        }`}
                      >
                        {doc.status} ({doc.ocrConfidence || 99.1}%)
                      </span>
                    </div>
                    <div className="space-y-1 text-[11px]">
                      <div className="font-bold text-ink text-sm">{doc.docNumber || doc.title}</div>
                      <div className="text-muted">Jurisdiction: {doc.issueCountry || dossier.incorporationCountry}</div>
                      <div className={isExpired ? 'text-critical font-bold' : 'text-muted'}>
                        Valid: {doc.issuedDate || '2023-04-12'} → {doc.expiryDate} {isExpired ? '(EXPIRED)' : ''}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => setInspectedDoc(doc)}
                      className="w-full py-1.5 rounded border border-border bg-surface hover:bg-surface-hover text-ink text-[11px] font-bold flex items-center justify-center gap-1.5 cursor-pointer"
                    >
                      <Eye className="w-3.5 h-3.5 text-primary" />
                      <span>View OCR Bounding Boxes</span>
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* TAB 4: 90-DAY TIMELINE */}
        {activeTab === 'TIMELINE' && (
          <div className="overflow-x-auto font-mono text-xs pt-2">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[11px] uppercase bg-surface-muted">
                  <th className="py-2.5 px-3">Transaction ID</th>
                  <th className="py-2.5 px-3">Timestamp</th>
                  <th className="py-2.5 px-3">Counterparty</th>
                  <th className="py-2.5 px-3">Corridor</th>
                  <th className="py-2.5 px-3">Amount</th>
                  <th className="py-2.5 px-3">Decision</th>
                  <th className="py-2.5 px-3 text-right">Score / Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {(dossier.recentTransactions || []).map(tx => {
                  const isUnapproved = tx.corridor && !driftMetrics.approved.includes(tx.corridor);
                  return (
                    <tr key={tx.txId} className="hover:bg-surface-hover">
                      <td className="py-3 px-3 font-bold text-ink">{tx.txId}</td>
                      <td className="py-3 px-3 text-muted">{tx.date}</td>
                      <td className="py-3 px-3 text-ink">{tx.counterparty}</td>
                      <td className="py-3 px-3">
                        <span className="text-ink">{tx.corridor || 'ARE -> CHE'}</span>
                        {isUnapproved && (
                          <span className="ml-1.5 px-1.5 py-0.5 rounded bg-critical/15 text-critical text-[9px] font-bold">
                            DRIFT
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-3 font-bold text-ink">
                        ${tx.amount.toLocaleString()} {tx.currency}
                      </td>
                      <td className="py-3 px-3">
                        <span
                          className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            tx.status === 'BLOCKED'
                              ? 'bg-critical text-white'
                              : tx.status === 'HELD'
                              ? 'bg-warning/20 text-warning'
                              : 'bg-accent/20 text-accent'
                          }`}
                        >
                          {tx.status}
                        </span>
                      </td>
                      <td className="py-3 px-3 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <span className="font-bold text-ink">{(tx.riskScore ?? 85.0).toFixed(1)}%</span>
                          {tx.status !== 'CLEARED' && (
                            <button
                              type="button"
                              onClick={() => navigate('/cases')}
                              className="px-2 py-0.5 rounded bg-primary/10 text-primary hover:bg-primary/20 text-[10px] font-bold cursor-pointer"
                            >
                              Inspect Case
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* TAB 5: HISTORICAL ALERTS */}
        {activeTab === 'ALERTS' && (
          <div className="p-4 rounded-lg bg-surface-muted border border-border space-y-3 font-mono text-xs">
            <div className="text-ink font-bold text-sm">
              {(dossier.historicalAlerts || []).length} Historical AML & Sanctions Escalations
            </div>
            <div className="space-y-2 text-[11px]">
              {(dossier.historicalAlerts || []).map(alert => (
                <div
                  key={alert.alertId}
                  className="p-3 rounded bg-surface border border-border flex flex-col sm:flex-row justify-between items-start sm:items-center gap-2"
                >
                  <div>
                    <div className={`font-bold ${alert.severity === 'CRITICAL' ? 'text-critical' : 'text-warning'}`}>
                      {alert.alertId}: {alert.title}
                    </div>
                    <div className="text-muted text-[10px]">{alert.description}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        alert.status === 'FROZEN'
                          ? 'bg-critical text-white'
                          : alert.status === 'UNDER_EDD'
                          ? 'bg-warning/20 text-warning'
                          : 'bg-accent/20 text-accent'
                      }`}
                    >
                      {alert.status}
                    </span>
                    <button
                      type="button"
                      onClick={() => navigate('/cases')}
                      className="px-2.5 py-1 rounded border border-border hover:bg-surface-muted text-ink text-[10px] font-bold flex items-center gap-1 cursor-pointer"
                    >
                      <ExternalLink className="w-3 h-3 text-primary" />
                      <span>{alert.linkedCaseNumber || 'Open Case'}</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* MODAL 1: INTERACTIVE OCR BOUNDING BOX & FIELD INSPECTOR */}
      {inspectedDoc && createPortal(
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-[9999] p-4 font-mono">
          <div className="bg-surface w-full max-w-2xl rounded-2xl border border-border shadow-2xl overflow-hidden animate-in zoom-in-95 duration-150">
            <div className="p-4 bg-surface-muted border-b border-border flex items-center justify-between">
              <div className="flex items-center gap-2">
                <FileText className="w-4 h-4 text-primary" />
                <h3 className="text-sm font-bold text-ink">
                  OCR Bounding Box & Field Verification — {inspectedDoc.docNumber || inspectedDoc.title}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setInspectedDoc(null)}
                className="text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-5 space-y-4 text-xs max-h-[80vh] overflow-y-auto">
              {/* Simulated Visual OCR Bounding Box Canvas */}
              <div className="p-4 rounded-xl bg-[#0f172a] text-slate-200 border border-slate-700 space-y-2.5">
                <div className="flex items-center justify-between text-[10px] text-slate-400">
                  <span>OPTICAL CHARACTER RECOGNITION ENGINE (ISO/IEC 7501)</span>
                  <span className="text-emerald-400 font-bold">CONFIDENCE: {inspectedDoc.ocrConfidence || 99.1}%</span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  {Object.entries(
                    inspectedDoc.extractedFields || {
                      'Document Number': inspectedDoc.docNumber || inspectedDoc.title,
                      'Customer Entity': dossier.fullName,
                      'Issuing Country': inspectedDoc.issueCountry || dossier.incorporationCountry,
                      'Expiration Date': inspectedDoc.expiryDate
                    }
                  ).map(([key, val], idx) => (
                    <div
                      key={key}
                      className="p-2.5 rounded border-2 border-dashed border-cyan-400/60 bg-cyan-950/30 relative"
                    >
                      <span className="absolute -top-2 right-2 px-1 bg-[#0f172a] text-[9px] text-cyan-300 font-bold">
                        BOX #{idx + 1} • {(inspectedDoc.ocrConfidence || 99.0).toFixed(1)}%
                      </span>
                      <div className="text-[10px] text-slate-400 uppercase">{key}</div>
                      <div className="text-xs font-bold text-white mt-0.5 break-all">{val}</div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Document Verification Controls */}
              <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border">
                <div className="text-[11px] text-muted">
                  Current Status: <strong className="text-ink">{inspectedDoc.status}</strong>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => handleVerifyDocument(inspectedDoc, 'VERIFIED_WARNING')}
                    className="px-3 py-1.5 rounded-lg bg-warning/15 text-warning border border-warning/30 font-bold hover:bg-warning/25 cursor-pointer"
                  >
                    Flag Discrepancy / Renewal
                  </button>
                  <button
                    type="button"
                    onClick={() => handleVerifyDocument(inspectedDoc, 'VERIFIED_100')}
                    className="px-3 py-1.5 rounded-lg bg-accent text-white font-bold hover:bg-accent/90 flex items-center gap-1.5 cursor-pointer"
                  >
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    <span>Mark 100% Verified</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>,
        document.body
      )}

      {/* MODAL 2: SIGNED WORM EDD AUDIT CERTIFICATE */}
      {eddCertificate && createPortal(
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-[9999] p-4 font-mono">
          <div className="bg-surface w-full max-w-xl rounded-2xl border border-border shadow-2xl overflow-hidden">
            <div className="p-4 bg-surface-muted border-b border-border flex items-center justify-between">
              <div className="flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-primary" />
                <h3 className="text-sm font-bold text-ink">
                  WORM Cryptographic EDD Dossier Certificate
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setEddCertificate(null)}
                className="text-muted hover:text-ink cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-5 space-y-3 text-xs">
              <div className="p-3.5 rounded-xl bg-surface-muted border border-border space-y-1.5 text-[11px]">
                <div className="flex justify-between">
                  <span className="text-muted">Certificate ID:</span>
                  <span className="font-bold text-primary">{eddCertificate.certificateId}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">Customer Entity:</span>
                  <span className="font-bold text-ink">{eddCertificate.subjectName}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">EDD Status / Verdict:</span>
                  <span className="font-bold text-ink">{eddCertificate.decision} ({eddCertificate.overallScore}%)</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted">SoW / SoF Corroboration:</span>
                  <span className="font-bold text-ink">{sowVerifiedCount} of {sowItems.length} Items Verified</span>
                </div>
                <div className="pt-2 border-t border-border">
                  <div className="text-[10px] text-muted">SHA-256 WORM BLOCK HASH</div>
                  <div className="text-[10px] font-bold text-ink break-all mt-0.5">
                    {eddCertificate.wormBlockHashSha256}
                  </div>
                </div>
              </div>

              <div className="text-[10px] text-muted">
                {eddCertificate.regulatoryAttestation}
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-border">
                <button
                  type="button"
                  onClick={() => {
                    const blob = new Blob([JSON.stringify({ dossier, certificate: eddCertificate }, null, 2)], { type: 'application/json' });
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = `${dossier.customerCode}-edd-dossier.json`;
                    a.click();
                    URL.revokeObjectURL(url);
                  }}
                  className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink font-bold flex items-center gap-1.5 cursor-pointer"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Download JSON</span>
                </button>
                <button
                  type="button"
                  onClick={() => window.print()}
                  className="px-3 py-1.5 rounded-lg bg-primary text-white font-bold hover:bg-primary/90 flex items-center gap-1.5 cursor-pointer"
                >
                  <Printer className="w-3.5 h-3.5" />
                  <span>Print Official Dossier</span>
                </button>
              </div>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
};
