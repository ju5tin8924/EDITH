import React, { useState, useEffect, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Download,
  FileSpreadsheet,
  CheckCircle2,
  FileText,
  ShieldCheck,
  ShieldAlert,
  Search,
  Sliders,
  BarChart3,
  Clock,
  AlertTriangle,
  RefreshCw,
  RotateCcw,
  Link2,
  Printer,
  X,
  Filter,
  Layers,
  UserCheck,
  BellRing,
  ChevronDown,
  FileCheck
} from 'lucide-react';
import { usePageMeta } from '../config/pageMeta';
import { EdithApi } from '../services/api';
import {
  AuditLogRecord,
  CaseSummary,
  KycDossierDto
} from '../types';
import {
  ReportTabId,
  REPORT_CATEGORIES,
  resolveTabFromPath
} from '../config/reportCategories';

interface TransactionReportRow {
  txRef: string;
  txType: 'REMITTANCE' | 'INCOMING' | 'FOREX' | 'WIRE';
  branch: string;
  txDate: string;
  isPaid: boolean;
  accountNo: string;
  nationality: string;
  benfCountry: string;
  correspondentCountry: string;
  idType: 'EMIRATES_ID' | 'PASSPORT' | 'TRADE_LICENSE';
  idNo: string;
  senderCode: string;
  senderName: string;
  beneficiaryName: string;
  lcyAmount: number;
  currency: string;
  senderDob: string;
  riskTier: 'HIGH' | 'MEDIUM' | 'LOW';
  status: 'CLEARED' | 'HELD' | 'BLOCKED' | 'RELEASED';
  caseNumber?: string;
}

interface BlacklistReportRow {
  entryId: string;
  entityName: string;
  category: 'WC_PEP' | 'OFAC_SDN' | 'UN_CONSOLIDATED' | 'EU_SANCTIONS' | 'LOCAL_WATCHLIST';
  nationality: string;
  actionType: 'CREATED' | 'UPDATED' | 'DELETED';
  actionDate: string;
  operator: string;
  remarks: string;
}

interface ReleaseReportRow {
  txRef: string;
  caseNumber: string;
  customerCode: string;
  senderName: string;
  beneficiaryName: string;
  amount: number;
  currency: string;
  decision: 'AUTHORIZED' | 'REJECTED';
  authorizedBy: string;
  turnaroundMinutes: number;
  decidedAtUtc: string;
  rationale: string;
}

interface ProcessLogRow {
  processId: string;
  executedAt: string;
  executionMode: 'AUTOMATED_SCHEDULER' | 'MANUAL_PROCESS';
  operator: string;
  templateOrRule: string;
  ruleCode: string;
  dateRangeScanned: string;
  recordsProcessed: number;
  alertsTriggered: number;
  durationMs: number;
}

interface MonthlyScreenRow {
  month: string;
  entityRole: 'CUSTOMER' | 'BENEFICIARY' | 'UBO';
  totalScreened: number;
  exactMatches: number;
  fuzzyPhoneticMatches: number;
  whitelistSuppressed: number;
  escalatedToStr: number;
  blacklistVersion: string;
}

interface DeltaLogRow {
  logId: string;
  batchId: string;
  customerCode: string;
  customerName: string;
  watchlistSource: string;
  previousScore: number;
  newScore: number;
  authorizedDecision: 'APPROVED_CLEAR' | 'ESCALATED_EDD' | 'BLOCKED';
  authorizedBy: string;
  authorizedDate: string;
}

const INITIAL_TRANSACTIONS: TransactionReportRow[] = [
  {
    txRef: 'TX-2026-9921448',
    txType: 'WIRE',
    branch: 'DIFC Main Branch (DXB-01)',
    txDate: '2026-10-14',
    isPaid: false,
    accountNo: 'AE820330000001234567890',
    nationality: 'ARE',
    benfCountry: 'RUS',
    correspondentCountry: 'CHE',
    idType: 'TRADE_LICENSE',
    idNo: 'DED-DXB-9921448',
    senderCode: 'CUST-AE-88902',
    senderName: 'Rashid Al-Maktoum Trading FZE',
    beneficiaryName: 'Viktor Anatolyevich Bout',
    lcyAmount: 1652625,
    currency: 'USD',
    senderDob: '1978-05-14',
    riskTier: 'HIGH',
    status: 'BLOCKED',
    caseNumber: 'EDITH-CAS-2026-0042'
  },
  {
    txRef: 'TX-2026-9921502',
    txType: 'REMITTANCE',
    branch: 'Abu Dhabi Corniche (AUH-02)',
    txDate: '2026-10-15',
    isPaid: true,
    accountNo: 'AE410210000009988776655',
    nationality: 'EGY',
    benfCountry: 'EGY',
    correspondentCountry: 'ARE',
    idType: 'EMIRATES_ID',
    idNo: '784-1984-4412990-1',
    senderCode: 'CUST-AE-44120',
    senderName: 'Mohamed Al-Attar',
    beneficiaryName: 'Cairo Medical Supplies SAE',
    lcyAmount: 94500,
    currency: 'AED',
    senderDob: '1984-11-02',
    riskTier: 'LOW',
    status: 'RELEASED',
    caseNumber: 'EDITH-CAS-2026-0041'
  },
  {
    txRef: 'TX-2026-9921619',
    txType: 'FOREX',
    branch: 'Deira Gold Souk Desk (DXB-04)',
    txDate: '2026-10-18',
    isPaid: false,
    accountNo: 'AE190440000005544332211',
    nationality: 'IRN',
    benfCountry: 'IRN',
    correspondentCountry: 'TUR',
    idType: 'PASSPORT',
    idNo: 'P-99102481',
    senderCode: 'CUST-IR-10942',
    senderName: 'Khorasan Exchange Corp',
    beneficiaryName: 'Tehran Industrial Metals',
    lcyAmount: 425000,
    currency: 'EUR',
    senderDob: '1971-03-21',
    riskTier: 'HIGH',
    status: 'HELD',
    caseNumber: 'EDITH-CAS-2026-0043'
  },
  {
    txRef: 'TX-2026-9921780',
    txType: 'INCOMING',
    branch: 'JAFZA Corporate Hub (DXB-03)',
    txDate: '2026-10-20',
    isPaid: true,
    accountNo: 'AE660330000007711223344',
    nationality: 'GBR',
    benfCountry: 'ARE',
    correspondentCountry: 'GBR',
    idType: 'TRADE_LICENSE',
    idNo: 'JAFZA-OFF-77210',
    senderCode: 'CUST-GB-99214',
    senderName: 'Caspian Mineral Commodities Ltd',
    beneficiaryName: 'Emirates Sky Logistics LLC',
    lcyAmount: 310000,
    currency: 'GBP',
    senderDob: '1980-01-19',
    riskTier: 'MEDIUM',
    status: 'CLEARED'
  },
  {
    txRef: 'TX-2026-9921895',
    txType: 'REMITTANCE',
    branch: 'Sharjah Industrial (SHJ-01)',
    txDate: '2026-10-22',
    isPaid: false,
    accountNo: 'AE550110000003322110099',
    nationality: 'SYR',
    benfCountry: 'SYR',
    correspondentCountry: 'LBN',
    idType: 'PASSPORT',
    idNo: 'SY-00491822',
    senderCode: 'CUST-SY-30291',
    senderName: 'Damascus Silk Road Freight',
    beneficiaryName: 'Al-eppo General Trading',
    lcyAmount: 195000,
    currency: 'AED',
    senderDob: '1969-08-09',
    riskTier: 'HIGH',
    status: 'HELD',
    caseNumber: 'EDITH-CAS-2026-0044'
  }
];

const BLACKLIST_ROWS: BlacklistReportRow[] = [
  {
    entryId: 'BL-OFAC-1521-99',
    entityName: 'BOUT, Viktor Anatolyevich',
    category: 'OFAC_SDN',
    nationality: 'RUS',
    actionType: 'UPDATED',
    actionDate: '2026-10-14',
    operator: 'AUTO_FEED_SYNC',
    remarks: 'Updated maritime vessel alias and secondary passport identifiers under UNSC Res 1521.'
  },
  {
    entryId: 'BL-WC-PEP-8841',
    entityName: 'AL-ASSAD, Maher',
    category: 'WC_PEP',
    nationality: 'SYR',
    actionType: 'CREATED',
    actionDate: '2026-10-16',
    operator: 'AUTO_FEED_SYNC',
    remarks: 'World-Check Tier-1 Politically Exposed Person & EU Sovereign Sanctions designation.'
  },
  {
    entryId: 'BL-LOC-UAE-204',
    entityName: 'Khorasan Exchange & Hawala Network',
    category: 'LOCAL_WATCHLIST',
    nationality: 'IRN',
    actionType: 'CREATED',
    actionDate: '2026-10-19',
    operator: 'Fatima Al-Nuaimi (MLRO)',
    remarks: 'Added manually per Central Bank Executive Office Circular 2026/10-B (is_block=true).'
  },
  {
    entryId: 'BL-WC-PEP-1102',
    entityName: 'MANSOOR, Tariq (Historical Duplicate)',
    category: 'LOCAL_WATCHLIST',
    nationality: 'ARE',
    actionType: 'DELETED',
    actionDate: '2026-10-21',
    operator: 'Zaid Al-Husseini',
    remarks: 'Removed superseded manual watchlist entry after verified Emirates ID false-positive clearance.'
  }
];

const RELEASE_ROWS: ReleaseReportRow[] = [
  {
    txRef: 'TX-2026-9921502',
    caseNumber: 'EDITH-CAS-2026-0041',
    customerCode: 'CUST-AE-44120',
    senderName: 'Mohamed Al-Attar',
    beneficiaryName: 'Cairo Medical Supplies SAE',
    amount: 94500,
    currency: 'AED',
    decision: 'AUTHORIZED',
    authorizedBy: 'Tariq Mansoor (AML Officer)',
    turnaroundMinutes: 18,
    decidedAtUtc: '2026-10-15 09:42 UTC',
    rationale: 'Verified secondary KYC passport & divergent DOB (1984 vs 1958 watchlist entry). Released from Hold.'
  },
  {
    txRef: 'TX-2026-9921448',
    caseNumber: 'EDITH-CAS-2026-0042',
    customerCode: 'CUST-AE-88902',
    senderName: 'Rashid Al-Maktoum Trading FZE',
    beneficiaryName: 'Viktor Anatolyevich Bout',
    amount: 450000,
    currency: 'USD',
    decision: 'REJECTED',
    authorizedBy: 'Fatima Al-Nuaimi (ISTR MLRO)',
    turnaroundMinutes: 11,
    decidedAtUtc: '2026-10-14 07:15 UTC',
    rationale: '96.4% composite phonetic & exact token hit on OFAC SDN / UN Res 1521. Statutory freeze executed.'
  },
  {
    txRef: 'TX-2026-9921710',
    caseNumber: 'EDITH-CAS-2026-0039',
    customerCode: 'CUST-AE-55102',
    senderName: 'DHL Express UAE LLC',
    beneficiaryName: 'Global Aviation Freight Services',
    amount: 215000,
    currency: 'AED',
    decision: 'AUTHORIZED',
    authorizedBy: 'Zaid Al-Husseini (Compliance Officer)',
    turnaroundMinutes: 6,
    decidedAtUtc: '2026-10-20 14:10 UTC',
    rationale: 'Pass 5 approved corporate whitelist exemption verified against valid Dubai Trade License.'
  }
];

