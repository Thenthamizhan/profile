-- One-off, idempotent grant for the Claims permissions on an ALREADY-seeded DB (Neon).
-- Same rationale as grant_ats / grant_leave: db/init/03 only runs on first container init, so a
-- hand-applied DB seeded earlier never picked these up (DEBT-002 residue).
--
-- Run as the owner:  psql "<owner conn>" -f db/neon/grant_claim_permissions.sql
-- Safe to run repeatedly.

INSERT INTO permission (id, key) VALUES
  (gen_random_uuid(), 'claim.read'),
  (gen_random_uuid(), 'claim.request'),
  (gen_random_uuid(), 'claim.approve'),
  (gen_random_uuid(), 'claim.reimburse')
ON CONFLICT (key) DO NOTHING;

INSERT INTO role_permission (tenant_id, role_id, permission_id)
SELECT '01900000-0000-7000-8000-0000000000a1',
       '01900000-0000-7000-8000-0000000000b1',
       p.id
FROM permission p
WHERE p.key IN ('claim.read', 'claim.request', 'claim.approve', 'claim.reimburse')
ON CONFLICT (role_id, permission_id) DO NOTHING;

SELECT 'hr_admin now holds ' || count(*) || ' permissions' AS status
FROM role_permission rp JOIN role r ON r.id = rp.role_id
WHERE r.key = 'hr_admin';
