-- Preloaded Enterprise User Groups, Menu Permissions & Personas
-- Tenant: 11111111-1111-1111-1111-111111111111 (Emirates Global Bank)

-- 1. Seed Core Groups
INSERT INTO core.user_groups (id, tenant_id, group_name, is_active) VALUES
    ('01932000-0000-7000-8000-000000000A01', '11111111-1111-1111-1111-111111111111', 'ADMINISTRATOR_GROUP', true),
    ('01932000-0000-7000-8000-000000000A02', '11111111-1111-1111-1111-111111111111', 'AML_AUTHORIZED_OFFICERS', true),
    ('01932000-0000-7000-8000-000000000A03', '11111111-1111-1111-1111-111111111111', 'EDD_INVESTIGATION_DESK', true),
    ('01932000-0000-7000-8000-000000000A04', '11111111-1111-1111-1111-111111111111', 'MLRO_APPROVAL_DEPT', true)
ON CONFLICT (id) DO NOTHING;

-- 2. Seed Preloaded Users (All equipped with PBKDF2-SHA256 salted credentials for instant verification)
-- Password for all: EdithCompliance2026!
-- Hash: pbkdf2_sha256$100000$c1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6$4f346b97aa2ca0c4974fe0ad70dcfb65aa79fefbe245a4a58ebfb26eb16ce348

-- Super Admin
INSERT INTO core.users (id, tenant_id, group_id, username, full_name, role, department, is_active, mfa_enabled, mfa_secret, must_change_password, password_hash)
VALUES (
    '01932000-0000-7000-8000-000000000001',
    '11111111-1111-1111-1111-111111111111',
    '01932000-0000-7000-8000-000000000A01',
    'superadmin@edith.aml.cloud',
    'Platform Super Administrator',
    'SUPER_ADMIN',
    'Global Security & Architecture',
    true,
    true,
    'JBSWY3DPEHPK3PXP',
    false,
    'pbkdf2_sha256$100000$4598031a2002d5359f8256cea3a86c01$168d55dcde04ae1ee53144db52949e176b2372f3cec6643dd0b1ec6c2f135f06'
) ON CONFLICT (tenant_id, username) DO UPDATE SET role = 'SUPER_ADMIN', mfa_secret = 'JBSWY3DPEHPK3PXP';

-- Administrator
INSERT INTO core.users (id, tenant_id, group_id, username, full_name, role, department, is_active, mfa_enabled, mfa_secret, must_change_password, password_hash)
VALUES (
    '01932000-0000-7000-8000-000000000002',
    '11111111-1111-1111-1111-111111111111',
    '01932000-0000-7000-8000-000000000A01',
    'admin@edith.aml.cloud',
    'Tenant Compliance Administrator',
    'ADMIN',
    'FCC Administration Desk',
    true,
    true,
    'JBSWY3DPEHPK3PXP',
    false,
    'pbkdf2_sha256$100000$4598031a2002d5359f8256cea3a86c01$168d55dcde04ae1ee53144db52949e176b2372f3cec6643dd0b1ec6c2f135f06'
) ON CONFLICT (tenant_id, username) DO UPDATE SET role = 'ADMIN', mfa_secret = 'JBSWY3DPEHPK3PXP';

-- AML Officer
INSERT INTO core.users (id, tenant_id, group_id, username, full_name, role, department, is_active, mfa_enabled, mfa_secret, must_change_password, password_hash)
VALUES (
    '01932000-0000-7000-8000-000000000003',
    '11111111-1111-1111-1111-111111111111',
    '01932000-0000-7000-8000-000000000A02',
    'aml.officer@adcb.ae',
    'Tariq Mansoor',
    'AML_OFFICER',
    'Financial Crime Compliance - L1 Desk',
    true,
    true,
    'JBSWY3DPEHPK3PXP',
    false,
    'pbkdf2_sha256$100000$4598031a2002d5359f8256cea3a86c01$168d55dcde04ae1ee53144db52949e176b2372f3cec6643dd0b1ec6c2f135f06'
) ON CONFLICT (tenant_id, username) DO UPDATE SET role = 'AML_OFFICER', mfa_secret = 'JBSWY3DPEHPK3PXP';

