#!/bin/sh
# Runs once on first container start (postgres docker-entrypoint-initdb.d).
# Establishes extensions + the two-role model:
#   - sahahr_owner (POSTGRES_USER): superuser, owns schema, runs migrations/DDL
#   - sahahr_app:  least-privilege LOGIN role used at runtime; NOT superuser, NOT BYPASSRLS,
#                  so Row-Level Security actually applies to it (defense in depth, §4.4).
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
  CREATE EXTENSION IF NOT EXISTS pgcrypto;   -- gen_random_uuid() fallback
  CREATE EXTENSION IF NOT EXISTS pg_trgm;    -- trigram search

  DO \$\$
  BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sahahr_app') THEN
      CREATE ROLE sahahr_app LOGIN PASSWORD '${SAHAHR_APP_PASSWORD}';
    END IF;
  END
  \$\$;

  GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO sahahr_app;
  GRANT USAGE ON SCHEMA public TO sahahr_app;

  -- future tables/sequences created by the owner are usable by the app role
  ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sahahr_app;
  ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO sahahr_app;
EOSQL

echo "SahaHR: extensions + sahahr_app role provisioned."
