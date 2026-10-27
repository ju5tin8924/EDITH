# EDITH Platform Core Modernization & Production Readiness TODO
**Document Version:** 10.2  
**Date:** October 27, 2026  
**Status:** Completed (Production Ready)  
**Target Environment:** PostgreSQL 17 (`edith_postgres_17`), OpenSearch 2.12 (`edith_opensearch_2`), .NET 10 Minimal APIs (`Edith.Gateway`), React 19 Frontend (`edith-portal`).

---

## Progress Overview

| Phase | Description | Status |
| :--- | :--- | :--- |
| **Phase 1** | Wire KYC 360 Dossier & Document Repository to PostgreSQL | `COMPLETED` |
| **Phase 2** | Wire System Masters & Scoring Weights to PostgreSQL with Update APIs | `COMPLETED` |
| **Phase 3** | Wire WORM Immutable Audit Ledger to `core.audit_ledgers` | `COMPLETED` |
| **Phase 4** | Multipart Bulk Screening File Upload with `AmlStreamingParser` | `COMPLETED` |
| **Phase 5** | Live Delta Rescreening with PostgreSQL & OpenSearch Timestamps | `COMPLETED` |
| **Phase 6** | Single Entity & Transaction Screening Console UX/Workflow Modernization | `COMPLETED` |
| **Phase 7** | Single Entity Recommended Enhancements (Whitelist Bypass, WORM Audit, Sensitivity Drawer) | `COMPLETED` |
| **Phase 8** | Multi-tenant IAM User & Group Administration CRUD Endpoints (`core.users`) | `COMPLETED` |
| **Phase 9** | CBS Outbound Webhooks & Reliability (HMAC-SHA256 Signatures, Polly Retries & Dead-Letter) | `COMPLETED` |
| **Phase 10** | Searches Module Advanced UX & Engine Modernization (History, Diff, What-If, PEP Tiering, Batch) | `COMPLETED` |
| **Phase 11** | Single Entity Enterprise Workflow Modernization (Personas, In-Line Adjudication, KYC Bridge, Clearance Slip) | `COMPLETED` |
| **Phase 12** | Case Workspace Investigation Canvas Workflow & UX Modernization (Layout, Multi-Duration Lease, Tabs, Whitelist-on-Dismissal, AI Copilot, Categorized Notes) | `COMPLETED` |
| **Phase 13** | EDITH Eagle SVG Brand Identity, Animated SVG Loader & Role-Gated Enterprise Dashboard Charts | `COMPLETED` |
| **Phase 14** | Customer KYC 360 & EDD Dossier Workflow, UX & Feature Modernization (Live Wiring, Switcher, UBO Cascade Screening, OCR Inspector, SoW/SoF EDD Sign-off, Drift Monitor & WORM Dossier Export) | `COMPLETED` |
| **Phase 15** | AML Rule Builder & Sandbox Workflow, UX & Feature Modernization (Full CRUD, Category Form Editor, Velocity Anomaly Engine, Draft "What-If" Backtesting, Sample Hit Inspector, 4-Eyes & Shadow Mode, Sandbox Promotion & WORM Audit) | `COMPLETED` |
| **Phase 16** | AML Rule Backtesting Sandbox & Calibration Engine Modernization (Baseline vs. Draft Delta, Ground-Truth STR Confusion Matrix, Portfolio Overlap Check, Threshold Sensitivity Elbow Curve, Auto-Balancing Sliders & Live 50k Portfolio Replay) | `COMPLETED` |
| **Phase 17** | Regulatory Reports, All 14 PDF Reports & WORM Audit Ledger Modernization (Route-Synced Report Hub, 16-Field Transaction Filter, 5-Dimension Analytics, 3d/7d SLA Watchdog, STR History Reopen/Branch Mail, Live WORM Chain Verifier & Certified Exports) | `COMPLETED` |
| **Phase 18** | Notification Admin & Webhook Reliability Modernization (Dead-Letter Replay, Live Multi-Channel CRUD, Shift Roster Engine, Global Notification Center & Test Sandbox) | `COMPLETED` |
| **Phase 19** | IAM Access System Workflow, UX/UI & Enterprise Security Modernization (In-Place Edit Drawer, Faceted Directory, Structured Offboarding, Hierarchical Sub-Menu Matrix, Active Sessions & Audit Console) | `COMPLETED` |
| **Phase 20** | System Masters & Calibration Hub Workflow, UX/UI & Feature Modernization (Full PostgreSQL CRUD Wiring, Auto-Balancing Sliders, Domain Separation, Currency Master, Real-Time Impact Calculator, Version History & 4-Eyes Staging) | `COMPLETED` |
| **Phase 21** | Delta Rescreening Workflow, UX/UI & System Modernization (Filter Bar Wiring, Closed-Loop Escalation, Token Diff Drawer & Delta Tests) | `COMPLETED` |

