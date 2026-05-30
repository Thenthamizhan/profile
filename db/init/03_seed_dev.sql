-- DEV-ONLY seed (runs on first container start). Gives the vertical slice a tenant, a company,
-- the employee.* permission catalog, an hr_admin role, and a user — so a dev JWT can exercise CRUD.
-- Fixed UUIDs are referenced by integration tests. Never load this in any non-local environment.

INSERT INTO tenant (id, name, subdomain) VALUES
  ('01900000-0000-7000-8000-0000000000a1', 'Acme Demo', 'acme')
ON CONFLICT (id) DO NOTHING;

INSERT INTO company (id, tenant_id, legal_name, uen) VALUES
  ('01900000-0000-7000-8000-0000000000c1', '01900000-0000-7000-8000-0000000000a1', 'Acme Pte Ltd', '200012345A')
ON CONFLICT (id) DO NOTHING;

INSERT INTO permission (id, key) VALUES
  (gen_random_uuid(), 'employee.read'),
  (gen_random_uuid(), 'employee.write'),
  (gen_random_uuid(), 'employee.delete')
ON CONFLICT (key) DO NOTHING;

INSERT INTO role (id, tenant_id, key, name, is_system) VALUES
  ('01900000-0000-7000-8000-0000000000b1', '01900000-0000-7000-8000-0000000000a1', 'hr_admin', 'HR Admin', true)
ON CONFLICT (id) DO NOTHING;

INSERT INTO role_permission (tenant_id, role_id, permission_id)
SELECT '01900000-0000-7000-8000-0000000000a1', '01900000-0000-7000-8000-0000000000b1', p.id
FROM permission p
WHERE p.key IN ('employee.read', 'employee.write', 'employee.delete')
ON CONFLICT (role_id, permission_id) DO NOTHING;

INSERT INTO user_account (id, tenant_id, email) VALUES
  ('01900000-0000-7000-8000-0000000000d1', '01900000-0000-7000-8000-0000000000a1', 'hr.admin@acme.example')
ON CONFLICT (id) DO NOTHING;

INSERT INTO user_role (tenant_id, user_id, role_id) VALUES
  ('01900000-0000-7000-8000-0000000000a1', '01900000-0000-7000-8000-0000000000d1', '01900000-0000-7000-8000-0000000000b1')
ON CONFLICT (user_id, role_id) DO NOTHING;
