# CI/CD Setup and Roadmap

This document describes Orbit's current GitHub Actions CI/CD setup as implemented in
[.github/workflows/ci.yml](.github/workflows/ci.yml) and
[.github/workflows/deploy-railway.yml](.github/workflows/deploy-railway.yml), and lays out an
in-depth, phased plan for closing the gaps between that setup and a production-grade pipeline
for a multi-tenant SaaS (.NET 10 API + worker + React/Vite PWA, deployed to Railway).

Read this alongside `ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md` §13.5 for feature-backlog sequencing;
this document only covers build/test/release infrastructure.

## Implementation status

All six phases below have code in the repo. Four decisions were made explicitly (not silently)
before implementing:

| Phase | Status | Decision made |
|---|---|---|
| 0 — Baseline hardening | Done | — |
| 1 — Security scanning | Done (CodeQL); secret scanning is a repo-settings toggle, verify it's on | — |
| 2 — Migration safety gate | Done, informational (`MIGRATION_SAFETY_ENFORCE: 'false'`) | — |
| 3 — Container image build/publish | Done | Web image uses build-time `VITE_API_URL` (one image per environment), not runtime config |
| 4 — Deploy workflow evolution | Code written, **unverified** | Written assuming a Railway image-source migration not yet performed; `deploy_mode: source` stays the default until proven |
| 5 — E2E tests in CI | Scaffolded | One trivial smoke test proves the wiring; no real suite yet |
| 6 — Governance | Done | Branch protection on `main` applied live via `gh api` (see below) |

