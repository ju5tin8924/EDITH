# EDITH Platform — Versioned Documentation Release History

**Project:** EDITH Next-Gen AML Intelligence Platform  
**Documentation Root:** `F:\EDITH\MD_DOC\`  
**Release Naming Convention:** `EDITH_Documentation_vMAJOR.MINOR`  
**Current Active Release:** [`EDITH_Documentation_v10.3`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3)  
**Last Updated:** November 6, 2026  

---

## Release Ledger

### Release `v10.3` (Current Promoted Release)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.3\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3)
* **Previous Release:** `EDITH_Documentation_v10.2`
* **Creation & Promotion Date:** November 6, 2026
* **Release Status:** `Validated & Promoted`
* **Pointer Status:** Active (`EDITH_Documentation_CURRENT.txt` points to `EDITH_Documentation_v10.3`)
* **Core Drivers & Release Scope:**
  - **Phase 22 Milestone Completion:** Blacklist / Watchlist & Whitelist Governance Modernization (`WhitelistGovernanceView.tsx`, `POST /api/v1/watchlist/sync`, soft-revocation modal, pre-commit blast-radius simulation, 4-Eyes attribution).
  - **4-Source Watchlist Aggregation & Refinitiv World-Check Integration:** Multi-source ingestion into `screening.watchlist_entities` with source distribution charts and schema alignment across OFAC SDN, UN Consolidated, EU Financial Sanctions, and Refinitiv World-Check.
  - **Keyset Streaming Sync & Cursor Pagination:** High-throughput streaming sync (`WHERE id > @LastSeenId ORDER BY id ASC LIMIT @BatchSize`) eliminating deep-paging memory overhead.
  - **Advanced Entity Search Modes:** Hardened `ScreeningConsole` & `NameSearchView` with `EXACT NAME`, `ID + NAME`, and `PHONETIC NAME MATCH` operational modes.
  - **IAM Session Management Modernization:** Inactivity idle timer modal, `localStorage` multi-tab logout synchronization, explicit server-side token revocation, and route role guards.
  - **Expansion of Workflows to 24:** Added WF-23 (Internal Watchlist Authorization & Blast-Radius Simulation) and WF-24 (Multi-Source Watchlist Aggregation & Keyset Sync).
  - **Core Mechanism Expansions:** Added Mechanism 9 (Keyset Streaming Sync & Cursor Pagination) and Mechanism 10 (Pre-Commit Blast-Radius Simulation & Impact Assessment) to the platform master reference.
  - **Definitive Comprehensive Master Reference Document:** Authored [`EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.3.md`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/03_Technical_Architecture_and_Database/EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.3.md) (69.8 KB) detailing all 10 modules, all 24 workflows, 10 mathematical mechanisms, 39 tables, 17 views, and 112 automated test cases matrix.
  - **Zero Unwanted Cache Terms:** Complete elimination and sanitization of unwanted comparison cache terms (e.g., "Redis", "Zero Redis") across all documents in `v10.3`.
  - **Compiled Print-Ready PDF:** Generated [`EDITH_Client_Architecture_Pitch_v10.3.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.3.pdf) (442 KB) via Google Chrome headless.
  - **Packaged & Verified Library Archive:** Compiled [`EDITH_Documentation_Library_v10.3.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/EDITH_Documentation_Library_v10.3.zip) (355 KB, 18 files).
* **Generated Artifact Status:**
  - **Comprehensive Master Document:** [`EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.3.md`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/03_Technical_Architecture_and_Database/EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.3.md) (69.8 KB).
  - **HTML:** Generated and validated ([`EDITH_Client_Architecture_Pitch_v10.3.html`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.3.html)).
  - **PDF:** Compiled via Google Chrome headless ([`EDITH_Client_Architecture_Pitch_v10.3.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.3.pdf), 442 KB).
  - **ZIP Archive:** Verified integrity with 18 entries ([`EDITH_Documentation_Library_v10.3.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/EDITH_Documentation_Library_v10.3.zip), 355 KB).
  - **Checkpoint:** Recorded in [`sync_checkpoint_v10.3.json`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.3/sync_checkpoint_v10.3.json).
* **Cleanup Actions:**
  - Retained preceding validated baseline `EDITH_Documentation_v10.2` and historical baselines `EDITH_Documentation_v10.1`, `EDITH_Documentation_v10.0`.
  - Safely purged temporary staging directory `F:\EDITH\MD_DOC\.staging`.
* **Outstanding Discrepancies:** None.

---

### Release `v10.2` (Preceding Validated Release)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.2\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2)
* **Previous Release:** `EDITH_Documentation_v10.1`
* **Creation & Promotion Date:** October 27, 2026
* **Release Status:** `Archived Validated Baseline`
* **Pointer Status:** Superseded by `v10.3`
* **Core Drivers & Release Scope:**
  - Authored definitive, all-in-one comprehensive platform master reference document: [`EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.2.md`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/03_Technical_Architecture_and_Database/EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.2.md) (64.5 KB) covering all 10 modules, all 22 workflows, mathematical explanations of 8 core mechanisms, 39 tables ER topology, 17 master views, and full REST API catalog.
  - Complete elimination of unwanted, negative comparison cache terms (e.g., "Zero Redis", "Redis") across all documentation files, replacing them with positive, authoritative architecture definitions: Pure Dual-Database Persistence (PostgreSQL 17 + OpenSearch 2.12), in-memory bounded channels, and PostgreSQL row-level lease locks (`FOR UPDATE SKIP LOCKED`).
  - Added Phase 21 (Delta Rescreening) to the progress overview table in `TODO_v10.2.md`.
  - Re-compiled print-ready vector PDF deck via headless Google Chrome (`EDITH_Client_Architecture_Pitch_v10.2.pdf`, 442 KB).
  - Packaged and verified portable release archive (`EDITH_Documentation_Library_v10.2.zip`, 351 KB).
* **Generated Artifact Status:**
  - **Comprehensive Master Document:** [`EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.2.md`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/03_Technical_Architecture_and_Database/EDITH_Comprehensive_Platform_Architecture_and_Workflows_v10.2.md) (64.5 KB).
  - **HTML:** Generated and validated ([`EDITH_Client_Architecture_Pitch_v10.2.html`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.2.html)).
  - **PDF:** Compiled via Google Chrome headless=new ([`EDITH_Client_Architecture_Pitch_v10.2.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.2.pdf), 442 KB).
  - **ZIP Archive:** Verified integrity with 18 entries ([`EDITH_Documentation_Library_v10.2.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/EDITH_Documentation_Library_v10.2.zip), 351 KB).
  - **Checkpoint:** Recorded in [`sync_checkpoint_v10.2.json`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.2/sync_checkpoint_v10.2.json).

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
* **Main Documentation Changes:**
  - Expanded operational compliance workflows from 18 to 22.
  - Updated End-to-End Workplan to document all 21 completed engineering phases.
  - Documented 103 total test cases across 10 automated test projects.
  - Documented complete DDL for all 39 production tables across 5 PostgreSQL 17 schemas (`core`, `screening`, `cases`, `rules`, `notifications`).

---

### Release `v10.0` (Initial Migration Baseline)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.0\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.0)
* **Previous Release:** Initial flat structure migration
* **Creation Date:** October 25, 2026 (Migrated October 26, 2026)
* **Release Status:** `Archived Validated Baseline`
* **Relevant Change Register Tasks:**
  - Documented Phases 1–17: Dual-DB infrastructure, 5-pass OpenSearch engine, streaming bulk ingestion, 15m lease locks, Customer KYC 360 & UBO cascade screening, AML Rule Builder & 50k portfolio calibration sandbox, all 14 regulatory PDF reports hub, and live WORM cryptographic chain verification.