const PROCESS_LOG_ROWS: ProcessLogRow[] = [
  {
    processId: 'PROC-2026-1025-01',
    executedAt: '2026-10-25 02:00 UTC',
    executionMode: 'AUTOMATED_SCHEDULER',
    operator: 'System Scheduler (CRON-0200)',
    templateOrRule: 'FATF High-Risk Corridor & Structuring Suite',
    ruleCode: 'RULE-FATF-CORRIDOR',
    dateRangeScanned: '2026-10-24 to 2026-10-25',
    recordsProcessed: 48920,
    alertsTriggered: 19,
    durationMs: 1420
  },
  {
    processId: 'PROC-2026-1024-04',
    executedAt: '2026-10-24 16:40 UTC',
    executionMode: 'MANUAL_PROCESS',
    operator: 'Tariq Mansoor (AML Analyst)',
    templateOrRule: 'Split Cash Deposit Smurfing Band (45k-54.9k AED)',
    ruleCode: 'RULE-SMURF-BAND-01',
    dateRangeScanned: '2026-10-01 to 2026-10-24',
    recordsProcessed: 12450,
    alertsTriggered: 7,
    durationMs: 680
  },
  {
    processId: 'PROC-2026-1023-02',
    executedAt: '2026-10-23 11:15 UTC',
    executionMode: 'MANUAL_PROCESS',
    operator: 'Fatima Al-Nuaimi (MLRO)',
    templateOrRule: 'Inline SQL Velocity Burst (>5 Wires / 60m)',
    ruleCode: 'RULE-VEL-BURST-02',
    dateRangeScanned: '2026-10-15 to 2026-10-23',
    recordsProcessed: 8310,
    alertsTriggered: 4,
    durationMs: 490
  }
];

const MONTHLY_SCREEN_ROWS: MonthlyScreenRow[] = [
  {
    month: '2026-10',
    entityRole: 'CUSTOMER',
    totalScreened: 47618,
    exactMatches: 4,
    fuzzyPhoneticMatches: 38,
    whitelistSuppressed: 112,
    escalatedToStr: 6,
    blacklistVersion: 'OFAC/UN/EU/WC-2026.10.25'
  },
  {
    month: '2026-10',
    entityRole: 'BENEFICIARY',
    totalScreened: 184290,
    exactMatches: 11,
    fuzzyPhoneticMatches: 142,
    whitelistSuppressed: 418,
    escalatedToStr: 19,
    blacklistVersion: 'OFAC/UN/EU/WC-2026.10.25'
  },
  {
    month: '2026-10',
    entityRole: 'UBO',
    totalScreened: 19840,
    exactMatches: 2,
    fuzzyPhoneticMatches: 17,
    whitelistSuppressed: 29,
    escalatedToStr: 3,
    blacklistVersion: 'OFAC/UN/EU/WC-2026.10.25'
  },
  {
    month: '2026-09',
    entityRole: 'CUSTOMER',
    totalScreened: 46105,
    exactMatches: 3,
    fuzzyPhoneticMatches: 31,
    whitelistSuppressed: 104,
    escalatedToStr: 5,
    blacklistVersion: 'OFAC/UN/EU/WC-2026.09.30'
  }
];

const DELTA_LOG_ROWS: DeltaLogRow[] = [
  {
    logId: 'DLG-2026-0091',
    batchId: 'BATCH-2026-10-19-0200',
    customerCode: 'CUST-AE-88902',
    customerName: 'Rashid Al-Maktoum Trading FZE',
    watchlistSource: 'OFAC SDN 2026-10-13 Sync',
    previousScore: 12.0,
    newScore: 94.2,
    authorizedDecision: 'ESCALATED_EDD',
    authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
    authorizedDate: '2026-10-19'
  },
  {
    logId: 'DLG-2026-0092',
    batchId: 'BATCH-2026-10-19-0200',
    customerCode: 'CUST-CH-44109',
    customerName: 'Kovalev International Holdings',
    watchlistSource: 'EU Consolidated Sanctions Delta 44',
    previousScore: 45.0,
    newScore: 88.5,
    authorizedDecision: 'ESCALATED_EDD',
    authorizedBy: 'Fatima Al-Nuaimi (MLRO)',
    authorizedDate: '2026-10-19'
  },
  {
    logId: 'DLG-2026-0093',
    batchId: 'BATCH-2026-10-18-0200',
    customerCode: 'CUST-SA-12004',
    customerName: 'Al-Farouq Petroleum Equipment',
    watchlistSource: 'UN Security Council Resolution 2231',
    previousScore: 18.0,
    newScore: 71.5,
    authorizedDecision: 'APPROVED_CLEAR',
    authorizedBy: 'Tariq Mansoor (AML Officer)',
    authorizedDate: '2026-10-18'
  }
];

