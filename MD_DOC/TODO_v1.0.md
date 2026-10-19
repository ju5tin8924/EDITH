# EDITH Platform Core Modernization & Production Readiness TODO
**Document Version:** 1.2.0  
**Date:** October 19, 2026  
**Status:** In Active Execution  
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
| **Phase 7** | Single Entity Recommended Enhancements (Whitelist Bypass, PDF Audit, Sensitive Drawer) | `PENDING (v1.3.0)` |
| **Phase 8** | IAM User & Group Administration CRUD Endpoints (`core.users`) | `PENDING` |
| **Phase 9** | CBS Webhook Reliability (HMAC-SHA256 Signatures & Retries) | `PENDING` |

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

### [ ] Phase 7: Single Entity Recommended Enhancements (v1.3.0 Target)
- [ ] **1-Click "Add to Whitelist" / False-Positive Exception**: Add button on candidate cards (70%–85% score range) with modal pre-filling entity UID, customer reference, and match scope for fast 12-month suppression approval.
- [ ] **Export Regulatory Screening Audit Certificate (PDF / JSON)**: Generate a cryptographically signed proof note containing query timestamp (UTC), system latency, 5-pass logs, and SHA-256 block hash for WORM regulatory compliance.
- [ ] **Engine Sensitivity & Pass Config Drawer**: Collapsible sensitivity drawer on `ScreeningConsole.tsx` allowing compliance officers to tune thresholds (e.g. 75% for high-risk corridors or 90% for domestic clearing) without modifying backend configs.
- [ ] **Direct 1-Click Intercept & Persistent Case Creation**: Automatically provision persistent case in `cases.aml_cases` from transaction console and link directly into 3-Pane Workspace.
- [ ] **Adverse Media & Secondary Identifier Quick Badges**: Highlight Year of Birth (YOB) delta (`±2 years`) and Passport/National ID match status as distinct badges.

### [ ] Phase 8: Multi-tenant IAM User & Group Administration
- [ ] Expose CRUD endpoints under `/api/v1/iam/users` (`core.users`).
- [ ] Expose CRUD endpoints under `/api/v1/iam/groups` (`core.user_groups`, `core.group_menu_permissions`).
- [ ] Store hashed credentials using standard password hashing.
- [ ] Support dynamic group menu permission assignments.

### [ ] Phase 9: CBS Outbound Webhooks & Reliability
- [ ] Add HMAC-SHA256 signature header (`X-Edith-Signature`) with shared secret.
- [ ] Implement exponential backoff retry policy using `Polly` / `HttpClient`.
- [ ] Store failed webhook deliveries into `notifications.delivery_ledgers` as a dead-letter log.
