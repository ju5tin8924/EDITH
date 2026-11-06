# EDITH Platform — Versioned Documentation Release History

**Project:** EDITH Next-Gen AML Intelligence Platform  
**Documentation Root:** `F:\EDITH\MD_DOC\`  
**Release Naming Convention:** `EDITH_Documentation_vMAJOR.MINOR`  
**Current Active Release:** [`EDITH_Documentation_v10.4`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4)  
**Last Updated:** November 6, 2026  

---

## Release Ledger

### Release `v10.4` (Current Promoted Release)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.4\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4)
* **Previous Release:** `EDITH_Documentation_v10.3`
* **Creation & Promotion Date:** November 6, 2026
* **Release Status:** `Validated & Promoted`
* **Pointer Status:** Active (`EDITH_Documentation_CURRENT.txt` points to `EDITH_Documentation_v10.4`)
* **Core Drivers & Release Scope:**
  - **Phase 23 Milestone Completion (Architecture Hardening & Resilience):**
    - Gated direct anonymous token minting (`POST /api/v1/iam/token`) behind `env.IsProduction()`, returning HTTP 403 ProblemDetails in production and enforcing real PBKDF2/TOTP login.
    - Fail-fast production persistence preventing silent in-memory state loss during database connection failures.
    - Centralized `apiFetch` in `api.ts` across 77 endpoints with 401 session expiry and 409 conflict handling.
    - Database indexing migrations: `0003_cases_performance_indexes.sql` and `0003_audit_ledgers_indexes.sql`.
    - Dynamic route-based code splitting in `App.tsx` reducing initial bundle size to 402 KB (120 KB gzip).
    - Active case lock heartbeat and $\le 120$s expiration countdown warning banner in `CaseWorkspace.tsx`.
  - **Phase 24 Milestone Completion (Enterprise Error Handling & Messaging):**
    - Multi-channel notification framework (`notify.success/info/warning/error`) in `errorEngine.ts`.
    - End-to-end correlation tracking (`EDITH-ERR-XXXXXX`) linking frontend envelopes with backend `ILogger` entries.
    - RFC 7807 / RFC 9457 `ProblemDetails` compliance with regex anti-leak shield redacting SQL queries, tables, and credentials.
    - Complete elimination of native browser `alert()` dialogs across all 17 master views.
    - Stacked accessible feedback toasts (`ErrorEngineUi.tsx`) with inline action buttons (`Retry`, `Refresh`, `Undo`, `View Case`).
    - Backend Gateway global exception middleware (`GlobalErrorHandlingMiddleware.cs`) using zero-allocation structured `LoggerMessage`.
  - **Expansion of Workflows to 26:** Added WF-25 (Active Case Investigation Lock Heartbeat & Contention Resolution) and WF-26 (Enterprise Error Sanitization, RFC 7807/9457 ProblemDetails & Anti-Leak Redaction).
  - **Core Mechanism Expansions:** Added Mechanism 11 (Enterprise Error Handling Engine, RFC 7807/9457 ProblemDetails & Anti-Leak Redaction) and Mechanism 12 (Active Case Lock Heartbeat, Concurrency & Lease Expiration Governance) to the definitive master reference.
  - **Automated Verification Matrix:** 130 automated test cases across 10 test projects achieving a **100% green pass rate** (0 failures, 0 skipped).
  - **Compiled Print-Ready PDF:** Generated [`EDITH_Client_Architecture_Pitch_v10.4.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.4.pdf) (442 KB) via Google Chrome headless.
  - **Packaged & Verified Library Archive:** Compiled [`EDITH_Documentation_Library_v10.4.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/EDITH_Documentation_Library_v10.4.zip) (362 KB, 18 files).
* **Generated Artifact Status:**
  - **Comprehensive Master Document:** [`EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.4.md`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/03_Technical_Architecture_and_Database/EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.4.md) (77.3 KB).
  - **HTML:** Generated and validated ([`EDITH_Client_Architecture_Pitch_v10.4.html`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.4.html)).
  - **PDF:** Compiled via Google Chrome headless ([`EDITH_Client_Architecture_Pitch_v10.4.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.4.pdf), 442 KB).
  - **ZIP Archive:** Verified integrity with 18 entries ([`EDITH_Documentation_Library_v10.4.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/EDITH_Documentation_Library_v10.4.zip), 362 KB).
  - **Checkpoint:** Recorded in [`sync_checkpoint_v10.4.json`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.4/sync_checkpoint_v10.4.json).
* **Cleanup Actions:**
  - Retained preceding validated baselines `EDITH_Documentation_v10.3`, `EDITH_Documentation_v10.2`, `EDITH_Documentation_v10.1`, and historical baseline `EDITH_Documentation_v10.0`.
  - Safely purged temporary staging directory `F:\EDITH\MD_DOC\.staging`.
* **Outstanding Discrepancies:** None.

---

### Release `v10.3` (Preceding Validated Release)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.3\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3)
* **Previous Release:** `EDITH_Documentation_v10.2`
* **Creation & Promotion Date:** November 6, 2026
* **Release Status:** `Archived Validated Baseline`
* **Pointer Status:** Superseded by `v10.4`
* **Core Drivers & Release Scope:**
  - Phase 22 Milestone Completion: Blacklist / Watchlist & Whitelist Governance Modernization (`WhitelistGovernanceView.tsx`, `POST /api/v1/watchlist/sync`, soft-revocation modal, pre-commit blast-radius simulation, 4-Eyes attribution).
  - 4-Source Watchlist Aggregation & Refinitiv World-Check integration (`screening.watchlist_entities`).
  - Keyset streaming sync and cursor pagination (`WHERE id > @LastSeenId ORDER BY id ASC LIMIT @BatchSize`).
  - Advanced search modes in `ScreeningConsole` & `NameSearchView` (`EXACT NAME`, `ID + NAME`, `PHONETIC NAME MATCH`).
  - IAM session management: inactivity idle timer modal, `localStorage` multi-tab sync, server logout, route role guards.
  - Expansion of workflows to 24 (WF-23 and WF-24).
  - Definitive master reference guide: `EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.3.md` (69.8 KB).

---

### Release `v10.2` (Historical Validated Baseline)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.2\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2)
* **Previous Release:** `EDITH_Documentation_v10.1`
* **Creation & Promotion Date:** October 27, 2026
* **Release Status:** `Archived Validated Baseline`
* **Pointer Status:** Superseded by `v10.3`
* **Core Drivers & Release Scope:**
  - Authored all-in-one comprehensive platform master reference document covering all 10 modules, all 22 workflows, 8 mechanisms, 39 tables ER topology, 17 views, and REST API catalog.
  - Sanitization of unwanted comparison cache terms.

---

### Release `v10.1` (Historical Validated Baseline)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.1\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1)
* **Previous Release:** `EDITH_Documentation_v10.0`
* **Creation & Promotion Date:** October 26, 2026
* **Release Status:** `Archived Validated Baseline`
* **Pointer Status:** Superseded by `v10.2`
* **Relevant Change Register Tasks (from `TODO_v10.1.md`):**
  - **Phase 18:** Notification Admin & Webhook Reliability Modernization.
  - **Phase 19:** IAM Access System Workflow, UX/UI & Enterprise Security Modernization.
  - **Phase 20:** System Masters & Calibration Hub Workflow, UX/UI & Feature Modernization.
  - **Phase 21:** Delta Rescreening Workflow, UX/UI & System Modernization.

---

### Release `v10.0` (Initial Migration Baseline)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.0\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.0)
* **Previous Release:** Initial flat structure migration
* **Creation Date:** October 25, 2026 (Migrated October 26, 2026)
* **Release Status:** `Archived Validated Baseline`
* **Relevant Change Register Tasks:**
  - Documented Phases 1–17: Dual-DB infrastructure, 5-pass OpenSearch engine, streaming bulk ingestion, 15m lease locks, Customer KYC 360 & UBO cascade screening, AML Rule Builder & 50k portfolio calibration sandbox, all 14 regulatory PDF reports hub, and live WORM cryptographic chain verification.
