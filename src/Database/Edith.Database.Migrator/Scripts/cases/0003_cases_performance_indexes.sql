-- Performance Indexes for Cases Workspaces, Concurrency, and SLAs
CREATE INDEX IF NOT EXISTS idx_aml_cases_tenant_status_created 
    ON cases.aml_cases(tenant_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_aml_cases_tenant_analyst 
    ON cases.aml_cases(tenant_id, assigned_analyst_id);

CREATE INDEX IF NOT EXISTS idx_aml_cases_lock_expiry 
    ON cases.aml_cases(locked_by_user_id, lock_expires_at);

CREATE INDEX IF NOT EXISTS idx_case_notes_case_created 
    ON cases.case_notes(case_id, created_at ASC);

CREATE INDEX IF NOT EXISTS idx_case_activities_case_created 
    ON cases.case_activities(case_id, created_at ASC);
