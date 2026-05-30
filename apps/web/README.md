# SahaHR Admin (web)

Next.js 16 (App Router, React 19, Tailwind v4) admin app. Acts as a **server-side BFF** in front of
the ASP.NET Core API: the JWT lives in an httpOnly cookie, all API calls run server-side, so the
browser never sees the token and there's no CORS.

## Run requirements (read before starting)

The app talks to the API **from the Next.js server process** (server actions + RSC fetches), so it
needs to know where the API is:

| env var | purpose | default |
|---------|---------|---------|
| `SAHAHR_API_URL` | base URL of the ASP.NET Core API, used by `lib/api.ts` | `http://127.0.0.1:5080` |

> **Gotcha (learned the hard way):** if the API runs on a non-default host/port and `SAHAHR_API_URL`
> is **not** exported in the *web* process's environment, the login server action can't reach
> `/v1/dev/token`, throws, and the page silently re-renders on `/login` with no visible error. Always
> export `SAHAHR_API_URL` for the web process when the API isn't on `127.0.0.1:5080`. Copy
> `.env.example` → `.env.local` to set it.

## Local run (two terminals)

```bash
# 1) API (from repo root) — uses .env for its DB connection (Neon or local Docker)
dotnet run --project apps/api/src/SahaHR.Api

# 2) web — point it at the API, then start
#    (PowerShell)  $env:SAHAHR_API_URL='http://127.0.0.1:5080'
#    (bash)        export SAHAHR_API_URL=http://127.0.0.1:5080
pnpm -C apps/web dev      # or: pnpm -C apps/web build && pnpm -C apps/web start
```

Open http://localhost:3000 → redirects to `/login`. The seeded dev tenant/user are pre-filled; click
**Sign in** to mint a token (permissions resolved from the DB) and land on `/employees`.

## Routes

- `/login` — dev token mint (server action). Replaced by Keycloak/OIDC in production.
- `/employees` — list + create, search/status-filter, cursor pagination, clickable rows → detail/edit
  drawer with soft-delete. All actions permission-gated (`employee.read/write/delete`).
- `/recruitment` — open positions → `/recruitment/[jobId]` Kanban board with permission-gated
  stage moves (`application.move`).
