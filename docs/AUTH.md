# Authentication & Authorization

SahaHR separates **authentication** (who are you — proven by a signed token) from
**authorization** (what may you do — our fine-grained, tenant-scoped permissions). The API never
trusts permissions baked into a token by an external party; it always resolves them from our own
RBAC tables.

## Two token modes (chosen by config)

The API host (`Program.cs`) configures JWT-bearer validation in one of two modes based on whether
`Jwt:Authority` is set:

| Mode | Trigger | Signing | Issuer of tokens |
|------|---------|---------|------------------|
| **Dev / test** | `Jwt:Authority` **unset** | HS256, shared `Jwt:SigningKey` | `POST /v1/dev/token` (Development only) |
| **Production OIDC** | `Jwt:Authority` **set** | Asymmetric (RS256), keys from the IdP JWKS | External IdP (Keycloak, §7) |

In both modes the API validates issuer, audience, signature, and lifetime, and reads claims
verbatim (`MapInboundClaims = false`) so `sub`, `tenant_id`, and `perm` keep their exact names.

## How permissions are resolved

A real IdP access token proves **identity** (`sub`, and a `tenant_id` claim) but does **not** carry
our `perm` claims — those live in `user_role → role_permission → permission`. The Identity module
bridges this:

- `IPermissionResolver` runs the canonical RBAC query for a `(tenant_id, user_id)` pair. It is the
  single source of truth, shared by the dev mint and the OIDC path.
- `PermissionClaimsTransformation` (an `IClaimsTransformation`, runs on every authenticated request):
  1. If the principal already has `perm` claims → **no-op** (the dev mint bakes them in; this keeps
     dev/test/E2E unchanged).
  2. Otherwise it reads `tenant_id` + `sub`, resolves permissions via `IPermissionResolver`
     (cached 60 s per `(tenant, user)` to avoid a DB round-trip every request), and projects them
     as `perm` claims.

`TenantContextMiddleware` then reads `tenant_id` / `sub` / `perm` exactly as before, so the
authorization layer is identical regardless of token source.

```
IdP token (sub, tenant_id)  ──▶  JWKS validation  ──▶  PermissionClaimsTransformation
                                                          │ resolve perms from RBAC tables
                                                          ▼
                                          principal with sub + tenant_id + perm[]
                                                          │
                                                          ▼
                                       TenantContextMiddleware → RLS GUC + permission checks
```

## Configuration keys

```
Jwt:Authority   (prod) e.g. https://id.example.com/realms/sahahr   — enables OIDC mode
Jwt:Audience    e.g. sahahr-api                                     — required (both modes)
Jwt:Issuer      (dev/test only) used to validate dev-minted tokens
Jwt:SigningKey  (dev/test only) symmetric key for dev-minted tokens (≥ 32 chars)
```

Set these via environment variables in production (`Jwt__Authority`, `Jwt__Audience`). Never commit
real values.

## What is implemented vs. remaining (infra-dependent)

**Implemented (this repo):**
- Config-switched JWKS / symmetric validation.
- DB-resolved permissions for IdP tokens via the claims transformation (+ short-TTL cache).
- Dev mint remains Development-only (`404` in Production).

**Remaining — requires you to stand up an IdP and is therefore not wired/tested here:**

1. **Provision Keycloak (or any OIDC IdP).** Create a realm, then a confidential client for the web
   BFF. Configure protocol mappers so issued access tokens include:
   - `aud` = `sahahr-api` (the value you set as `Jwt:Audience`)
   - a `tenant_id` claim (the user's tenant GUID)
   - `sub` = the user's GUID (must match `user_role.user_id` in our DB)
2. **Provision users + role grants.** Each IdP user's `sub` must correspond to a row set in our
   `user_role` table so `IPermissionResolver` can resolve their permissions.
3. **Web BFF OIDC flow** (`apps/web`). Replace the dev sign-in action with the Authorization Code
   flow: redirect to Keycloak → handle the callback → exchange the code for tokens → store the
   access token in the existing httpOnly session cookie → continue calling the API server-to-server
   with it as the bearer. The API side needs no further change.
4. **Set `Jwt__Authority` + `Jwt__Audience`** on the API service (e.g. Render) to flip it into OIDC
   mode. Leave them unset locally so the dev mint keeps working for development and the E2E suite.

Until step 4 is done in a given environment, that environment runs in dev-mint mode and must not
hold real employee data (see `DEPLOY.md` → Production Gates).
