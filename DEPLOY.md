# SahaHR — Deployment & Readiness Review

**Target:** Web → **Vercel**, API → **Render** (Docker), DB → **Neon** (already provisioned).
**Status of this review:** ✅ ready for a **staging / demo** deploy. ⚠️ **NOT yet cleared for real
employee PII** — see "Production gates" at the bottom. Read that section before loading real data.

## Architecture (why the split is clean)

```
Browser ──https──> Vercel (Next.js)  ──https (server-to-server)──>  Render (ASP.NET Core API)  ──>  Neon (Postgres)
                   server-side BFF                                   bounded-context modules           RLS per tenant
```

The Next.js app is a **server-side BFF**: the browser only ever talks to Vercel; the JWT lives in an
httpOnly cookie and the API is called from the Next *server*. **There is no browser→API traffic, so
there is no CORS to configure and no cross-origin cookie problem.** This is the main reason the
two-host split is low-risk.

## What was hardened for deploy (verified)

| Concern | Change | Verified |
|---------|--------|----------|
| Port binding | `Program.cs` binds `0.0.0.0:$PORT` when `PORT` is set (Render injects it) | ✅ container served `/health` 200 on `$PORT` |
| HTTPS metadata | `RequireHttpsMetadata` = true outside Development | ✅ build clean |
| Dev token endpoint | `/v1/dev/token` is `IsDevelopment()`-gated | ✅ returned **404** in a Production container |
| Cookie | `secure: true` when `NODE_ENV=production` | ✅ tsc clean |
| API image | multi-stage Dockerfile, non-root, Release publish | ✅ `docker build` + ran against Neon |
| Secrets | none committed; templates use placeholders; `.env*` gitignored | ✅ |

Local repro of the deploy artifact: built `apps/api/Dockerfile` from repo root, ran the image with
`ASPNETCORE_ENVIRONMENT=Production` + Neon connection strings → `/health` returned `{"status":"ok"}`
in ~2s and the dev-token route was absent (404).

## Deploy steps

### 0. Prerequisites
- GitHub repo pushed (`mvshashni/sahahr`), Neon DB migrated through `0005` (it is).
- Generate a JWT signing key: `openssl rand -base64 48`.

### 1. API → Render
1. New **Web Service** → connect the GitHub repo. Render reads `render.yaml` (Docker, root context,
   `apps/api/Dockerfile`, health check `/health`, Singapore region to match Neon).
2. Set the three `sync:false` env vars in the dashboard:
   - `ConnectionStrings__Default` — Neon **app role** (`sahahr_app`), **direct** endpoint, `SSL Mode=Require`
   - `ConnectionStrings__Migrator` — Neon **owner role**
   - `Jwt__SigningKey` — the generated key
   (`apps/api/.env.production.example` lists exact shapes.)
3. Deploy. Confirm `https://<svc>.onrender.com/health` → `{"status":"ok"}`.
4. **Migrations** (Render does not auto-migrate, by design). Neon is already at `0005`; for future
   schema changes run once from your machine:
   `node scripts/migrate/run.mjs up "<owner conn>"`

### 2. Web → Vercel
1. New project → import the repo → **Root Directory: `apps/web`**. Vercel auto-detects Next.js;
   `vercel.json` pins the install command (`pnpm install --frozen-lockfile`).
2. Set env var **`SAHAHR_API_URL`** = the Render API URL (no trailing slash). See
   `apps/web/.env.production.example`.
3. Deploy. Open the Vercel URL → `/login` → Sign in.

> **Set `Jwt__Issuer` consistently:** the API's `Jwt__Issuer` (render.yaml) must equal the issuer the
> tokens are minted with. With the current dev-token model the API mints *and* validates, so the
> render.yaml value is self-consistent — just keep it stable.

### 3. Branch protection (recommended)
After the first green CI run, apply the ruleset in `CONTRIBUTING.md` (require the 3 CI checks).

## Pre-deploy checklist

- [x] API builds in **Release**; 32/32 integration tests green; 5/5 fitness
- [x] Docker image builds from repo root and serves `/health` against Neon
- [x] `/v1/dev/token` absent in Production (404)
- [x] No secrets committed; env templates are placeholders only
- [x] Neon migrated through `0005`; migration runner verified
- [x] CORS: N/A (server-side BFF) — nothing to configure
- [ ] Render env vars set (you, in dashboard)
- [ ] Vercel `SAHAHR_API_URL` set (you)
- [ ] Smoke test the live URLs after deploy

## ⚠️ Production gates — do NOT load real employee data until these close

This codebase is **staging-grade**. Two architectural commitments from the blueprint remain open:

1. **PII encryption is not implemented.** `employee.national_id_enc / dob_enc / bank_account_enc` and
   `candidate.phone_enc` are `bytea` placeholders — there is no envelope/KMS encryption yet (§8.3).
   Storing real NRIC / bank / DOB data now would be a PDPA/GDPR breach. Keep demo data synthetic.
2. **Auth is the dev-JWT model, not a real IdP.** Production login (Keycloak/OIDC, password/MFA) is
   not built. In Production the dev-token endpoint is *off* (404), which means **there is currently no
   way for a real user to obtain a token in Production** — fine for a backend/API demo, but the login
   flow must be wired to a real IdP before end users sign in.

Also worth noting (not blockers for a demo): `Jwt__SigningKey` is a symmetric HS256 secret (rotate via
Render env); Render's free tier sleeps (use `starter` to avoid cold starts); Neon must stay on the
**direct** endpoint for RLS (the pooler is unsafe for non-transactional reads — DEBT-004).
