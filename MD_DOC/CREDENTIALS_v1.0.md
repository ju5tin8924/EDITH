# EDITH AML Platform — Infrastructure & Application Credentials (v1.2)

**Document ID:** EDITH-CREDENTIALS-V1.2  
**Environment:** Local Docker Compose (`docker/docker-compose.yaml`) & Development Services  
**Last Updated:** October 20, 2026  

---

## 1. PostgreSQL 17 (System of Record & Relational Audit)

| Parameter | Value |
| :--- | :--- |
| **Container Name** | `edith_postgres_17` |
| **Image** | `postgres:17` |
| **Host** | `localhost` |
| **Port** | `5432` |
| **Database Name** | `edith_aml` |
| **Username** | `edith_admin` |
| **Password** | `EdithSecurePassword2026!` |
| **Schemas** | `core`, `rules`, `screening`, `cases`, `notifications`, `public` |

### Npgsql Connection String (.NET)
```text
Host=localhost;Port=5432;Database=edith_aml;Username=edith_admin;Password=EdithSecurePassword2026!
```

### Standard PostgreSQL URI (`psql` / DBeaver / pgAdmin)
```text
postgresql://edith_admin:EdithSecurePassword2026!@localhost:5432/edith_aml
```

---

## 2. OpenSearch 2.12 (Fuzzy Phonetic & Multi-Match Screening Engine)

| Parameter | Value |
| :--- | :--- |
| **Container Name** | `edith_opensearch_2` |
| **Image** | `opensearchproject/opensearch:2.12.0` |
| **Cluster Name** | `edith-screening-cluster` |
| **Base URL** | `https://localhost:9200` |
| **Performance Analyzer Port** | `9600` |
| **Username** | `admin` |
| **Password** | `Tr@ns@ct10n$2026!#` |
| **SSL Verification** | Disabled in local dev (`curl -k` / `SkipSslValidation: true`) |
| **Watchlist Index** | `edith_watchlist_v1` |
| **Customers Index** | `edith_customers_v1` |

### Quick Verification Command (`curl`)
```powershell
curl.exe -sk -u "admin:Tr@ns@ct10n$2026!#" https://localhost:9200/_cat/indices?v
```

---

## 3. EDITH API Gateway & IAM Security (`Edith.Gateway`)

| Parameter | Value |
| :--- | :--- |
| **Gateway URL** | `http://localhost:5000` |
| **React Portal URL** | `http://localhost:3000` |
| **Standalone Screening API** | `http://localhost:5101` |
| **JWT Signing Secret** | `EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING` |
| **JWT Issuer / Audience** | `https://edith.aml.cloud` |
| **Token Endpoint** | `POST http://localhost:5000/api/v1/iam/token` |
| **Password Hashing Scheme** | `PBKDF2-SHA256` (100,000 iterations, 16-byte cryptographically secure salt, 32-byte digest) |

### Seeded Tenants (`X-Tenant-ID`)
| Tenant Name | Tenant GUID | Jurisdiction |
| :--- | :--- | :--- |
| **Emirates Global Bank (Demo Portal Default)** | `11111111-1111-1111-1111-111111111111` | `AE` |
| **First Abu Dhabi Corporate** | `22222222-2222-2222-2222-222222222222` | `AE` |
| **Riyadh Capital Bank** | `33333333-3333-3333-3333-333333333333` | `SA` |

### Application Personas / Roles & Seeded Accounts
| Role Code | Default Email / Username | Default Password | Description |
| :--- | :--- | :--- | :--- |
| `SUPER_ADMIN` | `admin@edith.aml.cloud` | `EdithCompliance2026!` | Full platform, IAM, rules, and system administration |
| `MLRO` | `analyst.fatima@adcb.ae` | `EdithCompliance2026!` | Money Laundering Reporting Officer (4-Eyes Freeze/Release/STR sign-off) |
| `ANALYST` | `tariq.mansoor@adcb.ae` | `EdithCompliance2026!` | L1/L2 alert triage, case locking, and EDD investigation |
| `AUDITOR` | `zayed.husseini@adcb.ae` | `EdithCompliance2026!` | Read-only access to cases and WORM audit trail |

### IAM Endpoints
* `POST /api/v1/iam/token`: Issue JWT bearer token with tenant & role claims.
* `GET /api/v1/iam/entitlements`: Returns active user permissions, menu access, and duty shift.
* `GET /api/v1/iam/users`: Lists all tenant operators with department and duty status.
* `POST /api/v1/iam/users`: Provisions new operator with PBKDF2-hashed credentials.
* `PUT /api/v1/iam/users/{id}`: Modifies operator role, active status, and shift status.
* `DELETE /api/v1/iam/users/{id}`: Soft/hard removes user account from tenant workspace.
* `GET /api/v1/iam/groups`: Returns RBAC user groups and associated menu permissions.
* `POST /api/v1/iam/groups`: Creates custom role group.
* `PUT /api/v1/iam/groups/{id}/permissions`: Updates group menu permissions (`can_view`, `can_edit`, `can_authorize`).

---

## 4. Core Banking System (CBS) Webhook Integration & Resilience

| Parameter | Value |
| :--- | :--- |
| **Signature Header** | `X-Edith-Signature` (`sha256={hex}`) via HMAC-SHA256 |
| **Metadata Headers** | `X-Edith-Timestamp`, `X-Edith-Event`, `X-Tenant-ID`, `X-Edith-Attempt` |
| **Default Dev Webhook Secret** | `edith-cbs-secret-2026` |
| **Retry Policy** | 3 attempts with exponential backoff (2s, 4s, 8s) via Polly |
| **Dead-Letter Ledger** | Unreachable endpoints logged in PostgreSQL `notifications.delivery_ledgers` with `DEAD_LETTER_HTTP_{code}` |

---

## 5. Seeded & Bulk-Ingested Database State

| Store / Table / Index | Verified Count | Source Dataset / Purpose |
| :--- | :--- | :--- |
| **PostgreSQL `screening.bulk_screening_jobs`** | `2` | `upload_doc/4C416D10.csv` (991 rows) + `upload_doc/NAMEs.json` (67,908 rows) |
| **PostgreSQL `screening.bulk_screening_items`** | `68,649` | Screened bulk records across both datasets |
| **PostgreSQL `screening.bulk_job_skipped`** | `250` | Deduplicated skipped records persisted for export |
| **PostgreSQL `screening.watchlist_entities`** | `47,618` | Deduplicated `(entity_external_id, full_name)` entities (UAE Watch + World-Check + Baseline) |
| **OpenSearch `edith_watchlist_v1`** | `47,525` | Active N-Gram + Double Metaphone indexed watchlist documents |
| **PostgreSQL `cases.aml_cases`** | `7` | Seeded + live-screened AML investigation cases |
| **PostgreSQL `rules.aml_rules`** | `3` | Active CBUAE / FATF AML typology rules |
| **PostgreSQL `core.tenants` / `core.users`** | `6` / `6` | Multi-tenant banking institutions and RBAC compliance officers |
| **PostgreSQL `core.audit_ledgers`** | Active | Cryptographically chained WORM audit ledger with SHA-256 blocks |
