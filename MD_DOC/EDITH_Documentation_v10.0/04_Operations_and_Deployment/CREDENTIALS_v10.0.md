# EDITH AML Platform — Infrastructure & Application Credentials (v10.0)

**Document ID:** EDITH-CREDENTIALS-v10.0  
**Environment:** Local Docker Compose (`docker/docker-compose.yaml`) & Production K8s Deployment  
**Publication Date:** October 25, 2026  
**Status:** Approved / Production-Verified Secrets Reference  

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
| **Schemas** | `core`, `rules`, `screening`, `cases`, `notifications` (39 production tables) |

### Connection Strings
```text
# Npgsql Connection String (.NET 10 LTS)
Host=localhost;Port=5432;Database=edith_aml;Username=edith_admin;Password=EdithSecurePassword2026!

# Standard PostgreSQL URI (psql / DBeaver / pgAdmin)
postgresql://edith_admin:EdithSecurePassword2026!@localhost:5432/edith_aml
```

---

## 2. OpenSearch 2.12 (Screening Engine & Vector Indices)

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

```powershell
# Health check command
curl.exe -sk -u "admin:Tr@ns@ct10n$2026!#" https://localhost:9200/_cat/indices?v
```

---

## 3. EDITH API Gateway & IAM Security (`Edith.Gateway`)

| Parameter | Value |
| :--- | :--- |
| **Gateway Base URL** | `http://localhost:5000` |
| **React Portal URL** | `http://localhost:3000` |
| **Screening Service API** | `http://localhost:5101` (proxied via Gateway) |
| **JWT Signing Secret** | `EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING` |
| **JWT Issuer / Audience** | `https://edith.aml.cloud` |
| **Password Hashing Scheme** | `PBKDF2-SHA256` (100,000 iterations, 16-byte cryptographically secure salt, 32-byte digest) |
| **TOTP 2FA Implementation** | RFC 6238 compliant HMAC-SHA1 6-digit tokens, 30-second time-step windows |

### Seeded Multi-Tenant Identifiers (`X-Tenant-ID`)
| Tenant Name | Tenant GUID | Jurisdiction |
| :--- | :--- | :--- |
| **Emirates Global Bank (Demo Default)** | `11111111-1111-1111-1111-111111111111` | `AE` |
| **First Abu Dhabi Corporate** | `22222222-2222-2222-2222-222222222222` | `AE` |
| **Riyadh Capital Bank** | `33333333-3333-3333-3333-333333333333` | `SA` |

### Application Personas & Pre-Configured Test Accounts
All seeded accounts use password: `EdithCompliance2026!` (or temporary credentials rotated on `/onboarding`):

| Persona / Role | Default Email / Username | Department / Entitlement Scope |
| :--- | :--- | :--- |
| `SUPER_ADMIN` | `admin@edith.aml.cloud` | Complete platform, IAM, rules, and system master administration. |
| `MLRO` | `analyst.fatima@adcb.ae` | Statutory sign-off: 4-Eyes `RELEASE`/`FREEZE`, goAML STR XML filing. |
| `ANALYST` | `tariq.mansoor@adcb.ae` | L1/L2 screening alert triage, 15m lease locks, case investigations. |
| `EDD_ANALYST` | `edd.sarah@adcb.ae` | Customer KYC 360 dossiers, UBO cascade screening, SoW/SoF checks. |
| `AML_OFFICER` | `aml.omar@adcb.ae` | Transaction rule builder, velocity anomaly thresholds, sandbox backtesting. |
| `AUDITOR` | `zayed.husseini@adcb.ae` | Read-only compliance access to WORM audit ledger & 14 regulatory reports. |

---

## 4. Core Banking System (CBS) Webhook Integration

| Parameter | Value |
| :--- | :--- |
| **Signature Header** | `X-Edith-Signature` (`sha256={hex}`) via HMAC-SHA256 |
| **Payload Metadata Headers** | `X-Edith-Timestamp`, `X-Edith-Event`, `X-Tenant-ID`, `X-Edith-Attempt` |
| **Dev Webhook Shared Secret** | `edith-cbs-secret-2026` |
| **Polly Retry Policy** | 3 attempts with exponential backoff (2s, 4s, 8s) and jitter |
| **Dead-Letter Audit Table** | `notifications.delivery_ledgers` (`DELIVERED` or `FAILED_DEADLETTER`) |

---

## 5. Seeded & Bulk-Ingested Verification State

| Store / Table / Index | Verified Records | Source Dataset / Purpose |
| :--- | :--- | :--- |
| **PostgreSQL `screening.bulk_screening_jobs`** | `2` | `upload_doc/4C416D10.csv` (991 rows) + `upload_doc/NAMEs.json` (67,908 rows) |
| **PostgreSQL `screening.bulk_screening_items`** | `68,649` | Screened bulk records across both datasets |
| **PostgreSQL `screening.bulk_job_skipped`** | `250` | Deduplicated skipped records persisted for export |
| **PostgreSQL `screening.watchlist_entities`** | `47,618` | Unified entities (UAE Local Terrorist List + World-Check + UN Sanctions) |
| **OpenSearch `edith_watchlist_v1`** | `47,525` | Active n-gram & Double Metaphone phonetic indexed records |
| **PostgreSQL `cases.aml_cases`** | `7` | Seeded + live-screened AML investigation cases |
| **PostgreSQL `rules.aml_rules`** | `5` | Active CBUAE / FATF AML typology & velocity rules |
| **PostgreSQL `core.tenants` / `core.users`** | `6` / `6` | Multi-tenant banking institutions and RBAC compliance officers |
| **PostgreSQL `core.audit_ledgers`** | Active | Cryptographically chained WORM audit ledger with SHA-256 blocks |

---
*Maintained by the EDITH Architecture & Compliance Engineering Team (v10.0).*