---

## Detailed Task Breakdown

### [x] Phase 1: Wire Customer KYC 360 to PostgreSQL
- [x] Connected `KycEndpoints.cs` to PostgreSQL `screening.customers`, `screening.customer_documents`, and `screening.transactions`.
- [x] Implemented `PostgreSqlKycService` with parameter-driven querying, dynamic UBO extraction, and risk tier resolution.
- [x] Seeded realistic customer dossiers (Al-Baraka General Trading LLC, Viktor Bout, Tariq Mansoor, Emirates Sky Logistics).
- [x] Verified `GET /api/v1/kyc/{customerId}` fetches real entity profile, UBOs, documents, and historical transactions.
- [x] Verified frontend `CustomerKyc360View.tsx` renders live database customer profile without mock fallback.

### [x] Phase 2: Wire System Masters & Scoring Weights to PostgreSQL
- [x] Connected `SettingsEndpoints.cs` to `rules.country_masters`, `rules.screening_score_configs`, and `rules.risk_rate_settings`.
- [x] Expose `GET /api/v1/settings/masters` pulling from live database tables.
- [x] Expose `PUT /api/v1/settings/scoring-weights` to update Name/Nationality/DOB/ID weights dynamically.
- [x] Expose `POST /api/v1/settings/countries` to add/update FATF Black, Grey, and Monitored lists.
- [x] Automatically record an immutable WORM audit log on every scoring weight or country master modification.

### [x] Phase 3: Wire WORM Immutable Audit Ledger to PostgreSQL
- [x] Connected `SettingsEndpoints.cs` / `ReportsAuditView.tsx` to `core.audit_ledgers`.
- [x] Implemented `PostgreSqlAuditLedgerService` with SHA-256 block hashing linking `previous_hash` $\to$ `current_hash`.
- [x] Seeded baseline genesis block and initial regulatory audit events.
- [x] Verified `GET /api/v1/reports/audit` and `POST /api/v1/reports/audit` read and write directly to `core.audit_ledgers`.

### [x] Phase 4: Multipart Bulk Screening File Upload Pipeline
- [x] Expose `POST /api/v1/bulk/upload` supporting `multipart/form-data` (CSV/JSON).
- [x] Integrated `AmlStreamingParser` to stream uploaded rows directly into `screening.bulk_screening_jobs`.
- [x] Persist skipped/invalid records to `screening.bulk_job_skipped`.
- [x] Linked SignalR `JobHub` progress updates to actual row chunks processed.
- [x] Expose `GET /api/v1/bulk/jobs/{id}/skipped` exporting skipped rows as JSON.

### [x] Phase 5: Live Delta Rescreening with Database Persistence
- [x] Connected `POST /api/v1/delta/sync` to query active customer profiles from `screening.customers`.
- [x] Rescreen against `screening.watchlist_entities` and OpenSearch delta stream.
- [x] Persist delta run into `screening.delta_sync_batches`.
- [x] Expose `POST /api/v1/delta/authorize` persisting approval into `screening.delta_authorizations`.

### [x] Phase 6: Single Entity & Transaction Screening Console Modernization (v1.2.0)
- [x] Overhauled `NameSearchView.tsx` with dedicated 1-click `Clear Search` / `Esc` keyboard shortcut and inline input `(X)` clears.
- [x] Implemented full coverage of all 6 single entity screening possibilities (Exact 100%, Fuzzy/Phonetic, Dual-Factor Name + ID, Clean Compliance Certificate, PEP Registry, Corporate Trade License).
- [x] Preserved on-click architecture overview diagram and per-match decision diagrams with score breakdown bars.
- [x] Resolved stale candidate match bug (`Candidate Matches Found (8)`): filtered below-threshold broad retrieval noise from `ScreeningEndpoints.cs` so cleared transactions return strictly 0 matches.
- [x] Added `updateFormData` and `setResult(null)` on presets and edits in `ScreeningConsole.tsx` to immediately clear stale results when searching with different details.
- [x] Added on-click `Explain Match Diagram` on candidate match cards in `ScreeningConsole.tsx` with interactive 4-step pipeline tracing.

