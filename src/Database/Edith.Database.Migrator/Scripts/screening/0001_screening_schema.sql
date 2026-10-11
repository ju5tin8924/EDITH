-- Module: screening (MOD-SCRN-BASE / MOD-SCRN-BULK)
-- Source: MD_DOC/database_v1.0.md, section 1, SCHEMA: screening. Verbatim except for IF NOT EXISTS on the schema.
-- NOTE: transactions is left unpartitioned on purpose. See docs/BUILD_PLAN.md, open issue 2.
CREATE SCHEMA IF NOT EXISTS screening;

CREATE TABLE screening.customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES core.tenants(id) ON DELETE RESTRICT,
    customer_code VARCHAR(50) NOT NULL,
    customer_type VARCHAR(20) NOT NULL CHECK (customer_type IN ('INDIVIDUAL', 'CORPORATE')),
    full_name VARCHAR(255) NOT NULL,
    dob DATE NOT NULL,
    nationality VARCHAR(3) NOT NULL REFERENCES rules.nationality_masters(code) ON DELETE RESTRICT,
    residence_country VARCHAR(3) NOT NULL REFERENCES rules.country_masters(code) ON DELETE RESTRICT,
    id_type VARCHAR(50) NOT NULL,
    id_number VARCHAR(100) NOT NULL,
    risk_tier VARCHAR(20) NOT NULL DEFAULT 'LOW',
    is_blocked BOOLEAN NOT NULL DEFAULT false,
    onboarding_status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT customers_tenant_code_key UNIQUE (tenant_id, customer_code)
);

CREATE TABLE screening.customer_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE CASCADE,
    document_type VARCHAR(50) NOT NULL,
    document_number VARCHAR(100) NOT NULL,
    file_path VARCHAR(255) NOT NULL,
    ocr_extracted_data JSONB NULL,
    verification_status VARCHAR(30) NOT NULL DEFAULT 'PENDING'
);

CREATE TABLE screening.customer_screening_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE CASCADE,
    screened_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    match_status VARCHAR(30) NOT NULL,
    match_score NUMERIC(5,2) NOT NULL
);

CREATE TABLE screening.watchlist_entities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_external_id VARCHAR(100) NOT NULL,
    entity_type VARCHAR(30) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    dob DATE NULL,
    nationality VARCHAR(3) NULL REFERENCES rules.nationality_masters(code) ON DELETE SET NULL,
    source_list VARCHAR(50) NOT NULL,
    category VARCHAR(50) NOT NULL,
    is_blocked BOOLEAN NOT NULL DEFAULT true,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE screening.transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES core.tenants(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE RESTRICT,
    transaction_ref_no VARCHAR(100) NOT NULL UNIQUE,
    idempotency_key VARCHAR(100) NOT NULL UNIQUE,
    transaction_type VARCHAR(50) NOT NULL,
    amount NUMERIC(18,4) NOT NULL CHECK (amount > 0.0000),
    currency VARCHAR(3) NOT NULL,
    lcy_amount NUMERIC(18,4) NOT NULL CHECK (lcy_amount > 0.0000),
    sender_nationality VARCHAR(3) NOT NULL REFERENCES rules.nationality_masters(code) ON DELETE RESTRICT,
    sender_country VARCHAR(3) NOT NULL REFERENCES rules.country_masters(code) ON DELETE RESTRICT,
    beneficiary_name VARCHAR(255) NOT NULL,
    beneficiary_country VARCHAR(3) NOT NULL REFERENCES rules.country_masters(code) ON DELETE RESTRICT,
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    transaction_date TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE screening.screening_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id UUID NOT NULL REFERENCES screening.transactions(id) ON DELETE CASCADE,
    pass_number INT NOT NULL,
    score NUMERIC(5,2) NOT NULL,
    match_type VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE screening.whitelists (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES core.tenants(id) ON DELETE RESTRICT,
    customer_id UUID NULL REFERENCES screening.customers(id) ON DELETE CASCADE,
    entity_identifier VARCHAR(255) NOT NULL,
    match_scope VARCHAR(50) NOT NULL,
    reason TEXT NOT NULL,
    expires_at TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE screening.bulk_screening_jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES core.users(id) ON DELETE RESTRICT,
    file_name VARCHAR(255) NOT NULL,
    total_records INT NOT NULL DEFAULT 0,
    processed_records INT NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING'
);

CREATE TABLE screening.bulk_screening_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id UUID NOT NULL REFERENCES screening.bulk_screening_jobs(id) ON DELETE CASCADE,
    matched_entity_id UUID NULL REFERENCES screening.watchlist_entities(id) ON DELETE SET NULL,
    input_name VARCHAR(255) NOT NULL,
    match_score NUMERIC(5,2) NOT NULL DEFAULT 0.00,
    match_found BOOLEAN NOT NULL DEFAULT false
);

CREATE TABLE screening.delta_sync_batches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_date TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    list_source VARCHAR(100) NOT NULL,
    hits_detected INT NOT NULL DEFAULT 0,
    status VARCHAR(30) NOT NULL DEFAULT 'COMPLETED'
);

CREATE TABLE screening.delta_authorizations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id UUID NOT NULL REFERENCES screening.delta_sync_batches(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES screening.customers(id) ON DELETE CASCADE,
    watchlist_entity_id UUID NOT NULL REFERENCES screening.watchlist_entities(id) ON DELETE CASCADE,
    match_score NUMERIC(5,2) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    authorized_by UUID NULL REFERENCES core.users(id) ON DELETE SET NULL
);
