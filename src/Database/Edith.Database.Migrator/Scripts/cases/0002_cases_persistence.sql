-- Module: cases (v2.0 real persistence upgrade)
ALTER TABLE cases.aml_cases DROP CONSTRAINT IF EXISTS aml_cases_tenant_id_fkey;
ALTER TABLE cases.aml_cases DROP CONSTRAINT IF EXISTS aml_cases_transaction_id_fkey;
ALTER TABLE cases.aml_cases DROP CONSTRAINT IF EXISTS aml_cases_customer_id_fkey;
ALTER TABLE cases.aml_cases DROP CONSTRAINT IF EXISTS aml_cases_assigned_analyst_id_fkey;
ALTER TABLE cases.aml_cases DROP CONSTRAINT IF EXISTS aml_cases_locked_by_user_id_fkey;
ALTER TABLE cases.aml_cases ALTER COLUMN customer_id DROP NOT NULL;

ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS transaction_ref_no VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS amount NUMERIC(18,4) NOT NULL DEFAULT 0;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS currency VARCHAR(10) NOT NULL DEFAULT 'AED';
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS sender_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS beneficiary_name VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS risk_score NUMERIC(5,2) NOT NULL DEFAULT 0;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS triggered_list VARCHAR(100) NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS hold_reason TEXT NOT NULL DEFAULT '';
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS sla_due_date TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS locked_by_name VARCHAR(150) NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS locked_at TIMESTAMPTZ NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS lock_expires_at TIMESTAMPTZ NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS resolution VARCHAR(50) NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS resolution_reason TEXT NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS resolved_at TIMESTAMPTZ NULL;
ALTER TABLE cases.aml_cases ADD COLUMN IF NOT EXISTS resolved_by UUID NULL;

CREATE TABLE cases.case_notes (
    id UUID PRIMARY KEY,
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE CASCADE,
    author_id UUID NOT NULL,
    author_name VARCHAR(150) NOT NULL,
    note_type VARCHAR(50) NOT NULL DEFAULT 'INVESTIGATION',
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE cases.case_audit_entries (
    id UUID PRIMARY KEY,
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE CASCADE,
    action VARCHAR(100) NOT NULL,
    previous_status VARCHAR(50) NOT NULL,
    new_status VARCHAR(50) NOT NULL,
    actor_id UUID NOT NULL,
    actor_name VARCHAR(150) NOT NULL,
    metadata TEXT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
