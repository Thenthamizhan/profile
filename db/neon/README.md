# Neon (cloud Postgres) setup

SahaHR's **Production / cloud** database. Local Docker (`pnpm infra:up`) remains the default for
dev and is what the integration tests use (hermetic Testcontainers) — Neon is **not** used by tests.

## Why a separate `sahahr_app` role (do not skip)

Neon provisions one role, `neondb_owner`, and it has **`rolbypassrls = true`** — the table owner
bypasses Row-Level Security. If the app connected as the owner, **tenant isolation would silently
not apply** (architecture §20.5: a cross-tenant leak is catastrophic). So we keep the two-role model:

- `neondb_owner` — runs DDL / migrations (owns the tables)
- `sahahr_app` — non-owner, `rolsuper=f`, `rolbypassrls=f`; the app connects as this → **RLS applies**

## One-time bootstrap

```bash
# 1. create the app role (password supplied as a psql var — never written to a file)
psql "<owner-direct-conn>" -v app_password="<generate-32-char>" -f db/neon/bootstrap.sql

# 2. apply schema + dev seed (owner role) in order
for f in 02_core_schema.sql 03_seed_dev.sql 04_ats_schema.sql 05_seed_ats.sql; do
  psql "<owner-direct-conn>" -v ON_ERROR_STOP=1 -f "db/init/$f"
done
```

## Connection notes

- **Use the DIRECT endpoint, not `-pooler`.** RLS relies on a session GUC (`SET app.tenant_id` via
  the connection interceptor). Neon's transaction-pooled endpoint can multiplex statements across
  backends, which breaks session-scoped settings. Direct endpoint = one session per connection.
- Connection strings live only in the gitignored `.env` (`ConnectionStrings__Default` = app role,
  `ConnectionStrings__Migrator` = owner). `.env.example` shows the shape with placeholders.
- Run the API against Neon by loading `.env` into the environment (env vars override appsettings).

## Verified invariants on Neon (re-run after any schema change)

| check | expected |
|-------|----------|
| `sahahr_app` rolsuper / rolbypassrls | `f` / `f` |
| `employee` table owner | `neondb_owner` (never `sahahr_app`) |
| RLS: correct tenant / wrong tenant / unset | rows / 0 / 0 (fail-closed) |
| `audit_log` UPDATE as app role | permission denied (append-only) |

> **Secret hygiene:** rotate the role passwords in the Neon console if a connection string is ever
> shared in plaintext. Never commit `.env`; `bootstrap.sql` takes the password as a `psql` variable.
