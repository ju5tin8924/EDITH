-- Migration 0004: World-Check Blacklist Data Source registration
-- Registers Refinitiv World-Check as an official sanctioned blacklist source in screening.blacklist_sources

UPDATE screening.watchlist_entities
SET source_type = 'WORLD_CHECK', source_ref_id = '540e11a944a9880b5137a07d'
WHERE source_type IS NULL AND (UPPER(source_list) LIKE 'WORLD_CHECK%' OR UPPER(source_list) LIKE 'WORLD%CHECK%');

INSERT INTO screening.blacklist_sources (source_type, source_ref_id, display_name, url, is_enabled, last_status)
VALUES
    ('WORLD_CHECK', '540e11a944a9880b5137a07d', 'Refinitiv World-Check Premium Global Intelligence', 'file:///opt/edith/worldcheck/premium-world-check.xml', true, 'NEVER_RUN')
ON CONFLICT (source_type) DO NOTHING;
