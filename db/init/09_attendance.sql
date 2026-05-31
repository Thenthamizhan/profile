-- Time & Attendance store. Kept in sync with infra/migrations/0006_attendance.up.sql so the local
-- Docker / Testcontainers path (db/init) and the migration path converge on the same schema.

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
-- At most one OPEN shift per employee (DB-level backstop for the service's check).
CREATE UNIQUE INDEX IF NOT EXISTS ux_attendance_open_per_emp
  ON attendance_entry (tenant_id, employee_id) WHERE status = 'open';

ALTER TABLE attendance_entry ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance_entry;
CREATE POLICY tenant_isolation ON attendance_entry
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
