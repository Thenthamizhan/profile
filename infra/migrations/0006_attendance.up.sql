-- 0006_attendance.up.sql — Time & Attendance context. A shift is one clock-in optionally closed by a
-- clock-out, which computes worked `hours` (numeric, not float — FF-5 family; it feeds payroll).
-- Tenant-scoped (FF-2). A partial unique index allows at most one open shift per employee.

CREATE TABLE IF NOT EXISTS attendance_entry (
  id           uuid PRIMARY KEY,
  tenant_id    uuid NOT NULL,
  employee_id  uuid NOT NULL REFERENCES employee(id),
  work_date    date NOT NULL,
  clock_in     timestamptz NOT NULL,
  clock_out    timestamptz,
  hours        numeric(6,2),                         -- null while open; computed on clock-out
  status       text NOT NULL DEFAULT 'open',         -- open|completed
  notes        text,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_attendance_tenant_emp ON attendance_entry (tenant_id, employee_id);
CREATE INDEX IF NOT EXISTS ix_attendance_tenant_status ON attendance_entry (tenant_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS ux_attendance_open_per_emp
  ON attendance_entry (tenant_id, employee_id) WHERE status = 'open';

ALTER TABLE attendance_entry ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance_entry;
CREATE POLICY tenant_isolation ON attendance_entry
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
