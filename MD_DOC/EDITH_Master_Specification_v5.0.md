# EDITH Next-Gen AML Intelligence Platform
## Master Technical Architecture & Engineering Specification (v5.1)

**Document ID:** EDITH-ARCH-SPEC-V5.1  
**Target Architecture:** Modular Microservices (.NET 10 LTS + React 19 Portal + Dual-Database)  
**System of Record:** PostgreSQL 17 (`edith_aml`)  
**Screening Engine:** OpenSearch 2.12 (Phonetic & Multi-Attribute Search Index + In-Memory Fallback)  
**Applicable Standards:** UAE AML/CFT Federal Decree-Law No. (20) of 2018, Cabinet Decision No. (74) of 2020 on Terrorist Lists & UNSCR, and FATF Recommendations 6, 7, 10 & 16  
**Publication Date:** October 19, 2026  
**Document Status:** Approved / Production-Ready Specification  

---

### Document Revision History

| Version | Date | Primary Author | Description of Changes |
| :--- | :--- | :--- | :--- |
| **v1.0** | Oct 11, 2026 | Architecture Team | Initial Database DDL schema, PostgreSQL 17 isolation (`core`, `screening`, `cases`, `rules`, `notifications`). |
| **v2.0** | Oct 12, 2026 | Platform Team | Modular monolith & decoupled microservices layout, YARP API gateway, IAM, and entitlement matrix. |
| **v3.0** | Oct 14, 2026 | Product Engineering | Master PRD, English-optimized 5-pass screening lifecycle, Add-on services (goAML, Copilot, Graph). |
| **v4.0** | Oct 15, 2026 | Senior Dev / EDITH Core | Realization of Phases 1–4: Local Docker Dual-Database Infrastructure (PostgreSQL 17 + OpenSearch 2.12), OpenSearch 5-pass phonetic screening engine, Bulk streaming ingestion pipeline with 9-field AML mapping & deduplication/skip export, Case Investigation Engine with 15-min pessimistic lease locking, 4-Eyes Maker-Checker governance, and CBS HMAC-SHA256 signed webhooks (`RELEASE`/`FREEZE`). |
| **v5.0** | Oct 17, 2026 | Senior Dev / EDITH Core | **Phase 5 Complete:** Live React 19 Portal (`edith-portal`) to `Edith.Gateway` integration bridge, real JWT token exchange per persona (`ANALYST`, `MLRO`, `SUPER_ADMIN`, `AUDITOR`), live case comment persistence & MLRO escalation/resolution wiring, portal bulk file upload (`CSV`/`JSON`/`XML`) with skipped duplicate download, offline-resilient OpenSearch/PostgreSQL fallback, and **93/93 automated tests passing green across 10 test projects**. |
| **v5.1** | Oct 19, 2026 | Senior Dev / EDITH Core | **Phase 6 Complete:** Universal Single Entity & Transaction Screening overhaul (`NameSearchView.tsx` & `ScreeningConsole.tsx`), 6 screening possibilities coverage (Exact, Fuzzy, Dual-Factor, Clean Certificate, PEP, Corporate), full reset lifecycle (`Esc`/inline clear), resolved stale candidate match threshold filtering, and interactive on-click match decision diagrams. |

---

## 1. Executive Summary & Strategic Scope

The **EDITH AML Intelligence Platform** modernizes legacy monolithic compliance systems into a cloud-native, high-throughput financial crime intelligence engine running on **.NET 10 LTS**, **React 19**, **PostgreSQL 17**, and **OpenSearch 2.12**.

### Core Architectural Pillars
1. **Zero-Redis Dual-Database Footprint:** Relies strictly on **PostgreSQL 17** for ACID-compliant transactions, master data, case histories, and immutable audit logs; and **OpenSearch 2.12** for read-optimized phonetic and multi-attribute entity resolution, backed by automatic in-memory fallback when local containers are offline.
2. **Sub-Second Real-Time Screening SLA:** Delivers 5-pass deterministic and phonetic fuzzy entity resolution across sender, beneficiary, and intermediary bank entities.
3. **Dual-Mode Core Banking System (CBS) Integration:**
   - **Synchronous Pre-Clearance:** Instantaneous evaluate-and-hold check returning HTTP 200 with `Status = CLEARED` or `Status = HELD` (auto-provisioning an investigation case).
   - **Asynchronous Signed Webhook Callbacks:** Automatic dispatch of HMAC-SHA256 signed payloads (`RELEASE` or `FREEZE`) back to the core banking ledger upon MLRO case resolution.
