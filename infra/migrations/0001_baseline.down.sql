-- 0001_baseline.down.sql — inverse of the baseline. Drops all baseline objects in
-- reverse-dependency order. CASCADE on the FK-referenced tables keeps it robust; policies and the
-- audit_log grant disappear with their tables.

-- ATS (0004 era)
DROP TABLE IF EXISTS offer CASCADE;
DROP TABLE IF EXISTS interview CASCADE;
DROP TABLE IF EXISTS application CASCADE;
DROP TABLE IF EXISTS candidate CASCADE;
DROP TABLE IF EXISTS job CASCADE;
DROP TABLE IF EXISTS pipeline CASCADE;

-- platform
DROP TABLE IF EXISTS consent_record CASCADE;
DROP TABLE IF EXISTS audit_log CASCADE;
DROP TABLE IF EXISTS outbox_message CASCADE;

-- identity & RBAC
DROP TABLE IF EXISTS user_role CASCADE;
DROP TABLE IF EXISTS role_permission CASCADE;
DROP TABLE IF EXISTS permission CASCADE;
DROP TABLE IF EXISTS role CASCADE;
DROP TABLE IF EXISTS user_account CASCADE;

-- people
DROP TABLE IF EXISTS employment_record CASCADE;
DROP TABLE IF EXISTS employee CASCADE;

-- tenancy
DROP TABLE IF EXISTS department CASCADE;
DROP TABLE IF EXISTS company CASCADE;
DROP TABLE IF EXISTS tenant CASCADE;
