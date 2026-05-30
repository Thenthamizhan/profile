# Versioned SQL migrations

Pays **DEBT-002**: schema evolution is now versioned, ordered, and reversible — replacing the
`db/init/*` scripts that only ran on first container init (and so silently drifted on
already-provisioned databases like Neon).

## Why SQL, not EF Core migrations

The EF model is a deliberately *thin* mapping over a *richer* SQL schema: Row-Level Security
policies, the append-only `audit_log` grant (`REVOKE UPDATE, DELETE … FROM sahahr_app`),
column-encrypted `bytea` PII, and (future) PostGIS + partitioning. EF Core cannot model those, so
EF-generated migrations would either drop that fidelity or wrap hand-written raw SQL anyway. The
architecture is SQL-first (§6) and calls for a Flyway-style approach (§16.3). So migrations are
plain SQL, applied by `scripts/migrate/run.mjs`.

## Layout

- `NNNN_name.up.sql`   — forward migration (applied in filename order)
- `NNNN_name.down.sql` — exact inverse (for rollback); required by FF-12
- `0001_baseline` is the frozen snapshot of the schema as of the `db/init` era.

## Prerequisites (NOT migrations — they bootstrap the database)

Run once, as the **owner**, before migrating:
- extensions: `pgcrypto`, `pg_trgm`  (local: `db/init/01_init.sh`; Neon: `db/neon/bootstrap.sql`)
- the `sahahr_app` login role (RLS-bound; the app connects as it, the owner runs migrations)

## Usage

```bash
# apply all pending migrations (owner connection)
node scripts/migrate/run.mjs up   "postgresql://sahahr_owner:...@host:5544/sahahr"

# roll back the most recently applied migration
node scripts/migrate/run.mjs down "postgresql://sahahr_owner:...@host:5544/sahahr"

# show applied vs pending
node scripts/migrate/run.mjs status "postgresql://..."
```

The runner tracks applied migrations in a `schema_migrations` table and wraps each migration in a
transaction. Migrations are written idempotently (`IF NOT EXISTS` / guarded policy drops) so a
baseline can be applied on top of a database that `db/init` already built, without error.

## Relationship to `db/init`

`db/init` remains the **local-dev convenience** (Docker Compose auto-applies it on first boot, so
`pnpm infra:up` gives a ready DB). For any shared/staging/prod database, migrations are the source
of truth. The baseline (`0001`) is intentionally identical in effect to `db/init/02`+`04`, so the
two paths converge on the same schema.
