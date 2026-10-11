// Types definition for EDITH AML Intelligence Platform

export type UserRole = 'SUPER_ADMIN' | 'MLRO' | 'ANALYST' | 'AUDITOR';

export interface UserSession {
  token: string;
  expiresAtUtc: string;
  username: string;
  role: UserRole;
  tenantId: string;
}

export interface MenuItemEntitlement {
  code: string;
  label: string;
  path: string;
  icon: string;
  enabled: boolean;
}

export interface EntitlementsResponse {
  tenantId: string;
  username: string;
  role: UserRole;
  menuItems: MenuItemEntitlement[];
  features: string[];
}

export type ScreeningDecision = 'CLEARED' | 'HELD' | 'BLOCKED';

export type WatchlistCategory = 'SANCTIONS' | 'PEP' | 'ADVERSE_MEDIA' | 'INTERNAL_BLACKLIST';

export interface TokenComparison {
  queryToken: string;
  candidateToken: string;
  tokenScore: number;
  matchType: 'EXACT' | 'FUZZY' | 'PHONETIC' | 'MISSING';
}

export interface CandidateMatch {
  candidateId: string;
  canonicalName: string;
  matchedName: string;
  category: WatchlistCategory;
  confidenceScore: number;
  stringSimilarity: number;
  phoneticSimilarity: number;
  sanctionsList?: string;
  isSuppressedByWhitelist: boolean;
  tokenBreakdown: TokenComparison[];
  dateOfBirth?: string;
  nationality?: string;
  designationDetails?: string;
}

export interface PartyEvaluation {
  role: string;
  queryName: string;
  matchFound: boolean;
  maxScore: number;
  matchedCandidates: CandidateMatch[];
}

export interface TransactionScreeningRequest {
  transactionId?: string;
  tenantId?: string;
  sourceAccount: string;
  destinationAccount: string;
  amount: number;
  currency: string;
  originatingCountry: string;
  destinationCountry: string;
  parties: {
    role: 'SENDER' | 'BENEFICIARY' | 'INTERMEDIARY' | 'ULTIMATE_BENEFICIARY';
    fullName: string;
    country: string;
    dateOfBirth?: string;
    nationalId?: string;
  }[];
}

export interface ScreeningResult {
  decision: ScreeningDecision;
  riskScore: number;
  primaryHitReason?: string;
  evaluationLatencyMs: number;
  matchCount: number;
  partyEvaluations: PartyEvaluation[];
  isCached: boolean;
  replayedFromIdempotency: boolean;
}

export type CaseStatus = 'Open' | 'UnderReview' | 'PendingApproval' | 'Resolved' | 'Closed';
export type CasePriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type CaseResolution = 'FalsePositive' | 'TruePositiveReported' | 'TruePositiveRemediated' | 'Dismissed';

export interface CaseSummary {
  id: string;
  caseNumber: string;
  tenantId: string;
  status: CaseStatus;
  priority: CasePriority;
  riskScore: number;
  primarySubjectName: string;
  assignedAnalystId?: string;
  assignedAnalystName?: string;
  createdAtUtc: string;
  dueAtUtc: string;
  isLocked: boolean;
  lockedByUserId?: string;
  lockedByUserName?: string;
  lockExpiresAtUtc?: string;
}

export interface CaseAuditEntry {
  id: string;
  timestampUtc: string;
  actorName: string;
  actorRole: UserRole;
  action: string;
  previousState?: string;
  newState?: string;
  notes?: string;
}

export interface CaseComment {
  id: string;
  timestampUtc: string;
  authorName: string;
  authorRole: UserRole;
  content: string;
}

export interface CaseDetail extends CaseSummary {
  transactionDetails: {
    transactionId: string;
    amount: number;
    currency: string;
    sourceAccount: string;
    destinationAccount: string;
    originatingCountry: string;
    destinationCountry: string;
  };
  matchedCandidates: CandidateMatch[];
  auditLog: CaseAuditEntry[];
  comments: CaseComment[];
  resolution?: CaseResolution;
  resolutionRationale?: string;
}

export interface BulkJobSummary {
  jobId: string;
  fileName: string;
  status: 'SUBMITTED' | 'PROCESSING' | 'COMPLETED' | 'FAILED';
  totalRecords: number;
  processedRecords: number;
  hitCount: number;
  percentComplete: number;
  startedAtUtc: string;
  completedAtUtc?: string;
}

export interface RuleDefinition {
  id: string;
  ruleCode: string;
  ruleName: string;
  ruleType: 'TRANSACTION_THRESHOLD' | 'HIGH_RISK_CORRIDOR' | 'STRUCTURING_SMURFING';
  severity: CasePriority;
  isActive: boolean;
  parametersJson: string;
  description: string;
  updatedAtUtc: string;
}

export interface BacktestResult {
  ruleCode: string;
  simulatedTransactions: number;
  triggeredAlerts: number;
  triggerRatePercent: number;
  falsePositiveRateEstimate: number;
  averageEvaluationLatencyUs: number;
}
