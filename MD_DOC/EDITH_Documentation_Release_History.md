# EDITH Platform — Versioned Documentation Release History

**Project:** EDITH Next-Gen AML Intelligence Platform  
**Documentation Root:** `F:\EDITH\MD_DOC\`  
**Release Naming Convention:** `EDITH_Documentation_vMAJOR.MINOR`  
**Current Active Release:** [`EDITH_Documentation_v10.1`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1)  
**Last Updated:** October 26, 2026  

---

## Release Ledger

### Release `v10.1` (Current Promoted Release)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.1\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1)
* **Previous Release:** `EDITH_Documentation_v10.0`
* **Creation & Promotion Date:** October 26, 2026
* **Release Status:** `Validated & Promoted`
* **Pointer Status:** Active (`EDITH_Documentation_CURRENT.txt` points to `EDITH_Documentation_v10.1`)
* **Relevant Change Register Tasks (from `TODO_v10.1.md`):**
  - **Phase 18:** Notification Admin & Webhook Reliability Modernization (Dead-letter reconciliation console with 1-click retry, dynamic notification rules CRUD, shift schedule roster engine, sliding header notification inbox drawer, and pre-flight multi-channel simulation sandbox).
  - **Phase 19:** IAM Access System Workflow, UX/UI & Enterprise Security Modernization (Faceted compliance directory with CSV export, interactive `UserDrawer`, structured offboarding with case reassignment modal, invitation setup link fallback, hierarchical sub-menu permissions matrix, and active sessions revocation console).
  - **Phase 20:** System Masters & Calibration Hub Workflow, UX/UI & Feature Modernization (Full PostgreSQL CRUD wiring for risk rates, setup controls, EWRA category weights, and nationality demonyms; 100% auto-balancing constraint sliders with pin-locks; domain separation; high-density country/nationality grids; config rollback history; and 4-Eyes parameter staging).
  - **Phase 21:** Delta Rescreening Workflow, UX/UI & System Modernization (Filter bar wiring, closed-loop case provisioning into `CaseWorkspace.tsx`, atomic drawer adjudication, 4-Eyes dual review with AI auto-draft rationale, side-by-side visual token diff drawer, sub-route separation, and unit test suite in `DeltaRescreeningTests.cs`).
* **Main Documentation Changes:**
  - Expanded operational compliance workflows from 18 to 22.
  - Updated End-to-End Workplan to document all 21 completed engineering phases.
  - Documented 103 total test cases across 10 automated test projects (92 offline unit & benchmark tests verified passing; 11 Gateway integration tests requiring PostgreSQL on `:5432`).
  - Documented complete DDL for all 39 production tables across 5 PostgreSQL 17 schemas (`core`, `screening`, `cases`, `rules`, `notifications`).
* **Generated Artifact Status:**
  - **HTML:** Generated and validated ([`EDITH_Client_Architecture_Pitch_v10.1.html`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.1.html)).
  - **PDF:** Compiled via Google Chrome headless=new ([`EDITH_Client_Architecture_Pitch_v10.1.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.1.pdf), 442 KB).
  - **ZIP Archive:** Verified integrity with 16 entries ([`EDITH_Documentation_Library_v10.1.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1/EDITH_Documentation_Library_v10.1.zip), 330 KB).
  - **Checkpoint:** Recorded in [`sync_checkpoint_v10.1.json`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.1/sync_checkpoint_v10.1.json).
* **Cleanup Actions:**
  - Retained baseline `EDITH_Documentation_v10.0` per retention policy (current promoted release + immediately preceding validated release).
  - Safely purged temporary staging directory `F:\EDITH\MD_DOC\.staging`.
* **Outstanding Discrepancies:** None.

---

### Release `v10.0` (Initial Migration Baseline)

* **Release Directory:** [`F:\EDITH\MD_DOC\EDITH_Documentation_v10.0\`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.0)
* **Previous Release:** Initial flat structure migration
* **Creation Date:** October 25, 2026 (Migrated October 26, 2026)
* **Release Status:** `Archived Validated Baseline`
* **Relevant Change Register Tasks:**
  - Documented Phases 1–17: Dual-DB infrastructure, 5-pass OpenSearch engine, streaming bulk ingestion, 15m lease locks, Customer KYC 360 & UBO cascade screening, AML Rule Builder & 50k portfolio calibration sandbox, all 14 regulatory PDF reports hub, and live WORM cryptographic chain verification.
* **Artifacts:**
  - [`EDITH_Client_Architecture_Pitch_v10.0.pdf`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.0/01_Executive_and_Pitch/EDITH_Client_Architecture_Pitch_v10.0.pdf) (442 KB).
  - [`EDITH_Documentation_Library_v10.0.zip`](file:///F:/EDITH/MD_DOC/EDITH_Documentation_v10.0/EDITH_Documentation_Library_v10.0.zip) (317 KB).
* **Retention Status:** Retained as the immediately preceding validated release.

---
*Maintained by the EDITH Automated Versioned Documentation Release System.*
