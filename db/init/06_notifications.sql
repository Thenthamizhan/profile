-- Notifications context store. Kept in sync with infra/migrations/0003_notification.up.sql so the
-- local Docker / Testcontainers path (db/init) and the migration path converge on the same schema.

CREATE TABLE IF NOT EXISTS notification (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  topic         text NOT NULL,
  channel       text NOT NULL DEFAULT 'inapp',
  subject       text NOT NULL,
  body          text,
  recipient     text,
  status        text NOT NULL DEFAULT 'pending',
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_notification_tenant_created ON notification (tenant_id, created_at DESC);

ALTER TABLE notification ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON notification;
CREATE POLICY tenant_isolation ON notification
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
