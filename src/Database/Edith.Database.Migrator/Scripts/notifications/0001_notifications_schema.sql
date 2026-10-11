-- Module: notifications (MOD-NOTIF-DYN)
-- Source: MD_DOC/database_v1.0.md, section 1, SCHEMA: notifications. Verbatim except for IF NOT EXISTS on the schema.
CREATE SCHEMA IF NOT EXISTS notifications;

CREATE TABLE notifications.notification_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
    alert_type VARCHAR(50) NOT NULL,
    severity_tier VARCHAR(10) NOT NULL,
    target_channels VARCHAR(255) NOT NULL
);

CREATE TABLE notifications.delivery_ledgers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_rule_id UUID NOT NULL REFERENCES notifications.notification_rules(id) ON DELETE CASCADE,
    trace_id VARCHAR(100) NOT NULL UNIQUE,
    delivery_status VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    sent_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE notifications.user_shifts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES core.users(id) ON DELETE CASCADE,
    shift_start TIMESTAMPTZ NOT NULL,
    shift_end TIMESTAMPTZ NOT NULL,
    is_on_duty BOOLEAN NOT NULL DEFAULT false
);