### [x] Phase 7: Single Entity Recommended Enhancements (v1.3.0 Target)
- [x] **1-Click "Add to Whitelist" / False-Positive Exception**: Added 1-click button on candidate match cards (both `NameSearchView.tsx` & `ScreeningConsole.tsx`) with modal pre-filling entity UID, customer reference, match scope, and duration for 12-month suppression approval.
- [x] **Export Regulatory Screening Audit Certificate (PDF / JSON)**: Generated cryptographically signed proof containing query timestamp (UTC), system latency, 5-pass logs, and SHA-256 block hash for WORM regulatory compliance. Added JSON download and Print view.
- [x] **Engine Sensitivity & Pass Config Drawer**: Collapsible sensitivity drawer on `ScreeningConsole.tsx` allowing compliance officers to tune thresholds (e.g. 70% strict, 75% standard FATF, 85% domestic) and weight factors (Name, Nationality, DOB, ID).
- [x] **Direct 1-Click Intercept & Persistent Case Creation**: Added `/api/v1/screen/intercept-case` in `ScreeningEndpoints.cs` provisioning persistent AML cases in `cases.aml_cases` and linking directly into 3-Pane Investigation Workspace.
- [x] **Adverse Media & Secondary Identifier Quick Badges**: Highlighted Year of Birth (YOB) delta (`±2 years`, `Exact Match`) and Passport / National ID / Nationality match status as distinct badges.

### [x] Phase 8: Multi-tenant IAM User & Group Administration
- [x] Expose CRUD endpoints under `/api/v1/iam/users` (`core.users`) & `/api/v1/iam/groups` (`core.user_groups`, `core.group_menu_permissions`).
- [x] Implemented `IIamUserService` with `PostgreSqlIamUserService` and fallback `InMemoryIamUserService`.
- [x] Secure credential hashing using salted PBKDF2-SHA256 (`Rfc2898DeriveBytes.Pbkdf2`) with 100,000 iterations.
- [x] Dynamic group menu permission assignments and tenant isolation.
- [x] Integrated `IamAccessView.tsx` with live user/group creation, filtering, role selection, shift tracking, and deletion.

### [x] Phase 9: CBS Outbound Webhooks & Reliability
- [x] Added HMAC-SHA256 signature header (`X-Edith-Signature`) with tenant-specific shared secret.
- [x] Implemented exponential backoff retry policy using `Polly` / `HttpClient` (3 retry attempts at 1s, 2s, 4s with jitter).
- [x] Stored failed webhook deliveries into `notifications.delivery_ledgers` as a dead-letter log with full delivery status tracking.
- [x] Implemented `CbsWebhookDispatcher` and comprehensive test suite in `Edith.Events.Tests`.

### [x] Phase 10: Searches Module Advanced UX & Engine Modernization (v1.4.0)
- [x] **UX 3: Session Recent Searches Breadcrumb Bar**: Synchronized with `sessionStorage` displaying the last 5 searched entities with timestamp, score, and verdict pills (`CLEARED`, `HELD`, `BLOCKED`) with instant 1-click re-query.
- [x] **UX 4: Side-by-Side Visual Diff Inspector**: Two-column layout comparing Query Parameters vs Matched Watchlist Entity Record with green (exact alignment), amber (fuzzy/phonetic alignment), and red/gray (discrepancy) chips for Full Name, Aliases, DOB/YOB, Nationality, and National IDs.
- [x] **Engine 5: Client-Side "What-If" Sensitivity Thresholding**: Instant (<1ms) candidate re-filtering and live verdict recalculation when adjusting threshold sliders (50% to 95%) without incurring redundant network latency.
- [x] **Engine 6: PEP Tiering & Adverse Media Disaggregation**: Discrete tabs and categorization dividing hits into Tier 1 Sanctions (Statutory Block), Tier 2 PEP (EDD & Source of Wealth), and Tier 3 Adverse Media with live counts.
- [x] **Engine 7: Batch Clipboard Paste Screening**: Rapid multi-line screening supporting up to 10 names or IDs pasted simultaneously, executing parallel requests and presenting a compact triage matrix with 1-click single-search inspection.

### [x] Phase 11: Single Entity Enterprise Workflow Modernization (v1.5.0)
- [x] **Target-Type Persona Modes**: 3-way toggle for **Individual (Person)**, **Corporate Entity (Trade License / TRN / LEI)**, and **Maritime Vessel / Cargo (IMO Number / Flag State)** with adaptive dynamic inputs and tailored test presets.
- [x] **In-Line Clearance Adjudication Slip**: Instant sign-off bar on search results allowing analysts to record clearance justification (`False Positive`, `PEP Approved`, `Trade License Verified`, `Routine Clearance`) directly committing an immutable block into `core.audit_ledgers` with clearance reference ID `AML-CLR-XXXX` and SHA-256 block hash.
- [x] **Bridge to KYC 360 & UBO Tree**: Real-time customer dossier detection that matches screened names/IDs against `screening.customers`, rendering a "Linked Customer Dossier Identified" pill and slide-over KYC/UBO preview drawer.
- [x] **Onboarding AML Clearance Memo**: Printable, compliance-branded bank clearance memorandum ready for onboarding filing with analyst sign-off, exact lists checked, zero-hit verification, and WORM SHA-256 hash stamp.

