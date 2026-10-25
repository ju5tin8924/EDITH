-- Migration: Core Menus Hierarchy, User Management & 2FA Schema
-- Module: core (MOD-CORE-IAM)

-- 1. Extend core.users with 2FA, password rotation, and onboarding tokens
ALTER TABLE core.users
    ADD COLUMN IF NOT EXISTS full_name VARCHAR(255),
    ADD COLUMN IF NOT EXISTS department VARCHAR(255) DEFAULT 'Financial Crime Compliance',
    ADD COLUMN IF NOT EXISTS shift_status VARCHAR(50) DEFAULT 'ON_DUTY',
    ADD COLUMN IF NOT EXISTS password_hash VARCHAR(256),
    ADD COLUMN IF NOT EXISTS must_change_password BOOLEAN NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS mfa_enabled BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS mfa_secret VARCHAR(128) NULL,
    ADD COLUMN IF NOT EXISTS mfa_enrolled_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS last_login_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS invitation_token VARCHAR(128) NULL,
    ADD COLUMN IF NOT EXISTS invitation_token_expires_at TIMESTAMPTZ NULL;

-- 2. Emergency Backup Recovery Codes Table (PBKDF2-SHA256 Hashed)
CREATE TABLE IF NOT EXISTS core.user_recovery_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES core.users(id) ON DELETE CASCADE,
    code_hash VARCHAR(128) NOT NULL,
    used_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_recovery_codes_user ON core.user_recovery_codes(user_id);
CREATE INDEX IF NOT EXISTS idx_users_invitation ON core.users(invitation_token);

-- 3. Enhance core.menus with unique code and icon
ALTER TABLE core.menus
    ADD COLUMN IF NOT EXISTS menu_code VARCHAR(100) UNIQUE,
    ADD COLUMN IF NOT EXISTS icon VARCHAR(50),
    ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT true;

-- 4. Outbound Email Dispatches Log
CREATE TABLE IF NOT EXISTS notifications.email_dispatches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NULL REFERENCES core.tenants(id) ON DELETE SET NULL,
    recipient_email VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body_html TEXT NOT NULL,
    status VARCHAR(50) NOT NULL, -- SENT, FAILED
    error_message TEXT NULL,
    sent_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 5. Seed the 12 Main Menus & Submenus
-- Main Menus
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES 
    ('01932000-0000-7000-8000-000000000100', NULL, 'Home / Dashboard', 'MENU_DASHBOARD', '/dashboard', 1, 'BarChart3'),
    ('01932000-0000-7000-8000-000000000200', NULL, 'Blacklist', 'MENU_BLACKLIST', '/blacklist/screening', 2, 'ShieldAlert'),
    ('01932000-0000-7000-8000-000000000300', NULL, 'White List', 'MENU_WHITELIST', '/whitelist', 3, 'ShieldCheck'),
    ('01932000-0000-7000-8000-000000000400', NULL, 'AML', 'MENU_AML', '/aml/rules', 4, 'Sliders'),
    ('01932000-0000-7000-8000-000000000500', NULL, 'STR Process', 'MENU_STR_PROCESS', '/str-process/alerts', 5, 'Briefcase'),
    ('01932000-0000-7000-8000-000000000600', NULL, 'Reports', 'MENU_REPORTS', '/reports/alerts', 6, 'FileSpreadsheet'),
    ('01932000-0000-7000-8000-000000000700', NULL, 'Log Report', 'MENU_LOG_REPORT', '/log-report/process', 7, 'FileText'),
    ('01932000-0000-7000-8000-000000000800', NULL, 'Settings', 'MENU_SETTINGS', '/settings/users', 8, 'Settings'),
    ('01932000-0000-7000-8000-000000000900', NULL, 'Master', 'MENU_MASTER', '/master/country', 9, 'Database'),
    ('01932000-0000-7000-8000-000000000A00', NULL, 'Authorization', 'MENU_AUTHORIZATION', '/authorization/sanction-alerts', 10, 'BadgeCheck'),
    ('01932000-0000-7000-8000-000000000B00', NULL, 'Customer', 'MENU_CUSTOMER', '/customer/kyc', 11, 'UserCheck'),
    ('01932000-0000-7000-8000-000000000C00', NULL, 'Delta', 'MENU_DELTA', '/delta/authorization', 12, 'RefreshCw')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, icon = EXCLUDED.icon;

