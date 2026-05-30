-- One-off, idempotent grant for the ATS (offers/scorecards) permissions on an ALREADY-seeded DB.
-- Needed because db/init/03_seed_dev.sql only runs on first container init (local Docker); a
-- hand-applied DB (e.g. Neon) seeded before the offer.*/interview.* permissions existed never
-- picked them up. This is a manifestation of DEBT-002 (no versioned migration path yet).
--
-- Run as the owner:  psql "<owner conn>" -f db/neon/grant_ats_permissions.sql
-- Safe to run repeatedly.

-- 1) ensure the permission rows exist in the global catalog
INSERT INTO permission (id, key) VALUES
  (gen_random_uuid(), 'offer.read'),
  (gen_random_uuid(), 'offer.write'),
  (gen_random_uuid(), 'interview.read'),
  (gen_random_uuid(), 'interview.write')
ON CONFLICT (key) DO NOTHING;

-- 2) grant them to the seeded hr_admin role (tenant Acme Demo)
INSERT INTO role_permission (tenant_id, role_id, permission_id)
SELECT '01900000-0000-7000-8000-0000000000a1',
       '01900000-0000-7000-8000-0000000000b1',
       p.id
FROM permission p
WHERE p.key IN ('offer.read', 'offer.write', 'interview.read', 'interview.write')
ON CONFLICT (role_id, permission_id) DO NOTHING;

SELECT 'hr_admin now holds ' || count(*) || ' permissions' AS status
FROM role_permission rp JOIN role r ON r.id = rp.role_id
WHERE r.key = 'hr_admin';
