// Types definition for EDITH AML Intelligence Platform

export type UserRole = 
  | 'SUPER_ADMIN' 
  | 'ADMIN' 
  | 'AML_OFFICER' 
  | 'EDD_ANALYST' 
  | 'ISTR_MLRO' 
  | 'MLRO' 
  | 'ANALYST' 
  | 'AUDITOR' 
  | 'COMPLIANCE_OFFICER';

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
  subGroup?: string;
  subMenus?: MenuItemEntitlement[];
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
  minThresholdScore?: number;
  nameWeightage?: number;
  nationalityWeightage?: number;
  dobWeightage?: number;
  idWeightage?: number;
  parties: {
    role: 'SENDER' | 'BENEFICIARY' | 'INTERMEDIARY' | 'ULTIMATE_BENEFICIARY';
    fullName: string;
    country: string;
    dateOfBirth?: string;
    nationalId?: string;
  }[];
}

export interface PassLogDto {
  passNumber: number;
  score: number;
  matchType: string;
  details: string;
}

export interface ScreeningResult {
  decision: ScreeningDecision;
  riskScore: number;
  primaryHitReason?: string;
  caseId?: string;
  caseNumber?: string;
  evaluationLatencyMs: number;
  matchCount: number;
  partyEvaluations: PartyEvaluation[];
  passLogs?: PassLogDto[];
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
  authorRole: string;
  content: string;
  noteType?: 'INVESTIGATION' | 'SUPERVISOR' | 'RATIONALE' | 'SYSTEM' | string;
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

export type RuleTypeCode = 'TRANSACTION_THRESHOLD' | 'HIGH_RISK_CORRIDOR' | 'STRUCTURING_SMURFING' | 'VELOCITY_ANOMALY' | string;
export type RuleExecutionMode = 'REALTIME' | 'SHADOW' | 'PENDING_APPROVAL';

export interface RuleDefinition {
  id: string;
  ruleCode: string;
  ruleName: string;
  ruleType: RuleTypeCode;
  severity: CasePriority;
  scoreWeightage?: number;
  executionMode?: RuleExecutionMode;
  isActive: boolean;
  thresholdAmount?: number | null;
  currency?: string;
  highRiskCountries?: string[];
  windowMinutes?: number | null;
  minTransactions?: number | null;
  structuringMinAmount?: number | null;
  structuringMaxAmount?: number | null;
  parametersJson: string;
  description: string;
  updatedAtUtc: string;
}

export type BacktestDatasetCohort =
  | 'STANDARD_MIX'
  | 'HIGH_RISK_CORRIDOR_SURGE'
  | 'SMURFING_RING_ATTACK'
  | 'VELOCITY_BURST_STRESS';

export interface BacktestSampleHit {
  transactionRefNo: string;
  senderName: string;
  beneficiaryName: string;
  amount: number;
  currency: string;
  corridor: string;
  triggerReason: string;
  deltaStatus?: 'NEWLY_CAPTURED' | 'RETAINED' | 'NEWLY_SUPPRESSED' | string;
  groundTruthLabel?: 'STR_FILED' | 'CLEARED_FP' | 'UNREVIEWED' | string;
  overlappingRules?: string[];
}

export interface SensitivityCurvePoint {
  label: string;
  parameterValue: number;
  triggeredCount: number;
  hitRatePercent: number;
  truePositivesCaptured: number;
  isCurrentCandidate: boolean;
}

export interface BacktestResult {
  ruleCode: string;
  datasetCohort?: BacktestDatasetCohort;
  simulatedTransactions: number;
  triggeredAlerts: number;
  baselineTriggeredAlerts?: number;
  netAlertDelta?: number;
  newlyCapturedCount?: number;
  newlySuppressedCount?: number;
  uniqueHitsCount?: number;
  overlappingHitsCount?: number;
  truePositivesCaptured?: number;
  totalHistoricalStrs?: number;
  falsePositivesFlagged?: number;
  triggerRatePercent: number;
  falsePositiveRateEstimate: number;
  averageEvaluationLatencyUs: number;
  estimatedDailyAlerts?: number;
  sampleTriggeredRefNos?: string[];
  sensitivityCurve?: SensitivityCurvePoint[];
  sampleHits?: BacktestSampleHit[];
}

export interface SandboxImpactedEntity {
  referenceId: string;
  entityName: string;
  corridor: string;
  baselineScore: number;
  simulatedScore: number;
  nameMatchScore: number;
  secondaryIdScore: number;
  deltaStatus: 'NEWLY_CAPTURED' | 'NEWLY_SUPPRESSED' | 'RETAINED_HIT' | string;
  groundTruth: 'STR_CONFIRMED' | 'CLEARED_FP' | 'STANDARD_REVIEW' | string;
}

export interface SandboxSimulationResult {
  datasetCohort: BacktestDatasetCohort;
  evaluatedRecords: number;
  projectedAlerts: number;
  baselineAlerts: number;
  netAlertDelta: number;
  newlyCapturedAlerts: number;
  newlySuppressedAlerts: number;
  fpReductionPercent: number;
  estimatedP99LatencyMs: number;
  truePositivesRetained: number;
  totalHistoricalStrs: number;
  falsePositivesSuppressed: number;
  activePortfolioRulesEvaluated: number;
  scoreBands: Array<{
    band: string;
    count: number;
    pct: number;
    color: string;
  }>;
  thresholdSensitivity: Array<{
    threshold: number;
    projectedAlerts: number;
    strRecallCount: number;
    totalStrCount: number;
    isSelected: boolean;
  }>;
  sampleImpactedEntities: SandboxImpactedEntity[];
}

export type NameSearchMode = 'EXACT' | 'FUZZY' | 'EXACT_NAME_AND_ID';

export interface NameSearchRequest {
  name?: string;
  idNumber?: string;
  birthYear?: number;
  searchMode: NameSearchMode;
  minScore?: number;
  maxResults?: number;
}

export interface NameSearchMatch {
  entityId: string;
  externalId: string;
  fullName: string;
  normalizedTargetName: string;
  entityType: string;
  aliasName?: string;
  idDetails?: string;
  nationality?: string;
  dob?: string;
  yobDelta?: number;
  isIdMatched?: boolean;
  sanctionsList: string;
  sanctionsAuthority: string;
  category: string;
  isBlocked: boolean;
  score: number;
  stringSimilarity: number;
  phoneticSimilarity: number;
  matchModeUsed: NameSearchMode;
  whySanctioned: string;
  whyMatched: string;
  technicalBreakdown: string;
}

export interface NameSearchResponse {
  queryName: string;
  normalizedQueryName: string;
  queryId?: string;
  queryBirthYear?: number;
  searchMode: NameSearchMode;
  totalMatches: number;
  latencyMs: number;
  matches: NameSearchMatch[];
  evaluatedAt: string;
}

export interface ScreeningAuditCertificate {
  certificateId: string;
  issuedAtUtc: string;
  tenantId: string;
  subjectName: string;
  subjectId?: string;
  screeningType: string;
  decision: string;
  overallScore: number;
  latencyMs: number;
  caseNumber?: string;
  matchCount: number;
  passLogs: PassLogDto[];
  wormBlockHashSha256: string;
  regulatoryAttestation: string;
}

export interface IamUserRecord {
  id: string;
  tenantId: string;
  groupId?: string;
  username: string;
  fullName: string;
  role: UserRole;
  department: string;
  isActive: boolean;
  mfaEnabled: boolean;
  shiftStatus: 'ON_DUTY' | 'OFF_DUTY';
  lastLogin: string;
  passwordHashScheme?: string;
  mustChangePassword?: boolean;
  invitationToken?: string;
}

export interface CreateIamUserResponse {
  user: IamUserRecord;
  generatedTemporaryPassword?: string;
  emailStatus: 'SENT' | 'FAILED' | 'SKIPPED';
  emailError?: string;
}

export interface LoginResponse {
  status: 'SUCCESS' | 'MFA_REQUIRED' | 'ONBOARDING_REQUIRED' | 'INVALID_CREDENTIALS' | 'ACCOUNT_LOCKED';
  accessToken?: string;
  mfaSessionToken?: string;
  onboardingToken?: string;
  role?: UserRole;
  tenantId?: string;
  message?: string;
}

export interface MenuMasterItem {
  id: string;
  parentMenuId?: string | null;
  menuName: string;
  menuCode: string;
  path: string;
  position: number;
  icon: string;
  isActive: boolean;
}

export interface GroupMenuMapping {
  groupId: string;
  menuId: string;
  menuName: string;
  menuCode: string;
  path: string;
  canView: boolean;
  canEdit: boolean;
  canAuthorize: boolean;
}

export interface IamGroupPermission {
  menuId: string;
  canView: boolean;
  canEdit: boolean;
  canAuthorize: boolean;
}

export interface IamGroupRecord {
  id: string;
  tenantId: string;
  groupName: string;
  isActive: boolean;
  permissions: IamGroupPermission[];
}

export interface KycSowChecklistItemDto {
  code: string;
  label: string;
  category: string;
  verified: boolean;
  evidenceRef?: string;
}

export interface KycAlertSummaryDto {
  alertId: string;
  title: string;
  description: string;
  status: string;
  severity: string;
  linkedCaseNumber?: string;
}

export interface KycDocumentDto {
  id: string;
  title: string;
  status: string;
  expiryDate: string;
  docType?: string;
  docNumber?: string;
  issueCountry?: string;
  issuedDate?: string;
  ocrConfidence?: number;
  extractedFields?: Record<string, string>;
}

export interface KycTransactionDto {
  txId: string;
  date: string;
  amount: number;
  currency: string;
  counterparty: string;
  status: string;
  corridor?: string;
  riskScore?: number;
  linkedCaseNumber?: string | null;
}

export interface KycDossierDto {
  customerId: string;
  customerCode: string;
  fullName: string;
  customerType: string;
  riskCategory: string;
  riskScore: number;
  nationality: string;
  incorporationCountry: string;
  uboIdentities: string[];
  documents: KycDocumentDto[];
  recentTransactions: KycTransactionDto[];
  registrationNumber?: string;
  jurisdiction?: string;
  pepStatus?: string;
  riskTier?: string;
  isBlocked?: boolean;
  blockReason?: string;
  onboardingStatus?: string;
  onboardedDate?: string;
  nextReviewDate?: string;
  relationshipManager?: string;
  primaryAccount?: string;
  expectedMonthlyTurnoverUsd?: number;
  approvedCorridors?: string[];
  sowChecklist?: KycSowChecklistItemDto[];
  historicalAlerts?: KycAlertSummaryDto[];
}

export interface AuditLogRecord {
  id: string;
  timestampUtc: string;
  actor: string;
  action: string;
  targetEntity: string;
  caseNumber?: string;
  sha256Hash: string;
  previousBlockHash: string;
  details: string;
}
