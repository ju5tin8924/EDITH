# Master Product Requirements Document (PRD v5.0)

**Product Name:** EDITH Next-Gen AML Intelligence Platform (formerly iShield)  
**Target Architecture:** Modular Microservices (.NET 10 LTS + React 19 Portal + OpenSearch 2.12 + PostgreSQL 17)  
**Target Market:** GCC Financial Institutions, Exchange Houses, and Commercial Banks  
**Screening Scope:** English & Transliterated Phonetic Optimized (Individual, Corporate, Vessel)  
**Performance SLA:** Sub-second 5-pass screening budget (P99 < 10ms in-memory / < 50ms OpenSearch)  
**Database Footprint:** Dual-Database (PostgreSQL 17 + OpenSearch 2.12 — Zero Redis Dependency)  
**Version:** 5.0  
**Last Updated:** October 20, 2026  

---

## 1. Executive Summary & Strategic Scope

The **EDITH AML Intelligence Platform** modernizes legacy monolithic compliance software into a cloud-native, modular microservices platform running on **.NET 10 LTS** and **React 19**. The platform is engineered to eliminate alert fatigue, accelerate compliance investigations, and automate regulatory reporting for financial institutions operating under FATF-aligned central bank mandates (CBUAE, SAMA, Central Bank of Oman).

### Core Business Objectives
* **Plug-and-Play Modularity:** Decouple platform capabilities into isolated core modules and optional add-ons, enabling feature activation and licensing tiers without modifying the underlying codebase.
* **True Asynchronous Microservices:** Heavy background processes (bulk screening, reporting, delta syncs) execute asynchronously via `HTTP 202 Accepted` and SignalR notifications while users continue navigating the platform.
* **Dual-Mode Core Banking System (CBS) API Integration:** Synchronous pre-clearance (`CLEARED` / `HELD`) paired with asynchronous HMAC-SHA256 signed webhook callbacks (`RELEASE` / `FREEZE`) backed by Polly retries and a dead-letter delivery ledger.
* **Streamlined Dual-Database Footprint:** Zero Redis dependency. Relies on PostgreSQL 17 for relational ACID integrity and OpenSearch 2.12 for phonetic/fuzzy entity resolution with transparent in-memory fallback.
* **Live React 19 Operator Workstation:** Unified 14-view compliance portal (`edith-portal`) bridged to `Edith.Gateway` with real PBKDF2-authenticated JWT role switching (`ANALYST`, `MLRO`, `SUPER_ADMIN`, `AUDITOR`).

---

## 2. Modular Architecture & Core System Integration

### 2.1. Core Banking System (CBS) API Integration Strategy
* **Synchronous Pre-Clearance:** The Core Banking System sends `POST /api/v1/screen/transaction` with an `Idempotency-Key` header. EDITH executes the 5-pass engine, auto-provisions an investigation case in `cases.aml_cases` if `HELD`, and returns immediately.
* **Asynchronous Signed Webhook Callbacks:** When an MLRO resolves a held case as `ClearedFalsePositive` (`RELEASE`) or `ReportedStrFreeze` (`FREEZE`), `ICbsWebhookDispatcher` dispatches an HMAC-SHA256 signed `AmlDecisionWebhookPayload` (`X-Edith-Signature`) back to the CBS webhook endpoint. Outbound requests employ Polly exponential backoff retries and persist unreachable drops to `notifications.delivery_ledgers`.

### 2.2. Schema-per-Module Isolation (PostgreSQL 17)
Every module manages its own isolated PostgreSQL schema (`core`, `screening`, `cases`, `rules`, `notifications`).

---

## 3. Dual-Database Persistence Architecture (No Redis)

| Database / Store | Technology | Scope & Responsibility |
| :--- | :--- | :--- |
| **System of Record & Audit** | **PostgreSQL 17** | Master entity catalog, case workflows, customer KYC records, audit trails, user IAM, custom rule configurations, bulk job metadata, skipped duplicate records, delivery ledgers, and whitelist tables. |
| **Search & Screening Engine** | **OpenSearch 2.12** | Read-optimized search index (`edith_watchlist_v1`, `edith_customers_v1`) with custom n-gram and Double Metaphone phonetic token analyzers, plus automatic in-memory fallback when offline. |

