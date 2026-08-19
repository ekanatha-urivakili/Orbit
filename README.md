# Orbit Work Management

Orbit is an open-source, headless sprint and Kanban work-management platform. The current implementation includes self-service organization signup, one-time local installation bootstrap, site-admin workspace creation, local email/password login with rotating sessions and a remember-me session lifetime, backend-brokered Google sign-in, global accounts and secure workspace switching, workspace teams and admin lifecycle (promote/demote/remove members), a guest membership tier scoped to explicitly assigned projects, projects, tenant-isolated work items, many-to-many work item dependency linking, workspace-level typography and logo branding settings, TipTap-based rich text editing with font size and attachment resolution, presigned MinIO/S3 attachment uploads, a tenant-configurable stable software item-type registry, optimistic status transitions, OIDC-backed tenant memberships, project roles, query-level permission enforcement, and a responsive installable PWA.

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

Open `http://localhost:5800`. The browser creates a local development tenant id and sends it through `X-Tenant-Id`. This bypass is enabled only by local configuration. Production requires a validated bearer token with a `tenant_id` claim and an active membership matching the token issuer and subject; service accounts additionally use `principal_type=service_account` and `client_id` or `azp` claims.

### Optional: HTTPS via `https://www.orbit-local.com`

The PWA can also be reached over HTTPS at `https://www.orbit-local.com`, proxied by a local nginx instance to `http://localhost:5800`. This is a local-only convenience (e.g. for testing PWA install prompts and service-worker behavior that require a secure context on a stable hostname); it is not part of the deployed stack and nothing here is pushed to source control.

