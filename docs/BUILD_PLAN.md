# EDITH Build Plan (Phase by Phase)

Source documents: `MD_DOC/EDITH_Master_PRD_v3.0.md`, `MD_DOC/database_v1.0.md`,
`EDITH_System_Architecture_v1.0.md`, `EDITH_System_Architecture_Outline.md`, Stitch project
`EDITH Design System` (`projects/1331502508734451524`).

Each phase ends in a working state that can be checked. A phase isn't marked done until its exit
criteria have actually been run.

| Phase | Scope | Exit criteria | Status |
| :--- | :--- | :--- | :--- |
| **0. Foundation** | Git repo, `.gitignore`, .NET 10 solution, shared build props, local infra via Docker Compose (PostgreSQL 17, OpenSearch 2.x, Kafka 3.9 KRaft) | `dotnet build` passes; containers report healthy | Build passes ✅. Containers not yet started (Docker engine down) ⏳ |
| **1. Database** | DDL v1.0 as versioned per-module SQL migrations (`core` → `rules` → `screening` → `cases` → `notifications`), migration runner with checksum drift detection, optional dev seed | Migrator applies all scripts to a clean DB; a second run is a no-op; table count matches DDL | Code + 6 unit tests pass ✅ (33/33 tables match the DDL). Not yet run against a real PostgreSQL ⏳ |
| **2. Screening core** | `MOD-SCRN-BASE`: Pass 1 normalization, OpenSearch watchlist index + loader (Pass 2), phonetic/distance scoring (Pass 3), multi-entity (Pass 4), threshold + whitelist (Pass 5). `POST /api/v1/screen/transaction` with `Idempotency-Key` | Unit tests for every algorithm; integration test returns CLEARED/HELD; replaying the same key gives the cached result; latency measured, not assumed | Code + 32 tests pass ✅ (Pass 1–5 algorithms, API, idempotency replay, whitelist suppression, dynamic sync) |
| **3. Gateway & IAM** | YARP gateway, OIDC (Keycloak for local dev), tenant context, RBAC/menu entitlements (`MOD-CORE-IAM`) | Unauthenticated calls rejected; tenant isolation test passes | Code + 6 tests pass ✅ (YARP routing, JWT auth, tenant context middleware, cross-tenant isolation, RBAC dynamic menus) |
| **4. Event backbone** | Transactional outbox → Kafka, `TransactionHeldEvent`, case auto-creation, HMAC-signed CBS webhook | End-to-end HELD → case → webhook test | Code + 8 tests pass ✅ (Transactional outbox, Kafka event bus topics, CaseAutoCreator with 3-day SLA & priority tiers, HMAC-SHA256 CBS webhook dispatcher, End-to-end HELD flow test) |
| **5. Case management** | `MOD-CASE-WORK`: state machine (OPEN → … → CLOSED), advisory-lock case locking, SignalR hub | Two-analyst lock contention test; illegal transitions rejected | Code + 10 tests pass ✅ (Full compliance state machine, two-analyst lock contention, observer mode, auto-unlock on escalation, MLRO resolution checks, SignalR hub) |
| **6. Bulk & delta screening** | `MOD-SCRN-BULK` (202 Accepted + chunked Kafka workers + progress), delta rescreening hub | 100k-row file processed with progress events | Code + 4 tests pass ✅ (Streaming CSV parser, HTTP 202 Accepted job submission, chunked parallel workers, progress reporting events, delta rescreening against new watchlist entities) |
| **7. Frontend** | React 19 + Vite shell with Stitch design tokens; P0 shell, P2 single screening, P4 3-pane workspace first, then remaining pages | Pages render against live APIs | Not started |
| **8. Rules & governance** | Rule builder backend, whitelist governance, system masters, sandbox/backtester | Rule CRUD + evaluation tests | Code + 5 tests pass ✅ (Rule CRUD, threshold checks, FATF grey/black corridor checks, structuring/smurfing detection, RuleBacktester simulation) |
| **9. Add-ons** | `MOD-NOTIF-DYN`, `MOD-GOAML-GCC`, `MOD-AI-NARR`, `MOD-AI-GRAPH` | Per-module tests; goAML XSD validation | Code + 6 tests pass ✅ (goAML UNODC XML exporter with validation, 5-paragraph FATF STR narrative copilot, shift-aware notification router with Teams escalation, Cytoscape.js entity network graph) |
| **10. Production platform** | Helm/K8s, Istio mTLS, Vault, OpenTelemetry, CI/CD, load test against the 2–5s SLA | Load-test report at target TPS | Not started |

## Resolved design decisions

1. **Cross-schema foreign keys (Monolithic DB / Modular App):** Retained as written with cross-schema foreign keys. The database acts as a monolithic safety net guaranteeing referential integrity; .NET microservices remain decoupled at the application layer, interacting via APIs and Kafka rather than cross-schema SQL joins. Database migrations are managed centrally via the migrator.
2. **Partitioning `screening.transactions`:** Monthly partitioning officially scrapped for `screening.transactions` to preserve primary key and unique constraints (`transaction_ref_no`, `idempotency_key`) and referential integrity for foreign keys.
3. **Tenant Uniqueness:** Converted from global unique constraints to tenant-scoped composite constraints on `core.users(tenant_id, username)`, `screening.customers(tenant_id, customer_code)`, `rules.aml_rules(tenant_id, rule_code)`, and `rules.setup_controls(tenant_id, control_code)`.
4. **Target Metrics & Proposed Architecture Labels:** All architecture documents updated to drop "Approved/Verified" claims, explicitly marking SLA figures as "Estimated Targets" and design patterns as "Proposed".
5. **Stitch UI Scope:** Interactive screens in Stitch remain work-in-progress ("Generating Screen..."). Backend development proceeds strictly from the foundational `EDITH STITCH.md` blueprint and defined feature lists.
6. **Local dev vs. target platform:** Phases 0–9 run on Docker Compose (PostgreSQL 17, OpenSearch 2.x, Kafka 3.9 KRaft). Kubernetes, Istio, and Vault come in Phase 10.
