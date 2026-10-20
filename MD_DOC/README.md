# EDITH Platform — Master Documentation Catalog & Architecture Index

**Project:** EDITH Next-Gen AML Intelligence Platform  
**Target Environment:** .NET 10 LTS, React 19 Portal, PostgreSQL 17, OpenSearch 2.12 (Zero Redis)  
**Applicable Regulations:** CBUAE AML/CFT Decree-Law No. (20) of 2018, SAMA AML Rules, FATF Recommendations 6, 7, 10 & 16  
**Last Updated:** October 20, 2026  

---

## 1. Documentation Index & Version Matrix

All platform specifications, database DDLs, product requirements, and execution roadmaps are maintained exclusively within this directory:

| Document | Current Version | Primary Purpose | Key Contents |
| :--- | :---: | :--- | :--- |
| [`TODO_v1.0.md`](file:///f:/EDITH/MD_DOC/TODO_v1.0.md) | **v1.5.0** | **Live Engineering Roadmap** | Task-by-task execution ledger: All 11 phases marked `COMPLETED` (Phases 1–11), tracking test suite coverage (96/96 passing across 10 test projects) and production readiness. |
| [`EDITH_END_TO_END_WORKPLAN_v2.0.md`](file:///f:/EDITH/MD_DOC/EDITH_END_TO_END_WORKPLAN_v2.0.md) | **v3.4** | **Executive Workplan & Data Flow** | 11-Phase executive milestones, comprehensive end-to-end dataflow flowchart (Core Banking $\to$ 5-Pass Screening $\to$ 4-Eyes Adjudication $\to$ Signed CBS Webhooks $\to$ WORM Audit Ledger). |
| [`CREDENTIALS_v1.0.md`](file:///f:/EDITH/MD_DOC/CREDENTIALS_v1.0.md) | **v1.2** | **Developer & Infrastructure Secrets** | PostgreSQL & OpenSearch ports/credentials, JWT signing keys, multi-tenant GUIDs, RBAC personas, PBKDF2 hashing, CBS webhook retries, seeded data counts. |
| [`database_v2.0.md`](file:///f:/EDITH/MD_DOC/database_v2.0.md) | **v2.2** | **Database DDL & Search Index Schema** | PostgreSQL 17 DDL across 5 isolated schemas (`core`, `screening`, `cases`, `rules`, `notifications`), SHA-256 block-chained WORM audit ledger, OpenSearch analyzer & mapping configs. |
| [`EDITH_Master_PRD_v4.0.md`](file:///f:/EDITH/MD_DOC/EDITH_Master_PRD_v4.0.md) | **v5.0** | **Master Product Requirements (PRD)** | Business objectives, CBS integration workflows, 14 portal views, Target-Type Personas (Person/Corporate/Vessel), In-line Adjudication slips, KYC 360 bridge, What-If slider, batch clipboard paste, module packaging matrix (`MOD-CORE-IAM` through `MOD-AI-GRAPH`). |
| [`EDITH_Master_Specification_v5.0.md`](file:///f:/EDITH/MD_DOC/EDITH_Master_Specification_v5.0.md) | **v6.0** | **Master Technical Architecture Spec** | 5-pass scoring equations, streaming bulk ingestion, 15-min pessimistic locking, 4-Eyes state machine, live test suite breakdown (96/96 passing across 10 test projects), resolved design decisions. |

---

## 2. Recommended Reading Order for Engineers

```mermaid
flowchart TD
    A["1. README.md (Root)"] --> B["2. MD_DOC/README.md (This Index)"]
    B --> C["3. CREDENTIALS_v1.0.md (Local Ports & Auth)"]
    B --> D["4. EDITH_Master_PRD_v4.0.md (Domain & Requirements)"]
    D --> E["5. EDITH_Master_Specification_v5.0.md (Technical Specs)"]
    E --> F["6. database_v2.0.md (PostgreSQL DDL & OpenSearch)"]
    E --> G["7. EDITH_END_TO_END_WORKPLAN_v2.0.md (Architecture Flow)"]
    G --> H["8. TODO_v1.0.md (Active Implementation Tasks)"]
```

1. **Start with [`CREDENTIALS_v1.0.md`](file:///f:/EDITH/MD_DOC/CREDENTIALS_v1.0.md)** to spin up Docker containers and connect to PostgreSQL (`localhost:5432`) and OpenSearch (`https://localhost:9200`).
2. **Review [`EDITH_Master_PRD_v4.0.md`](file:///f:/EDITH/MD_DOC/EDITH_Master_PRD_v4.0.md)** for regulatory background, CBS integration contracts, Persona modes, and module boundaries.
3. **Study [`EDITH_Master_Specification_v5.0.md`](file:///f:/EDITH/MD_DOC/EDITH_Master_Specification_v5.0.md)** for 5-pass scoring algorithms, 4-Eyes Maker-Checker workflows, and architectural decisions.
4. **Inspect [`database_v2.0.md`](file:///f:/EDITH/MD_DOC/database_v2.0.md)** for exact table structures, cryptographic audit chaining, and migrator scripts.
5. **Review [`EDITH_END_TO_END_WORKPLAN_v2.0.md`](file:///f:/EDITH/MD_DOC/EDITH_END_TO_END_WORKPLAN_v2.0.md)** for the complete system topology and cross-service data pipelines.
6. **Track active engineering on [`TODO_v1.0.md`](file:///f:/EDITH/MD_DOC/TODO_v1.0.md)** for phase status and roadmap targets.

---

## 3. Core Architectural Principles

* **Zero-Redis Dual-Database Footprint:** Relies strictly on **PostgreSQL 17** for relational ACID integrity and audit ledgers, and **OpenSearch 2.12** for phonetic and n-gram entity search, with automatic in-memory fallback when containers are offline.
* **Sub-Second 5-Pass Real-Time Screening:**
  $$\text{CompositeScore} = (0.60 \times \text{Name}) + (0.15 \times \text{DOB}) + (0.15 \times \text{ID}) + (0.10 \times \text{Nationality})$$
  Hits with score $\ge 80.00\%$ trigger `HELD` and auto-provision an investigation case.
* **Dual-Mode CBS Integration:** Instantaneous pre-clearance API paired with HMAC-SHA256 signed webhook callbacks (`RELEASE` / `FREEZE`) backed by Polly exponential backoff retries and dead-letter ledger recording.
* **Streaming Bulk Ingestion & Batch Paste:** Bounded memory stream parser supporting CSV, XML, and JSON with deduplication skipping and multi-format export of skipped records, alongside direct multi-line clipboard paste.
* **4-Eyes Governance & In-Line Adjudication:** 15-minute pessimistic locking prevents collision; Analyst escalates to MLRO; MLRO adjudicates final decision. Quick triage slips allow instant false-positive dismissal or case escalation directly from search results.
* **Cryptographic WORM Audit Trails:** Every audit entry in `core.audit_ledgers` computes a SHA-256 block hash chained to the preceding entry, producing tamper-evident compliance certificates.
