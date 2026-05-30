# Architectural fitness functions

Executable checks that assert the architecture's invariants (AOM §6). Run locally with `pnpm ff`
or in CI via `.github/workflows/ci.yml`. A red check **blocks promotion**. Changing a check requires
an ADR (it is owned by the Chief Architect, STR-3).

These use **only Node builtins** — no install needed to run them.

## Implemented now (static)

| FF | Check | Script | Owner |
|----|-------|--------|-------|
| FF-1 | Module boundary — no cross-module internal imports | `ff-01-module-boundary.mjs` | QA-2 |
| FF-2 | Tenant isolation — `tenant_id` + RLS on tenant-scoped tables | `ff-02-tenant-isolation.mjs` | DSN-2 |
| FF-4 | No hard-coded statutory rates in payroll | `ff-04-no-hardcoded-rates.mjs` | IMP-2 |
| FF-5 | Money type safety — no float/double on money | `ff-05-money-type.mjs` | DSN-2 |
| FF-12 | Migration reversibility — every migration has a rollback | `ff-12-migration-reversibility.mjs` | IMP-6 |

> They are **tolerant of absent code** (vacuous pass) so CI is green from day one and the checks bite
> the moment their subject (payroll module, SQL schema, migrations) lands.

## Deferred (need running code or CI services) — added as their subjects land

FF-3 payroll reproducibility (replay test) · FF-6 transactional outbox (integration test) ·
FF-7 PII encryption coverage · FF-8 authZ presence vs permission registry · FF-9 API compatibility (contract diff) ·
FF-10 latency budget (perf test) · FF-11 AI fairness (QA-5) · FF-13 audit completeness (trace sampling) ·
FF-14 dependency hygiene (SBOM/CVE scan) · FF-15 statutory format · FF-16 model drift · FF-17 onboarding reconciliation.