### [x] Phase 12: Case Workspace Investigation Canvas Workflow & UX Modernization (v1.6.0 Target)
- [x] **1. Collapsible Vertical Case Queue & Status Filters**:
  - Compact, scrollable vertical case list / split-pane.
  - Interactive filter chips: `All Cases`, `My Locks`, `Escalated to MLRO`, `SLA Critical (<24h)`, and `Resolved`.
  - Instant client-side search by Case Number (`CAS-2026-XXXX`), Primary Subject Name, or Transaction Reference.
- [x] **2. Live Lock Lease Countdown & Multi-Duration Renewal (15m, 30m, 1hr)**:
  - Dynamic user-aware lock ownership check (comparing against current authenticated persona).
  - Real-time countdown timer badge with amber warning at `< 3:00` and critical alert at `< 1:00`.
  - Multi-duration lease renewal popover/dropdown with options: `15 Minutes`, `30 Minutes`, and `1 Hour`.
  - Locked-by-other banner surfacing active investigator name and lock expiration countdown with write-protection safeguards.
- [x] **3. Tabbed Multi-Angle Investigation Console (Pane 2)**:
  - **Tab 1: Token & Phonetic Diff**: Visual token breakdown, Jaro-Winkler scores, and designation details.
  - **Tab 2: UBO & Entity Relationship Graph**: Embedded multi-hop network visualizer rendering circular ownership and intermediary accounts from `Edith.Addons`.
  - **Tab 3: Transaction Corridor & AML Typology Rules**: Geographic remittance flow map, FATF Grey/Black list corridor badges, and tripped typology rule conditions (`rules.aml_rules`).
  - **Tab 4: KYC 360 Customer Profile Bridge**: Direct slide-in or tabbed view of linked customer profile, identity documents, risk score, and screening history from `screening.customers`.
- [x] **4. 4-Eyes Maker-Checker & Adjudication Workflows**:
  - **Direct Whitelist-on-Dismissal**: Checkbox toggle in MLRO false-positive modal calling `screening.whitelists` on sign-off.
  - **Enhanced Due Diligence (EDD) / RFI Transition**: Analyst ability to move case into `UnderReview (EDD)` with structured checklist (*Source of Funds*, *Invoice Copy*, *Secondary ID Verification*).
  - **Dual Rationale Presentation**: Side-by-side display of Maker's Escalation Rationale and MLRO's Final Sign-off Rationale in the audit ledger.
- [x] **5. Editable AI Copilot Narrative & 1-Click goAML XML Export**:
  - Interactive multi-paragraph STR narrative editor allowing analysts to modify the 5 generated FATF sections inline before applying directly into statutory case rationale.
  - 1-Click XML download button (`goAML_STR_{CaseNumber}.xml`) with syntax-highlighted review and FIU copy buffer.
- [x] **6. Categorized Note Types & Unified Audit Activity Stream**:
  - Note classification selector: `Investigation Note` (blue), `Supervisor Direction` (purple), and `Statutory Rationale` (amber) mapped to `cases.case_activities.note_type`.
  - Chronological activity timeline unifying user notes with automated system events.

### [x] Phase 13: EDITH Eagle SVG Brand Identity, Animated Loader & Role-Adaptive Enterprise Dashboard (v1.7.0 Target)
- [x] **1. Vectorized EDITH Eagle SVG Logo & Favicon**:
  - Heraldic EDITH Eagle & Radar emblem into multi-layered vector SVG paths (`EdithLogo.tsx` & `public/favicon.svg`) with enterprise Investment Navy (`#1a237e`), Sovereign Gold (`#ffd54f` / `#d97706`), and Signal Emerald (`#059669`) color themes.
  - Wired `/favicon.svg` into `index.html` and integrated `<EdithLogo />` into `Header.tsx`, `LoginView.tsx`, `DashboardView.tsx`, and printable compliance briefs.
- [x] **2. Animated SVG Eagle Radar Loading Component (`EdithLoader`)**:
  - Multi-part SVG loading animation featuring sequential radar arc sweeps across the 9 dashed segments, wing feather pulse waves, and telemetry status messages.
- [x] **3. General Role-Gated Enterprise Dashboard**:
  - Automatically show/hide charts, diagrams, and operational streams based on user access level (`ANALYST`, `EDD_ANALYST`, `AML_OFFICER`, `COMPLIANCE_OFFICER`, `MLRO`, `AUDITOR`, `ADMIN`).
  - Global Timeframe Sync (`1H | 24H | 7D`) and 1-Click Printable Executive PDF Brief modal.
- [x] **4. New Enterprise AML Charts & Architecture Diagrams**:
  - *Chart 1*: Screening Throughput & Intercept Velocity Chart.
  - *Diagram 2*: 5-Pass Deterministic Engine Resolution Funnel.
  - *Chart 3*: Case SLA Aging & 4-Eyes Maker-Checker Pipeline.
  - *Chart 4*: CBUAE / FATF Rec. 11 Governance & WORM Integrity Matrix.

