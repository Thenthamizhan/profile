# Technical-Debt Register

> Owned by **CI-1 (Tech Debt Steward)** per `Agent_Operating_Model.md` §10. Debt is a budgeted
> liability: detected → classified → quantified → scheduled. Critical debt blocks a phase exit
> (debt-ceiling gate). Items are *accepted* (not paid) only with a named human `acceptedBy`.
>
> Schema mirrors the `DebtItem` type in AOM §12. Estimate points are rough (1 ≈ half-day).

| id | class | title | context | detectedBy | evidence | est | riskIfUnpaid | sla | status | acceptedBy |
|----|-------|-------|---------|-----------|----------|-----|--------------|-----|--------|-----------|
| DEBT-001 | structural | Latent snake_case column mismatch on `Offer.DocumentS3Key` | recruitment | CI-1 sweep (post-`resume_s3_key` fix) | `Offer` entity now implemented with explicit `HasColumnName("document_s3_key")`; FF-18 verifies the mapping and 19/19 integration tests pass, including the offer lifecycle. The trap can no longer ship silently. | 1 | 500 on offer create | when Offer entity is added | **paid** | — |
| DEBT-002 | operational | Schema applied via `db/init` scripts, not versioned migrations | platform | CI-1 | `infra/migrations/` is empty, so FF-12 (migration reversibility) passes vacuously; prod has no expand-contract migration path yet. Acceptable for pre-Phase-0 dev; must graduate before any shared environment. | 5 | no safe prod schema evolution | before first shared/staging deploy | open | — |
| DEBT-003 | cosmetic | Web mutations refetch the whole list via `revalidatePath` | web | IMP-4 self-review | Employee/ATS mutations trigger a full server re-render instead of optimistic UI (`useOptimistic`). Fine at current scale; revisit when tables are large. | 2 | minor latency on large tables | opportunistic / 20% budget | open | — |
| DEBT-004 | structural | Non-transactional reads are not pooler-safe | platform | IMP-6 (pooler hardening) | The tenant GUC is now set both session-scoped (direct endpoint) and transaction-local (`TenantTransactionInterceptor`, pooler-safe). EF writes always run in a transaction → covered on the pooler. But non-transactional reads on a transaction-mode pooler get no GUC → **fail-closed (0 rows)**, i.e. safe but functionally broken. We run on the **direct endpoint**, so this is latent. Pay before switching `ConnectionStrings__Default` to a `-pooler` host (wrap reads in a transaction / unit-of-work). | 3 | reads return empty on pooler endpoint | before adopting the pooler endpoint | open | — |

## Notes

- **DEBT-001 is `mitigated`, not `paid`:** FF-18 (schema/model mapping integrity, AOM §6) now fails
  the build the moment an `Offer` entity maps a non-existent column — so the trap cannot ship silently.
  It flips to `paid` when the `Offer` entity is implemented with a correct explicit mapping.
- **Forward-looking (not yet active):** AOM §5.7 mandates logging the **QA-5 maker-checker interim**
  (IMP-3 self-certifying AI fairness until QA-5 activates in Phase 3) as *critical* debt on day one.
  It is not yet open here because no product AI exists; CI-1 opens it when IMP-3 ships its first model.
