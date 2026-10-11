-- Module: cases (MOD-CASE-WORK / MOD-GOAML-GCC)
-- Source: MD_DOC/database_v1.0.md, section 1, SCHEMA: cases. Verbatim except for IF NOT EXISTS on the schema.
CREATE SCHEMA IF NOT EXISTS cases;

CREATE TABLE cases.aml_cases (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES core.tenants(id) ON DELETE RESTRICT,
    case_number VARCHAR(50) NOT NULL UNIQUE,
    transaction_id UUID NULL REFERENCES screening.transactions(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE RESTRICT,
    alert_type VARCHAR(50) NOT NULL,
    priority VARCHAR(10) NOT NULL,
    stage VARCHAR(30) NOT NULL DEFAULT 'TRIAGE',
    status VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    assigned_analyst_id UUID NULL REFERENCES core.users(id) ON DELETE SET NULL,
    locked_by_user_id UUID NULL REFERENCES core.users(id) ON DELETE SET NULL,
    edd_deadline TIMESTAMPTZ NULL,
    mlro_deadline TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE cases.case_rule_matches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE CASCADE,
    rule_id UUID NOT NULL REFERENCES rules.aml_rules(id) ON DELETE CASCADE,
    rule_violation_summary TEXT NOT NULL
);

CREATE TABLE cases.case_activities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE CASCADE,
    actor_id UUID NULL REFERENCES core.users(id) ON DELETE SET NULL,
    action_type VARCHAR(50) NOT NULL,
    comments TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE cases.account_freezes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE RESTRICT,
    transaction_id UUID NULL REFERENCES screening.transactions(id) ON DELETE RESTRICT,
    freeze_scope VARCHAR(30) NOT NULL,
    requested_by UUID NOT NULL REFERENCES core.users(id) ON DELETE RESTRICT,
    approved_by_mlro UUID NOT NULL REFERENCES core.users(id) ON DELETE RESTRICT,
    is_unfrozen BOOLEAN NOT NULL DEFAULT false
);

CREATE TABLE cases.goaml_filings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id UUID NOT NULL REFERENCES cases.aml_cases(id) ON DELETE RESTRICT,
    report_type VARCHAR(30) NOT NULL,
    report_payload_xml TEXT NOT NULL,
    validation_status VARCHAR(30) NOT NULL,
    submission_date TIMESTAMPTZ NULL
);