Everything under "Open decisions requiring a human call" (§5) has been resolved for the
first implementation pass; revisit if circumstances change (e.g., a firm "build once, promote
unchanged" requirement would flip the Phase 3 web-image decision).

## Review conclusions

The existing pipeline is a good, secure baseline: immutable action pins, locked .NET restores,
real PostgreSQL migrations, and GitHub Environment protection are all appropriate for Orbit. The
recommended path is incremental hardening, not a replacement of the current workflows.

Four corrections shape the implementation plan:

- Railway already waits for its configured liveness checks for the API and web services
  (`deploy/railway/api.railway.json` and `deploy/railway/web.railway.json`). Add an *independent*
  post-deploy smoke test only after public domains are recorded, and test API readiness
  (`/health/ready`) rather than merely liveness (`/health/live`). The worker has no HTTP endpoint;
  validate it through a queue-processing probe or logs, not an invented health URL.
- A CodeQL workflow needs job-level `security-events: write` permission to upload results. Keep
  the existing CI workflow at `contents: read`; do not broaden it unnecessarily.
- An idempotent migration script contains historic migrations. A raw grep of that whole script
  will repeatedly flag a past destructive statement. Safety analysis must be limited to the
  migration files added in the pull request, with SQL generated from the prior migration to the
  new migration where possible.
- `railway up` uploads source and triggers a Railway build. It cannot promote a GHCR image by
  itself. An image-based delivery model requires a one-time Railway service-source migration to
  Docker Image, registry pull credentials, and a tested mechanism (Railway API or an explicitly
  approved manual release step) to update the image reference. Do not implement Phase 4 until
  this is proven in staging.

## 1. Current state

### 1.1 `ci.yml` — pull request and `main` push gate

Single job (`verify`) on `ubuntu-latest`, with `postgres:18-alpine` and `valkey/valkey:9.1-alpine`
service containers. Steps, in order:

1. Checkout, `setup-dotnet` (pinned to `global.json`), `setup-node` (Node 24, npm cache keyed on
   `web/package-lock.json`).
2. `dotnet restore --locked-mode` → `dotnet tool restore` → `dotnet build --configuration Release`.
3. `dotnet ef database update` against the Postgres service container — migrations are applied
   for real, not mocked.
4. `dotnet test --collect:"XPlat Code Coverage"` — coverage is collected but not published or
   gated on.
5. `dotnet list package --vulnerable --include-transitive` — NuGet advisory audit.
6. `npm ci`, `npm audit --audit-level=high`, `npm run lint` (oxlint), `npm test` (Vitest),
   `npm run build` (`tsc -b && vite build` — this is also the frontend typecheck), `npm run
   lint:docs` (markdownlint over the two root `.md` files).
7. Upload `web/dist` as a build artifact (`orbit-pwa`, 7-day retention).

Notable things already done right, worth preserving in any redesign:

- Actions are pinned to full commit SHAs, not floating tags (supply-chain hardening).
- `permissions: contents: read` is set at workflow level (least privilege).
- Migrations run against a real Postgres in CI rather than being skipped — catches EF model /
  migration drift before merge.
- `dotnet restore --locked-mode` fails the build if `packages.lock.json` is stale, so dependency
  changes are explicit.

### 1.2 `deploy-railway.yml` — manual deploy

`workflow_dispatch` only, with a required `environment` choice input (`staging` default,
`production` option). Uses a GitHub Environment (`environment: ${{ inputs.environment }}`) so
environment-scoped secrets/protection rules apply. `concurrency` group per environment prevents
overlapping deploys to the same target. Steps: install Railway CLI, run EF migrations through
`railway run`, then `railway up` three times (`orbit-api`, `orbit-worker`, `orbit-web`).

Important existing constraint, called out in the step name: **"Apply expand-only migrations"**.
Railway's deploy model means old and new code briefly coexist, so migrations run in this workflow
must be additive/backward-compatible (add nullable column, add table, add index — never rename,
drop, or add a `NOT NULL` column without a default in the same deploy).

### 1.3 What's absent today

- No workflow builds or pushes `Dockerfile.api` / `Dockerfile.worker` / `Dockerfile.web` — the
  Railway deploy relies on Railway's own build (`railway up` uploads source and builds remotely),
  so the Dockerfiles are validated only by Railway itself, never by CI.
- No CI job runs Playwright / `test:e2e` (the script doesn't exist yet in `web/package.json`
  despite being documented as a project command in `CLAUDE.md`).
- No CodeQL / static-analysis security scanning, no secret scanning workflow (GitHub's native
  secret scanning may be on at the repo-settings level, but nothing in-workflow).
- No automatic deploy path — `deploy-railway.yml` is 100% manual, dispatched by a human.
- No independent post-deploy smoke validation or documented rollback. Railway itself does perform
  the configured liveness checks for the API and web services.
- No coverage threshold enforcement or trend tracking (coverage is collected, not read).
- No architecture test visibility gate beyond "the build passes" — `Orbit.ArchitectureTests`
  runs inside `dotnet test` but a failure there looks identical to any other test failure in the
  Actions UI.
- No branch protection / required-checks documentation in-repo (may exist in GitHub settings,
  undocumented).
- No caching for `dotnet restore` (NuGet packages are re-downloaded every run; only npm is cached).
- No matrix — single OS/runtime, which is fine for now but worth naming as a decision, not an
  oversight.

## 2. Design principles for the plan below

1. **Don't rebuild what works.** `ci.yml`'s core sequence (restore → build → migrate → test →
   audit → frontend) is sound. Extend it; don't replace it.
2. **Expand-only migrations stay a hard rule**, not a convention — Phase 2 turns it into an
   automated CI check instead of a comment.
3. **Promote immutable artifacts only after a proven platform transition.** Today Railway builds
   source uploaded through `railway up`. If the team adopts registry images, CI produces a
   SHA-tagged image and migration bundle and Railway runs those exact artifacts. The Railway
   service-source migration is an explicit prerequisite, not an implementation detail.
4. **Every new gate is additive and non-blocking until proven stable.** Add new jobs as
   `continue-on-error: true` or informational for one iteration, confirm signal quality, then flip
   to required — avoids a security-scan false positive blocking every PR on day one.
5. **Staging auto-deploys, production stays a deliberate action.** Reduce toil on the low-risk
   path, keep human-in-the-loop on the high-risk one.

## 3. Phased plan

### Phase 0 — Baseline hardening (no new workflows, low risk)

Goal: make the existing `ci.yml` faster and its signal more legible before adding scope.

- **NuGet restore caching.** Add `actions/cache` (pinned SHA) keyed on
  `hashFiles('**/packages.lock.json')` for `~/.nuget/packages`, or switch to
  `actions/setup-dotnet`'s built-in `cache: true` / `cache-dependency-path` support. Cuts several
  minutes off `dotnet restore`.
- **Split architecture tests into their own step** (`dotnet test tests/Orbit.ArchitectureTests/...`
  run separately from the rest of `dotnet test Orbit.slnx`) so a layering violation shows as a
  distinctly named, immediately obvious failing step rather than buried in the aggregate test run.
- **Publish coverage as a job summary.** Add a step that parses the Cobertura XML
  `dotnet test` already produces and writes a `$GITHUB_STEP_SUMMARY` table (line/branch % per
  project). No external service, no new secret, just visibility. Defer Codecov/Coveralls to a
  later phase if trend tracking becomes a real need.
- **Fail the audit steps that currently only warn.** Confirm `npm audit --audit-level=high` and
  `dotnet list package --vulnerable` are actually non-zero-exit on findings (they are, by
  default) — verify no `|| true` has crept in, and keep it that way.

Verify: `ci.yml` run time drops (restore caching), architecture-test failures are visually
distinct in the Actions UI, a step summary shows coverage %, dependency-audit steps still fail the
job on real findings.

### Phase 1 — Security scanning (additive, informational first)

Goal: catch vulnerable code patterns and leaked secrets before they reach `main`, without
blocking merges on day one.

- **CodeQL.** Add `.github/workflows/codeql.yml` using `github/codeql-action` (pinned SHA),
  languages `csharp` and `javascript-typescript`, triggered on `pull_request` to `main` and a
  weekly `schedule` cron for drift. Give only this job `security-events: write` (and `actions:
  read` if required by the selected CodeQL version). Start as a required check only after a 1–2
  week burn-in with no false positives on this codebase.
- **Secret scanning / push protection.** This is a GitHub repo-settings toggle
  (Settings → Code security → Secret scanning + push protection), not a workflow file — verify
  it's enabled; document the toggle here rather than reinventing it with `gitleaks` in Actions.
- **Container image scanning (ties into Phase 3).** Once images are built in CI, add
  `aquasecurity/trivy-action` against the built `orbit-api`/`orbit-worker`/`orbit-web` images
  before push, failing on `CRITICAL`/`HIGH` with no available fix version ignored via an explicit,
  reviewed `.trivyignore`.

Verify: CodeQL workflow completes and posts to the Security tab; no plaintext secrets are
introduced by a deliberate test PR with a dummy token (should be blocked at push, not just
flagged after).

### Phase 2 — Migration safety gate

Goal: turn the "expand-only migrations" rule from a comment in `deploy-railway.yml` into something
CI enforces, since a destructive migration reaching `main` today is only caught by a human reading
the diff.

- Create a small checked-in migration-policy script and run it only when the pull-request diff
  adds a non-Designer migration file. The script must: (1) identify the base branch's last EF
  migration and each newly-added migration, (2) generate SQL for that bounded range rather than
  the whole idempotent history, (3) detect `DROP`, rename, and unsafe nullability operations, and
  (4) print the migration name, operation, and required remediation. Treat code inspection as a
  second signal—the EF model and provider version can make a textual SQL rule incomplete.
- Require a checked-in, reviewed exception record for an intentional contract migration. It must
  state the prior expand release that made the operation safe, earliest eligible release, data
  backfill evidence, rollback plan, and approving owner. A pull-request label alone is not an
  adequate override because it is not durable or reviewable with the schema change.
- Start informational, by adding a job summary and PR annotation. After two release cycles with
  no false negatives/positives, make any unapproved destructive operation fail the required
  `migration-safety` check.
- Document the two-phase migration pattern (expand in one deploy, contract in a later one) in
  `ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md` if not already covered, so the check's failure message
  can link to it.

Verify: a deliberately destructive test migration in a draft PR triggers the flag; a normal
additive migration does not.

### Phase 3 — Container image build/publish in CI

Goal: CI produces the exact artifact that gets deployed, closing the gap where Railway currently
builds from source independently of what `ci.yml` tested.

- Prefer one workflow with a `build-images` job that `needs: verify`, rather than `workflow_run`.
  It preserves the tested commit SHA, avoids a second checkout/event boundary, and can expose the
  image digests as job outputs. Trigger this release path on push to `main` and version tags
  (`v*`), never directly on a pull request with package-write credentials.
- Matrix over the three Dockerfiles (`Dockerfile.api`, `Dockerfile.worker`, `Dockerfile.web`),
  building with `docker/build-push-action` (pinned SHA) and Buildx layer caching
  (`cache-from`/`cache-to: type=gha`).
- Push to GitHub Container Registry (`ghcr.io/<org>/orbit-{api,worker,web}`) tagged with the full
  commit SHA and, on tag pushes, the semver tag. Record the immutable image **digest** in the run
  summary/release manifest; tags aid people, while the digest is the deployment identity. Grant
  only the build job `packages: write`, and make package visibility and Railway's read-only GHCR
  credential an explicit staging prerequisite.
- Build a Linux migration bundle from the same commit and publish it with the release manifest.
  The deployment workflow must run that bundle with the migration/admin connection, rather than
  checking out mutable `main` and executing a separately built `dotnet ef` command.
- Run Trivy (Phase 1) against each built image before push; fail on CRITICAL with a known fix.
- `Dockerfile.web` compiles `VITE_API_URL`, while the client also consumes OIDC `VITE_*` values.
  Therefore an nginx `envsubst` change alone cannot make a single web image environment-neutral:
  the values are already embedded in static JavaScript. Choose either (a) environment-specific
  web images with distinct, explicit tags, or (b) a small runtime `config.js` generated at
  container start and read by the client before application bootstrap. Option (b) is recommended
  if "build once, promote unchanged" is a firm release requirement, but is an application change
  and needs dedicated tests.
- Before automating, convert the three Railway services in **staging** from repository sources to
  Docker Image sources, configure the same health/restart settings, set least-privilege GHCR pull
  credentials, and prove deployment and rollback with a disposable SHA. Record exactly how the
  image reference is updated; `railway up` is not that mechanism.

Verify: pushing to `main` produces three new `ghcr.io` image tags matching the commit SHA; Trivy
shows in the run; the API and web images start successfully in a local smoke harness, and the
worker image starts with its required dependencies available.

### Phase 4 — Deploy workflow evolution

Goal: keep production deploys human-gated (matches current `deploy-railway.yml` design and
Railway's environment-protection model) while removing manual toil from staging.

- **Staging: auto-deploy on merge to `main`**, only after the Phase 3 staging proof. The release
  job resolves the manifest's API, worker, web, and migration-bundle digests; applies the bundle;
  updates Railway image references through the proven mechanism; and waits for the deployment's
  terminal state. Do not deploy three services with `railway up`, because that reintroduces a
  remote source build.
- **Production: keep `workflow_dispatch`** with GitHub Environment approval. Its required input
  is an immutable release-manifest ID (or API image digest), not a branch name, tag, or run ID.
  Validate that all three image digests and the migration bundle exist, were scanned, and passed
  staging before any production action starts.
- **Post-deploy validation.** Store API and web public base URLs as environment variables. Retry
  `GET /health/ready` for the API and `GET /health/live` for web with a bounded timeout; execute
  one authenticated, non-mutating API smoke path using a purpose-created staging tenant. Validate
  the worker by publishing a harmless outbox probe and observing its completed state. A failed
  smoke test fails the release and pages/alerts; it does not automatically roll back a migration.
- **Manual rollback path.** Add a dispatch input for a prior release-manifest ID and document the
  exact rollback decision tree: rollback application images immediately when schema-compatible;
  do not reverse a production migration automatically; use a forward-fix or an approved restore
  procedure for data/schema failures. Capture the deployed manifest ID in every deployment
  summary so on-call staff can identify the last known good version without Git history.

Verify: merging a PR to `main` results in staging showing the new commit within minutes,
unattended; a manual production dispatch requires picking an image reference and fails loudly if
the post-deploy health check doesn't pass.

### Phase 5 — End-to-end tests in CI

Goal: `npm run test:e2e` (documented in `CLAUDE.md`, not yet implemented in `web/package.json`)
gets a real Playwright suite and a CI job to run it, rather than only existing as a documented
command.

- This phase is scoped to *wiring CI for it*, not authoring the test suite — that's a separate,
  larger effort the user should confirm scope for before it's started.
- Once a `test:e2e` script and Playwright config exist: add a job in `ci.yml` (or a separate
  workflow to keep the fast unit-test feedback loop unblocked) that starts the API + worker +
  Postgres + Valkey (mirroring `scripts/start-dev.sh`, containerized via the service-container
  pattern already used) and the built PWA (`vite preview` against `web/dist` from the `orbit-pwa`
  artifact), then runs `playwright test` against it. Upload the Playwright HTML report and
  trace/video artifacts on failure.
- Given runtime cost, consider running this on `pull_request` only for PRs labeled `e2e` or
  targeting `main` from a release branch, plus always on `push: main`, rather than on every commit
  to every PR — ask before deciding this if it's not obvious once the suite exists (test count and
  runtime will make the right tradeoff clear).

Verify: a deliberately broken UI flow in a draft PR fails the e2e job with a downloadable trace;
an unrelated backend-only PR either skips or passes it quickly.

### Phase 6 — Governance and required-checks documentation

Goal: make branch protection and required checks explicit and reviewable, not tribal knowledge in
GitHub repo settings.

- Added a "CI/CD" section cross-reference from `README.md` to this document (mirrors how
  `README.md` already points to `deploy/railway/README.md` for Railway specifics). Done.
- **Applied to `main` branch protection via the GitHub API** (2026-08-19): `required_status_checks`
  was previously unset (the endpoint 404'd — status checks weren't enabled at all); a `PUT` on
  `/repos/.../branches/main/protection` enabled it with `strict: true` and `contexts: ["verify"]`,
  while explicitly round-tripping every pre-existing setting so nothing else changed:
  `required_pull_request_reviews` (1 approving review), `required_signatures` (untouched — managed
  by its own sub-resource, not part of this payload), `enforce_admins: false`,
  `required_linear_history: false`, `allow_force_pushes: false`, `allow_deletions: false`,
  `required_conversation_resolution: true`, `lock_branch: false`. Only `verify` is required for
  now, matching the Phase 1/2 "start informational" principle — CodeQL and the migration-safety
  check are intentionally **not** required yet; add them to `contexts` once each has run cleanly
  across a normal PR flow for a couple of weeks with no false positives.
- Still open, deliberately not decided here: merge-commit vs. squash vs. rebase policy, and
  whether `required_approving_review_count` should move to 2 for a small team. Revisit once the
  team using this repo is bigger than one or two people.

Verify: branch protection settings match this document; a PR cannot merge without a green `verify`
check on an up-to-date branch. Confirmed live via `gh api repos/.../branches/main/protection`
after applying.

## 4. Suggested sequencing

Phases are ordered by risk/effort ratio, not strict dependency, except where noted:

1. Phase 0 (baseline hardening) — do first, low risk, immediate payoff, no new secrets/infra.
2. Phase 1 (security scanning) — additive, can run in parallel with Phase 0.
3. Phase 2 (migration safety gate) — independent, valuable given multi-tenant RLS/migration risk
   called out in `CLAUDE.md`'s architecture notes.
4. Phase 3 (image build/publish) — prerequisite for Phase 4; requires a decision on GHCR access
   and environment-specific web images versus runtime client configuration.
5. Phase 4 (deploy evolution) — depends on Phase 3.
6. Phase 5 (E2E in CI) — independent of 1–4, but blocked on the E2E suite itself being written;
   sequence last only because the suite doesn't exist yet, not because it's low value.
7. Phase 6 (governance docs) — do last, once the check set is stable enough to document as
   "required."

## 5. Open decisions requiring a human call

These are flagged, not resolved, in the plan above — surface them before implementing the
relevant phase:

- **GHCR vs. Docker Hub vs. staying source-deploy-only on Railway** (Phase 3) — GHCR is free and
  colocated with the repo, recommended default, but confirm the org's container registry has no
  existing convention first.
- **Environment-specific web images vs. runtime client config** (Phase 3) — runtime config is
  recommended for a single promoted web image, but it requires a generated client-visible config
  asset and a client bootstrap change; nginx `envsubst` alone cannot alter Vite's compiled values.
- **E2E suite scope and authorship** (Phase 5) — out of scope for this plan; confirm separately
  before starting.
- **Required-check enforcement timing** (Phases 1–2) — how long each new check stays informational
  before blocking merges is a judgment call on false-positive tolerance versus risk exposure.
