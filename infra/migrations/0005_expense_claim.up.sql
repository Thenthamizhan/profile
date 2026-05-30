-- 0005_expense_claim.up.sql — Claims (the other half of "Leave & Claims", architecture §5.1/§6.5).
-- Expense claims: submit → approved|rejected → reimbursed. Tenant-scoped (FF-2); amount is
-- numeric(18,4) — money is never float (FF-5). FK to employee enforces referential integrity.

CREATE TABLE IF NOT EXISTS expense_claim (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  employee_id   uuid NOT NULL REFERENCES employee(id),
  category      text NOT NULL,                        -- travel|meals|equipment|...
  amount        numeric(18,4) NOT NULL,               -- money: numeric, never float
  currency      char(3) NOT NULL DEFAULT 'SGD',
  description   text,
  status        text NOT NULL DEFAULT 'pending',      -- pending|approved|rejected|reimbursed
  requested_by  uuid,
  decided_by    uuid,
  decided_at    timestamptz,
  reimbursed_at timestamptz,
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_expense_claim_tenant_emp ON expense_claim (tenant_id, employee_id);
CREATE INDEX IF NOT EXISTS ix_expense_claim_tenant_status ON expense_claim (tenant_id, status);

ALTER TABLE expense_claim ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON expense_claim;
CREATE POLICY tenant_isolation ON expense_claim
  USING (tenant_id = current_setting('app.tenant_id', true)::uuid);