### [x] Phase 14: Customer KYC 360 & EDD Dossier Workflow, UX & Feature Modernization (v1.8.0 Target)
- [x] **1. Live Backend Wiring, Tenant Scoping & Baseline Customer Dossier Seeding**:
  - Seeded realistic high-risk corporate, PEP, and sanctioned customer dossiers (`screening.customers`, `screening.customer_documents`, `screening.transactions`, `screening.customer_screening_history`).
  - Expose `GET /api/v1/kyc`, `GET /api/v1/kyc/{customerId}`, and `PUT /api/v1/kyc/{customerId}/edd`.
- [x] **2. Deep-Linkable Customer Switcher & EDD Disposition Action Bar**:
  - Live customer search/selector bar in `CustomerKyc360View.tsx` synced with `?customer=` URL query parameter.
  - Actionable EDD Review & Sign-Off bar writing immutable SHA-256 WORM entries to `core.audit_ledgers`.
- [x] **3. Interactive UBO Ownership Tree & 1-Click Cascade Screening**:
  - Visual UBO ownership hierarchy with equity share bars, PEP/Sanctions badges, and per-UBO screening links.
  - 1-Click **"Cascade Screen Corporate & UBO Tree"** running parallel 5-pass screening across entity and all UBOs.
- [x] **4. Interactive OCR Document Inspector Modal & Document Verification**:
  - Inspection drawer showing extracted JSONB fields, per-field OCR confidence bars, expiry countdown badges, and status toggles.
- [x] **5. SoW / SoF Checklist, Activity Drift Monitor & Signed EDD Dossier Certificate**:
  - Interactive SoW/SoF EDD corroboration checklist tracking required statutory evidence.
  - Expected vs. Actual 90-Day Activity Drift monitor highlighting unapproved high-risk corridors.
  - Printable/downloadable WORM-sealed Customer EDD Audit Dossier.

### [x] Phase 15: AML Rule Builder & Sandbox Workflow, UX & Feature Modernization (v1.9.0 Target)
- [x] **1. Complete Backend Rule Engine (`VelocityAnomaly`), Execution Modes & WORM Audit Wiring**:
  - Implemented `RuleCategory.VelocityAnomaly` evaluation in `IRuleEngine.cs` and rolling-window burst detection in `IRuleBacktester.cs`.
  - Persist `ExecutionMode` (`REALTIME`, `SHADOW`, `PENDING_APPROVAL`) in `rules.aml_rules.execution_mode`.
- [x] **2. Full CRUD, Category-Aware Parameter Editor & Catalog Filtering in `RuleBuilderView.tsx`**:
  - Search input and status/category filter pills (`ALL`, `ACTIVE`, `SHADOW`, `CRITICAL`).
  - Category-aware parameter form editor (`TRANSACTION_THRESHOLD`, `HIGH_RISK_CORRIDOR`, `STRUCTURING_SMURFING`, `VELOCITY_ANOMALY`).
- [x] **3. Pre-Save "What-If" Draft Backtesting & Sample Triggered Hit Inspector**:
  - Upgraded `POST /api/v1/rules/backtest` to evaluate unsaved draft parameters directly against historical transactions.
  - Interactive **Sample Triggered Transactions** table showing reference numbers, corridors, amounts, and trigger reasons.
- [x] **4. Connect `RuleSandboxView.tsx` "Promote to Active Config" to Live System Masters**:
  - Hydrated sliders from `GET /api/v1/settings/masters` and wired **"Promote to Active Config"** to `PUT /api/v1/settings/scoring-weights`.

### [x] Phase 16: AML Rule Backtesting Sandbox & Calibration Engine Modernization (v2.0.0 Target)
- [x] **1. Baseline vs. Candidate Delta, Portfolio Overlap & Rolling-Window Structuring Engine**:
  - Evaluates both candidate and saved baseline rules simultaneously, detects rule overlap against active catalog, and generates 5-point `SensitivityCurve`.
- [x] **2. Cohort-Aware Backtesting & Portfolio Scoring Sandbox Endpoint**:
  - Added cohort dataset selection (`STANDARD_MIX`, `HIGH_RISK_CORRIDOR_SURGE`, `SMURFING_RING_ATTACK`, `VELOCITY_BURST_STRESS`).
  - Exposed `POST /api/v1/rules/sandbox/simulate` evaluating 50,000-record archive projections.
- [x] **3. Interactive Sensitivity "Elbow Curve", Sample Delta Filters & KYC 360 Bridge in `RuleBuilderView.tsx`**:
  - Clickable 5-step **Threshold Sensitivity "Elbow Curve"** to apply optimal thresholds in 1 click.
  - Sample Impacted Transactions table with `NEWLY_CAPTURED` / `NEWLY_SUPPRESSED` / `STR_FILED` filter pills.