4. **Resilient Streaming Bulk Ingestion:** Processes CSV, XML, and JSON batches via bounded asynchronous streaming, skipping pre-existing records with on-demand export of skipped items in CSV, JSON, or XML.
5. **Strict 4-Eyes Governance (Maker-Checker) & Live React 19 Workstation:** Full 14-view operator portal (`http://localhost:3000`) connected to `Edith.Gateway` (`http://localhost:5000`).

---

## 2. Phase 1: Local Infrastructure & Dual-Database Architecture

* **PostgreSQL 17 (`localhost:5432`, DB: `edith_aml`):** Schema-per-module isolation across `core`, `screening`, `cases`, `rules`, and `notifications`.
* **OpenSearch 2.12 (`https://localhost:9200`):** Indices `edith_watchlist_v1` and `edith_customers_v1` configured with `edith_name_analyzer`, `edith_ngram_analyzer` ($3 \le n \le 10$), and Double Metaphone `phonetic_tokens`.
* **Transparent Offline Resilience:** `OpenSearchWatchlistSearchEngine` and `PostgreSqlBulkJobRepository` automatically fall back to thread-safe in-memory stores when Docker containers are stopped.

---

## 3. Phase 2: OpenSearch-Backed Real-Time Screening Engine (`Edith.Screening.Core`)

1. **Pass 1 (Normalization & Cleansing):** Strips corporate noise suffixes (*LLC, FZE, Inc, Ltd, PJSC*), honorifics, diacritics, and non-alphanumeric symbols.
2. **Pass 2 (Broad Retrieval):** Parallel multi-role candidate retrieval (`Sender`, `Beneficiary`, `IntermediaryBank`) via OpenSearch DisMax and phonetic token queries.
3. **Pass 3 (Phonetic & String Distance Scoring):** Combines Double Metaphone phonetic similarity with Damerau-Levenshtein and Jaro-Winkler string metrics.
4. **Pass 4 (Multi-Attribute Composite Scoring):**
   $$\text{CompositeScore} = (0.60 \times \text{Name}) + (0.15 \times \text{DOB}) + (0.15 \times \text{ID}) + (0.10 \times \text{Nationality})$$
5. **Pass 5 (Thresholding, Whitelist Suppression & Auto-Case Creation):** Suppresses matches covered by active `screening.whitelists`. Non-whitelisted hits with $\text{CompositeScore} \ge 80.00\%$ return `HELD` and automatically create an `Open` case in `ICaseService`.

---

## 4. Phase 3: Bulk Screening & Streaming Ingestion Pipeline (`Edith.Screening.Bulk`)

* **9-Field Standardized Mapping:** `AML_TYPE`, `AML_FULLNAME`, `AML_IDDETAILS`, `AML_CATEGORY`, `AML_DATEOFBIRTH`, `AML_REMARKS`, `AML_NATIONALITY`, `AML_PROGRAM`, `AML_ALIASNAME`.
* **Streaming Parser (`AmlStreamingParser`):** Asynchronous `IAsyncEnumerable<AmlEntityRecord>` parsing for CSV, XML, and JSON.
* **Duplicate Detection & Skipped Export (`AmlFormatExporter`):** Skips entities already in the system, retains skipped records when `keepSkipped=true`, and exports them in CSV, JSON, or XML via `GET /api/v1/bulk/jobs/{id}/skipped?format=csv|json|xml`.

---

## 5. Phase 4: Case Investigation Engine & Adjudication Workflows (`Edith.Cases`)

