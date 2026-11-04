-- Migration 0005: Add pg_trgm extension and GIN trigram indexes for sub-20ms screening retrieval across 2.6M records
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX IF NOT EXISTS idx_watchlist_entities_trgm_name 
ON screening.watchlist_entities USING gin (UPPER(full_name) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_watchlist_entities_trgm_alias 
ON screening.watchlist_entities USING gin (UPPER(alias_name) gin_trgm_ops) 
WHERE alias_name IS NOT NULL;
