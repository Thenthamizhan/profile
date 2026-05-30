-- Leave & Claims store. Kept in sync with infra/migrations/0004_leave_request.up.sql so the local
-- Docker / Testcontainers path (db/init) and the migration path converge on the same schema.

CREATE TABLE IF NOT EXISTS leave_request (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  employee_id   uuid NOT NULL REFERENCES employee(id),
  leave_type    text NOT NULL,
  start_date    date NOT NULL,
  end_date      date NOT NULL,
  days          numeric(5,2) NOT NULL,
  reason        text,
  status        text NOT NULL DEFAULT 'pending',
  requested_by  uuid,
  decided_by    uuid,
  decided_at    timestamptz,
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_leave_request_tenant_emp ON leave_request (tenant_id, employee_id);
CREATE INDEX IF NOT EXISTS ix_leave_request_tenant_status ON leave_request (tenant_id, status);

ALTER TABLE leave_request ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON leave_request;
CREATE POLICY tenant_isolation ON leave_request
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