* **15-Minute Pessimistic Lease Locking (`CaseLockManager`):** Prevents dual-analyst collisions with atomic 15-minute leases (`409 Conflict` on competing lock attempts) and SignalR `CaseHub` broadcasts.
* **Multi-Type Investigation Notes:** Persists `INVESTIGATION`, `SUPERVISOR`, and `RATIONALE` notes via `POST /api/v1/cases/{id}/comments` and returns them in `GET /api/v1/cases/{id}`.
* **4-Eyes Maker-Checker State Machine (`CaseStateMachine`):**
  - **Analyst (`ANALYST`):** Transitions `Open` $\rightarrow$ `UnderReview` $\rightarrow$ `PendingApproval` (`Escalate Case to MLRO Queue`). Blocked from resolving cases.
  - **MLRO (`MLRO` / `SUPER_ADMIN`):** Resolves cases as `ClearedFalsePositive` (triggers signed CBS `RELEASE` webhook) or `ReportedStrFreeze` (triggers signed CBS `FREEZE` webhook + UNODC `goAML` STR XML).

---

## 6. Phase 5: Frontend Live Integration & React 19 UI Bridge (`edith-portal`)

* **Vite Proxy & Gateway Integration:** `src/Web/edith-portal` (`http://localhost:3000`) proxies `/api` and `/hubs` to `Edith.Gateway` (`http://localhost:5000`).
* **Dynamic Persona JWT Minting (`api.ts`):** Switching roles in the portal header (`ANALYST`, `MLRO`, `SUPER_ADMIN`, `AUDITOR`) automatically acquires a real signed JWT from `POST /api/v1/iam/token` and refreshes `/api/v1/iam/entitlements`.
* **3-Pane Case Workspace (`CaseWorkspace.tsx`):** Wired to live `/api/v1/cases` endpoints for lock acquisition/release, real-time note creation, analyst escalation to MLRO queue, MLRO resolution (`Confirm Hit (STR)` / `Dismiss (False +)`), AI STR narrative generation, and `goAML` XML export.
* **Bulk Screening Hub (`BulkScreeningView.tsx`):** Wired with real `.csv`/`.json`/`.xml` file upload (`POST /api/v1/bulk/upload`), `Keep Skipped Duplicates` toggle, `Skipped Export Format` selector (`CSV`/`JSON`/`XML`), live job progress polling, and one-click `Download Skipped` export.

---

## 7. Verification & Test Suite Results

```
Total Test Projects:  10
Total Test Cases:     93
Passed:               93 (100%)
Failed:               0
Skipped:              0
Frontend Build:       tsc -b && vite build (0 errors)
```

| Test Project | Passed | Scope Verified |
| :--- | :---: | :--- |
| `Edith.Screening.Tests` | 26 | Double Metaphone, n-grams, text normalization, Jaro-Winkler, Damerau-Levenshtein, OpenSearch engine. |
| `Edith.Screening.Api.Tests` | 13 | Pre-clearance screening, idempotency replay, whitelist suppression, auto-case creation, bulk upload & skipped download. |
| `Edith.Screening.Bulk.Tests` | 10 | Streaming CSV/XML/JSON parser, 9-field mapping, duplicate skipping, multi-format skipped exporter. |
| `Edith.Cases.Tests` | 10 | State machine transitions, 15-min pessimistic locking, collision blocking, 4-Eyes RBAC, notes & audit trail. |
| `Edith.Gateway.Tests` | 8 | JWT issuance, role entitlements, tenant isolation, E2E hold-to-resolution workflow, portal bulk upload & UI resolution. |
| `Edith.Events.Tests` | 8 | HMAC-SHA256 webhook signing/verification, CBS webhook dispatcher, transactional outbox processor. |
| `Edith.Database.Migrator.Tests` | 6 | Multi-schema DDL validation (`core`, `screening`, `cases`, `rules`, `notifications`) and seed integrity. |
| `Edith.Addons.Tests` | 6 | UNODC goAML XML export, AI STR narrative copilot, multi-hop entity relationship graph visualizer. |
| `Edith.Rules.Tests` | 5 | High-risk corridor, structuring/smurfing, large cash threshold rules, and historical backtesting sandbox. |
| `Edith.Performance.Benchmark` | 1 | High-concurrency 5-pass screening latency benchmark. |