-- Sub-Menus: Blacklist
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000201', '01932000-0000-7000-8000-000000000200', 'Sanction Screening', 'MENU_BLACKLIST_SCREENING', '/blacklist/screening', 1, 'Search'),
    ('01932000-0000-7000-8000-000000000202', '01932000-0000-7000-8000-000000000200', 'Add/Edit Watch list', 'MENU_BLACKLIST_WATCHLIST', '/blacklist/watchlist', 2, 'FilePlus'),
    ('01932000-0000-7000-8000-000000000203', '01932000-0000-7000-8000-000000000200', 'Bulk Screening', 'MENU_BLACKLIST_BULK', '/blacklist/bulk', 3, 'Layers')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: AML
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000401', '01932000-0000-7000-8000-000000000400', 'Customize Rules', 'MENU_AML_RULES', '/aml/rules', 1, 'Sliders'),
    ('01932000-0000-7000-8000-000000000402', '01932000-0000-7000-8000-000000000400', 'Process AML', 'MENU_AML_PROCESS', '/aml/process', 2, 'PlayCircle'),
    ('01932000-0000-7000-8000-000000000403', '01932000-0000-7000-8000-000000000400', 'STR History', 'MENU_AML_STR_HISTORY', '/aml/str-history', 3, 'History'),
    ('01932000-0000-7000-8000-000000000404', '01932000-0000-7000-8000-000000000400', 'ISTR MLRO Monitoring', 'MENU_AML_MLRO_MONITORING', '/aml/mlro-monitoring', 4, 'Eye')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: STR Process
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000501', '01932000-0000-7000-8000-000000000500', 'Alerts', 'MENU_STR_ALERTS', '/str-process/alerts', 1, 'Bell'),
    ('01932000-0000-7000-8000-000000000502', '01932000-0000-7000-8000-000000000500', 'EDD ANALYST & STR Branch [KYC]', 'MENU_STR_EDD_ANALYST', '/str-process/edd-analyst', 2, 'UserCheck'),
    ('01932000-0000-7000-8000-000000000503', '01932000-0000-7000-8000-000000000500', 'ISTR MLRO (Freeze/Unfreeze)', 'MENU_STR_ISTR_MLRO', '/str-process/istr-mlro', 3, 'Lock'),
    ('01932000-0000-7000-8000-000000000504', '01932000-0000-7000-8000-000000000500', 'EDD Analyst Monitoring', 'MENU_STR_EDD_MONITORING', '/str-process/edd-monitoring', 4, 'ShieldAlert')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Reports
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000601', '01932000-0000-7000-8000-000000000600', 'Alert Reports', 'MENU_REPORTS_ALERTS', '/reports/alerts', 1, 'FileBarChart'),
    ('01932000-0000-7000-8000-000000000602', '01932000-0000-7000-8000-000000000600', 'Transaction Report', 'MENU_REPORTS_TRANSACTIONS', '/reports/transactions', 2, 'FileSpreadsheet'),
    ('01932000-0000-7000-8000-000000000603', '01932000-0000-7000-8000-000000000600', 'Blacklist Report (WC/PEP)', 'MENU_REPORTS_BLACKLIST', '/reports/blacklist', 3, 'FileMinus'),
    ('01932000-0000-7000-8000-000000000604', '01932000-0000-7000-8000-000000000600', 'Transaction Release Report', 'MENU_REPORTS_RELEASES', '/reports/releases', 4, 'CheckCircle2'),
    ('01932000-0000-7000-8000-000000000605', '01932000-0000-7000-8000-000000000600', 'Online STR Report', 'MENU_REPORTS_ONLINE_STR', '/reports/online-str', 5, 'FileText'),
    ('01932000-0000-7000-8000-000000000606', '01932000-0000-7000-8000-000000000600', 'Transaction Analytics Report', 'MENU_REPORTS_ANALYTICS', '/reports/analytics', 6, 'PieChart')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Log Report
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000701', '01932000-0000-7000-8000-000000000700', 'Process Log', 'MENU_LOG_PROCESS', '/log-report/process', 1, 'FileCode'),
    ('01932000-0000-7000-8000-000000000702', '01932000-0000-7000-8000-000000000700', 'Upload Log', 'MENU_LOG_UPLOAD', '/log-report/upload', 2, 'UploadCloud'),
    ('01932000-0000-7000-8000-000000000703', '01932000-0000-7000-8000-000000000700', 'Suspicious Log', 'MENU_LOG_SUSPICIOUS', '/log-report/suspicious', 3, 'AlertOctagon'),
    ('01932000-0000-7000-8000-000000000704', '01932000-0000-7000-8000-000000000700', 'Custom Reports', 'MENU_LOG_CUSTOM', '/log-report/custom', 4, 'Layers'),
    ('01932000-0000-7000-8000-000000000705', '01932000-0000-7000-8000-000000000700', 'Monthly Screen Data Report', 'MENU_LOG_MONTHLY_SCREEN', '/log-report/monthly-screen', 5, 'Calendar')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Settings
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000801', '01932000-0000-7000-8000-000000000800', 'User Groups', 'MENU_SETTINGS_GROUPS', '/settings/user-groups', 1, 'Users'),
    ('01932000-0000-7000-8000-000000000802', '01932000-0000-7000-8000-000000000800', 'Users', 'MENU_SETTINGS_USERS', '/settings/users', 2, 'UserPlus'),
    ('01932000-0000-7000-8000-000000000803', '01932000-0000-7000-8000-000000000800', 'Menu Master', 'MENU_SETTINGS_MENU_MASTER', '/settings/menu-master', 3, 'Menu'),
    ('01932000-0000-7000-8000-000000000804', '01932000-0000-7000-8000-000000000800', 'Menu Mappings', 'MENU_SETTINGS_MENU_MAPPINGS', '/settings/menu-mappings', 4, 'GitMerge'),
    ('01932000-0000-7000-8000-000000000805', '01932000-0000-7000-8000-000000000800', 'Risk Rate Settings', 'MENU_SETTINGS_RISK_RATES', '/settings/risk-rates', 5, 'Activity'),
    ('01932000-0000-7000-8000-000000000806', '01932000-0000-7000-8000-000000000800', 'Screening Score', 'MENU_SETTINGS_SCREENING_SCORE', '/settings/screening-score', 6, 'SlidersHorizontal'),
    ('01932000-0000-7000-8000-000000000807', '01932000-0000-7000-8000-000000000800', 'Setup Control', 'MENU_SETTINGS_SETUP_CONTROL', '/settings/setup-control', 7, 'ToggleRight'),
    ('01932000-0000-7000-8000-000000000808', '01932000-0000-7000-8000-000000000800', 'Change Password', 'MENU_SETTINGS_CHANGE_PASSWORD', '/settings/change-password', 8, 'Key'),
    ('01932000-0000-7000-8000-000000000809', '01932000-0000-7000-8000-000000000800', 'Risk Assessment Master', 'MENU_SETTINGS_RISK_ASSESSMENT', '/settings/risk-assessment', 9, 'Target')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Master
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000901', '01932000-0000-7000-8000-000000000900', 'Nationality Master', 'MENU_MASTER_NATIONALITY', '/master/nationality', 1, 'Globe'),
    ('01932000-0000-7000-8000-000000000902', '01932000-0000-7000-8000-000000000900', 'Country Master', 'MENU_MASTER_COUNTRY', '/master/country', 2, 'Flag'),
    ('01932000-0000-7000-8000-000000000903', '01932000-0000-7000-8000-000000000900', 'Unblock Customer', 'MENU_MASTER_UNBLOCK', '/master/unblock-customer', 3, 'Unlock')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Authorization
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000A01', '01932000-0000-7000-8000-000000000A00', 'Sanction Alert Authorization', 'MENU_AUTH_SANCTION_ALERTS', '/authorization/sanction-alerts', 1, 'ShieldCheck'),
    ('01932000-0000-7000-8000-000000000A02', '01932000-0000-7000-8000-000000000A00', 'Risk Calculate', 'MENU_AUTH_RISK_CALC', '/authorization/risk-calculate', 2, 'Calculator'),
    ('01932000-0000-7000-8000-000000000A03', '01932000-0000-7000-8000-000000000A00', 'Customer Authorization', 'MENU_AUTH_CUSTOMER', '/authorization/customer', 3, 'UserCheck')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Customer
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000B01', '01932000-0000-7000-8000-000000000B00', 'Customer Screening Report', 'MENU_CUSTOMER_SCREENING', '/customer/screening-report', 1, 'FileSearch'),
    ('01932000-0000-7000-8000-000000000B02', '01932000-0000-7000-8000-000000000B00', 'Customer KYC', 'MENU_CUSTOMER_KYC', '/customer/kyc', 2, 'FolderCheck')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;

-- Sub-Menus: Delta
INSERT INTO core.menus (id, parent_menu_id, menu_name, menu_code, path, position, icon)
VALUES
    ('01932000-0000-7000-8000-000000000C01', '01932000-0000-7000-8000-000000000C00', 'Authorization', 'MENU_DELTA_AUTHORIZATION', '/delta/authorization', 1, 'CheckCircle'),
    ('01932000-0000-7000-8000-000000000C02', '01932000-0000-7000-8000-000000000C00', 'Log Report', 'MENU_DELTA_LOG_REPORT', '/delta/log-report', 2, 'FileText')
ON CONFLICT (id) DO UPDATE SET menu_code = EXCLUDED.menu_code, path = EXCLUDED.path;
