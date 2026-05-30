# Contributing to SahaHR

Trunk-based: short-lived branches off `main`, PR, green CI, merge. Conventional Commits.
Read `CLAUDE.md` for the operational cheat-sheet and `docs/Agent_Operating_Model.md` for governance.

## Before you push

```bash
pnpm ff                                              # fitness functions (FF-1..18)
dotnet test apps/api/SahaHR.sln                      # 22 integration tests (needs Docker)
pnpm -C apps/web exec playwright test --project=chromium   # E2E (needs `pnpm infra:up` first)
```

CI (`.github/workflows/ci.yml`) runs all three on every push/PR. **Never merge red.**

## Branch protection on `main` (apply once a GitHub remote exists)

This repo is currently local-only, so protection can't be enabled yet. The moment it has a remote,
apply the settings below so CI becomes a hard merge gate (the governance complement to wiring CI —
AOM quality gates).

### Via the GitHub UI
Settings → Branches → Add branch ruleset (or classic protection rule) for `main`:
- ✅ Require a pull request before merging (≥1 approval)
- ✅ Require status checks to pass before merging — select all three:
  `Fitness functions`, `Build & integration tests (.NET 9)`, `E2E (Playwright)`
- ✅ Require branches to be up to date before merging
- ✅ Require conversation resolution before merging
- ✅ Do not allow bypassing the above (include administrators)
- ✅ Restrict force-pushes / deletions on `main`

### Via the `gh` CLI (once authed against the remote)

```bash
# classic protection (replace OWNER/REPO)
gh api -X PUT repos/OWNER/REPO/branches/main/protection \
  --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "checks": [
      { "context": "Fitness functions" },
      { "context": "Build & integration tests (.NET 9)" },
      { "context": "E2E (Playwright)" }
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": { "required_approving_review_count": 1 },
  "restrictions": null
}
JSON
```

> The check **contexts** above are the job `name:` values in `.github/workflows/ci.yml`. If you rename
> a job, update the protection rule to match or merges will block on a check that never reports.

## Commit hygiene

- Commit only when asked; branch off `main` first.
- **Read the build/test exit code before committing.** A commit message must not claim a result that
  wasn't observed (see `CLAUDE.md` working agreements).
- No secrets in tracked files — connection strings live in the gitignored `.env` (`*.example` is the
  committed template).
