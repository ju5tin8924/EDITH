# EDITH Platform Core Modernization & Production Readiness TODO
**Document Version:** 1.6.0  
**Date:** October 21, 2026  
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
  - Replace the horizontal top 3-column card block with a compact, scrollable vertical case list / split-pane.
  - Interactive filter chips: `All Cases`, `My Locks`, `Escalated to MLRO`, `SLA Critical (<24h)`, and `Resolved`.
  - Instant client-side search by Case Number (`CAS-2026-XXXX`), Primary Subject Name, or Transaction Reference.
- [x] **2. Live Lock Lease Countdown & Multi-Duration Renewal (15m, 30m, 1hr)**:
  - Dynamic user-aware lock ownership check (comparing against current authenticated persona, removing hardcoded user strings).
  - Real-time countdown timer badge (`🔒 Locked by You (12:45 remaining)`) with amber warning at `< 3:00` and critical alert at `< 1:00`.
  - Multi-duration lease renewal popover/dropdown with options: `15 Minutes`, `30 Minutes`, and `1 Hour`.
  - Locked-by-other banner surfacing active investigator name and lock expiration countdown with write-protection safeguards.
- [x] **3. Tabbed Multi-Angle Investigation Console (Pane 2)**:
  - **Tab 1: Token & Phonetic Diff**: Visual token breakdown, Jaro-Winkler scores, and designation details.
  - **Tab 2: UBO & Entity Relationship Graph**: Embedded multi-hop network visualizer rendering circular ownership and intermediary accounts from `Edith.Addons`.
  - **Tab 3: Transaction Corridor & AML Typology Rules**: Geographic remittance flow map, FATF Grey/Black list corridor badges, and tripped typology rule conditions (`rules.aml_rules`).
  - **Tab 4: KYC 360 Customer Profile Bridge**: Direct slide-in or tabbed view of linked customer profile, identity documents, risk score, and screening history from `screening.customers`.
- [x] **4. 4-Eyes Maker-Checker & Adjudication Workflows**:
  - **Direct Whitelist-on-Dismissal**: Checkbox toggle in MLRO false-positive modal (`[x] Auto-provision Whitelist Exemption for Beneficiary (Scope: Exact Name, Expiry: 1 Year)`) calling `screening.whitelists` on sign-off.
  - **Enhanced Due Diligence (EDD) / RFI Transition**: Analyst ability to move case into `UnderReview (EDD)` with structured checklist (*Source of Funds*, *Invoice Copy*, *Secondary ID Verification*).
  - **Dual Rationale Presentation**: Side-by-side display of Maker's Escalation Rationale and MLRO's Final Sign-off Rationale in the audit ledger.
- [x] **5. Editable AI Copilot Narrative & 1-Click goAML XML Export**:
  - Interactive multi-paragraph STR narrative editor allowing analysts to modify the 5 generated FATF sections inline before applying directly into statutory case rationale.
  - 1-Click XML download button (`goAML_STR_{CaseNumber}.xml`) with syntax-highlighted review and FIU copy buffer.
- [x] **6. Categorized Note Types & Unified Audit Activity Stream**:
  - Note classification selector: `Investigation Note` (blue), `Supervisor Direction` (purple), and `Statutory Rationale` (amber) mapped to `cases.case_activities.note_type`.
  - Chronological activity timeline unifying user notes with automated system events (*CBS Interceptor Hold*, *Lock Acquired*, *Escalated to MLRO*, *CBS Release Webhook Delivered*).