- [x] **4. Auto-Balancing Weightage Sliders, Live Histogram & Staged Shadow Promotion in `RuleSandboxView.tsx`**:
  - Auto-Balance 100% with per-slider lock pins plus 1-click Normalize to 100%.
  - Live `EdithApi.simulateSandboxPortfolio` telemetry and Stage 7-Day Shadow vs. Promote to Active Config actions.

### [x] Phase 17: Regulatory Reports, All 14 PDF Reports & WORM Audit Ledger Modernization (v2.1.0 Target)
- [x] **1. Live WORM Audit Ledger API, Baseline Auto-Seeding & Cryptographic Chain Verification**:
  - Deterministic chained SHA-256 fallback blocks (`previousBlockHash` $\to$ `sha256Hash` down to `GENESIS`).
  - Interactive **Verify WORM Cryptographic Chain** in `ReportsAuditView.tsx` with block payload inspector drawer.
- [x] **2. Complete Coverage of All 6 PDF Section 4e Reports in `ReportsAuditView.tsx`**:
  - Alert Reports, Transaction Report (16-field filter bar), Blacklist Report, Transaction Release Report, Online STR Report, Transaction Analytics.
- [x] **3. Complete Coverage of All 5 PDF Section 4f Log Reports & 3 Cross-Module Reports**:
  - Process Log, Upload Log, Suspicious Log, Custom Reports, Monthly Screen Data Report, STR History, Customer Screening Report, Delta Screening Log Report.
- [x] **4. Self-Auditing CSV/JSON Exports & Printable WORM Regulatory Attestation Certificate**:
  - Real `.csv` download with embedded WORM SHA-256 hash footer.
  - Printable **Certified Regulatory Examination Report Modal** (`window.print()` + JSON bundle download).

### [x] Phase 18: Notification Admin & Webhook Reliability Modernization (v2.2.0 Target)
- [x] **1. Navigation, Page Metadata & Global Header Notification Center**:
  - Re-aligned `/notifications` route metadata in `pageMeta.ts` from mislabeled `"Log Report"` to `"Notification & Webhook Admin"`.
  - Added explicit entry in `Sidebar.tsx` / `api.ts` menu entitlements under Settings and unified with Command Palette (`Ctrl+K`).
  - Upgraded the inert bell button in `Header.tsx` to an active sliding **Notification Inbox Drawer** with unread count badge, real-time alert triage feed, acknowledge actions, and deep links into `/cases`.
- [x] **2. Full CRUD Visual Rule Authoring Drawer & Multi-Condition Trigger Engine**:
  - Replaced static `MOCK_RULES` with dynamic state and persistent storage (`notifications.notification_rules`).
  - Interactive drawer for rule creation/editing: Alert Type, Severity Tier (`CRITICAL`, `HIGH`, `MEDIUM`, `LOW`), Trigger Event, Target Channels (`TEAMS`, `EMAIL`, `SMS`, `WEBHOOK`), and Shift-Presence toggle.
  - Action buttons: Add New Rule, Edit Rule, Clone Rule, Toggle Active, and Four-Eyes Delete Protection.
- [x] **3. Delivery Ledgers & Dead-Letter Queue (DLQ) Reconciliation Console**:
  - Dedicated Ledgers & DLQ tab backed by `notifications.delivery_ledgers` and `CbsWebhookDispatcher`.
  - Filter by status (`ALL`, `DELIVERED`, `DEAD_LETTER`), channel, and trace ID.
  - Interactive 1-click **"Replay Failed Callback"** and **"Bulk Replay All Dead-Letters"** with live retry status updates.
  - Diagnostic payload inspector drawer displaying request headers (`X-Edith-Signature`, `X-Edith-Timestamp`), payload body, and remote HTTP error responses.
- [x] **4. Shift Schedule Roster & On-Duty Substitution Management**:
  - Connected UI to `notifications.user_shifts` and `DynamicNotificationEngine.cs`.
  - Visual duty roster displaying current shift periods (APAC, EMEA, Americas) and on-duty analysts.
  - On-call substitution delegation: when an analyst marks `OFF_DUTY`, nominate a substitute analyst or fallback to MLRO On-Duty Escalation.
- [x] **5. Dynamic Multi-Channel Simulation Studio & Pre-Flight Test-Fire Sandbox**:
  - Upgraded static Teams card preview to an interactive 4-channel simulation studio: Microsoft Teams Adaptive Card, HTML Email template, SMS (160 char preview), and Outbound Webhook JSON payload.
  - Dynamic token chips insertion (`{{CaseRef}}`, `{{MatchedName}}`, `{{Amount}}`, `{{RiskScore}}`, `{{ActionUrl}}`).
  - Pre-flight **"Test Fire Alert"** action sending mock/synthetic compliance alerts to verify endpoint connectivity, response latency, and rendering before publishing.
