-- SahaHR Recruitment / ATS schema (architecture §6.4). Same discipline as core:
-- tenant_id on every table, RLS as the backstop, UUIDv7 PKs, money as numeric(18,4).
-- Applied by Postgres on first container start, after 02_core_schema.sql.

CREATE TABLE IF NOT EXISTS pipeline (                 -- configurable hiring stages
  id        uuid PRIMARY KEY,
  tenant_id uuid NOT NULL,
  name      text NOT NULL,
  stages    jsonb NOT NULL DEFAULT '[]',              -- ordered [{key,name}]
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_pipeline_tenant ON pipeline (tenant_id);

CREATE TABLE IF NOT EXISTS job (
  id              uuid PRIMARY KEY,
  tenant_id       uuid NOT NULL,
  company_id      uuid NOT NULL REFERENCES company(id),
  pipeline_id     uuid NOT NULL REFERENCES pipeline(id),
  title           text NOT NULL,
  description     text,
  status          text NOT NULL DEFAULT 'open',       -- draft|open|on_hold|closed
  location        text,
  employment_type text,
  posted_at       timestamptz,
  created_by      uuid,
  created_at      timestamptz NOT NULL DEFAULT now(),
  deleted_at      timestamptz
);
CREATE INDEX IF NOT EXISTS ix_job_tenant_status ON job (tenant_id, status);

CREATE TABLE IF NOT EXISTS candidate (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  full_name     text,
  email         text,
  phone_enc     bytea,                                -- PII, column-encrypted (§8.3)
  source        text,                                 -- portal|referral|sourced|agency
  resume_s3_key text,
  parsed        jsonb,                                -- AI resume parse output
  consent       jsonb,                                -- PDPA consent record
  created_at    timestamptz NOT NULL DEFAULT now(),
  deleted_at    timestamptz
);
CREATE INDEX IF NOT EXISTS ix_candidate_tenant ON candidate (tenant_id);

CREATE TABLE IF NOT EXISTS application (
  id            uuid PRIMARY KEY,
  tenant_id     uuid NOT NULL,
  job_id        uuid NOT NULL REFERENCES job(id),
  candidate_id  uuid NOT NULL REFERENCES candidate(id),
  current_stage text NOT NULL DEFAULT 'applied',
  match_score   numeric(5,2),                         -- AI match %
  status        text NOT NULL DEFAULT 'active',       -- active|rejected|hired|withdrawn
  created_at    timestamptz NOT NULL DEFAULT now(),
  UNIQUE (job_id, candidate_id)
);
-- powers the Kanban board load (job -> columns by stage)
CREATE INDEX IF NOT EXISTS ix_application_tenant_job_stage ON application (tenant_id, job_id, current_stage);

CREATE TABLE IF NOT EXISTS interview (
  id             uuid PRIMARY KEY,
  tenant_id      uuid NOT NULL,
  application_id uuid NOT NULL REFERENCES application(id),
  scheduled_at   timestamptz,
  interviewers   uuid[],
  scorecard      jsonb,
  created_at     timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_interview_tenant_app ON interview (tenant_id, application_id);

CREATE TABLE IF NOT EXISTS offer (
  id              uuid PRIMARY KEY,
  tenant_id       uuid NOT NULL,
  application_id  uuid NOT NULL REFERENCES application(id),
  salary          numeric(18,4),                      -- money: numeric only
  currency        char(3),
  status          text NOT NULL DEFAULT 'draft',      -- draft|sent|accepted|declined
  document_s3_key text,
  sent_at         timestamptz,
  responded_at    timestamptz,
  created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_offer_tenant_app ON offer (tenant_id, application_id);

-- ============================ Row-Level Security (§4.4) ============================
-- Stated explicitly per table so it is greppable + auditable (FF-2).

ALTER TABLE pipeline    ENABLE ROW LEVEL SECURITY;
ALTER TABLE job         ENABLE ROW LEVEL SECURITY;
ALTER TABLE candidate   ENABLE ROW LEVEL SECURITY;
ALTER TABLE application ENABLE ROW LEVEL SECURITY;
ALTER TABLE interview   ENABLE ROW LEVEL SECURITY;
ALTER TABLE offer       ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['pipeline','job','candidate','application','interview','offer'] LOOP
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I;', t);
    EXECUTE format(
      'CREATE POLICY tenant_isolation ON %I USING (tenant_id = current_setting(''app.tenant_id'', true)::uuid);', t);
  END LOOP;
END $$;
