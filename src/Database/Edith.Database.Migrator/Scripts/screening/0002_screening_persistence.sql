-- Module: screening (v2.0 real persistence upgrade)
ALTER TABLE screening.bulk_screening_jobs DROP CONSTRAINT IF EXISTS bulk_screening_jobs_user_id_fkey;
ALTER TABLE screening.bulk_screening_jobs ADD COLUMN IF NOT EXISTS skipped_records INT NOT NULL DEFAULT 0;
ALTER TABLE screening.bulk_screening_jobs ADD COLUMN IF NOT EXISTS keep_skipped BOOLEAN NOT NULL DEFAULT true;
ALTER TABLE screening.bulk_screening_jobs ADD COLUMN IF NOT EXISTS skipped_format VARCHAR(10) NOT NULL DEFAULT 'csv';

CREATE TABLE screening.bulk_job_skipped (
    id BIGSERIAL PRIMARY KEY,
    job_id UUID NOT NULL REFERENCES screening.bulk_screening_jobs(id) ON DELETE CASCADE,
    row_index INT NOT NULL DEFAULT 0,
    aml_uid TEXT NULL,
    aml_type TEXT NULL,
    aml_full_name TEXT NOT NULL,
    aml_id_details TEXT NULL,
    aml_category TEXT NULL,
    aml_date_of_birth TEXT NULL,
    aml_remarks TEXT NULL,
    aml_nationality TEXT NULL,
    aml_program TEXT NULL,
    aml_alias_name TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_bulk_job_skipped_job_id ON screening.bulk_job_skipped(job_id);

ALTER TABLE screening.watchlist_entities DROP CONSTRAINT IF EXISTS watchlist_entities_nationality_fkey;
ALTER TABLE screening.watchlist_entities ALTER COLUMN entity_external_id TYPE VARCHAR(255);
ALTER TABLE screening.watchlist_entities ALTER COLUMN entity_type TYPE VARCHAR(100);
ALTER TABLE screening.watchlist_entities ALTER COLUMN full_name TYPE TEXT;
ALTER TABLE screening.watchlist_entities ALTER COLUMN nationality TYPE TEXT;
ALTER TABLE screening.watchlist_entities ALTER COLUMN source_list TYPE TEXT;
ALTER TABLE screening.watchlist_entities ALTER COLUMN category TYPE TEXT;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS id_details TEXT NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS alias_name TEXT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_watchlist_entities_ext_name ON screening.watchlist_entities (entity_external_id, full_name);

ALTER TABLE screening.bulk_screening_items ALTER COLUMN input_name TYPE TEXT;

ALTER TABLE screening.whitelists DROP CONSTRAINT IF EXISTS whitelists_tenant_id_fkey;

CREATE TABLE screening.idempotency_cache (
    tenant_id UUID NOT NULL,
    idempotency_key VARCHAR(200) NOT NULL,
    response_json JSONB NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (tenant_id, idempotency_key)
);