- [x] **6. Backend Gateway API Endpoints & Service Integration**:
  - Implemented notification and webhook endpoints in `Edith.Gateway` (`NotificationEndpoints.cs`) and portal service layer (`EdithApi`) supporting rules CRUD, delivery ledger queries, manual retry execution, shift updates, in-app notification center, and test dispatch.

### [x] Phase 19: IAM Access System Workflow, UX/UI & Enterprise Security Modernization (v2.3.0 Target)
- [x] **1. Faceted Filtering & Exportable User Inventory Console (`IamAccessView.tsx`)**:
  - Multi-criteria filter bar: Filter by Role (`ALL`, `ADMIN`, `AML_OFFICER`, `EDD_ANALYST`, `ISTR_MLRO`), Shift Status (`ALL`, `ON_DUTY`, `OFF_DUTY`), 2FA Enrollment (`ALL`, `ENROLLED`, `PENDING_ACTIVATION`), and Account State (`ACTIVE`, `SUSPENDED`).
  - 1-Click CSV export of current filtered compliance officer directory with role, department, shift status, and 2FA health for regulatory access reviews.
- [x] **2. Slide-Out Officer Detail & In-Place Editing Drawer (`UserDrawer`)**:
  - Interactive profile drawer on row click for inspecting and updating Full Name, Department, and Assigned Role.
  - Quick Security Actions: 1-Click 2FA Secret Reset (invalidates TOTP secret, re-flags `mfa_enabled = false`, and forces re-enrollment on next login), Trigger Mandatory Password Rotation, and Soft Account Suspension toggle (`is_active = false`).
- [x] **3. Structured Offboarding & Case Reassignment Confirmation Modal**:
  - Replaces harsh browser `window.confirm` with a rich dialog modal with danger styling.
  - Simulates/detects pending sanctions cases and prompts selecting an active On-Duty officer for clean handover before de-provisioning.
- [x] **4. Onboarding Setup Link Fallback & Direct Sharing**:
  - Return `invitationToken` from backend provisioning and resend endpoints.
  - Provide a 1-click **"Copy Onboarding Setup Link"** in the provisioning result modal to support on-premise deployments where outbound SMTP is firewalled.
- [x] **5. Hierarchical Sub-Menu Permissions Matrix & Column Bulk Toggles**:
  - Accordion expand/collapse on all 12 root menus in `MENU_MAPPINGS` tab to view and configure individual sub-menus (`Sanction Screening`, `Watchlist`, `Freeze/Unfreeze`, `Customize Rules`, etc.).
  - Role column header bulk controls: **"Grant All"** / **"Clear All"** for rapid role profile provisioning.
- [x] **6. Active Sessions, Concurrent Logins & Security Audit Ledger Tab**:
  - 5th tab `AUDIT_SESSIONS`: Live active operator sessions monitor with client IP, last active timestamp, and 1-click **"Revoke / Kill Session"**.
  - Visual timeline feed of recent IAM security actions (user creation, 2FA enrollment, password rotation, permission modifications) committed to `core.audit_ledgers`.
- [x] **7. Backend Gateway Endpoints & Service Layer Updates**:
  - Upgraded `UpdateIamUserRequest` in `IIamUserService.cs` and `PostgreSqlIamUserService` to support updating `FullName`, `Department`, `Role`, `IsActive`, `ShiftStatus`, and resetting `MfaEnabled`.
  - Added direct onboarding link token support in `CreateIamUserResponse`.
  - Updated `EdithApi` in `api.ts` to support expanded user updates and audit sessions.

### [x] Phase 20: System Masters & Calibration Hub Workflow, UX/UI & Feature Modernization (v2.4.0 Target)
- [x] **1. Full PostgreSQL CRUD Wiring & Missing Endpoints in Gateway (`SettingsEndpoints.cs`, `ISystemMastersService.cs`)**:
  - Persist `rules.risk_rate_settings` (Customer & Transaction Risk Benchmarks).
  - Persist `rules.setup_controls` (Automated SysControl scenario toggles).
  - Persist `rules.risk_assessment_masters` (EWRA category weights: Geo, Client, Product).
  - Persist `rules.nationality_masters` (Nationality demonyms catalog with risk tiers & high-risk flags).
  - Expose Currency & Mandatory Reporting Thresholds (`AED`, `USD`, `EUR`) with update API and WORM audit logging.
- [x] **2. Auto-Balancing Constraint Sliders with Pin-Locks & Real-Time Math**:
  - Strict 100% sum constraint on Multi-Factor Screening Weights (`Name`, `Nationality`, `DOB`, `ID`) preventing negative ID weights.
  - Strict 100% sum constraint on Enterprise-Wide Risk Assessment (EWRA) weights.
  - Individual pin-lock controls to fix key weights while auto-balancing the remaining sliders.
