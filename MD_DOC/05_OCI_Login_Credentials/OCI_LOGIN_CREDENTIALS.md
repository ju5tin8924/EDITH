# EDITH — OCI Production & Single-Inbox Testing Credentials

**Environment:** Oracle Cloud Infrastructure (`uk-london-1`, ARM64 `VM.Standard.A1.Flex`)  
**Public IP:** `http://144.21.52.172` (Local SSH Tunnel: `http://localhost:8080`)  
**Default Tenant:** Emirates Global Bank (`11111111-1111-1111-1111-111111111111`)  

---

## 1. Primary Single-Inbox Login (Full Platform & MLRO Access)

| Field | Value |
| :--- | :--- |
| **Username / Email** | `justinkuriakose16@gmail.com` |
| **Password** | `EdithCompliance2026!` |
| **Assigned Role** | `SUPER_ADMIN` (`ADMINISTRATOR_GROUP` — all 12 menus, all 17 views, MLRO approvals) |
| **2FA TOTP Setup Key (Base32)** | `JBSWY3DPEHPK3PXP` |
| **OTP Auth URI** | `otpauth://totp/EDITH-AML:justinkuriakose16@gmail.com?secret=JBSWY3DPEHPK3PXP&issuer=EDITH-AML&algorithm=SHA1&digits=6&period=30` |

> **Tip:** Add the Base32 key `JBSWY3DPEHPK3PXP` once in Google Authenticator, Microsoft Authenticator, or 1Password to generate the 6-digit verification code for all seeded personas below.

---

## 2. Single-Inbox Role Personas (Gmail `+tag` Sub-Addressing)

All of the following logins deliver email alerts directly to **`justinkuriakose16@gmail.com`** without creating separate email accounts. All share the same password (`EdithCompliance2026!`) and 2FA key (`JBSWY3DPEHPK3PXP`):

| Login Username | Role | Group | Menu & Workflow Scope |
| :--- | :--- | :--- | :--- |
| `justinkuriakose16@gmail.com` | `SUPER_ADMIN` | `ADMINISTRATOR_GROUP` | All 12 menus, IAM, Rules, Cases, and MLRO Approvals |
| `justinkuriakose16+mlro@gmail.com` | `ISTR_MLRO` | `MLRO_APPROVAL_DEPT` | MLRO 4-Eyes Freeze/Unfreeze & goAML STR Filing |
| `justinkuriakose16+aml@gmail.com` | `AML_OFFICER` | `AML_AUTHORIZED_OFFICERS` | L1 Sanction Screening, Bulk Upload & Rule Builder |
| `justinkuriakose16+edd@gmail.com` | `EDD_ANALYST` | `EDD_INVESTIGATION_DESK` | Customer KYC 360 Dossiers & EDD Investigation Desk |

---

## 3. Built-In System Accounts (Also Seeded)

| Login Username | Role | Password | 2FA Base32 Key |
| :--- | :--- | :--- | :--- |
| `superadmin@edith.aml.cloud` | `SUPER_ADMIN` | `EdithCompliance2026!` | `JBSWY3DPEHPK3PXP` |
| `admin@edith.aml.cloud` | `ADMIN` | `EdithCompliance2026!` | `JBSWY3DPEHPK3PXP` |
| `aml.officer@adcb.ae` | `AML_OFFICER` | `EdithCompliance2026!` | `JBSWY3DPEHPK3PXP` |
| `edd.analyst@adcb.ae` | `EDD_ANALYST` | `EdithCompliance2026!` | `JBSWY3DPEHPK3PXP` |
| `istr.mlro@adcb.ae` | `ISTR_MLRO` | `EdithCompliance2026!` | `JBSWY3DPEHPK3PXP` |

---

## 4. OCI VM & SSH Access Reference

| Parameter | Value |
| :--- | :--- |
| **Public IPv4** | `144.21.52.172` |
| **Private IPv4** | `10.0.0.251` |
| **Internal FQDN** | `edith-vcn.subnet11011011.vcn11011011.oraclevcn.com` |
| **SSH User** | `ubuntu` |
| **Local SSH Private Key** | `F:\EDITH\Keys\ssh-key-2026-11-01.key` (gitignored) |
| **Application Root on VM** | `/opt/edith/{gateway,screening-api,migrator}` |
| **Frontend Web Root on VM** | `/var/www/edith-portal` |
| **Runtime Secrets File on VM** | `/etc/edith/edith.env` (`chmod 0600`) |