1. Install [mkcert](https://github.com/FiloSottile/mkcert) and [nginx](https://nginx.org) (`brew install mkcert nginx`), then trust the local CA once:

   ```bash
   mkcert -install
   ```

2. Generate a certificate for the local domain (kept outside the repo), and copy the bundled offline page next to it so nginx can serve it without proxying to a downed dev server:

   ```bash
   mkdir -p ~/.local/orbit-nginx-certs
   cd ~/.local/orbit-nginx-certs
   mkcert -cert-file orbit-local.com.crt -key-file orbit-local.com.key www.orbit-local.com orbit-local.com
   cp /path/to/Orbit/deploy/local/offline.html ~/.local/orbit-nginx-certs/offline.html
   ```

3. Point the hostname at localhost:

   ```bash
   sudo sh -c 'printf "127.0.0.1 orbit-local.com\n127.0.0.1 www.orbit-local.com\n" >> /etc/hosts'
   ```

4. Add an nginx server block (Homebrew nginx auto-includes `$(brew --prefix)/etc/nginx/servers/*`) at `$(brew --prefix)/etc/nginx/servers/orbit-local.conf`:

   ```nginx
   server {
       listen 80;
       server_name orbit-local.com www.orbit-local.com;
       return 301 https://$host$request_uri;
   }

   server {
       listen 443 ssl;
       server_name orbit-local.com www.orbit-local.com;

       ssl_certificate     /Users/<you>/.local/orbit-nginx-certs/orbit-local.com.crt;
       ssl_certificate_key /Users/<you>/.local/orbit-nginx-certs/orbit-local.com.key;
       ssl_protocols TLSv1.2 TLSv1.3;

       # Served directly (no proxy_pass) so it still renders when the dev server is down.
       location = /offline.html {
           root /Users/<you>/.local/orbit-nginx-certs;
           internal;
       }

       location / {
           proxy_pass http://localhost:5800/;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection 'upgrade';
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
           proxy_intercept_errors on;
           error_page 502 503 504 = /offline.html;
       }
   }
   ```

   With this in place, stopping `./scripts/start-dev.sh` (or the dev server not being up yet) shows the branded "Orbit is starting up" page instead of nginx's raw 502, and it polls in the background and reloads itself automatically once `localhost:5800` responds again.

5. Start nginx (binding ports 80/443 requires root) and the dev stack:

   ```bash
   sudo nginx
   ./scripts/start-dev.sh
   ```

`Cors:Origins` and `Frontend:BaseUrl` in [src/Orbit.Api/appsettings.Development.json](src/Orbit.Api/appsettings.Development.json) already allow both `http://localhost:5800` and `https://www.orbit-local.com`.

Self-service accounts do not require the installation bootstrap. Anyone can register their own organization, first workspace, and owner account directly:

```bash
curl -X POST http://localhost:5014/api/v1/auth/register \
  -H 'Content-Type: application/json' \
  --data '{"displayName":"Ada Lovelace","email":"ada@example.com","password":"ReplaceWithStrongPassword123","organizationName":"Analytical Engines","workspaceName":"Engineering"}'
```

This is a separate path from the installation bootstrap below: bootstrap creates exactly one installation-level site super admin, while registration creates an organization-scoped owner with no site role.

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
  --data '{"email":"admin@example.com","password":"ReplaceWithStrongPassword123","rememberMe":false}'
```

Omitting `rememberMe` (or passing `false`) issues a session lasting about one day; passing `true` extends it to about thirty days. The application header also offers "Sign in with Google," a backend-brokered OAuth flow (`GET /auth/google/start`, `/auth/google/callback`, `POST /auth/google/exchange`) that never exposes a Google client secret or raw Google ID token to the browser — the callback redirect carries only a single-use, hashed handoff code that the frontend immediately exchanges for a session.

Accounts belonging to multiple workspaces can select the active workspace from the application
header. The switch is authorized server-side and rotates the refresh session into the selected
workspace; changing `X-Tenant-Id` or browser storage alone never grants access.

The bootstrap-created site super administrator can create additional workspaces from the plus button
beside the workspace selector. Orbit creates the workspace and owner membership in one transaction,
then rotates the current session into the new workspace.

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

Owners and administrators can invite a local account by email. The invitation is hashed at rest,
expires after seven days, is single-use, and may add the accepted member to a team:

```bash
curl -X POST http://localhost:5014/api/v1/invitations -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  --data '{"email":"member@example.com","role":"Member","teamId":null}'
curl http://localhost:5014/api/v1/invitations -H "Authorization: Bearer $TOKEN"
```

The email opens the PWA acceptance form. Existing local accounts prove their password; a new email
creates its global account and workspace membership atomically. Federated-only invitation acceptance
remains a follow-up increment.

A workspace membership also carries a `MembershipTier` orthogonal to its role: `Standard` members get the
usual tenant-wide project visibility for their role, while `Guest` members (always `Member` role — enforced
by domain validation and a database check constraint) see only projects they are explicitly assigned to.
Owners and administrators can upload a workspace logo the same way work items get attachments — presign,
upload directly to MinIO/S3, then confirm:

```bash
curl -X POST http://localhost:5014/api/v1/workspaces/current/settings/logo/presign \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  --data '{"fileName":"logo.png","contentType":"image/png","sizeBytes":20480}'
# PUT the file to the returned presigned URL, then:
curl -X PUT http://localhost:5014/api/v1/workspaces/current/settings/logo \
  -H "Authorization: Bearer $TOKEN" -H "If-Match: $VERSION" -H 'Content-Type: application/json' \
  --data '{"objectKey":"'"$OBJECT_KEY"'"}'
```

## Sharing and Slack integration (optional)

Every work item has a stable, copyable URL at `/browse/<PROJECTKEY-NUMBER>` (e.g. `/browse/ORB-42`) —
this is what the ticket detail page's copy-link button and the "Share work item" email panel send.
Tenancy isn't encoded in the URL (Orbit resolves the tenant from the viewer's session, not the link
itself — see "Architecture boundaries" below), so a recipient needs an active session in the same
workspace to open it.

"Connect Slack channel" and "Share in Slack" (from a work item's Actions/Share menus) post to a Slack
channel via an [Incoming Webhook](https://api.slack.com/messaging/webhooks) obtained through Slack
OAuth — one connection per project. This requires a Slack app with OAuth redirect
`{web origin}/slack/callback` and the `incoming-webhook` scope; configure its credentials via
environment variables (bound to `Slack:*` in configuration):

```bash
Slack__ClientId=...
Slack__ClientSecret=...
Slack__SigningSecret=...
Slack__RedirectUri=https://your-web-origin/slack/callback
```

Without these set, "Connect Slack channel" fails with a clear "Slack is not configured" error rather
than silently doing nothing. The connected webhook URL is encrypted at rest via ASP.NET Core Data
Protection; in a multi-instance deployment, configure a shared Data Protection key ring (e.g. persisted
to blob storage or Redis) so connections remain decryptable across restarts and instances — the default
local key ring is single-machine only.

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
