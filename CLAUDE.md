# SahaHR — working notes for agents

Enterprise multi-tenant HRMS. Read `docs/HRMS_Architecture.md` (the product blueprint) and
`docs/Agent_Operating_Model.md` (how this codebase is governed) for the full picture. This file is
the **operational cheat-sheet** — the things that cost real time to (re)discover.

## Working agreements (non-negotiable)

- **Never commit on red.** Read the build/test exit code *before* `git commit`. A commit message
  must not claim a result that wasn't observed. (This was violated repeatedly early on; don't.)
- **"Tool call issued" ≠ "step succeeded."** Read the actual output. For UI/browser work, assert on
  what the page/DB actually shows, not on what an action *should* have done.
- **Verify, don't narrate.** CI is the source of truth for green — see `.github/workflows/ci.yml`.
- Commit only when the user asks; branch off `main` first. Conventional Commits.

## Layout

```
apps/api/      ASP.NET Core 9 modular monolith (SahaHR.sln). Bounded contexts = modules:
               Common (tenancy, outbox, RBAC, audit, DbContext, Security cipher, Observability),
               Identity, People, Recruitment, Notifications, Leave (+Claims).
apps/web/      Next.js 16 + React 19 admin app (server-side BFF). UI design system in
               components/ui (calm-enterprise, §14); authenticated shell = app/(app) route group.
               e2e/ = Playwright.
db/init/       Canonical SQL: 01 roles, 02 core schema+RLS, 03 seed, 04 ATS, 05 ATS seed,
               06 notifications, 07 leave, 08 claims. ⚠ Runs ONLY on first container init (DEBT-002).
db/neon/       Neon (cloud) bootstrap + one-off grant scripts.
scripts/fitness/  Executable architectural fitness functions (FF-1..18). `pnpm ff`.
infra/         docker-compose (local Postgres+Redis). Terraform/Helm later.
.claude/agents/   The 28-agent operating model, materialized.
docs/debt/register.md   Technical-debt register (CI-1 owns it).
```

## Run it locally

```bash
pnpm install
pnpm infra:up                                   # Postgres on localhost:5544, Redis on 6380
dotnet run --project apps/api/src/SahaHR.Api    # API on :5080  (needs .env or appsettings)
# web — MUST set SAHAHR_API_URL or the login server action fails silently (see Gotchas):
SAHAHR_API_URL=http://127.0.0.1:5080 pnpm -C apps/web dev   # :3000
```

Open http://localhost:3000 → `/login` → seeded tenant/user pre-filled → Sign in.

## Test / verify

```bash
pnpm ff                                                              # fitness functions
dotnet test apps/api/SahaHR.sln                                      # 21 integration tests (Testcontainers — needs Docker)
pnpm -C apps/web exec playwright test --project=chromium             # 3 E2E (starts its own servers)
```

CI runs all three on push/PR.

## Gotchas (learned the hard way — each cost real time)

1. **`SAHAHR_API_URL` must be set for the web process.** The login + all data flows are *server
   actions* that call the API from the Next server. If unset and the API isn't on the default
   `http://127.0.0.1:5080`, the login action throws and the page **silently re-renders on /login**
   with no error. `apps/web/.env.example` documents it.

2. **Two-role DB model — the app must NOT be the table owner.** In Postgres the table owner bypasses
   RLS by default. DDL/migrations run as the owner (`sahahr_owner` local / `neondb_owner` on Neon);
   the app connects as **`sahahr_app`** (non-owner, no BYPASSRLS) so Row-Level Security actually
   applies. Connection strings: `ConnectionStrings__Default` = app role, `__Migrator` = owner.
   Neon's single role is an owner with `rolbypassrls=t`, so `db/neon/bootstrap.sql` creates
   `sahahr_app` separately. **Never point `__Default` at an owner role.**

3. **`db/init/*` only runs on FIRST container init.** Editing a seed file does NOT update an
   existing volume or a hand-applied DB (Neon). This is **DEBT-002** and it has bitten us (the Neon
   ATS-permission gap). To re-seed local: `docker compose -f infra/docker-compose.yml down -v && pnpm infra:up`.

4. **Neon: use the DIRECT endpoint, not `-pooler`,** for `ConnectionStrings__Default`. RLS uses a
   session GUC (`app.tenant_id`); the transaction interceptor makes writes pooler-safe, but
   non-transactional reads on a pooler are not (DEBT-004). Direct endpoint = one session per conn.

5. **EF snake_case convention mis-splits acronyms** — `ResumeS3Key` → `resume_s3key`, but the column
   is `resume_s3_key`. Map such columns explicitly with `HasColumnName`. **FF-18** catches this.

6. **Browser/E2E via Playwright, not hand-driving.** Manual browser tooling here was flaky
   (stale element refs, tab handling). The Playwright suite in `apps/web/e2e` is the reliable path —
   extend it instead. Ports are overridable: `E2E_WEB_PORT` / `E2E_API_PORT` (CI uses 3000/5080) —
   needed when another project already holds those ports locally.

7. **Some services read config at *registration* time, before `builder.Build()`** — connection
   strings, the PII `Encryption:DataKey`, and `RateLimit:PermitLimit`. The env-var provider outranks
   appsettings and is in place then; `ConfigureAppConfiguration`/`UseSetting` overrides may be too
   late. So integration tests that need to override these set **environment variables** (see
   `SahaHrApiFactory` and `OpsHardeningTests`' `RateLimit__PermitLimit`).

## Security & ops (env vars)

- **`Encryption__DataKey`** (base64, 32 bytes) — REQUIRED. PII (`employee.national_id/dob/bank`) is
  AES-256-GCM encrypted at rest (§8.3); the API **fails fast at startup** without it. Dev/test key is
  in `appsettings.Development.json` / `SahaHrApiFactory`. Generate prod: `openssl rand -base64 32`.
- **`Jwt__Authority`** set → production OIDC: tokens validated against the IdP JWKS, perms resolved
  from RBAC tables by `PermissionClaimsTransformation`. Unset → dev HS256 mint (`/v1/dev/token`,
  Development only). See `docs/AUTH.md`.
- **`RateLimit__PermitLimit` / `__WindowSeconds`** — coarse global limiter (per subject, else IP).
  Production default 120/60s; Development is effectively unlimited so suites aren't throttled.
- Unhandled exceptions → RFC7807 ProblemDetails (no stack leak in prod); every response carries an
  `X-Correlation-Id` and logs are JSON-with-scopes outside Development.

## Architectural invariants (enforced by fitness functions — don't break them)

- Every tenant-scoped table has `tenant_id` + an RLS policy (FF-2). Money is `numeric(18,4)`, never
  float (FF-5). No hard-coded statutory rates in payroll (FF-4). Modules don't import each other's
  internals — cross-context talk is via events only (FF-1). Migrations are reversible (FF-12).
  EF mappings resolve to real columns (FF-18).
- **Cross-context choreography**: contexts communicate through the outbox + `IDomainEventHandler`,
  never direct calls. Example: Recruitment emits `recruitment.CandidateHired`; People consumes it by
  topic string (no Recruitment import) and auto-provisions an employee idempotently. The
  `OutboxDispatcher` establishes tenant context from the message row before invoking handlers.
