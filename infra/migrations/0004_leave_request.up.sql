-- 0004_leave_request.up.sql — Leave & Claims context (architecture §6.5). Leave requests go through
-- a simple state machine: pending → approved|rejected|cancelled. Tenant-scoped (FF-2); `days` is
-- numeric (fractional days for half-day leave), not float (FF-5 family).

CREATE TABLE IF NOT EXISTS leave_request (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  employee_id   uuid NOT NULL REFERENCES employee(id),
  leave_type    text NOT NULL,                        -- annual|sick|unpaid|...
  start_date    date NOT NULL,
  end_date      date NOT NULL,
  days          numeric(5,2) NOT NULL,                -- fractional days allowed (half-day)
  reason        text,
  status        text NOT NULL DEFAULT 'pending',      -- pending|approved|rejected|cancelled
  requested_by  uuid,                                  -- the actor who submitted
  decided_by    uuid,                                  -- the approver/rejecter (maker != checker)
  decided_at    timestamptz,
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_leave_request_tenant_emp ON leave_request (tenant_id, employee_id);
CREATE INDEX IF NOT EXISTS ix_leave_request_tenant_status ON leave_request (tenant_id, status);

ALTER TABLE leave_request ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON leave_request;
CREATE POLICY tenant_isolation ON leave_request
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
