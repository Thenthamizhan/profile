-- 0003_notification.up.sql — the Notifications context's store. A notification is recorded by
-- event consumers (e.g. on EmployeeHired) and later delivered by a channel worker (Phase 4).
-- Tenant-scoped (FF-2). channel/status are plain text enums-by-convention.

CREATE TABLE IF NOT EXISTS notification (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  topic         text NOT NULL,                 -- source event type, e.g. 'people.EmployeeHired'
  channel       text NOT NULL DEFAULT 'inapp', -- inapp|email|sms|push
  subject       text NOT NULL,
  body          text,
  recipient     text,                          -- email/handle; null = in-app/system
  status        text NOT NULL DEFAULT 'pending', -- pending|sent|failed
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_notification_tenant_created ON notification (tenant_id, created_at DESC);

ALTER TABLE notification ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON notification;
CREATE POLICY tenant_isolation ON notification
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
