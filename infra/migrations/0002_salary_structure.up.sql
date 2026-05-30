-- 0002_salary_structure.up.sql — adds the bitemporal salary_structure table (architecture §6.5).
-- A real forward migration on top of the baseline: demonstrates versioned evolution and makes
-- FF-12 (migration reversibility) non-vacuous. Tenant-scoped (FF-2), money as numeric(18,4) (FF-5).

CREATE TABLE IF NOT EXISTS salary_structure (
  id             uuid PRIMARY KEY,
  tenant_id      uuid NOT NULL,
  employee_id    uuid NOT NULL REFERENCES employee(id),
  effective_from date NOT NULL,
  effective_to   date,                                   -- null = current
  basic          numeric(18,4) NOT NULL,                 -- money: numeric, never float (FF-5)
  currency       char(3) NOT NULL DEFAULT 'SGD',
  components     jsonb NOT NULL DEFAULT '[]',             -- allowances, fixed/variable, taxable flags
  created_at     timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_salary_structure_tenant_emp ON salary_structure (tenant_id, employee_id);

-- RLS backstop (FF-2): visible only within the current tenant.
ALTER TABLE salary_structure ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON salary_structure;
CREATE POLICY tenant_isolation ON salary_structure
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