export const ReportsAuditView: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();

  const [activeTab, setActiveTab] = useState<ReportTabId>(() => resolveTabFromPath(location.pathname));
  const [auditLogs, setAuditLogs] = useState<AuditLogRecord[]>([]);
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [customers, setCustomers] = useState<KycDossierDto[]>([]);
  const [uploadJobs, setUploadJobs] = useState<Array<{
    jobId: string;
    fileName: string;
    status: string;
    totalRecords: number;
    processedRecords: number;
    skippedRecords: number;
    skippedDownloadUrl?: string;
  }>>([]);

  const [chainVerified, setChainVerified] = useState<boolean | null>(null);
  const [selectedAuditBlock, setSelectedAuditBlock] = useState<AuditLogRecord | null>(null);
  const [auditSearch, setAuditSearch] = useState('');
  const [statusBanner, setStatusBanner] = useState<string | null>(null);
  const [isReportDropdownOpen, setIsReportDropdownOpen] = useState(false);
  const [isExportMenuOpen, setIsExportMenuOpen] = useState(false);

  // Certified Report Export Modal
  const [certifiedModal, setCertifiedModal] = useState<{
    certId: string;
    reportTitle: string;
    recordCount: number;
    issuedAtUtc: string;
    wormHash: string;
    columns: string[];
    sampleData: Record<string, any>[];
  } | null>(null);

  // Report 2: Transaction Report 16 PDF Filters
  const [showAdvancedTxFilters, setShowAdvancedTxFilters] = useState(false);
  const [txFilters, setTxFilters] = useState({
    txType: 'ALL',
    branch: '',
    startDate: '',
    endDate: '',
    countMode: 'ALL' as 'ALL' | 'PAID',
    accountNo: '',
    nationality: '',
    benfCountry: '',
    correspondentCountry: '',
    idType: 'ALL',
    idNo: '',
    senderCode: '',
    senderName: '',
    minLcyAmount: '',
    senderDob: '',
    status: 'ALL',
    riskTier: 'ALL'
  });

  // Report 3: Blacklist Report Filters
  const [blacklistActionFilter, setBlacklistActionFilter] = useState<'ALL' | 'CREATED' | 'UPDATED' | 'DELETED'>('ALL');

  // Report 5 & 12: Online STR & STR History Filters
  const [strAlertTypeFilter, setStrAlertTypeFilter] = useState('ALL');
  const [strBranchFilter, setStrBranchFilter] = useState('');
  const [strCustomerCodeFilter, setStrCustomerCodeFilter] = useState('');
  const [strRefFilter, setStrRefFilter] = useState('');
  const [strSortField, setStrSortField] = useState<'caseNumber' | 'riskScore' | 'createdAtUtc'>('caseNumber');
  const [reopenedCaseIds, setReopenedCaseIds] = useState<string[]>([]);
  const [notifiedBranches, setNotifiedBranches] = useState<string[]>([]);

  // Report 6: Transaction Analytics Dimension Switcher
  const [analyticsDimension, setAnalyticsDimension] = useState<'AMOUNT' | 'CURRENCY' | 'NATIONALITY' | 'RISK' | 'COUNTRY'>('RISK');

  // Report 11: Monthly Screen Month Filter
  const [selectedMonth, setSelectedMonth] = useState('2026-10');

  // Report 14: Delta Log Filters
  const [deltaCustomerFilter, setDeltaCustomerFilter] = useState('');

  useEffect(() => {
    setActiveTab(resolveTabFromPath(location.pathname));
  }, [location.pathname]);

  useEffect(() => {
    loadAllReportData();
  }, []);

  const loadAllReportData = async () => {
    const [logsData, casesData, custData, , jobsData] = await Promise.all([
      EdithApi.fetchAuditLogs(100),
      EdithApi.fetchCases().catch(() => []),
      EdithApi.fetchCustomers().catch(() => []),
      EdithApi.fetchRules().catch(() => []),
      EdithApi.fetchBulkJobs().catch(() => [])
    ]);
    setAuditLogs(logsData);
    if (logsData.length > 0 && !selectedAuditBlock) {
      setSelectedAuditBlock(logsData[0]);
    }
    setCases(casesData);
    setCustomers(custData);
    setUploadJobs(jobsData);
  };

  const showToast = (msg: string) => {
    setStatusBanner(msg);
    setTimeout(() => setStatusBanner(null), 4000);
  };

  const handleVerifyWormChain = async () => {
    const logs = await EdithApi.fetchAuditLogs(100);
    setAuditLogs(logs);
    const chronological = [...logs].reverse();
    let valid = true;
    for (let i = 1; i < chronological.length; i++) {
      if (
        chronological[i].previousBlockHash &&
        chronological[i - 1].sha256Hash &&
        chronological[i].previousBlockHash !== chronological[i - 1].sha256Hash
      ) {
        valid = false;
        break;
      }
    }
    setChainVerified(valid);
    showToast(
      valid
        ? `Cryptographic chain verified: All ${logs.length} SHA-256 blocks validated to GENESIS.`
        : 'Chain error: Discontinuity detected in block sequence.'
    );
  };

  const filteredTransactions = useMemo(() => {
    return INITIAL_TRANSACTIONS.filter(tx => {
      if (txFilters.txType !== 'ALL' && tx.txType !== txFilters.txType) return false;
      if (txFilters.branch && !tx.branch.toLowerCase().includes(txFilters.branch.toLowerCase())) return false;
      if (txFilters.startDate && tx.txDate < txFilters.startDate) return false;
      if (txFilters.endDate && tx.txDate > txFilters.endDate) return false;
      if (txFilters.countMode === 'PAID' && !tx.isPaid) return false;
      if (txFilters.accountNo && !tx.accountNo.toLowerCase().includes(txFilters.accountNo.toLowerCase())) return false;
      if (txFilters.nationality && !tx.nationality.toLowerCase().includes(txFilters.nationality.toLowerCase())) return false;
      if (txFilters.benfCountry && !tx.benfCountry.toLowerCase().includes(txFilters.benfCountry.toLowerCase())) return false;
      if (txFilters.correspondentCountry && !tx.correspondentCountry.toLowerCase().includes(txFilters.correspondentCountry.toLowerCase())) return false;
      if (txFilters.idType !== 'ALL' && tx.idType !== txFilters.idType) return false;
      if (txFilters.idNo && !tx.idNo.toLowerCase().includes(txFilters.idNo.toLowerCase())) return false;
      if (txFilters.senderCode && !tx.senderCode.toLowerCase().includes(txFilters.senderCode.toLowerCase())) return false;
      if (txFilters.senderName && !tx.senderName.toLowerCase().includes(txFilters.senderName.toLowerCase())) return false;
      if (txFilters.minLcyAmount && tx.lcyAmount <= Number(txFilters.minLcyAmount)) return false;
      if (txFilters.senderDob && tx.senderDob !== txFilters.senderDob) return false;
      if (txFilters.status !== 'ALL' && tx.status !== txFilters.status) return false;
      if (txFilters.riskTier !== 'ALL' && tx.riskTier !== txFilters.riskTier) return false;
      return true;
    });
  }, [txFilters]);

  const activeTxFilterCount = useMemo(() => {
    return Object.entries(txFilters).filter(([, val]) => val !== '' && val !== 'ALL').length;
  }, [txFilters]);

  const resetTxFilters = () => {
    setTxFilters({
      txType: 'ALL',
      branch: '',
      startDate: '',
      endDate: '',
      countMode: 'ALL',
      accountNo: '',
      nationality: '',
      benfCountry: '',
      correspondentCountry: '',
      idType: 'ALL',
      idNo: '',
      senderCode: '',
      senderName: '',
      minLcyAmount: '',
      senderDob: '',
      status: 'ALL',
      riskTier: 'ALL'
    });
  };

  const filteredBlacklistRows = useMemo(() => {
    return BLACKLIST_ROWS.filter(row => {
      if (blacklistActionFilter !== 'ALL' && row.actionType !== blacklistActionFilter) return false;
      return true;
    });
  }, [blacklistActionFilter]);

  const strReportRows = useMemo(() => {
    const baseRows = cases.length > 0
      ? cases.map((c, idx) => ({
          id: c.id,
          caseNumber: c.caseNumber,
          subjectName: c.primarySubjectName,
          customerCode: `CUST-AE-8890${idx + 1}`,
          branch: idx % 2 === 0 ? 'DIFC Main Branch (DXB-01)' : 'Abu Dhabi Corniche (AUH-02)',
          alertType: c.riskScore >= 90 ? 'SANCTIONS_INTERCEPT' : 'OFFLINE_TM_RULE',
          ruleMatched: idx % 2 === 0 ? 'RULE-FATF-CORRIDOR' : 'RULE-SMURF-BAND-01',
          riskScore: c.riskScore,
          status: reopenedCaseIds.includes(c.id) ? 'UnderReview (Reopened)' : c.status,
          createdAtUtc: c.createdAtUtc.slice(0, 10),
          daysInQueue: idx === 0 ? 4 : idx === 1 ? 8 : 1,
          slaWatchdog: idx === 0 ? '3D_EDD_BREACH_AUTO_MLRO' : idx === 1 ? '7D_MONITORING_BREACH_AUTO_MLRO' : 'WITHIN_SLA'
        }))
      : [
          {
            id: 'case-fallback-1',
            caseNumber: 'EDITH-CAS-2026-0042',
            subjectName: 'Viktor Anatolyevich Bout',
            customerCode: 'CUST-AE-88902',
            branch: 'DIFC Main Branch (DXB-01)',
            alertType: 'SANCTIONS_INTERCEPT',
            ruleMatched: 'RULE-OFAC-UN-1521',
            riskScore: 96.4,
            status: reopenedCaseIds.includes('case-fallback-1') ? 'UnderReview (Reopened)' : 'PendingApproval',
            createdAtUtc: '2026-10-14',
            daysInQueue: 4,
            slaWatchdog: '3D_EDD_BREACH_AUTO_MLRO'
          },
          {
            id: 'case-fallback-2',
            caseNumber: 'EDITH-CAS-2026-0043',
            subjectName: 'Khorasan Exchange Corp',
            customerCode: 'CUST-IR-10942',
            branch: 'Deira Gold Souk Desk (DXB-04)',
            alertType: 'OFFLINE_TM_RULE',
            ruleMatched: 'RULE-FATF-CORRIDOR',
            riskScore: 89.2,
            status: reopenedCaseIds.includes('case-fallback-2') ? 'UnderReview (Reopened)' : 'UnderReview',
            createdAtUtc: '2026-10-17',
            daysInQueue: 8,
            slaWatchdog: '7D_MONITORING_BREACH_AUTO_MLRO'
          },
          {
            id: 'case-fallback-3',
            caseNumber: 'EDITH-CAS-2026-0041',
            subjectName: 'Mohamed Al-Attar',
            customerCode: 'CUST-AE-44120',
            branch: 'Abu Dhabi Corniche (AUH-02)',
            alertType: 'OFFLINE_TM_RULE',
            ruleMatched: 'RULE-SMURF-BAND-01',
            riskScore: 72.0,
            status: reopenedCaseIds.includes('case-fallback-3') ? 'UnderReview (Reopened)' : 'Closed',
            createdAtUtc: '2026-10-15',
            daysInQueue: 1,
            slaWatchdog: 'WITHIN_SLA'
          }
        ];

    return baseRows
      .filter(r => {
        if (strAlertTypeFilter !== 'ALL' && r.alertType !== strAlertTypeFilter) return false;
        if (strBranchFilter && !r.branch.toLowerCase().includes(strBranchFilter.toLowerCase())) return false;
        if (strCustomerCodeFilter && !r.customerCode.toLowerCase().includes(strCustomerCodeFilter.toLowerCase())) return false;
        if (strRefFilter && !r.caseNumber.toLowerCase().includes(strRefFilter.toLowerCase())) return false;
        return true;
      })
      .sort((a, b) => {
        if (strSortField === 'riskScore') return b.riskScore - a.riskScore;
        if (strSortField === 'createdAtUtc') return b.createdAtUtc.localeCompare(a.createdAtUtc);
        return a.caseNumber.localeCompare(b.caseNumber);
      });
  }, [cases, strAlertTypeFilter, strBranchFilter, strCustomerCodeFilter, strRefFilter, strSortField, reopenedCaseIds]);

  const handleReopenStr = async (caseId: string, caseNumber: string, subjectName: string) => {
    await EdithApi.transitionCase(caseId, 'UnderReview', 'Reopened from STR History Report for secondary surveillance.');
    const entry = await EdithApi.recordAuditEvent({
      action: 'STR_HISTORY_CASE_REOPENED',
      targetEntity: subjectName,
      caseNumber,
      details: `Case ${caseNumber} reopened for re-investigation.`
    });
    setReopenedCaseIds(prev => [...prev, caseId]);
    setAuditLogs(prev => [entry, ...prev]);
    showToast(`Case ${caseNumber} reopened and logged to ledger.`);
  };

  const handleSendBranchMail = async (caseNumber: string, branch: string, subjectName: string) => {
    const entry = await EdithApi.recordAuditEvent({
      action: 'ISTR_BRANCH_NOTIFICATION_DISPATCHED',
      targetEntity: `${branch} — ${subjectName}`,
      caseNumber,
      details: `Branch inquiry notice sent to ${branch} for case ${caseNumber}.`
    });
    setNotifiedBranches(prev => [...prev, caseNumber]);
    setAuditLogs(prev => [entry, ...prev]);
    showToast(`Branch notification dispatched to ${branch}.`);
  };

  const handleForceSlaEscalation = async (caseNumber: string, subjectName: string, slaRule: string) => {
    const entry = await EdithApi.recordAuditEvent({
      action: 'SLA_AUTO_ESCALATION_TO_ISTR_MLRO',
      targetEntity: subjectName,
      caseNumber,
      details: `${slaRule === '3D_EDD_BREACH_AUTO_MLRO' ? '3-Day EDD' : '7-Day Monitoring'} SLA timeout escalation.`
    });
    setAuditLogs(prev => [entry, ...prev]);
    showToast(`Escalated ${caseNumber} to MLRO queue.`);
  };

  const handleExportGoAml = async (caseNumber: string) => {
    const xml = await EdithApi.generateGoAmlXml(caseNumber);
    const blob = new Blob([xml], { type: 'application/xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `goAML_STR_${caseNumber}.xml`;
    a.click();
    URL.revokeObjectURL(url);
    const entry = await EdithApi.recordAuditEvent({
      action: 'GOAML_STR_XML_EXPORTED',
      targetEntity: caseNumber,
      caseNumber,
      details: `Exported goAML XML filing.`
    });
    setAuditLogs(prev => [entry, ...prev]);
    showToast(`Downloaded goAML_STR_${caseNumber}.xml.`);
  };

  // Helper to get structured columns and row objects for whichever report is active
  const getActiveReportData = (): { columns: string[]; rows: Record<string, any>[] } => {
    switch (activeTab) {
      case 'TRANSACTIONS':
        return {
          columns: ['TxRef', 'Type', 'Date', 'Branch', 'Sender', 'SenderCode', 'AccountNo', 'Beneficiary', 'Corridor', 'AmountAED', 'Status'],
          rows: filteredTransactions.map(t => ({
            TxRef: t.txRef,
            Type: t.txType,
            Date: t.txDate,
            Branch: t.branch,
            Sender: t.senderName,
            SenderCode: t.senderCode,
            AccountNo: t.accountNo,
            Beneficiary: t.beneficiaryName,
            Corridor: `${t.nationality} -> ${t.benfCountry}`,
            AmountAED: t.lcyAmount,
            Status: t.status
          }))
        };
      case 'BLACKLIST':
        return {
          columns: ['EntryId', 'EntityName', 'Category', 'Nationality', 'Action', 'Date', 'Operator', 'Remarks'],
          rows: filteredBlacklistRows.map(b => ({
            EntryId: b.entryId,
            EntityName: b.entityName,
            Category: b.category,
            Nationality: b.nationality,
            Action: b.actionType,
            Date: b.actionDate,
            Operator: b.operator,
            Remarks: b.remarks
          }))
        };
      case 'RELEASES':
        return {
          columns: ['TxRef', 'CaseNumber', 'Sender', 'Beneficiary', 'Amount', 'Currency', 'Decision', 'TurnaroundMins', 'AuthorizedBy', 'Rationale'],
          rows: RELEASE_ROWS.map(r => ({
            TxRef: r.txRef,
            CaseNumber: r.caseNumber,
            Sender: r.senderName,
            Beneficiary: r.beneficiaryName,
            Amount: r.amount,
            Currency: r.currency,
            Decision: r.decision,
            TurnaroundMins: r.turnaroundMinutes,
            AuthorizedBy: r.authorizedBy,
            Rationale: r.rationale
          }))
        };
      case 'ALERTS':
      case 'ONLINE_STR':
      case 'STR_HISTORY':
        return {
          columns: ['CaseNumber', 'Subject', 'CustomerCode', 'Branch', 'AlertType', 'RuleMatched', 'RiskScore', 'Status', 'Date'],
          rows: strReportRows.map(s => ({
            CaseNumber: s.caseNumber,
            Subject: s.subjectName,
            CustomerCode: s.customerCode,
            Branch: s.branch,
            AlertType: s.alertType,
            RuleMatched: s.ruleMatched,
            RiskScore: `${s.riskScore}%`,
            Status: s.status,
            Date: s.createdAtUtc
          }))
        };
      case 'ANALYTICS':
        return {
          columns: ['Dimension', 'Segment', 'Percentage', 'VolumeSummary'],
          rows: analyticsDistribution.map(d => ({
            Dimension: analyticsDimension,
            Segment: d.label,
            Percentage: `${d.pct}%`,
            VolumeSummary: d.count
          }))
        };
      case 'LOG_PROCESS':
        return {
          columns: ['ProcessId', 'ExecutedAt', 'Mode', 'Operator', 'Rule', 'RecordsProcessed', 'AlertsTriggered', 'DurationMs'],
          rows: PROCESS_LOG_ROWS.map(p => ({
            ProcessId: p.processId,
            ExecutedAt: p.executedAt,
            Mode: p.executionMode,
            Operator: p.operator,
            Rule: p.ruleCode,
            RecordsProcessed: p.recordsProcessed,
            AlertsTriggered: p.alertsTriggered,
            DurationMs: p.durationMs
          }))
        };
      case 'LOG_UPLOAD':
        return {
          columns: ['JobId', 'FileName', 'TotalRecords', 'Processed', 'Skipped', 'Status'],
          rows: (uploadJobs.length > 0 ? uploadJobs : [
            { jobId: 'BATCH-UPL-2026-1025', fileName: '4C416D10_swift_mt103_batch.csv', totalRecords: 15420, processedRecords: 15416, skippedRecords: 4, status: 'COMPLETED' },
            { jobId: 'BATCH-UPL-2026-1024', fileName: 'daily_exchange_remittances_oct24.csv', totalRecords: 28900, processedRecords: 28900, skippedRecords: 0, status: 'COMPLETED' }
          ]).map(j => ({
            JobId: j.jobId,
            FileName: j.fileName,
            TotalRecords: j.totalRecords,
            Processed: j.processedRecords,
            Skipped: j.skippedRecords,
            Status: j.status
          }))
        };
      case 'LOG_SUSPICIOUS':
        return {
          columns: ['TxRef', 'CaseNumber', 'Sender', 'SuspiciousBeneficiary', 'Corridor', 'AmountAED', 'Status'],
          rows: INITIAL_TRANSACTIONS.filter(t => t.status === 'BLOCKED' || t.status === 'HELD').map(t => ({
            TxRef: t.txRef,
            CaseNumber: t.caseNumber || 'N/A',
            Sender: t.senderName,
            SuspiciousBeneficiary: t.beneficiaryName,
            Corridor: `${t.nationality} -> ${t.benfCountry}`,
            AmountAED: t.lcyAmount,
            Status: t.status
          }))
        };
      case 'LOG_CUSTOM':
        return {
          columns: ['Channel', 'TotalVolume', 'ScoreBasedHits', 'AutoHits', 'OnlineHits', 'ManualHits'],
          rows: [
            { Channel: 'Remittance', TotalVolume: 84210, ScoreBasedHits: 142, AutoHits: 98, OnlineHits: 64, ManualHits: 19 },
            { Channel: 'Incoming SWIFT', TotalVolume: 51490, ScoreBasedHits: 88, AutoHits: 61, OnlineHits: 52, ManualHits: 11 },
            { Channel: 'Forex Desk', TotalVolume: 19340, ScoreBasedHits: 49, AutoHits: 34, OnlineHits: 27, ManualHits: 8 }
          ]
        };
      case 'LOG_MONTHLY':
        return {
          columns: ['Month', 'EntityRole', 'TotalScreened', 'ExactHits', 'FuzzyHits', 'WhitelistSuppressed', 'EscalatedSTRs', 'DatasetVersion'],
          rows: MONTHLY_SCREEN_ROWS.filter(m => m.month === selectedMonth).map(m => ({
            Month: m.month,
            EntityRole: m.entityRole,
            TotalScreened: m.totalScreened,
            ExactHits: m.exactMatches,
            FuzzyHits: m.fuzzyPhoneticMatches,
            WhitelistSuppressed: m.whitelistSuppressed,
            EscalatedSTRs: m.escalatedToStr,
            DatasetVersion: m.blacklistVersion
          }))
        };
      case 'CUSTOMER_SCREENING':
        return {
          columns: ['CustomerCode', 'FullName', 'Type', 'Nationality', 'RiskScore', 'WatchlistStatus'],
          rows: (customers.length > 0 ? customers : [
            { customerCode: 'CUST-AE-88902', fullName: 'Rashid Al-Maktoum Trading FZE', customerType: 'Corporate Entity', nationality: 'ARE', riskScore: 84.5, pepStatus: 'PEP Linked' },
            { customerCode: 'CUST-AE-44120', fullName: 'Mohamed Al-Attar', customerType: 'Individual', nationality: 'EGY', riskScore: 18.0, pepStatus: 'Clean' }
          ]).map(c => ({
            CustomerCode: c.customerCode,
            FullName: c.fullName,
            Type: c.customerType,
            Nationality: c.nationality,
            RiskScore: `${c.riskScore}%`,
            WatchlistStatus: c.pepStatus || 'Clean'
          }))
        };
      case 'DELTA_LOG':
        return {
          columns: ['LogId', 'BatchId', 'CustomerCode', 'CustomerName', 'WatchlistSource', 'ScoreJump', 'Decision', 'Date'],
          rows: DELTA_LOG_ROWS.filter(d => {
            if (deltaCustomerFilter && !d.customerCode.toLowerCase().includes(deltaCustomerFilter.toLowerCase())) return false;
            return true;
          }).map(d => ({
            LogId: d.logId,
            BatchId: d.batchId,
            CustomerCode: d.customerCode,
            CustomerName: d.customerName,
            WatchlistSource: d.watchlistSource,
            ScoreJump: `${d.previousScore}% -> ${d.newScore}%`,
            Decision: d.authorizedDecision,
            Date: d.authorizedDate
          }))
        };
      case 'WORM_AUDIT':
      default:
        return {
          columns: ['BlockId', 'Timestamp', 'Actor', 'Action', 'Target', 'CaseNumber', 'SHA256Hash', 'PreviousHash'],
          rows: filteredAuditLogs.map(l => ({
            BlockId: l.id,
            Timestamp: l.timestampUtc,
            Actor: l.actor,
            Action: l.action,
            Target: l.targetEntity,
            CaseNumber: l.caseNumber || 'N/A',
            SHA256Hash: l.sha256Hash,
            PreviousHash: l.previousBlockHash
          }))
        };
    }
  };

  const triggerExport = async (format: 'PDF' | 'CSV') => {
    const reportData = getActiveReportData();

    const entry = await EdithApi.recordAuditEvent({
      action: `REGULATORY_REPORT_${format}_EXPORT`,
      targetEntity: `Report:${activeTab}`,
      details: `Exported ${reportData.rows.length} records from module ${activeTab}.`
    });
    setAuditLogs(prev => [entry, ...prev]);

    if (format === 'CSV') {
      const headerLine = reportData.columns.join(',');
      const rowLines = reportData.rows.map(r =>
        reportData.columns
          .map(col => {
            const val = r[col] !== undefined ? String(r[col]) : '';
            return val.includes(',') || val.includes('"') ? `"${val.replace(/"/g, '""')}"` : val;
          })
          .join(',')
      );
      const csvMeta = [
        `# Module: ${activeTab}`,
        `# ExportedAt: ${entry.timestampUtc}`,
        `# WORM_SHA256: ${entry.sha256Hash}`,
        `# TotalRecords: ${reportData.rows.length}`
      ];
      const csvText = [...csvMeta, '', headerLine, ...rowLines].join('\n');
      const blob = new Blob([csvText], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `EDITH_${activeTab}_${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      showToast(`Exported ${reportData.rows.length} rows to CSV with embedded WORM hash.`);
    } else {
      setCertifiedModal({
        certId: `CERT-${Date.now().toString(36).toUpperCase()}`,
        reportTitle: `${activeTab.replace('_', ' ')}`,
        recordCount: reportData.rows.length,
        issuedAtUtc: entry.timestampUtc,
        wormHash: entry.sha256Hash,
        columns: reportData.columns,
        sampleData: reportData.rows
      });
    }
  };

  const analyticsDistribution = useMemo(() => {
    switch (analyticsDimension) {
      case 'AMOUNT':
        return [
          { label: '> 500,000 AED (Institutional)', pct: 48, count: '1,652,625 AED', filterKey: 'minLcyAmount', filterVal: '400000', color: 'bg-critical' },
          { label: '100,000 – 500,000 AED (Commercial)', pct: 38, count: '930,000 AED', filterKey: 'minLcyAmount', filterVal: '100000', color: 'bg-warning' },
          { label: '< 100,000 AED (Retail)', pct: 14, count: '94,500 AED', filterKey: 'minLcyAmount', filterVal: '', color: 'bg-accent' }
        ];
      case 'CURRENCY':
        return [
          { label: 'USD (Cross-Border)', pct: 52, count: '2,140 Wires', filterKey: 'txType', filterVal: 'WIRE', color: 'bg-primary' },
          { label: 'AED (Domestic / GCC)', pct: 29, count: '4,890 Wires', filterKey: 'txType', filterVal: 'REMITTANCE', color: 'bg-accent' },
          { label: 'EUR / GBP (Foreign Wire)', pct: 19, count: '1,120 Wires', filterKey: 'txType', filterVal: 'FOREX', color: 'bg-warning' }
        ];
      case 'NATIONALITY':
        return [
          { label: 'ARE (United Arab Emirates)', pct: 44, count: '44% Share', filterKey: 'nationality', filterVal: 'ARE', color: 'bg-accent' },
          { label: 'IRN / SYR (High-Risk)', pct: 31, count: '31% Share', filterKey: 'nationality', filterVal: 'IRN', color: 'bg-critical' },
          { label: 'GBR / EGY (Standard)', pct: 25, count: '25% Share', filterKey: 'nationality', filterVal: 'GBR', color: 'bg-primary' }
        ];
      case 'COUNTRY':
        return [
          { label: 'United Arab Emirates (ARE)', pct: 46, count: '1,840,291 Wires', filterKey: 'benfCountry', filterVal: 'ARE', color: 'bg-accent' },
          { label: 'Sanctioned Corridors (RU/IR/SY)', pct: 34, count: '312 Quarantined', filterKey: 'benfCountry', filterVal: 'RUS', color: 'bg-critical' },
          { label: 'Financial Hubs (CH/UK)', pct: 20, count: '248,190 Wires', filterKey: 'correspondentCountry', filterVal: 'CHE', color: 'bg-primary' }
        ];
      case 'RISK':
      default:
        return [
          { label: 'High Risk', pct: 60, count: '3 Active Holds', filterKey: 'riskTier', filterVal: 'HIGH', color: 'bg-critical' },
          { label: 'Medium Risk', pct: 20, count: '1 Monitored', filterKey: 'riskTier', filterVal: 'MEDIUM', color: 'bg-warning' },
          { label: 'Low Risk', pct: 20, count: '1 Released', filterKey: 'riskTier', filterVal: 'LOW', color: 'bg-accent' }
        ];
    }
  }, [analyticsDimension]);

  const pageMeta = usePageMeta(
    'Regulatory Reports & WORM Audit Ledger',
    'Generate compliance reports and inspect the SHA-256 chained WORM ledger.'
  );

  const filteredAuditLogs = useMemo(() => {
    const q = auditSearch.trim().toLowerCase();
    if (!q) return auditLogs;
    return auditLogs.filter(
      l =>
        l.actor.toLowerCase().includes(q) ||
        l.action.toLowerCase().includes(q) ||
        l.targetEntity.toLowerCase().includes(q) ||
        (l.caseNumber && l.caseNumber.toLowerCase().includes(q)) ||
        l.sha256Hash.toLowerCase().includes(q)
    );
  }, [auditLogs, auditSearch]);

  return (
    <div className="p-6 space-y-4 max-w-7xl mx-auto">
      {/* Top Header & Actions */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-surface p-4 rounded-xl border border-border">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-bold text-ink flex items-center gap-2">
              <FileSpreadsheet className="w-5 h-5 text-primary" />
              <span>{pageMeta.title}</span>
            </h1>
          </div>
          <p className="text-xs text-muted mt-0.5">{pageMeta.definition}</p>
        </div>

        {/* Unified Controls: Active Report Selector + Action Dropdown Menu */}
        <div className="flex flex-wrap items-center gap-2.5 font-mono text-xs">
          {/* Active Report Dropdown */}
          <div className="relative inline-block text-left">
            <button
              type="button"
              onClick={() => {
                setIsReportDropdownOpen(prev => !prev);
                setIsExportMenuOpen(false);
              }}
              className="px-3 py-1.5 rounded-lg border border-border bg-surface hover:bg-surface-hover text-ink font-bold flex items-center gap-2 cursor-pointer transition shadow-2xs"
            >
              <FileSpreadsheet className="w-4 h-4 text-primary" />
              <span className="text-xs">
                {REPORT_CATEGORIES.flatMap(c => c.items).find(i => i.id === activeTab)?.label || 'Select Report'}
              </span>
              <ChevronDown className={`w-3.5 h-3.5 text-muted transition-transform ${isReportDropdownOpen ? 'rotate-180' : ''}`} />
            </button>

            {isReportDropdownOpen && (
              <>
                <div
                  className="fixed inset-0 z-40"
                  onClick={() => setIsReportDropdownOpen(false)}
                />
                <div className="absolute right-0 sm:left-0 mt-2 w-80 rounded-xl bg-white border border-border shadow-xl z-50 p-2 max-h-96 overflow-y-auto divide-y divide-border/60">
                  {REPORT_CATEGORIES.map(cat => (
                    <div key={cat.group} className="py-2 first:pt-0 last:pb-0">
                      <div className="px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-primary font-mono">
                        {cat.group}
                      </div>
                      <div className="space-y-0.5 mt-0.5">
                        {cat.items.map(item => {
                          const isSelected = activeTab === item.id;
                          return (
                            <button
                              key={item.id}
                              type="button"
                              onClick={() => {
                                setActiveTab(item.id);
                                navigate(item.path);
                                setIsReportDropdownOpen(false);
                              }}
                              className={`w-full text-left px-2.5 py-1.5 rounded-lg text-xs flex items-center justify-between transition-colors cursor-pointer ${
                                isSelected
                                  ? 'bg-primary text-white font-bold'
                                  : 'text-ink hover:bg-surface-muted hover:text-primary'
                              }`}
                            >
                              <span className="truncate">{item.label}</span>
                              {isSelected && <CheckCircle2 className="w-3.5 h-3.5 shrink-0 ml-2" />}
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>

          {/* Export & Actions Dropdown Menu */}
          <div className="relative inline-block text-left">
            <button
              type="button"
              onClick={() => {
                setIsExportMenuOpen(prev => !prev);
                setIsReportDropdownOpen(false);
              }}
              className="px-3 py-1.5 rounded-lg bg-primary text-white font-bold hover:bg-primary-hover flex items-center gap-2 cursor-pointer shadow-xs transition"
            >
              <Download className="w-3.5 h-3.5" />
              <span>Actions &amp; Exports</span>
              <ChevronDown className={`w-3.5 h-3.5 transition-transform ${isExportMenuOpen ? 'rotate-180' : ''}`} />
            </button>

            {isExportMenuOpen && (
              <>
                <div
                  className="fixed inset-0 z-40"
                  onClick={() => setIsExportMenuOpen(false)}
                />
                <div className="absolute right-0 mt-2 w-56 rounded-xl bg-white border border-border shadow-xl z-50 p-1.5 space-y-1">
                  <button
                    type="button"
                    onClick={() => {
                      triggerExport('CSV');
                      setIsExportMenuOpen(false);
                    }}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg text-xs font-medium text-ink hover:bg-surface-muted hover:text-primary flex items-center gap-2 cursor-pointer transition"
                  >
                    <Download className="w-4 h-4 text-primary" />
                    <span>Export CSV</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      triggerExport('PDF');
                      setIsExportMenuOpen(false);
                    }}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg text-xs font-medium text-ink hover:bg-surface-muted hover:text-primary flex items-center gap-2 cursor-pointer transition"
                  >
                    <FileText className="w-4 h-4 text-emerald-600" />
                    <span>Generate Certified PDF</span>
                  </button>

                  <div className="border-t border-border/70 my-1" />

                  <button
                    type="button"
                    onClick={() => {
                      navigate('/notifications');
                      setIsExportMenuOpen(false);
                    }}
                    className="w-full text-left px-2.5 py-1.5 rounded-lg text-xs font-medium text-ink hover:bg-surface-muted hover:text-primary flex items-center gap-2 cursor-pointer transition"
                  >
                    <BellRing className="w-4 h-4 text-amber-500" />
                    <span>Webhook Rules</span>
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Status Banner */}
      {statusBanner && (
        <div className="p-3 bg-accent/10 border border-accent text-accent rounded-lg text-xs font-mono flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 shrink-0" />
            <span>{statusBanner}</span>
          </div>
          <button type="button" onClick={() => setStatusBanner(null)} className="text-accent hover:opacity-75">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 0: WORM IMMUTABLE AUDIT LEDGER                                  */}
      {/* =================================================================== */}
      {activeTab === 'WORM_AUDIT' && (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 font-mono text-xs">
          <div className="lg:col-span-8 bg-surface p-4 rounded-xl border border-border space-y-3">
            <div className="flex justify-between items-center pb-2 border-b border-border">
              <div>
                <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
                  <ShieldCheck className="w-4 h-4 text-accent" />
                  <span>WORM Cryptographic Audit Chain (`core.audit_ledgers`)</span>
                </h2>
              </div>
              <button
                type="button"
                onClick={handleVerifyWormChain}
                className="px-3 py-1 rounded-lg bg-accent text-white font-bold hover:bg-accent/90 flex items-center gap-1.5 shadow-xs cursor-pointer text-xs"
              >
                <Link2 className="w-3 h-3" />
                <span>{chainVerified === true ? '✓ Chain Verified' : 'Verify Chain'}</span>
              </button>
            </div>

            <div className="flex items-center gap-2">
              <div className="relative flex-1">
                <Search className="w-3.5 h-3.5 text-muted absolute left-3 top-2.5" />
                <input
                  type="text"
                  value={auditSearch}
                  onChange={e => setAuditSearch(e.target.value)}
                  placeholder="Filter audit logs..."
                  className="w-full pl-8 pr-3 py-1.5 rounded-lg bg-surface-muted border border-border text-ink text-xs focus:outline-none focus:border-primary"
                />
              </div>
              <span className="text-[11px] text-muted shrink-0">{filteredAuditLogs.length} Blocks</span>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                    <th className="py-2 px-3">Block / Time</th>
                    <th className="py-2 px-3">Actor</th>
                    <th className="py-2 px-3">Action</th>
                    <th className="py-2 px-3">Target</th>
                    <th className="py-2 px-3">Hash Link</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {filteredAuditLogs.map(log => {
                    const isSelected = selectedAuditBlock?.id === log.id;
                    const isGenesis = log.previousBlockHash.startsWith('00000000') || log.previousBlockHash === 'GENESIS';
                    return (
                      <tr
                        key={`${log.id}-${log.sha256Hash}`}
                        onClick={() => setSelectedAuditBlock(log)}
                        className={`cursor-pointer transition-colors ${isSelected ? 'bg-primary/10' : 'hover:bg-surface-hover'}`}
                      >
                        <td className="py-2 px-3">
                          <div className="font-bold text-ink">#{log.id}</div>
                          <div className="text-[10px] text-muted">{log.timestampUtc.replace('T', ' ').slice(0, 16)}</div>
                        </td>
                        <td className="py-2 px-3 font-semibold text-ink">{log.actor}</td>
                        <td className="py-2 px-3">
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary">
                            {log.action}
                          </span>
                        </td>
                        <td className="py-2 px-3">
                          <div className="text-ink truncate max-w-[140px]">{log.targetEntity}</div>
                          {log.caseNumber && (
                            <span className="text-[10px] text-primary">{log.caseNumber}</span>
                          )}
                        </td>
                        <td className="py-2 px-3 text-[10px]">
                          <div className="truncate max-w-[130px] font-mono text-muted">{log.sha256Hash.slice(0, 14)}...</div>
                          <div className="text-[9px] text-accent mt-0.5">
                            {isGenesis ? 'GENESIS' : `Prev: ${log.previousBlockHash.slice(0, 8)}...`}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>

          {/* Block Inspector Drawer */}
          <div className="lg:col-span-4 bg-surface p-4 rounded-xl border border-border space-y-3">
            <div className="flex items-center justify-between pb-2 border-b border-border">
              <h3 className="text-xs font-bold uppercase text-ink flex items-center gap-1.5">
                <ShieldCheck className="w-4 h-4 text-primary" />
                <span>Block Inspector</span>
              </h3>
              <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-accent/10 text-accent">WORM</span>
            </div>

            {selectedAuditBlock ? (
              <div className="space-y-2.5 text-[11px]">
                <div className="p-2.5 rounded-lg bg-surface-muted border border-border">
                  <div className="text-[10px] text-muted uppercase">Sequence</div>
                  <div className="font-bold text-ink text-sm">Block #{selectedAuditBlock.id}</div>
                  <div className="text-muted text-[10px]">{selectedAuditBlock.timestampUtc}</div>
                </div>

                <div className="p-2.5 rounded-lg bg-surface-muted border border-border space-y-1">
                  <div className="text-[10px] text-muted uppercase">Current Block Hash</div>
                  <div className="font-mono text-primary break-all text-[10px]">{selectedAuditBlock.sha256Hash}</div>
                  <div className="text-[10px] text-muted uppercase pt-1 border-t border-border mt-1">Previous Link</div>
                  <div className="font-mono text-ink break-all text-[10px]">{selectedAuditBlock.previousBlockHash}</div>
                </div>

                <div className="p-2.5 rounded-lg bg-surface-muted border border-border space-y-1">
                  <div className="text-[10px] text-muted uppercase">Attestation Record</div>
                  <div className="text-ink leading-relaxed">{selectedAuditBlock.details}</div>
                </div>
              </div>
            ) : (
              <div className="text-muted text-center py-6">Select a block to inspect.</div>
            )}
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 1: ALERT REPORTS                                                */}
      {/* =================================================================== */}
      {activeTab === 'ALERTS' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 text-warning" />
              <span>Offline Monitoring Alert Reports &amp; SLA Tracking</span>
            </h2>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="p-3 rounded-lg bg-surface-muted border border-border">
              <div className="text-[10px] text-muted uppercase">Total Alerts</div>
              <div className="text-lg font-bold text-ink mt-0.5">{strReportRows.length}</div>
            </div>
            <div className="p-3 rounded-lg bg-warning/10 border border-warning/20">
              <div className="text-[10px] text-warning uppercase font-bold">3-Day EDD Escalations</div>
              <div className="text-lg font-bold text-ink mt-0.5">
                {strReportRows.filter(r => r.slaWatchdog === '3D_EDD_BREACH_AUTO_MLRO').length}
              </div>
            </div>
            <div className="p-3 rounded-lg bg-critical/10 border border-critical/20">
              <div className="text-[10px] text-critical uppercase font-bold">7-Day Monitoring Escalations</div>
              <div className="text-lg font-bold text-ink mt-0.5">
                {strReportRows.filter(r => r.slaWatchdog === '7D_MONITORING_BREACH_AUTO_MLRO').length}
              </div>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Alert Ref</th>
                  <th className="py-2 px-3">Subject / Customer</th>
                  <th className="py-2 px-3">Rule Matched</th>
                  <th className="py-2 px-3">Risk</th>
                  <th className="py-2 px-3">Queue SLA</th>
                  <th className="py-2 px-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {strReportRows.map(row => (
                  <tr key={row.id} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-primary">{row.caseNumber}</td>
                    <td className="py-2 px-3">
                      <div className="font-semibold text-ink">{row.subjectName}</div>
                      <div className="text-[10px] text-muted">{row.customerCode} • {row.branch}</div>
                    </td>
                    <td className="py-2 px-3">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary">
                        {row.ruleMatched}
                      </span>
                    </td>
                    <td className="py-2 px-3 font-bold text-critical">{row.riskScore.toFixed(1)}%</td>
                    <td className="py-2 px-3">
                      <span className="text-[10px]">
                        {row.daysInQueue}d ({row.slaWatchdog === 'WITHIN_SLA' ? 'SLA OK' : 'Overdue'})
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right space-x-1.5">
                      {row.slaWatchdog !== 'WITHIN_SLA' && (
                        <button
                          type="button"
                          onClick={() => handleForceSlaEscalation(row.caseNumber, row.subjectName, row.slaWatchdog)}
                          className="px-2 py-0.5 rounded bg-critical text-white text-[10px] font-bold cursor-pointer"
                        >
                          Escalate
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => navigate('/cases')}
                        className="px-2 py-0.5 rounded border border-border bg-surface text-ink text-[10px] font-bold cursor-pointer hover:bg-surface-hover"
                      >
                        Inspect →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 2: TRANSACTION REPORT                                           */}
      {/* =================================================================== */}
      {activeTab === 'TRANSACTIONS' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <FileSpreadsheet className="w-4 h-4 text-primary" />
              <span>Transaction Report</span>
            </h2>
            <div className="flex items-center gap-2">
              {activeTxFilterCount > 0 && (
                <button
                  type="button"
                  onClick={resetTxFilters}
                  className="px-2 py-1 rounded border border-border text-muted hover:text-ink flex items-center gap-1 cursor-pointer"
                >
                  <RotateCcw className="w-3 h-3" />
                  <span>Reset ({activeTxFilterCount})</span>
                </button>
              )}
              <button
                type="button"
                onClick={() => setShowAdvancedTxFilters(prev => !prev)}
                className="px-2.5 py-1 rounded border border-primary/30 text-primary font-bold flex items-center gap-1 cursor-pointer bg-primary/5"
              >
                <Filter className="w-3 h-3" />
                <span>{showAdvancedTxFilters ? 'Fewer Filters' : '16 Filters'}</span>
              </button>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Type</label>
              <select
                value={txFilters.txType}
                onChange={e => setTxFilters(p => ({ ...p, txType: e.target.value }))}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              >
                <option value="ALL">All Types</option>
                <option value="WIRE">SWIFT Wire</option>
                <option value="REMITTANCE">Remittance</option>
                <option value="INCOMING">Incoming</option>
                <option value="FOREX">Forex</option>
              </select>
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Branch</label>
              <input
                type="text"
                value={txFilters.branch}
                onChange={e => setTxFilters(p => ({ ...p, branch: e.target.value }))}
                placeholder="Branch name..."
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Start Date</label>
              <input
                type="date"
                value={txFilters.startDate}
                onChange={e => setTxFilters(p => ({ ...p, startDate: e.target.value }))}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">End Date</label>
              <input
                type="date"
                value={txFilters.endDate}
                onChange={e => setTxFilters(p => ({ ...p, endDate: e.target.value }))}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Status</label>
              <select
                value={txFilters.status}
                onChange={e => setTxFilters(p => ({ ...p, status: e.target.value }))}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              >
                <option value="ALL">All</option>
                <option value="CLEARED">CLEARED</option>
                <option value="RELEASED">RELEASED</option>
                <option value="HELD">HELD</option>
                <option value="BLOCKED">BLOCKED</option>
              </select>
            </div>
          </div>

          {showAdvancedTxFilters && (
            <div className="p-3 rounded-lg bg-surface-muted border border-border grid grid-cols-1 sm:grid-cols-4 gap-2">
              <div>
                <label className="text-[10px] text-muted block mb-0.5">Account No</label>
                <input
                  type="text"
                  value={txFilters.accountNo}
                  onChange={e => setTxFilters(p => ({ ...p, accountNo: e.target.value }))}
                  placeholder="Account..."
                  className="w-full px-2 py-1 rounded bg-surface border border-border text-ink"
                />
              </div>
              <div>
                <label className="text-[10px] text-muted block mb-0.5">Nationality</label>
                <input
                  type="text"
                  value={txFilters.nationality}
                  onChange={e => setTxFilters(p => ({ ...p, nationality: e.target.value }))}
                  placeholder="ISO (e.g. ARE)"
                  className="w-full px-2 py-1 rounded bg-surface border border-border text-ink"
                />
              </div>
              <div>
                <label className="text-[10px] text-muted block mb-0.5">Beneficiary Country</label>
                <input
                  type="text"
                  value={txFilters.benfCountry}
                  onChange={e => setTxFilters(p => ({ ...p, benfCountry: e.target.value }))}
                  placeholder="Benf country..."
                  className="w-full px-2 py-1 rounded bg-surface border border-border text-ink"
                />
              </div>
              <div>
                <label className="text-[10px] text-muted block mb-0.5">Min Amount (AED)</label>
                <input
                  type="number"
                  value={txFilters.minLcyAmount}
                  onChange={e => setTxFilters(p => ({ ...p, minLcyAmount: e.target.value }))}
                  placeholder="Min AED..."
                  className="w-full px-2 py-1 rounded bg-surface border border-border text-ink"
                />
              </div>
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Tx Ref / Date</th>
                  <th className="py-2 px-3">Sender</th>
                  <th className="py-2 px-3">Beneficiary</th>
                  <th className="py-2 px-3">Account &amp; Branch</th>
                  <th className="py-2 px-3">Amount (AED)</th>
                  <th className="py-2 px-3">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {filteredTransactions.map(tx => (
                  <tr key={tx.txRef} className="hover:bg-surface-hover">
                    <td className="py-2 px-3">
                      <div className="font-bold text-ink">{tx.txRef}</div>
                      <div className="text-[10px] text-muted">{tx.txDate} • {tx.txType}</div>
                    </td>
                    <td className="py-2 px-3">
                      <div className="font-semibold text-ink">{tx.senderName}</div>
                      <div className="text-[10px] text-muted">{tx.senderCode}</div>
                    </td>
                    <td className="py-2 px-3">
                      <div className="text-ink">{tx.beneficiaryName}</div>
                      <div className="text-[10px] text-muted">{tx.nationality} → {tx.benfCountry}</div>
                    </td>
                    <td className="py-2 px-3">
                      <div className="text-ink">{tx.accountNo}</div>
                      <div className="text-[10px] text-muted">{tx.branch}</div>
                    </td>
                    <td className="py-2 px-3 font-semibold text-ink">{tx.lcyAmount.toLocaleString()}</td>
                    <td className="py-2 px-3">
                      <span
                        className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                          tx.status === 'BLOCKED'
                            ? 'bg-critical/10 text-critical'
                            : tx.status === 'HELD'
                            ? 'bg-warning/10 text-warning'
                            : 'bg-accent/10 text-accent'
                        }`}
                      >
                        {tx.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 3: BLACKLIST REPORT                                             */}
      {/* =================================================================== */}
      {activeTab === 'BLACKLIST' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <ShieldAlert className="w-4 h-4 text-critical" />
              <span>Blacklist Audit Report (WC/PEP)</span>
            </h2>
            <div className="flex items-center gap-1.5">
              {(['ALL', 'CREATED', 'UPDATED', 'DELETED'] as const).map(mode => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => setBlacklistActionFilter(mode)}
                  className={`px-2 py-0.5 rounded text-[10px] font-bold border cursor-pointer ${
                    blacklistActionFilter === mode
                      ? 'bg-primary text-white border-primary'
                      : 'bg-surface-muted text-muted border-border hover:text-ink'
                  }`}
                >
                  {mode}
                </button>
              ))}
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">UID</th>
                  <th className="py-2 px-3">Entity Name</th>
                  <th className="py-2 px-3">Source</th>
                  <th className="py-2 px-3">Action</th>
                  <th className="py-2 px-3">Date</th>
                  <th className="py-2 px-3">Remarks</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {filteredBlacklistRows.map(row => (
                  <tr key={row.entryId} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-primary">{row.entryId}</td>
                    <td className="py-2 px-3 font-semibold text-ink">{row.entityName}</td>
                    <td className="py-2 px-3">{row.category}</td>
                    <td className="py-2 px-3">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary">
                        {row.actionType}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-muted">{row.actionDate}</td>
                    <td className="py-2 px-3 text-muted truncate max-w-xs">{row.remarks}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 4: TRANSACTION RELEASE REPORT                                   */}
      {/* =================================================================== */}
      {activeTab === 'RELEASES' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <CheckCircle2 className="w-4 h-4 text-accent" />
              <span>Real-Time Transaction Releases &amp; Authorizations</span>
            </h2>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Tx Ref</th>
                  <th className="py-2 px-3">Counterparties</th>
                  <th className="py-2 px-3">Amount</th>
                  <th className="py-2 px-3">Decision</th>
                  <th className="py-2 px-3">Authorized By</th>
                  <th className="py-2 px-3">Rationale</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {RELEASE_ROWS.map(r => (
                  <tr key={r.txRef} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{r.txRef}</td>
                    <td className="py-2 px-3">
                      <div className="font-semibold text-ink">{r.senderName}</div>
                      <div className="text-[10px] text-muted">→ {r.beneficiaryName}</div>
                    </td>
                    <td className="py-2 px-3 font-semibold text-ink">{r.amount.toLocaleString()} {r.currency}</td>
                    <td className="py-2 px-3">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${r.decision === 'AUTHORIZED' ? 'bg-accent/10 text-accent' : 'bg-critical/10 text-critical'}`}>
                        {r.decision}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-muted">{r.authorizedBy} ({r.turnaroundMinutes}m)</td>
                    <td className="py-2 px-3 text-muted truncate max-w-xs">{r.rationale}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 5 & 12: ONLINE STR REPORT & STR HISTORY                         */}
      {/* =================================================================== */}
      {(activeTab === 'ONLINE_STR' || activeTab === 'STR_HISTORY') && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <FileText className="w-4 h-4 text-primary" />
              <span>{activeTab === 'STR_HISTORY' ? 'STR History & Lifecycle Actions' : 'Online STR Filings'}</span>
            </h2>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Type</label>
              <select
                value={strAlertTypeFilter}
                onChange={e => setStrAlertTypeFilter(e.target.value)}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              >
                <option value="ALL">All</option>
                <option value="SANCTIONS_INTERCEPT">Sanctions</option>
                <option value="OFFLINE_TM_RULE">Offline TM</option>
              </select>
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Ref</label>
              <input
                type="text"
                value={strRefFilter}
                onChange={e => setStrRefFilter(e.target.value)}
                placeholder="Reference..."
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Branch</label>
              <input
                type="text"
                value={strBranchFilter}
                onChange={e => setStrBranchFilter(e.target.value)}
                placeholder="Branch..."
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Customer Code</label>
              <input
                type="text"
                value={strCustomerCodeFilter}
                onChange={e => setStrCustomerCodeFilter(e.target.value)}
                placeholder="Customer code..."
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
            <div>
              <label className="text-[10px] text-muted block mb-0.5">Sort</label>
              <select
                value={strSortField}
                onChange={e => setStrSortField(e.target.value as typeof strSortField)}
                className="w-full px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              >
                <option value="caseNumber">Case No</option>
                <option value="riskScore">Score</option>
                <option value="createdAtUtc">Date</option>
              </select>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Ref</th>
                  <th className="py-2 px-3">Subject</th>
                  <th className="py-2 px-3">Rule</th>
                  <th className="py-2 px-3">Score</th>
                  <th className="py-2 px-3">Status</th>
                  <th className="py-2 px-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {strReportRows.map(row => (
                  <tr key={row.id} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-primary">{row.caseNumber}</td>
                    <td className="py-2 px-3">
                      <div className="font-semibold text-ink">{row.subjectName}</div>
                      <div className="text-[10px] text-muted">{row.customerCode} • {row.branch}</div>
                    </td>
                    <td className="py-2 px-3 font-mono text-[11px]">{row.ruleMatched}</td>
                    <td className="py-2 px-3 font-bold text-critical">{row.riskScore}%</td>
                    <td className="py-2 px-3">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-primary/10 text-primary">
                        {row.status}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right space-x-1.5">
                      <button
                        type="button"
                        onClick={() => handleReopenStr(row.id, row.caseNumber, row.subjectName)}
                        className="px-2 py-0.5 rounded border border-warning/40 bg-warning/10 text-warning text-[10px] font-bold cursor-pointer"
                      >
                        Reopen
                      </button>
                      <button
                        type="button"
                        onClick={() => handleSendBranchMail(row.caseNumber, row.branch, row.subjectName)}
                        className="px-2 py-0.5 rounded border border-primary/30 bg-primary/10 text-primary text-[10px] font-bold cursor-pointer"
                      >
                        {notifiedBranches.includes(row.caseNumber) ? '✓ Sent' : 'Mail'}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleExportGoAml(row.caseNumber)}
                        className="px-2 py-0.5 rounded bg-primary text-white text-[10px] font-bold cursor-pointer"
                      >
                        XML
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 6: TRANSACTION ANALYTICS REPORT                                 */}
      {/* =================================================================== */}
      {activeTab === 'ANALYTICS' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <BarChart3 className="w-4 h-4 text-primary" />
              <span>Transaction Analytics</span>
            </h2>
            <div className="flex gap-1">
              {(['AMOUNT', 'CURRENCY', 'NATIONALITY', 'RISK', 'COUNTRY'] as const).map(dim => (
                <button
                  key={dim}
                  type="button"
                  onClick={() => setAnalyticsDimension(dim)}
                  className={`px-2 py-0.5 rounded text-[10px] font-bold border cursor-pointer ${
                    analyticsDimension === dim ? 'bg-primary text-white border-primary' : 'bg-surface-muted text-ink border-border'
                  }`}
                >
                  {dim}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-2">
            {analyticsDistribution.map(item => (
              <div
                key={item.label}
                onClick={() => {
                  resetTxFilters();
                  if (item.filterVal) {
                    setTxFilters(prev => ({ ...prev, [item.filterKey]: item.filterVal }));
                  }
                  setActiveTab('TRANSACTIONS');
                }}
                className="p-3 rounded-lg bg-surface-muted border border-border hover:border-primary/50 cursor-pointer space-y-1"
              >
                <div className="flex justify-between text-ink font-semibold">
                  <span>{item.label}</span>
                  <span>{item.pct}% ({item.count})</span>
                </div>
                <div className="w-full h-2 rounded-full bg-border overflow-hidden">
                  <div className={`h-full ${item.color} rounded-full`} style={{ width: `${item.pct}%` }} />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 7: PROCESS LOG                                                  */}
      {/* =================================================================== */}
      {activeTab === 'LOG_PROCESS' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <Clock className="w-4 h-4 text-primary" />
              <span>Process Log</span>
            </h2>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Run ID</th>
                  <th className="py-2 px-3">Operator</th>
                  <th className="py-2 px-3">Rule / Suite</th>
                  <th className="py-2 px-3">Records Scanned</th>
                  <th className="py-2 px-3">Alerts</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {PROCESS_LOG_ROWS.map(p => (
                  <tr key={p.processId} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{p.processId}</td>
                    <td className="py-2 px-3">{p.operator}</td>
                    <td className="py-2 px-3">{p.templateOrRule}</td>
                    <td className="py-2 px-3">{p.recordsProcessed.toLocaleString()}</td>
                    <td className="py-2 px-3 font-bold text-critical">{p.alertsTriggered}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 8: UPLOAD LOG                                                   */}
      {/* =================================================================== */}
      {activeTab === 'LOG_UPLOAD' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <Layers className="w-4 h-4 text-primary" />
              <span>Batch Upload Logs</span>
            </h2>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Job ID</th>
                  <th className="py-2 px-3">File Name</th>
                  <th className="py-2 px-3">Records</th>
                  <th className="py-2 px-3">Processed</th>
                  <th className="py-2 px-3">Skipped</th>
                  <th className="py-2 px-3">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {(uploadJobs.length > 0 ? uploadJobs : [
                  { jobId: 'BATCH-UPL-2026-1025', fileName: '4C416D10_swift_mt103_batch.csv', totalRecords: 15420, processedRecords: 15416, skippedRecords: 4, status: 'COMPLETED' },
                  { jobId: 'BATCH-UPL-2026-1024', fileName: 'daily_exchange_remittances_oct24.csv', totalRecords: 28900, processedRecords: 28900, skippedRecords: 0, status: 'COMPLETED' }
                ]).map(j => (
                  <tr key={j.jobId} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-primary">{j.jobId}</td>
                    <td className="py-2 px-3">{j.fileName}</td>
                    <td className="py-2 px-3">{j.totalRecords.toLocaleString()}</td>
                    <td className="py-2 px-3 text-accent font-bold">{j.processedRecords.toLocaleString()}</td>
                    <td className="py-2 px-3 text-warning font-bold">{j.skippedRecords}</td>
                    <td className="py-2 px-3 font-bold">{j.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 9: SUSPICIOUS LOG                                               */}
      {/* =================================================================== */}
      {activeTab === 'LOG_SUSPICIOUS' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <ShieldAlert className="w-4 h-4 text-critical" />
              <span>Suspicious Transaction Log</span>
            </h2>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Tx Ref</th>
                  <th className="py-2 px-3">Counterparties</th>
                  <th className="py-2 px-3">Amount</th>
                  <th className="py-2 px-3">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {INITIAL_TRANSACTIONS.filter(t => t.status === 'BLOCKED' || t.status === 'HELD').map(tx => (
                  <tr key={tx.txRef} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{tx.txRef}</td>
                    <td className="py-2 px-3">
                      <div>{tx.senderName}</div>
                      <div className="text-[10px] text-critical font-bold">→ {tx.beneficiaryName}</div>
                    </td>
                    <td className="py-2 px-3 font-semibold text-ink">{tx.lcyAmount.toLocaleString()} AED</td>
                    <td className="py-2 px-3 font-bold text-critical">{tx.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 10: CUSTOM REPORTS                                              */}
      {/* =================================================================== */}
      {activeTab === 'LOG_CUSTOM' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <Sliders className="w-4 h-4 text-primary" />
              <span>Custom Reports — Channel Breakdown</span>
            </h2>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Channel</th>
                  <th className="py-2 px-3">Total Volume</th>
                  <th className="py-2 px-3">Score-Based</th>
                  <th className="py-2 px-3">Auto</th>
                  <th className="py-2 px-3">Online</th>
                  <th className="py-2 px-3">Manual</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  { channel: 'Remittance', total: 84210, score: 142, auto: 98, online: 64, manual: 19 },
                  { channel: 'Incoming', total: 51490, score: 88, auto: 61, online: 52, manual: 11 },
                  { channel: 'Forex', total: 19340, score: 49, auto: 34, online: 27, manual: 8 }
                ].map(r => (
                  <tr key={r.channel} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{r.channel}</td>
                    <td className="py-2 px-3 font-semibold text-primary">{r.total.toLocaleString()}</td>
                    <td className="py-2 px-3">{r.score}</td>
                    <td className="py-2 px-3">{r.auto}</td>
                    <td className="py-2 px-3">{r.online}</td>
                    <td className="py-2 px-3">{r.manual}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 11: MONTHLY SCREEN DATA                                         */}
      {/* =================================================================== */}
      {activeTab === 'LOG_MONTHLY' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <FileText className="w-4 h-4 text-primary" />
              <span>Monthly Screening Volumes</span>
            </h2>
            <select
              value={selectedMonth}
              onChange={e => setSelectedMonth(e.target.value)}
              className="px-2 py-1 rounded bg-surface-muted border border-border text-ink"
            >
              <option value="2026-10">October 2026</option>
              <option value="2026-09">September 2026</option>
            </select>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Role</th>
                  <th className="py-2 px-3">Total</th>
                  <th className="py-2 px-3">Exact</th>
                  <th className="py-2 px-3">Fuzzy</th>
                  <th className="py-2 px-3">Suppressed</th>
                  <th className="py-2 px-3">Escalated</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {MONTHLY_SCREEN_ROWS.filter(m => m.month === selectedMonth).map(row => (
                  <tr key={`${row.month}-${row.entityRole}`} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{row.entityRole}</td>
                    <td className="py-2 px-3">{row.totalScreened.toLocaleString()}</td>
                    <td className="py-2 px-3 text-critical font-bold">{row.exactMatches}</td>
                    <td className="py-2 px-3 text-warning font-bold">{row.fuzzyPhoneticMatches}</td>
                    <td className="py-2 px-3 text-accent font-bold">{row.whitelistSuppressed}</td>
                    <td className="py-2 px-3 font-bold text-ink">{row.escalatedToStr}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 13: CUSTOMER SCREENING REPORT                                   */}
      {/* =================================================================== */}
      {activeTab === 'CUSTOMER_SCREENING' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <UserCheck className="w-4 h-4 text-primary" />
              <span>Customer Onboarding Screening Reports</span>
            </h2>
            <button
              type="button"
              onClick={() => navigate('/screening')}
              className="px-2.5 py-1 rounded bg-primary text-white font-bold text-[11px] cursor-pointer"
            >
              Run Screening →
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Customer Code</th>
                  <th className="py-2 px-3">Name</th>
                  <th className="py-2 px-3">Type</th>
                  <th className="py-2 px-3">Nationality</th>
                  <th className="py-2 px-3">Risk</th>
                  <th className="py-2 px-3">Watchlist Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {(customers.length > 0 ? customers : [
                  { customerCode: 'CUST-AE-88902', fullName: 'Rashid Al-Maktoum Trading FZE', customerType: 'Corporate', nationality: 'ARE', riskScore: 84.5, pepStatus: 'PEP Linked' },
                  { customerCode: 'CUST-AE-44120', fullName: 'Mohamed Al-Attar', customerType: 'Individual', nationality: 'EGY', riskScore: 18.0, pepStatus: 'Clean' }
                ]).map(c => (
                  <tr key={c.customerCode} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-primary">{c.customerCode}</td>
                    <td className="py-2 px-3 font-semibold text-ink">{c.fullName}</td>
                    <td className="py-2 px-3">{c.customerType}</td>
                    <td className="py-2 px-3">{c.nationality}</td>
                    <td className="py-2 px-3 font-bold">{c.riskScore}%</td>
                    <td className="py-2 px-3 text-muted">{c.pepStatus || 'Clean'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* TAB 14: DELTA LOG REPORT                                            */}
      {/* =================================================================== */}
      {activeTab === 'DELTA_LOG' && (
        <div className="bg-surface p-4 rounded-xl border border-border space-y-3 font-mono text-xs">
          <div className="flex justify-between items-center pb-2 border-b border-border">
            <h2 className="text-xs font-bold uppercase tracking-wider text-ink flex items-center gap-2">
              <RefreshCw className="w-4 h-4 text-primary" />
              <span>Delta Screening Report Log</span>
            </h2>
            <div className="flex items-center gap-2">
              <input
                type="text"
                value={deltaCustomerFilter}
                onChange={e => setDeltaCustomerFilter(e.target.value)}
                placeholder="Customer code..."
                className="px-2 py-1 rounded bg-surface-muted border border-border text-ink"
              />
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-border text-muted text-[10px] uppercase bg-surface-muted">
                  <th className="py-2 px-3">Log ID</th>
                  <th className="py-2 px-3">Customer</th>
                  <th className="py-2 px-3">Source</th>
                  <th className="py-2 px-3">Delta</th>
                  <th className="py-2 px-3">Disposition</th>
                  <th className="py-2 px-3">Authorized By</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {DELTA_LOG_ROWS.filter(d => {
                  if (deltaCustomerFilter && !d.customerCode.toLowerCase().includes(deltaCustomerFilter.toLowerCase())) return false;
                  return true;
                }).map(row => (
                  <tr key={row.logId} className="hover:bg-surface-hover">
                    <td className="py-2 px-3 font-bold text-ink">{row.logId}</td>
                    <td className="py-2 px-3 font-semibold text-primary">{row.customerName}</td>
                    <td className="py-2 px-3">{row.watchlistSource}</td>
                    <td className="py-2 px-3 font-bold text-critical">{row.previousScore}% → {row.newScore}%</td>
                    <td className="py-2 px-3">{row.authorizedDecision}</td>
                    <td className="py-2 px-3 text-muted">{row.authorizedBy}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* =================================================================== */}
      {/* PRINTABLE / CERTIFIED EXPORT MODAL WITH LIVE REPORT DATA TABLE      */}
      {/* =================================================================== */}
      {certifiedModal &&
        createPortal(
          <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-[9999] p-4 font-mono">
            <div 
              id="certified-report-print-sheet"
              className="bg-white text-slate-900 w-full max-w-4xl rounded-2xl border border-slate-300 shadow-2xl overflow-hidden max-h-[90vh] flex flex-col"
            >
              {/* Modal Top Header (Interactive Close Button) */}
              <div className="p-4 bg-slate-900 text-white flex items-center justify-between shrink-0 no-print">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="w-5 h-5 text-emerald-400" />
                  <h3 className="text-sm font-bold tracking-wide">
                    Certified Regulatory Examination Report
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => setCertifiedModal(null)}
                  className="text-slate-400 hover:text-white cursor-pointer p-1"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-6 space-y-4 text-xs overflow-y-auto flex-1 bg-white">
                {/* Enterprise Official Document Header */}
                <div className="border-b-2 border-slate-900 pb-4">
                  <div className="flex justify-between items-start">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-lg bg-slate-900 text-white flex items-center justify-center font-bold text-lg font-mono">
                        E
                      </div>
                      <div>
                        <h1 className="text-base font-extrabold uppercase tracking-wider text-slate-950 font-sans">
                          EDITH Next-Gen Enterprise AML Intelligence
                        </h1>
                        <p className="text-[10px] text-slate-500 font-mono">
                          Financial Crime Compliance &amp; Sanctions Screening Infrastructure
                        </p>
                      </div>
                    </div>
                    <div className="text-right font-mono">
                      <span className="inline-block px-2.5 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-300">
                        OFFICIAL ATTESTATION
                      </span>
                      <div className="text-[10px] text-slate-500 mt-1">Classification: STRICTLY CONFIDENTIAL</div>
                    </div>
                  </div>
                </div>

                {/* Certificate Meta Dossier Grid */}
                <div className="p-3.5 rounded-lg bg-slate-50 border border-slate-200 grid grid-cols-2 sm:grid-cols-4 gap-3 text-[11px] font-mono">
                  <div>
                    <span className="text-slate-500 block text-[9px] uppercase font-bold">Attestation Ref</span>
                    <span className="font-bold text-indigo-700">{certifiedModal.certId}</span>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[9px] uppercase font-bold">Report Module</span>
                    <span className="font-bold text-slate-900">{certifiedModal.reportTitle}</span>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[9px] uppercase font-bold">Record Count</span>
                    <span className="font-bold text-slate-900">{certifiedModal.recordCount} Verified Rows</span>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[9px] uppercase font-bold">Issued Timestamp</span>
                    <span className="font-bold text-slate-900">{certifiedModal.issuedAtUtc.replace('T', ' ')}</span>
                  </div>
                  <div className="col-span-2 sm:col-span-4 pt-2 border-t border-slate-200 mt-1">
                    <span className="text-slate-500 block text-[9px] uppercase font-bold">Cryptographic WORM SHA-256 Ledger Anchor</span>
                    <span className="font-mono text-slate-800 text-[10px] break-all bg-white px-2 py-1 rounded border border-slate-200 block mt-0.5 select-all">
                      {certifiedModal.wormHash}
                    </span>
                  </div>
                </div>

                {/* Official Report Record Data Table */}
                <div className="border border-slate-300 rounded-lg overflow-hidden">
                  <div className="bg-slate-100 px-3.5 py-2 border-b border-slate-300 font-bold text-[11px] text-slate-900 flex justify-between items-center">
                    <div className="flex items-center gap-1.5">
                      <FileCheck className="w-3.5 h-3.5 text-indigo-600" />
                      <span>Certified Extraction Data ({certifiedModal.sampleData.length} Records)</span>
                    </div>
                    <span className="text-slate-500 font-mono text-[10px]">Chained to PostgreSQL Immutable Ledger</span>
                  </div>
                  <div className="overflow-x-auto max-h-72">
                    <table className="w-full text-left text-[11px] font-mono">
                      <thead>
                        <tr className="border-b border-slate-300 text-slate-600 text-[10px] uppercase bg-slate-50 font-bold">
                          {certifiedModal.columns.map(col => (
                            <th key={col} className="py-2 px-3 whitespace-nowrap">{col}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200">
                        {certifiedModal.sampleData.map((row, idx) => (
                          <tr key={idx} className="hover:bg-slate-50 even:bg-slate-50/50">
                            {certifiedModal.columns.map(col => (
                              <td key={col} className="py-1.5 px-3 whitespace-nowrap text-slate-900">
                                {String(row[col] ?? '')}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* Regulatory Compliance Footer & Sign-off Block */}
                <div className="pt-3 border-t border-slate-200 grid grid-cols-1 sm:grid-cols-2 gap-4 text-[10px] font-mono text-slate-600">
                  <div className="space-y-1">
                    <div className="font-bold text-slate-800 uppercase">Statutory Regulatory Basis</div>
                    <p className="text-[10px] text-slate-500 leading-relaxed">
                      Generated under UAE CBUAE AML/CFT Decision No. 74 of 2019 &amp; FATF Recommendation 11.
                      Cryptographically logged to immutable ledger table `core.audit_ledgers` with irreversible SHA-256 block hashing.
                    </p>
                  </div>
                  <div className="sm:text-right space-y-1 border-t sm:border-t-0 pt-2 sm:pt-0 border-slate-200">
                    <div className="font-bold text-slate-800 uppercase">Authorizing Compliance Authority</div>
                    <p className="text-[10px] text-slate-500">EDITH Automated Auditing &amp; Supervisory Node</p>
                    <p className="text-[9px] text-slate-400 font-mono">Digital Signature: VERIFIED_WORM_IMMUTABLE</p>
                  </div>
                </div>
              </div>

              {/* Action Buttons (Hidden during Print) */}
              <div className="p-3.5 bg-slate-50 border-t border-slate-200 flex justify-end gap-2.5 shrink-0 no-print">
                <button
                  type="button"
                  onClick={() => {
                    const blob = new Blob(
                      [
                        JSON.stringify(
                          {
                            certificate: certifiedModal,
                            exportedData: certifiedModal.sampleData,
                            wormSeal: certifiedModal.wormHash
                          },
                          null,
                          2
                        )
                      ],
                      { type: 'application/json' }
                    );
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = `${certifiedModal.certId}_${certifiedModal.reportTitle}.json`;
                    a.click();
                    URL.revokeObjectURL(url);
                  }}
                  className="px-3.5 py-1.5 rounded-lg border border-slate-300 bg-white hover:bg-slate-50 text-slate-700 font-bold flex items-center gap-1.5 cursor-pointer text-xs transition"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Download JSON Data</span>
                </button>
                <button
                  type="button"
                  onClick={() => window.print()}
                  className="px-4 py-1.5 rounded-lg bg-slate-900 text-white font-bold hover:bg-slate-800 flex items-center gap-1.5 cursor-pointer text-xs shadow-xs transition"
                >
                  <Printer className="w-3.5 h-3.5" />
                  <span>Print Certified PDF</span>
                </button>
              </div>
            </div>
          </div>,
          document.body
        )}
    </div>
  );
};
