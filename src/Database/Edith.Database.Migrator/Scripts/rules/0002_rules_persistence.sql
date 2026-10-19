-- Module: rules (v2.0 real persistence upgrade)
ALTER TABLE rules.aml_rules DROP CONSTRAINT IF EXISTS aml_rules_tenant_id_fkey;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS category VARCHAR(50) NOT NULL DEFAULT 'TransactionThreshold';
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS risk_level VARCHAR(20) NOT NULL DEFAULT 'High';
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS threshold_amount NUMERIC(18,4) NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS currency VARCHAR(10) NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS high_risk_countries TEXT[] NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS window_minutes INT NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS min_transactions INT NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS structuring_min_amount NUMERIC(18,4) NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS structuring_max_amount NUMERIC(18,4) NULL;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE rules.aml_rules ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