---

## 4. Screening Engine Innovations & Workflows

### 4.1. The 5-Pass Real-Time Screening Lifecycle
1. **Pass 1 — Normalization & Cleansing:** ASCII folding, punctuation removal, case folding, corporate suffix stripping (*LLC, FZE, Inc, Ltd, PJSC*).
2. **Pass 2 — Broad Retrieval:** OpenSearch DisMax query + phonetic token matching pulls top candidate matches.
3. **Pass 3 — Phonetic & String Distance Scoring:** Double Metaphone, Jaro-Winkler, and Damerau-Levenshtein evaluation.
4. **Pass 4 — Multi-Entity Evaluation:** Simultaneously screens Sender, Beneficiary, and Intermediary/Correspondent Bank.
5. **Pass 5 — Deterministic Scoring & Whitelist Bypass:** Calculates weighted composite score; suppresses hits if active in PostgreSQL `screening.whitelists`; auto-creates an `Open` case if score $\ge 80\%$.

### 4.2. Target-Type Persona Modes (Individual, Corporate, Vessel)
Screening workflows adjust input fields and scoring weightings dynamically according to target entity persona:
* **Individual:** Focuses on Full Name, Date of Birth, Passport/National ID, and Nationality.
* **Corporate / Legal Entity:** Normalizes corporate legal structures, evaluates Tax IDs / Commercial Register Numbers, and inspects UBO associations.
* **Vessel / Maritime:** Screens Vessel Name, IMO Number, MMSI, and Flag State against OFAC maritime sanctions and North Korea/Iran shipping advisories.

### 4.3. In-Line Adjudication Slip & KYC 360 Bridge
* **In-Line Adjudication Slip:** Analysts can execute quick triage (`False Positive Dismissal` or `Escalate to Case`) directly from single entity search results without navigating away.
* **Onboarding AML Clearance Memo:** Produces a standardized compliance clearance memo for clean checks (`Score < 80%`), complete with timestamp and operator identity.
* **KYC 360 Bridge:** Seamless 1-click transition from a screened individual or corporate hit to their comprehensive KYC 360 Customer Profile (`screening.customers`), displaying document vaults and past screening history.

### 4.4. Interactive What-If Sensitivity Slider
Enables compliance officers to simulate match threshold adjustments in real time (from 50% to 95%) directly in the UI. Re-scores and re-ranks existing candidate hits instantly on the client side without issuing redundant network round-trips.

### 4.5. Batch Multi-Line Clipboard Paste
Allows compliance operators to paste multi-line entity lists (names, IDs, countries) directly from spreadsheets into an interactive batch text area. Automatically sanitizes, validates, and screens the batch in parallel.

### 4.6. PEP Tiering & Side-by-Side Diff Analysis
* **PEP Hierarchy Tiering:** Differentiates Politically Exposed Persons into **Tier-1** (Heads of State, Ministers), **Tier-2** (Senior military, judiciary, SOE executives), and **Tier-3** (Local councilors, embassy attachés).
* **Side-by-Side Visual Diffing:** Side-by-side comparison modal highlighting exact matched tokens, transposed characters, and conflicting dates of birth or nationalities.

### 4.7. WORM Compliance Certificate & Inline Whitelisting
* **Tamper-Evident Clearance Certificate:** Generates a verifiable compliance audit certificate complete with SHA-256 hash stamp, verifying that screening took place against active watchlists.
* **Direct Whitelisting Modal:** Allows authorized officers to safely whitelist false-positive matches directly from screening results, specifying match scope, reason, and expiry date.

