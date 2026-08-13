# Orbit Work Management

Orbit is an open-source, headless sprint and Kanban work-management platform. The current implementation includes one-time local installation bootstrap, local email/password login with rotating sessions, global accounts and workspaces, workspace teams and admin lifecycle (promote/demote/remove members), projects, tenant-isolated work items, stable software item types, optimistic status transitions, OIDC-backed tenant memberships, project roles, query-level permission enforcement, and a responsive installable PWA.

The target architecture and phased backlog are in [ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md](ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md).

## Prerequisites

- .NET SDK 10.0.201 or a compatible 10.0.2xx patch
- Node.js 24 and npm
- Podman Desktop with Compose support

Run `dotnet tool restore` once after cloning to install the pinned Entity Framework Core CLI (`.config/dotnet-tools.json`) used by migrations.

## Run locally

Start the complete development stack with one command:

```bash
./scripts/start-dev.sh
```

The script starts PostgreSQL and Valkey when required, waits for both containers, applies migrations, and runs the API, worker, and web client. Press `Ctrl+C` to stop the application processes; the Podman containers remain running.

Alternatively, start each component separately:

Start PostgreSQL 18 and Valkey 9.1:

```bash
cp .env.example .env
./scripts/start-local-services.sh
./scripts/migrate.sh
```

Start the headless API:

```bash
dotnet run --project src/Orbit.Api
```

Start the PWA in another terminal:

```bash
cd web
npm ci
npm run dev
```

Open `http://localhost:5173`. The browser creates a local development tenant id and sends it through `X-Tenant-Id`. This bypass is enabled only by local configuration. Production requires a validated bearer token with a `tenant_id` claim and an active membership matching the token issuer and subject; service accounts additionally use `principal_type=service_account` and `client_id` or `azp` claims.

On a new database, initialize the installation through the public bootstrap contract:

```bash
curl http://localhost:5014/api/v1/bootstrap/status
curl -X POST http://localhost:5014/api/v1/bootstrap \
  -H 'Content-Type: application/json' \
  --data '{"displayName":"First Admin","email":"admin@example.com","password":"ReplaceWithStrongPassword123","workspaceName":"My Workspace"}'
```

The bootstrap write is rate limited and protected by a PostgreSQL transaction-scoped advisory lock. It creates exactly one global account, Argon2id credential, site super-admin role, workspace, and owner membership.

Log in with that account to receive a rotating session:

```bash
curl -X POST http://localhost:5014/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  --data '{"email":"admin@example.com","password":"ReplaceWithStrongPassword123"}'
```

## Workspace administration

Once signed in, an owner or administrator can manage teams and workspace membership:

```bash
# Teams
curl -X POST http://localhost:5014/api/v1/teams -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' --data '{"name":"Platform Team"}'
curl http://localhost:5014/api/v1/teams -H "Authorization: Bearer $TOKEN"
curl -X POST http://localhost:5014/api/v1/teams/$TEAM_ID/members -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' --data '{"membershipId":"'"$MEMBERSHIP_ID"'"}'

# Promote, demote, or remove a workspace member (the workspace always keeps at least one owner)
curl -X PUT http://localhost:5014/api/v1/memberships/$MEMBERSHIP_ID/role -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' --data '{"role":"Administrator"}'
curl -X DELETE http://localhost:5014/api/v1/memberships/$MEMBERSHIP_ID -H "Authorization: Bearer $TOKEN"
```

Email-delivered invitations are not implemented yet — new members are added directly by an owner/administrator via `POST /api/v1/memberships` (federated identity or, once linked, a local account). Invitation delivery/acceptance is tracked as the next identity increment; see [ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.5](ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md).

## Verify

```bash
dotnet test Orbit.slnx
cd web
npm run lint
npm test
npm run build
npm run lint:docs
```

## Architecture boundaries

- `Orbit.Domain` contains aggregates, invariants, and defined choice values.
- `Orbit.Application` contains CQRS commands, queries, validation, and ports.
- `Orbit.Infrastructure` contains EF Core, Npgsql, tenant filters, repositories, and migrations.
- `Orbit.Api` is a headless HTTP composition root and never serves frontend assets.
- `web` is independently built and deployed as a responsive PWA.

PostgreSQL row-level security is defense in depth. Every tenant-scoped API request starts a transaction and sets `app.tenant_id` locally before any query executes; login/refresh set `app.principal_user_id` instead so a user can discover their own memberships before a workspace is selected. Production startup rejects a `SUPERUSER` or `BYPASSRLS` runtime connection. Migrations use the separate `ConnectionStrings__PostgresAdmin` owner connection, while the API uses a `NOSUPERUSER NOBYPASSRLS` role through `ConnectionStrings__Postgres`.

## Deployment

OCI builds are defined by `Dockerfile.api`, `Dockerfile.worker`, and `Dockerfile.web`. Railway service configuration and environment requirements are documented in [deploy/railway/README.md](deploy/railway/README.md).
