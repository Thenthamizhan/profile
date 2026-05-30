-- DEV-ONLY ATS seed (runs after 04_ats_schema.sql). A default pipeline, one open job, and a
-- couple of candidates/applications so the Kanban board has content. Fixed UUIDs are used by tests.

INSERT INTO pipeline (id, tenant_id, name, stages) VALUES
  ('01900000-0000-7000-8000-00000000e001', '01900000-0000-7000-8000-0000000000a1', 'Default pipeline',
   '[{"key":"applied","name":"Applied"},
     {"key":"screening","name":"Screening"},
     {"key":"interview","name":"Interview"},
     {"key":"offer","name":"Offer"},
     {"key":"hired","name":"Hired"}]'::jsonb)
ON CONFLICT (id) DO NOTHING;

INSERT INTO job (id, tenant_id, company_id, pipeline_id, title, status, location, employment_type, posted_at) VALUES
  ('01900000-0000-7000-8000-00000000f001', '01900000-0000-7000-8000-0000000000a1',
   '01900000-0000-7000-8000-0000000000c1', '01900000-0000-7000-8000-00000000e001',
   'Senior Backend Engineer', 'open', 'Singapore', 'full_time', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO candidate (id, tenant_id, full_name, email, source) VALUES
  ('01900000-0000-7000-8000-00000000ca01', '01900000-0000-7000-8000-0000000000a1', 'Jasmine Tan', 'jasmine.tan@example.com', 'portal'),
  ('01900000-0000-7000-8000-00000000ca02', '01900000-0000-7000-8000-0000000000a1', 'Arif Lee', 'arif.lee@example.com', 'referral')
ON CONFLICT (id) DO NOTHING;

INSERT INTO application (id, tenant_id, job_id, candidate_id, current_stage, match_score) VALUES
  ('01900000-0000-7000-8000-00000000aa01', '01900000-0000-7000-8000-0000000000a1',
   '01900000-0000-7000-8000-00000000f001', '01900000-0000-7000-8000-00000000ca01', 'applied', 92.0),
  ('01900000-0000-7000-8000-00000000aa02', '01900000-0000-7000-8000-0000000000a1',
   '01900000-0000-7000-8000-00000000f001', '01900000-0000-7000-8000-00000000ca02', 'screening', 88.0)
ON CONFLICT (job_id, candidate_id) DO NOTHING;
