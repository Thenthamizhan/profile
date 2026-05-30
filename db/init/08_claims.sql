-- Claims store. Kept in sync with infra/migrations/0005_expense_claim.up.sql so the local Docker /
-- Testcontainers path (db/init) and the migration path converge on the same schema.

CREATE TABLE IF NOT EXISTS expense_claim (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  employee_id   uuid NOT NULL REFERENCES employee(id),
  category      text NOT NULL,
  amount        numeric(18,4) NOT NULL,
  currency      char(3) NOT NULL DEFAULT 'SGD',
  description   text,
  status        text NOT NULL DEFAULT 'pending',
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