-- EDD Analyst
INSERT INTO core.users (id, tenant_id, group_id, username, full_name, role, department, is_active, mfa_enabled, mfa_secret, must_change_password, password_hash)
VALUES (
    '01932000-0000-7000-8000-000000000004',
    '11111111-1111-1111-1111-111111111111',
    '01932000-0000-7000-8000-000000000A03',
    'edd.analyst@adcb.ae',
    'Sara Ahmed',
    'EDD_ANALYST',
    'Enhanced Due Diligence & Investigations',
    true,
    true,
    'JBSWY3DPEHPK3PXP',
    false,
    'pbkdf2_sha256$100000$4598031a2002d5359f8256cea3a86c01$168d55dcde04ae1ee53144db52949e176b2372f3cec6643dd0b1ec6c2f135f06'
) ON CONFLICT (tenant_id, username) DO UPDATE SET role = 'EDD_ANALYST', mfa_secret = 'JBSWY3DPEHPK3PXP';

-- ISTR MLRO
INSERT INTO core.users (id, tenant_id, group_id, username, full_name, role, department, is_active, mfa_enabled, mfa_secret, must_change_password, password_hash)
VALUES (
    '01932000-0000-7000-8000-000000000005',
    '11111111-1111-1111-1111-111111111111',
    '01932000-0000-7000-8000-000000000A04',
    'istr.mlro@adcb.ae',
    'Fatima Al-Nuaimi',
    'ISTR_MLRO',
    'Financial Crime Compliance (Unit 04 MLRO)',
    true,
    true,
    'JBSWY3DPEHPK3PXP',
    false,
    'pbkdf2_sha256$100000$4598031a2002d5359f8256cea3a86c01$168d55dcde04ae1ee53144db52949e176b2372f3cec6643dd0b1ec6c2f135f06'
) ON CONFLICT (tenant_id, username) DO UPDATE SET role = 'ISTR_MLRO', mfa_secret = 'JBSWY3DPEHPK3PXP';

-- 3. Seed Group Menu Permissions from Spec
DELETE FROM core.group_menu_permissions WHERE group_id IN (
    '01932000-0000-7000-8000-000000000A01',
    '01932000-0000-7000-8000-000000000A02',
    '01932000-0000-7000-8000-000000000A03',
    '01932000-0000-7000-8000-000000000A04'
);

-- Admin: All 12 Menus
INSERT INTO core.group_menu_permissions (group_id, menu_id, can_view, can_edit, can_authorize)
SELECT '01932000-0000-7000-8000-000000000A01', id, true, true, true
FROM core.menus WHERE parent_menu_id IS NULL;

-- AML Officers Group
INSERT INTO core.group_menu_permissions (group_id, menu_id, can_view, can_edit, can_authorize)
SELECT '01932000-0000-7000-8000-000000000A02', id, true, true, (menu_code IN ('MENU_AUTHORIZATION', 'MENU_DELTA', 'MENU_WHITELIST'))
FROM core.menus WHERE menu_code IN ('MENU_DASHBOARD', 'MENU_AUTHORIZATION', 'MENU_STR_PROCESS', 'MENU_AML', 'MENU_BLACKLIST', 'MENU_WHITELIST', 'MENU_DELTA') AND parent_menu_id IS NULL;

-- EDD Desk Group
INSERT INTO core.group_menu_permissions (group_id, menu_id, can_view, can_edit, can_authorize)
SELECT '01932000-0000-7000-8000-000000000A03', id, true, (menu_code IN ('MENU_STR_PROCESS', 'MENU_CUSTOMER')), false
FROM core.menus WHERE menu_code IN ('MENU_DASHBOARD', 'MENU_STR_PROCESS', 'MENU_CUSTOMER', 'MENU_WHITELIST') AND parent_menu_id IS NULL;

-- MLRO Group
INSERT INTO core.group_menu_permissions (group_id, menu_id, can_view, can_edit, can_authorize)
SELECT '01932000-0000-7000-8000-000000000A04', id, true, true, true
FROM core.menus WHERE menu_code IN ('MENU_DASHBOARD', 'MENU_STR_PROCESS', 'MENU_AML', 'MENU_REPORTS', 'MENU_LOG_REPORT', 'MENU_CUSTOMER') AND parent_menu_id IS NULL;

