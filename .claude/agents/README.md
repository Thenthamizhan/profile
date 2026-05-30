# SahaHR Subagents — the Agent Operating Model, materialized

These are the 28 single-responsibility agents from [`docs/Agent_Operating_Model.md`](../../docs/Agent_Operating_Model.md),
expressed as Claude Code subagent definitions. Each file's body is a distilled brief; the **authoritative**
spec (typed I/O contract, eval criteria, full escalation rules) lives in the AOM section referenced in the file.

## How tool scope maps

The AOM uses capability tokens; Claude Code uses concrete tools. Translation:

| AOM token | Claude Code tools |
|-----------|-------------------|
| `repo.read` | Read, Grep, Glob |
| `repo.write(<glob>)` | Edit, Write *(honor the path glob noted in the file — not enforced by frontmatter)* |
| `ci.trigger` / `db.*` / `eval.run` | Bash |
| `web.search` / `web.fetch` | WebSearch, WebFetch |
| `notify` | (normal output) |

> **Path scopes are by convention.** Frontmatter `tools` can't restrict *paths*, only *tools*. Each agent's
> write-glob is stated in its body and enforced by the **module-boundary fitness function (FF-1)** + code review (QA-2),
> exactly as in the product.

## Autonomy tiers (see AOM §3)

`T0` propose-only · `T1` act-with-approval · `T2` act-and-notify · `T3` autonomous. The effective tier for any
action is the most conservative of the agent default and the action's required tier (AOM §8 approval matrix).

## Roster (28)

| Layer | Agents |
|-------|--------|
| 1 Strategic | `str-1-product-strategist` · `str-2-compliance-strategist`† · `str-3-chief-architect`† · `str-4-rate-table-curator`🆕 |
| 2 Design | `dsn-1-system-designer` · `dsn-2-data-architect` · `dsn-3-api-contract-designer` · `dsn-4-experience-designer` · `dsn-5-security-architect` |
| 3 Implementation | `imp-1-backend-domain` · `imp-2-payroll-engine` · `imp-3-ai-services` · `imp-4-web` · `imp-5-mobile` · `imp-6-platform-migration` · `imp-7-integrations`🆕 |
| 4 Quality | `qa-1-test` · `qa-2-code-reviewer` · `qa-3-security-reviewer` · `qa-4-compliance-verifier` · `qa-5-ai-eval-safety`🆕 |
| 5 Release & Ops | `ops-1-release-manager` · `ops-2-infrastructure` · `ops-3-sre-observability` · `ops-4-data-migration-onboarding`🆕 |
| 6 Continuous Improvement | `ci-1-tech-debt-steward` · `ci-2-agent-eval` · `ci-3-retrospective` |

† = constitutional authority (veto). 🆕 = **phase-activated** — defined here but dormant until its concern is real
(AOM §5.7); its description starts with `[INACTIVE]` so it isn't routed to prematurely.