- [x] **3. Domain Separation & Information Architecture Consolidation**:
  - Eject `UNBLOCK_CUSTOMER` from System Masters; relocate to an authenticated Administrative Hold / Unblock Action in `CustomerKyc360View.tsx`.
  - Move `CHANGE_PASSWORD` out of System Masters to `UserProfileDrawer.tsx`.
  - Introduce **Currency & Reporting Thresholds** tab.
  - Consolidate navigation under unified categories with dirty-state indicator.
- [x] **4. High-Density Country & Nationality Master Grids**:
  - Hydrate full country names, ISO-2/3 codes, flags, and accurate FATF status badges (`BLACKLIST`, `GREYLIST`, `MONITORED`, `COOPERATIVE`).
  - Search debounce, risk tier filter chips, and inline risk score adjustment.
- [x] **5. Real-Time "What-If" Sensitivity & Alert Volume Impact Preview**:
  - Live impact mini-calculator showing estimated alert volume surge, SLA impact, and historical recall when adjusting match thresholds.
- [x] **6. Configuration Version History & Rollback Drawer**:
  - Interactive slide-over drawer showing immutable configuration change timeline from `core.audit_ledgers` with side-by-side diffs and 1-click restore.
- [x] **7. 4-Eyes Maker-Checker Configuration Staging**:
  - Option to stage parameter updates as `PENDING_APPROVAL` drafts requiring MLRO cryptographic sign-off before committing to the live 5-pass screening engine.

### [x] Phase 21: Delta Rescreening Workflow, UX/UI & System Modernization (v2.5.0 Target)
- [x] **1. Filter Bar Wiring, Quick Search & Active Preset Chips (`DeltaRescreeningView.tsx`)**:
  - Connected previously disconnected filter states (`filterCustomerCode`, `filterStartDate`, `filterEndDate`) to live `useMemo` table filtering and CSV export.
  - Quick-search across Customer Name, Code, Source List, and Watchlist Rationale.
  - Preset filter chips: `Critical Jumps (Δ ≥ 50%)`, `Pending Review`, `OFAC Updates`, `EU/UN Designations`.
- [x] **2. Closed-Loop Case Provisioning & Deep Links into Case Workspace**:
  - Escalating a delta hit calls `POST /api/v1/delta/escalate-case`, automatically provisioning an active EDD case in `cases.aml_cases`.
  - Display clickable badge `Case #{caseNumber} →` linking directly into `CaseWorkspace.tsx` with pre-filled delta provenance.
- [x] **3. Atomic Drawer Adjudication & DB Persistence**:
  - Connect "Mark False Positive" and "Create Alert Case" in inspector drawer to `POST /api/v1/delta/adjudicate`.
  - Require justification rationale and commit decision to `screening.delta_authorizations` and `core.audit_ledgers`.
- [x] **4. 4-Eyes Maker-Checker Governance & Dual Review Controls**:
  - Maker stage: Analyst recommends dismissal or escalation with regulatory reason code (`SECONDARY_ID_MISMATCH`, `ALIAS_CONFIRMED`, `DIVERGENT_DOB`).
  - 1-Click AI Auto-Draft rationale generator for regulatory justification.
  - Checker stage: MLRO reviews rationale and counter-signs with cryptographic WORM ledger attestation.
- [x] **5. Side-by-Side "What Changed" Visual Token Diff Drawer**:
  - Display side-by-side snapshot comparing previous watchlist state vs. updated watchlist entry.
  - Visual token chips highlighting newly added aliases, modified dates/nationalities, and matching tokens.
- [x] **6. Sub-Route / Tab Separation & Watchlist Feed Health Watermark**:
  - Distinct views for **Active Authorization Queue** (`/delta/authorization`), **Delta Screening Log Report** (`/delta/log-report`, PDF p. 59), and **Watchlist Source Health** (`/delta/sources`).
  - Real-time watermark banner tracking OFAC, EU, UN, and UAE list freshness and sync status.
- [x] **7. Bulk Action Safety Confirmation Modal**:
  - Safety dialog confirming selected hits, count, and requiring mandatory justification before executing batch dismissals or escalations.
- [x] **8. Backend Gateway Scoped Authorization & User Claims Hardening**:
  - Refactor `POST /api/v1/delta/authorize` in `BulkScreeningEndpoints.cs` to filter strictly by approved customer IDs instead of updating all batch items blindly.
  - Extract genuine user identity from JWT claims/headers instead of hardcoding MLRO name.
  - Added new endpoints: `/api/v1/delta/adjudicate`, `/api/v1/delta/escalate-case`, `/api/v1/delta/sources`, and `/api/v1/delta/log-report`.
  - Added unit test suite `DeltaRescreeningTests.cs` verifying nationality weighting and threshold filtering (all 13 bulk tests passing).

---
*Maintained by the EDITH Architecture & Compliance Engineering Team (v10.2).*
