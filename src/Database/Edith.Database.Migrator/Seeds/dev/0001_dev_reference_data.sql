-- DEV SEED ONLY. Applied only when the migrator is run with --seed.
-- These are placeholder values for local testing, not compliance-approved reference data.

INSERT INTO core.tenants (id, name)
VALUES ('00000000-0000-0000-0000-000000000001', 'EDITH Dev Tenant')
ON CONFLICT (id) DO NOTHING;

INSERT INTO rules.country_masters (code, country_name, is_high_risk, risk_score) VALUES
    ('ARE', 'United Arab Emirates', false, 10.00),
    ('SAU', 'Saudi Arabia',         false, 10.00),
    ('OMN', 'Oman',                 false, 10.00),
    ('IND', 'India',                false, 20.00),
    ('PAK', 'Pakistan',             false, 35.00),
    ('GBR', 'United Kingdom',       false, 10.00),
    ('USA', 'United States',        false, 10.00),
    ('IRN', 'Iran',                 true,  95.00),
    ('PRK', 'North Korea',          true,  99.00)
ON CONFLICT (code) DO NOTHING;

INSERT INTO rules.nationality_masters (code, nationality_name, is_high_risk, risk_score) VALUES
    ('ARE', 'Emirati',     false, 10.00),
    ('SAU', 'Saudi',       false, 10.00),
    ('OMN', 'Omani',       false, 10.00),
    ('IND', 'Indian',      false, 20.00),
    ('PAK', 'Pakistani',   false, 35.00),
    ('GBR', 'British',     false, 10.00),
    ('USA', 'American',    false, 10.00),
    ('IRN', 'Iranian',     true,  95.00),
    ('PRK', 'North Korean', true, 99.00)
ON CONFLICT (code) DO NOTHING;

INSERT INTO rules.screening_score_configs
    (id, tenant_id, name_weightage, nationality_weightage, dob_weightage, id_weightage, min_threshold_score)
VALUES
    ('00000000-0000-0000-0000-0000000000a1', '00000000-0000-0000-0000-000000000001', 60.00, 10.00, 15.00, 15.00, 80.00)
ON CONFLICT (id) DO NOTHING;

INSERT INTO core.users (id, tenant_id, username, role, is_active)
VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'system_admin', 'ADMIN', true)
ON CONFLICT (id) DO NOTHING;

