-- Neon bootstrap — run ONCE as neondb_owner. Managed-Postgres equivalent of db/init/01_init.sh
-- (we can't use the docker entrypoint on Neon). Creates the RLS-bound application role so the
-- table owner (neondb_owner) runs DDL while the app connects as a NON-owner -> Row-Level Security
-- actually applies (§4.4). The table owner bypasses RLS by default, so the app must not be the owner.
--
-- Usage (password supplied as a psql variable, never written into this file):
--   psql "<owner conn>" -v app_password="<generated>" -f db/neon/bootstrap.sql

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Create the app login role only if missing (idempotent via \gexec; %L safely quotes the value).
SELECT format('CREATE ROLE sahahr_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sahahr_app')
\gexec

GRANT CONNECT ON DATABASE neondb TO sahahr_app;
GRANT USAGE  ON SCHEMA   public TO sahahr_app;

-- Tables/sequences the owner creates later are usable by the app role (RLS still constrains rows).
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sahahr_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO sahahr_app;

SELECT 'bootstrap complete; sahahr_app present = ' ||
       EXISTS(SELECT 1 FROM pg_roles WHERE rolname='sahahr_app')::text AS status;
