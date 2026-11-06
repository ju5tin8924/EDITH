-- Performance Indexes for WORM Audit Trail Queries and Regulatory Compliance
CREATE INDEX IF NOT EXISTS idx_audit_ledgers_tenant_action_created 
    ON core.audit_ledgers(tenant_id, action_type, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_audit_ledgers_created 
    ON core.audit_ledgers(created_at DESC);
