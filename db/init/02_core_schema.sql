-- SahaHR core schema (vertical-slice scope): tenancy, identity/RBAC, people, platform.
-- Canonical source of truth (architecture §6). Applied by Postgres on first container start
-- as the owner role; the app connects as sahahr_app, to which Row-Level Security applies (§4.4).
-- Money is numeric(18,4) only; tenant_id on every tenant-scoped table; RLS as the backstop.

-- ============================ tenancy (shared kernel) ============================

CREATE TABLE IF NOT EXISTS tenant (
  id             uuid PRIMARY KEY,
  name           text NOT NULL,
  subdomain      text UNIQUE NOT NULL,
  isolation_tier text NOT NULL DEFAULT 'pooled',     -- pooled|bridge|siloed
  plan           text NOT NULL DEFAULT 'standard',
  branding       jsonb NOT NULL DEFAULT '{}',
  region         text NOT NULL DEFAULT 'ap-southeast-1',
  created_at     timestamptz NOT NULL DEFAULT now(),
  deleted_at     timestamptz
);

CREATE TABLE IF NOT EXISTS company (                  -- legal entity
  id               uuid PRIMARY KEY,
  tenant_id        uuid NOT NULL REFERENCES tenant(id),
  legal_name       text NOT NULL,
  uen              text,
  country          char(2) NOT NULL DEFAULT 'SG',
  base_currency    char(3) NOT NULL DEFAULT 'SGD',
  cpf_employer_ref text,
  iras_profile     jsonb,
  created_at       timestamptz NOT NULL DEFAULT now(),
  deleted_at       timestamptz
);
CREATE INDEX IF NOT EXISTS ix_company_tenant ON company (tenant_id);

