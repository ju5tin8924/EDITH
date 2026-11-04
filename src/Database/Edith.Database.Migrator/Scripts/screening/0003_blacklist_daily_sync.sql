-- Module: screening (v2.7 daily blacklist sync, 4-category classification, 90-day skipped retention & admin scheduler config)

ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS source_type VARCHAR(16) NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS source_ref_id VARCHAR(64) NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS addresses TEXT NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS programs TEXT NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS remarks TEXT NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS attributes_json JSONB NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS content_hash CHAR(64) NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS last_run_id UUID NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS removed_at TIMESTAMPTZ NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS removed_in_run_id UUID NULL;
ALTER TABLE screening.watchlist_entities ADD COLUMN IF NOT EXISTS removal_reason TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS idx_watchlist_entities_source_extid
    ON screening.watchlist_entities (source_type, entity_external_id)
    WHERE source_type IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_watchlist_entities_active_source
    ON screening.watchlist_entities (source_type)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_watchlist_entities_content_hash
    ON screening.watchlist_entities (content_hash)
    WHERE content_hash IS NOT NULL;

-- Backfill source_type on existing baseline rows where source_list matches one of the four primary sources
UPDATE screening.watchlist_entities
SET source_type = 'OFAC', source_ref_id = '536ca6ad74b9fe781f6a3cd6'
WHERE source_type IS NULL AND UPPER(source_list) LIKE 'OFAC%';

UPDATE screening.watchlist_entities
SET source_type = 'UN', source_ref_id = '538f31c944a9880b5137a07b'
WHERE source_type IS NULL AND UPPER(source_list) LIKE 'UN%';

UPDATE screening.watchlist_entities
SET source_type = 'EU', source_ref_id = '538f325c44a9880b5137a07c'
WHERE source_type IS NULL AND UPPER(source_list) LIKE 'EU%';

UPDATE screening.watchlist_entities
SET source_type = 'UK', source_ref_id = '5526919db8143b3250b77a02'
WHERE source_type IS NULL AND (UPPER(source_list) LIKE 'UK%' OR UPPER(source_list) LIKE 'OFSI%' OR UPPER(source_list) LIKE 'HMT%');