### 4.8. Streaming Bulk Ingestion & Skipped Duplicate Export (`MOD-SCRN-BULK`)
* **Supported Formats:** Streaming parser (`IAmlStreamingParser`) supports **CSV**, **XML**, and **JSON** mapped to 9 standardized fields (`AML_TYPE`, `AML_FULLNAME`, `AML_IDDETAILS`, `AML_CATEGORY`, `AML_DATEOFBIRTH`, `AML_REMARKS`, `AML_NATIONALITY`, `AML_PROGRAM`, `AML_ALIASNAME`).
* **Duplicate Detection & Keep-Skipped Option:** Entities already present in PostgreSQL/OpenSearch are automatically skipped during bulk upload. When `keepSkipped=true`, skipped entities are retained and can be downloaded on demand in the user's chosen format (`CSV`, `JSON`, or `XML`) via `GET /api/v1/bulk/jobs/{id}/skipped?format=csv|json|xml`.

---

## 5. Case Investigation Engine & 4-Eyes Adjudication (`MOD-CASE-WORK`)

* **Pessimistic 15-Minute Lease Locking:** Only one analyst can hold the active investigation lock on a case at a time (`POST /api/v1/cases/{id}/acquire-lock`). Competing lock attempts return `409 Conflict`.
* **Multi-Type Investigation Notes:** Supports `INVESTIGATION`, `SUPERVISOR`, and `RATIONALE` notes persisted in real time and broadcast via SignalR `CaseHub`.
* **4-Eyes Maker-Checker Governance:**
  * **Analyst (Maker):** Investigates token breakdowns, adds notes, and escalates cases to `PendingApproval` (`Escalate Case to MLRO Queue`). Cannot execute final `Resolved` disposition.
  * **MLRO / Compliance Officer (Checker):** Reviews escalated cases and executes final adjudication (`ClearedFalsePositive` $\rightarrow$ CBS `RELEASE`, or `ReportedStrFreeze` $\rightarrow$ CBS `FREEZE` + UNODC `goAML` STR XML generation).

---

## 6. Master Feature Matrix & Packaging

| Module Code | Module / Feature Name | Package Tier | Modernized Implementation |
| :--- | :--- | :--- | :--- |
| `MOD-CORE-IAM` | Multi-Tenant IAM & RBAC | **Core** | PBKDF2 password hashing, JWT Bearer auth, dynamic role persona switching, tenant header isolation. |
| `MOD-CORE-API` | Core System Gateway (`Edith.Gateway`) | **Core** | Dual-mode API (Sync pre-clearance + Async HMAC-SHA256 CBS Webhooks with Polly retries) with Idempotency. |
| `MOD-SCRN-BASE` | Watchlist & Sanctions Screening | **Core** | 5-pass OpenSearch 2.12 + In-Memory hybrid engine, Double Metaphone, Jaro-Winkler, What-If slider, Target Personas. |
| `MOD-SCRN-BULK` | Bulk File Screening Engine | **Core** | Streaming CSV/XML/JSON ingestion, batch clipboard paste, duplicate skipping, and multi-format skipped export. |
| `MOD-RULE-PROC` | AML Transaction Rule Engine | **Core** | Real-time corridor/threshold/structuring rules and historical backtesting sandbox. |
| `MOD-CASE-WORK` | 3-Pane Case Investigation Canvas | **Core** | 15-min pessimistic locking, 4-Eyes Maker-Checker workflow, In-line adjudication slips, CBS webhook dispatch. |
| `MOD-AUDIT-WORM` | Immutable Cryptographic Audit Ledger | **Core** | SHA-256 block-chained WORM audit ledger in `core.audit_ledgers` and tamper-evident PDF/HTML clearance certificates. |
| `MOD-GOAML-GCC` | Automated `goAML` Exporter | **Add-on** | UNODC goAML v5.0 XML payload generator for GCC Financial Intelligence Units. |
| `MOD-AI-NARR` | GenAI STR Narrative Copilot | **Add-on** | Automated 5-section regulatory STR narrative drafting from case telemetry. |
| `MOD-AI-GRAPH` | Entity Relationship Visualizer | **Add-on** | Multi-hop network graph builder detecting UBO and circular layering rings. |
