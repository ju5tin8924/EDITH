# EDITH AML Platform — Infrastructure & Application Credentials (v1.1)

**Document ID:** EDITH-CREDENTIALS-V1.1  
**Environment:** Local Docker Compose (`docker/docker-compose.yaml`) & Development Services  
**Last Updated:** 2026-10-19

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

## 3. EDITH API Gateway & JWT Authentication (`Edith.Gateway`)

| Parameter | Value |
| :--- | :--- |
| **Gateway URL** | `http://localhost:5000` |
| **React Portal URL** | `http://localhost:3000` |
| **Standalone Screening API** | `http://localhost:5101` |
| **JWT Signing Secret** | `EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING` |
| **JWT Issuer / Audience** | `https://edith.aml.cloud` |
| **Token Endpoint** | `POST http://localhost:5000/api/v1/iam/token` |

### Seeded Tenants (`X-Tenant-ID`)
| Tenant Name | Tenant GUID | Jurisdiction |
| :--- | :--- | :--- |
| **Emirates Global Bank (Demo Portal Default)** | `11111111-1111-1111-1111-111111111111` | `AE` |
| **First Abu Dhabi Corporate** | `22222222-2222-2222-2222-222222222222` | `AE` |
| **Riyadh Capital Bank** | `33333333-3333-3333-3333-333333333333` | `SA` |

### Application Personas / Roles
| Role Code | Default Username | Description |
| :--- | :--- | :--- |
| `SUPER_ADMIN` | `tariq.mansoor` | Full platform, IAM, rules, and system administration |
| `MLRO` | `fatima.alnuaimi` | Money Laundering Reporting Officer (4-Eyes Freeze/Release/STR sign-off) |
| `COMPLIANCE_OFFICER` | `omar.khalid` | Senior compliance investigator & rule manager |
| `ANALYST` | `sara.ahmed` | L1/L2 alert triage, case locking, and EDD investigation |
| `AUDITOR` | `khalid.auditor` | Read-only access to cases and WORM audit trail |

---

## 4. Core Banking System (CBS) Webhook Signing

| Parameter | Value |
| :--- | :--- |
| **Signature Header** | `X-Edith-Signature` (HMAC-SHA256) |
| **Default Dev Webhook Secret** | `edith-cbs-secret-2026` |

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

