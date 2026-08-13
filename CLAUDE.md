# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Orbit is an open-source, headless sprint/Kanban work-management platform: a .NET 10 Clean Architecture API (`src/`) plus an independently deployed React/Vite PWA (`web/`). The target architecture and phased backlog live in `ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md` — read its §13.5 ("Implementation baseline and next increments") before starting new feature work; it tracks exactly what's implemented vs. stubbed and gives the next-increment order. `README.md` has the day-to-day run/verify instructions this file doesn't repeat.

## Commands

### Local stack

```bash
./scripts/start-dev.sh          # starts Postgres/Valkey, applies migrations, runs api+worker+web together
./scripts/start-local-services.sh  # Postgres 18 + Valkey 9.1 only (podman compose)
./scripts/migrate.sh            # applies EF migrations
dotnet run --project src/Orbit.Api      # API only, http://localhost:5014
cd web && npm run dev           # PWA only, http://localhost:5173
```

`dotnet tool restore` once after cloning (pins `dotnet-ef` per `.config/dotnet-tools.json`).

### Backend

```bash
dotnet build Orbit.slnx
dotnet test Orbit.slnx
dotnet test tests/Orbit.Application.Tests/Orbit.Application.Tests.csproj   # single project
dotnet test --filter "FullyQualifiedName~BoardHandlerTests"               # single class/test
```

EF migrations **must** use `Orbit.Infrastructure` as both `--project` and `--startup-project` (it has the `IDesignTimeDbContextFactory` in `OrbitDbContextFactory.cs`) — `Orbit.Api` as startup project fails with a missing-`EFCore.Design`-reference error:

```bash
dotnet ef migrations add <Name> --project src/Orbit.Infrastructure --startup-project src/Orbit.Infrastructure
dotnet ef database update --project src/Orbit.Infrastructure --startup-project src/Orbit.Infrastructure
```

### Frontend (`web/`)

```bash
npm run dev
npm run build      # tsc -b && vite build — this is the typecheck step, there is no separate `typecheck` script
npm run lint        # oxlint
npm test            # vitest run
npx vitest run path/to/file.test.ts   # single file
npm run test:e2e    # Playwright (see README)
npm run lint:docs   # markdownlint over the two root .md files
```

## Architecture

### Backend layering

`src/Orbit.Domain` → `src/Orbit.Application` → `src/Orbit.Infrastructure` → `src/Orbit.Api` (+ `src/Orbit.Worker`, currently a bare `IHostedService` stub not yet wired to Infrastructure — background job processing is a tracked future increment, not existing code to extend casually).

`tests/Orbit.ArchitectureTests/LayerDependencyTests.cs` enforces this with NetArchTest: Domain must not depend on Application/Infrastructure/Api; Application must not depend on Infrastructure/Api; Infrastructure must not depend on Api. A change that violates this fails the test, not just a review comment.

Each bounded-context folder (`Choices`, `Directory`, `Identity`, `Projects`, `Settings`, `WorkItems`, `Workspaces`, `Access`, `Boards`, …) is mirrored across Domain → Application → Infrastructure, e.g. `Domain/Directory/DirectoryModels.cs` (aggregates) → `Application/Directory/Teams.cs` (commands/queries/handlers) → `Infrastructure/Persistence/TeamConfiguration.cs` + `TeamRepository.cs`. New features should follow whichever existing folder is the closest structural match (see `ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md` §2.1 for the bounded-context map) rather than inventing a new layout.

Application-layer convention: one file per feature holds the DTO record(s), `ICommand<T>`/`IQuery<T>` records, `AbstractValidator<T>` (FluentValidation), and the MediatR `IRequestHandler<TRequest, TResponse>` together — see `Application/Directory/Teams.cs` or `Application/Settings/ManageSettings.cs`. Repositories are interfaces in the single `Application/Abstractions/Persistence.cs` file, implemented as `internal sealed` classes in `Infrastructure/Persistence/`.

### Multi-tenancy and authorization

Every tenant-scoped request runs inside a Postgres transaction (`TenantTransactionMiddleware` in `Orbit.Api/Tenancy/`) that sets `app.tenant_id` (and, for login/refresh only, `app.principal_user_id`) via `set_config` before any query executes. Tenant resolution differs by environment: local dev reads `X-Tenant-Id` when `Tenancy:AllowHeaderTenant=true` (`appsettings.Development.json`); everywhere else it comes from the validated JWT's `tenant_id` claim. EF global query filters (`OrbitDbContext.OnModelCreating`) are the primary enforcement; PostgreSQL `FORCE ROW LEVEL SECURITY` policies (every migration that adds a tenant table includes one, matching the pattern in the most recent migration under `Infrastructure/Persistence/Migrations/`) are defense in depth — **but note README's caveat: no shipped environment (Podman/CI/Railway) yet connects as a `NOSUPERUSER NOBYPASSRLS` role, so RLS is not actually enforced anywhere today.** Don't treat RLS as a substitute for correct query filters.

Permission checks are query-level predicates, not post-hoc filters: `IProjectRepository.GetAsync(tenantId, projectId, ProjectPermission, ct)` only returns a project the caller can see for that permission, so a missing/forbidden project both come back as `NotFoundException` (existence-hiding reads — see `Infrastructure/Persistence/ProjectRepository.cs` and `ProjectPermissionRoles.cs` for the `ProjectPermission → ProjectRole[]` mapping). Reuse this pattern (`projects.GetAsync(..., ProjectPermission.View|Administer, ...)`) instead of adding new authorization abstractions for project-scoped resources.

### Versioned/settings-style resources

Any aggregate that's a per-project or per-workspace singleton (`ProjectSetting`, `WorkspaceSetting`, `Board`, …) follows the same shape: a `Version` concurrency token, a GET endpoint that returns a zero-version sentinel DTO when the row doesn't exist yet, and a single PATCH endpoint that both creates (when `If-Match` is `0`) and updates (otherwise) — see `Application/Settings/ManageSettings.cs` + `Api/Endpoints/SettingsEndpoints.cs`, and its `SettingsConcurrency.EnsureVersion` helper (reused across bounded contexts, not just Settings). `SettingsEndpoints.TryParseVersion`/`PreconditionRequired` are the shared `If-Match` parsing helpers other endpoint files call into.

### Frontend (`web/src`)

No per-feature hooks/API modules convention beyond one exception (`hooks/useCreateWorkItem.ts`, extracted because it's reused in two places). Everything else — every query and mutation — lives inline in `App.tsx` via `@tanstack/react-query`, with `queryClient.setQueryData` used for optimistic cache updates rather than `invalidateQueries` in most cases. `api/client.ts` is a single flat `orbitApi` object of request functions; `api/types.ts` is flat `type`/`interface` declarations mirroring the C# enums/DTOs as string unions. Feature components under `features/*` are mostly presentational, receiving data and mutation callbacks as props from `App.tsx` rather than fetching themselves. Styling mixes a custom BEM-ish stylesheet (`App.css` — `.dialog`, `.onboarding`, `.board-header`, `.primary-button`, etc.) with inline Tailwind utility classes; check `App.css` for an existing class before adding new bespoke CSS.