-- 1. Configurable Blacklist Data Sources (Admin Settings)
CREATE TABLE IF NOT EXISTS screening.blacklist_sources (
    source_type VARCHAR(16) PRIMARY KEY,
    source_ref_id VARCHAR(64) NOT NULL,
    display_name VARCHAR(120) NOT NULL,
    url TEXT NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT true,
    last_successful_sync_at TIMESTAMPTZ NULL,
    last_attempt_at TIMESTAMPTZ NULL,
    last_status VARCHAR(40) NOT NULL DEFAULT 'NEVER_RUN',
    last_error_user_message TEXT NULL,
    active_record_count INT NOT NULL DEFAULT 0,
    updated_by VARCHAR(100) NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO screening.blacklist_sources (source_type, source_ref_id, display_name, url, is_enabled, last_status)
VALUES
    ('OFAC', '536ca6ad74b9fe781f6a3cd6', 'US Treasury OFAC SDN List', 'http://www.treasury.gov/ofac/downloads/sdn.xml', true, 'NEVER_RUN'),
    ('UN', '538f31c944a9880b5137a07b', 'UN Security Council Consolidated List', 'https://scsanctions.un.org/resources/xml/en/consolidated.xml', true, 'NEVER_RUN'),
    ('EU', '538f325c44a9880b5137a07c', 'European Union Financial Sanctions List (FSF)', 'https://webgate.ec.europa.eu/fsd/fsf/public/files/xmlFullSanctionsList/content?token=dG9rZW4tMjAxNw', true, 'NEVER_RUN'),
    ('UK', '5526919db8143b3250b77a02', 'UK FCDO Consolidated Sanctions List', 'https://sanctionslist.fcdo.gov.uk/docs/UK-Sanctions-List.xml', true, 'NEVER_RUN')
ON CONFLICT (source_type) DO NOTHING;

-- 2. Singleton Scheduler & Execution Configuration (initially disabled until manual verification completes)
CREATE TABLE IF NOT EXISTS screening.blacklist_sync_config (
    id INT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    is_schedule_enabled BOOLEAN NOT NULL DEFAULT false,
    daily_execution_time_utc VARCHAR(5) NOT NULL DEFAULT '02:00',
    timezone VARCHAR(16) NOT NULL DEFAULT 'UTC',
    retention_days INT NOT NULL DEFAULT 90,
    last_scheduled_run_date DATE NULL,
    updated_by VARCHAR(100) NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO screening.blacklist_sync_config (id, is_schedule_enabled, daily_execution_time_utc, timezone, retention_days)
VALUES (1, false, '02:00', 'UTC', 90)
ON CONFLICT (id) DO NOTHING;

-- 3. Daily Blacklist Update Execution Runs
CREATE TABLE IF NOT EXISTS screening.blacklist_sync_runs (
    id UUID PRIMARY KEY,
    trigger_mode VARCHAR(30) NOT NULL,
    triggered_by VARCHAR(100) NOT NULL,
    started_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ NULL,
    duration_ms BIGINT NOT NULL DEFAULT 0,
    status VARCHAR(40) NOT NULL DEFAULT 'RUNNING',
    current_source VARCHAR(16) NULL,
    sources_processed INT NOT NULL DEFAULT 0,
    sources_succeeded INT NOT NULL DEFAULT 0,
    sources_failed INT NOT NULL DEFAULT 0,
    total_retrieved INT NOT NULL DEFAULT 0,
    total_new INT NOT NULL DEFAULT 0,
    total_updated INT NOT NULL DEFAULT 0,
    total_skipped INT NOT NULL DEFAULT 0,
    total_removed INT NOT NULL DEFAULT 0,
    total_failed INT NOT NULL DEFAULT 0,
    user_summary_message TEXT NULL,
    worm_hash CHAR(64) NULL
);

CREATE INDEX IF NOT EXISTS idx_bl_sync_runs_started_at
    ON screening.blacklist_sync_runs (started_at DESC);

-- 4. Per-Source Statistics for Each Execution Run
CREATE TABLE IF NOT EXISTS screening.blacklist_sync_source_stats (
    id UUID PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES screening.blacklist_sync_runs(id) ON DELETE CASCADE,
    source_type VARCHAR(16) NOT NULL,
    source_ref_id VARCHAR(64) NOT NULL,
    source_url TEXT NOT NULL,
    started_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMPTZ NULL,
    duration_ms BIGINT NOT NULL DEFAULT 0,
    attempt_count INT NOT NULL DEFAULT 1,
    status VARCHAR(40) NOT NULL DEFAULT 'RUNNING',
    total_retrieved INT NOT NULL DEFAULT 0,
    new_count INT NOT NULL DEFAULT 0,
    updated_count INT NOT NULL DEFAULT 0,
    skipped_count INT NOT NULL DEFAULT 0,
    removed_count INT NOT NULL DEFAULT 0,
    failed_count INT NOT NULL DEFAULT 0,
    user_error_message TEXT NULL,
    technical_error_details TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_bl_sync_source_stats_run
    ON screening.blacklist_sync_source_stats (run_id, source_type);

CREATE INDEX IF NOT EXISTS idx_bl_sync_source_stats_source_date
    ON screening.blacklist_sync_source_stats (source_type, started_at DESC);

-- 5. Detailed Record Audit Items (NEW, UPDATED, SKIPPED, REMOVED, FAILED)
CREATE TABLE IF NOT EXISTS screening.blacklist_sync_items (
    id BIGSERIAL PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES screening.blacklist_sync_runs(id) ON DELETE CASCADE,
    source_type VARCHAR(16) NOT NULL,
    source_ref_id VARCHAR(64) NOT NULL,
    entity_external_id VARCHAR(255) NOT NULL,
    full_name TEXT NOT NULL,
    action VARCHAR(20) NOT NULL,
    reason TEXT NOT NULL,
    comparison_result VARCHAR(64) NOT NULL,
    previous_details JSONB NULL,
    current_details JSONB NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_bl_sync_items_run_action_source
    ON screening.blacklist_sync_items (run_id, action, source_type, id);

CREATE INDEX IF NOT EXISTS idx_bl_sync_items_source_date
    ON screening.blacklist_sync_items (source_type, processed_at DESC, action);

CREATE INDEX IF NOT EXISTS idx_bl_sync_items_skipped_retention
    ON screening.blacklist_sync_items (processed_at, id)
    WHERE action = 'SKIPPED';

-- 6. Structured Error & Troubleshooting Ledger
CREATE TABLE IF NOT EXISTS screening.blacklist_sync_errors (
    id BIGSERIAL PRIMARY KEY,
    run_id UUID NOT NULL REFERENCES screening.blacklist_sync_runs(id) ON DELETE CASCADE,
    source_type VARCHAR(16) NOT NULL,
    error_category VARCHAR(50) NOT NULL,
    user_message TEXT NOT NULL,
    technical_details TEXT NOT NULL,
    affected_record_id VARCHAR(255) NULL,
    is_recoverable BOOLEAN NOT NULL DEFAULT true,
    retry_attempted BOOLEAN NOT NULL DEFAULT false,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_bl_sync_errors_run
    ON screening.blacklist_sync_errors (run_id, occurred_at DESC);