CREATE TABLE IF NOT EXISTS department (
  id          uuid PRIMARY KEY,
  tenant_id   uuid NOT NULL,
  company_id  uuid NOT NULL REFERENCES company(id),
  parent_id   uuid REFERENCES department(id),
  name        text NOT NULL,
  cost_center text,
  created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_department_tenant ON department (tenant_id);

-- ============================ people ============================

CREATE TABLE IF NOT EXISTS employee (
  id                uuid PRIMARY KEY,
  tenant_id         uuid NOT NULL,
  company_id        uuid NOT NULL REFERENCES company(id),
  employee_no       text NOT NULL,
  first_name        text NOT NULL,
  last_name         text NOT NULL,
  work_email        text,
  national_id_enc   bytea,            -- NRIC/FIN, column-encrypted (§8.3)
  dob_enc           bytea,
  bank_account_enc  bytea,
  status            text NOT NULL DEFAULT 'active',   -- active|on_leave|terminated
  hire_date         date,
  termination_date  date,
  custom_fields     jsonb NOT NULL DEFAULT '{}',
  created_at        timestamptz NOT NULL DEFAULT now(),
  deleted_at        timestamptz,
  UNIQUE (company_id, employee_no)
);
CREATE INDEX IF NOT EXISTS ix_employee_tenant ON employee (tenant_id);
CREATE INDEX IF NOT EXISTS ix_employee_tenant_status ON employee (tenant_id, status);

CREATE TABLE IF NOT EXISTS employment_record (        -- bitemporal position/reporting history
  id              uuid PRIMARY KEY,
  tenant_id       uuid NOT NULL,
  employee_id     uuid NOT NULL REFERENCES employee(id),
  manager_id      uuid REFERENCES employee(id),
  department_id   uuid REFERENCES department(id),
  employment_type text NOT NULL,                      -- full_time|part_time|contract
  effective_from  date NOT NULL,
  effective_to    date,
  created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_employment_record_tenant_emp ON employment_record (tenant_id, employee_id);

-- ============================ identity & RBAC ============================

CREATE TABLE IF NOT EXISTS user_account (
  id          uuid PRIMARY KEY,
  tenant_id   uuid NOT NULL,
  employee_id uuid REFERENCES employee(id),
  email       text NOT NULL,
  status      text NOT NULL DEFAULT 'active',
  mfa_enabled boolean NOT NULL DEFAULT false,
  sso_subject text,
  created_at  timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, email)
);

CREATE TABLE IF NOT EXISTS role (
  id        uuid PRIMARY KEY,
  tenant_id uuid NOT NULL,
  key       text NOT NULL,
  name      text NOT NULL,
  is_system boolean NOT NULL DEFAULT false,
  UNIQUE (tenant_id, key)
);

CREATE TABLE IF NOT EXISTS permission (                -- GLOBAL catalog (intentionally no tenant_id)
  id  uuid PRIMARY KEY,
  key text UNIQUE NOT NULL                             -- e.g. 'employee.salary.read'
);

CREATE TABLE IF NOT EXISTS role_permission (
  tenant_id     uuid NOT NULL,                         -- denormalized for RLS
  role_id       uuid NOT NULL REFERENCES role(id),
  permission_id uuid NOT NULL REFERENCES permission(id),
  constraints   jsonb NOT NULL DEFAULT '{}',
  PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE IF NOT EXISTS user_role (
  tenant_id uuid NOT NULL,                             -- denormalized for RLS
  user_id   uuid NOT NULL REFERENCES user_account(id),
  role_id   uuid NOT NULL REFERENCES role(id),
  scope     jsonb NOT NULL DEFAULT '{}',
  PRIMARY KEY (user_id, role_id)
);

-- ============================ platform: outbox + audit + consent ============================

CREATE TABLE IF NOT EXISTS outbox_message (            -- transactional outbox (§3.3, FF-6)
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  type          text NOT NULL,                         -- domain event name, e.g. 'people.EmployeeHired'
  payload       jsonb NOT NULL,
  occurred_at   timestamptz NOT NULL DEFAULT now(),
  processed_at  timestamptz
);
CREATE INDEX IF NOT EXISTS ix_outbox_unprocessed ON outbox_message (occurred_at) WHERE processed_at IS NULL;

CREATE TABLE IF NOT EXISTS audit_log (                 -- append-only (§8.5)
  id          uuid PRIMARY KEY,
  tenant_id   uuid NOT NULL,
  actor_id    uuid,
  actor_type  text,                                    -- user|system|ai
  action      text NOT NULL,                           -- e.g. 'employee.create'
  entity_type text,
  entity_id   uuid,
  before      jsonb,
  after       jsonb,
  ip          inet,
  user_agent  text,
  occurred_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_audit_tenant_time ON audit_log (tenant_id, occurred_at DESC);

CREATE TABLE IF NOT EXISTS consent_record (
  id           uuid PRIMARY KEY,
  tenant_id    uuid NOT NULL,
  subject_id   uuid NOT NULL,
  purpose      text NOT NULL,
  granted      boolean NOT NULL,
  basis        text,
  granted_at   timestamptz,
  withdrawn_at timestamptz,
  evidence     jsonb
);

-- ============================ Row-Level Security (the backstop, §4.4) ============================
-- Policy: a row is visible iff its tenant_id matches the per-connection GUC app.tenant_id.
-- current_setting(..., true) returns NULL when unset -> predicate false -> zero rows (fail-closed).
-- Stated explicitly per table (not a loop) so it is greppable + auditable (FF-2).

ALTER TABLE company           ENABLE ROW LEVEL SECURITY;
ALTER TABLE department        ENABLE ROW LEVEL SECURITY;
ALTER TABLE employee          ENABLE ROW LEVEL SECURITY;
ALTER TABLE employment_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_account      ENABLE ROW LEVEL SECURITY;
ALTER TABLE role              ENABLE ROW LEVEL SECURITY;
ALTER TABLE role_permission   ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_role         ENABLE ROW LEVEL SECURITY;
ALTER TABLE outbox_message    ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_log         ENABLE ROW LEVEL SECURITY;
ALTER TABLE consent_record    ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'company','department','employee','employment_record',
    'user_account','role','role_permission','user_role',
    'outbox_message','audit_log','consent_record'
  ] LOOP
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I;', t);
    EXECUTE format(
      'CREATE POLICY tenant_isolation ON %I USING (tenant_id = current_setting(''app.tenant_id'', true)::uuid);', t);
  END LOOP;
END $$;

-- audit_log is append-only for the app role: INSERT + SELECT, never UPDATE/DELETE (§6.6).
REVOKE UPDATE, DELETE ON audit_log FROM sahahr_app;
