# Orbit Work Management

Orbit is an open-source work-management application for teams that plan, track, and deliver software. It combines project boards, backlogs, sprints, work-item details, team access control, and reporting in one responsive web app.

The project is deliberately split into a headless .NET API, a background worker, and a React progressive web app (PWA). You can run the complete stack on your machine with Podman.

For the product and technical design behind the project, see [the architecture document](ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md). For Railway deployment, see [the deployment guide](deploy/railway/README.md).

## What Orbit can do

### Plan and manage work

- Create projects and organise work into initiatives, epics, stories, tasks, bugs, and subtasks.
- Write rich descriptions with headings, lists, tables, links, images, checklists, mentions, and attached files.
- Assign work, set start and due dates, estimate effort, add labels, and use project-specific custom fields.
- Link related items, including blocking relationships, clone or move work items, archive completed work, and export an item when needed.
- Keep the conversation and history with the work: comments, watchers, votes, work logs, covers, and a full change history are supported.
- Open or share a work item using a stable link such as `/browse/ORB-42`.

### Run Kanban and sprint workflows

- Use project boards with drag-and-drop ordering and configurable columns.
- Create, rename, colour, reorder, set defaults for, and remove workflow statuses without changing code.
- Build and start sprints, add items to them, and complete or reopen a sprint.
- Choose what happens to unfinished items when closing a sprint: roll them into a new sprint or return them to the top of the backlog.
- Review sprint health through burndown, scope-change, cumulative-flow, cycle-time, control-chart, and sprint report views.
- Personalise board and backlog views, including visible card fields, column size, completed-item behaviour, filters, and the backlog preview panel.

### Work safely across organisations and workspaces

- Register a new organisation, first workspace, and owner account from the app, or use the one-time installation bootstrap for a site administrator.
- Sign in with local email and password, optional persistent sessions, password reset, or Google sign-in.
- Switch safely between workspaces; the server validates every switch and rotates the session.
- Invite people, create teams and directory groups, manage memberships, and promote or demote workspace roles.
- Give guests access only to the projects they are explicitly assigned to.
- Create custom project roles and choose their permissions. Service accounts are also available for system-to-system access.
- Keep workspace branding consistent with a logo and typography settings.

### Integrate and operate

- Upload attachments directly to MinIO or any S3-compatible object store using presigned URLs. The worker can scan uploads with ClamAV before they become available.
- Send invitation, password-reset, sprint, and sharing emails through the outbox worker. Mailpit is included for local testing.
- Connect a Slack channel to a project and share a work item through Slack OAuth and an incoming webhook.
- Install Orbit as a PWA and keep local drafts while offline.
- Use health checks, structured telemetry, OpenTelemetry, Prometheus, Loki, Tempo, and Grafana in the local stack.

## Technology at a glance

| Area | Implementation |
| --- | --- |
| API and worker | .NET 10, ASP.NET Core, MediatR, EF Core |
| Web app | React 19, TypeScript, Vite, Tailwind, React Query, TipTap |
| Data and cache | PostgreSQL 18 with row-level security, Valkey |
| Files and email | MinIO/S3-compatible storage, SMTP / Mailpit |
| Local containers | Podman Compose |
| Observability | OpenTelemetry, Prometheus, Loki, Tempo, Grafana |

## Run Orbit locally

### 1. Install the prerequisites

You need the following tools:

- .NET SDK `10.0.201` or a compatible `10.0.2xx` release
- Node.js 24 and npm
- Podman Desktop with Compose support

Start Podman Desktop before continuing. The setup uses it for PostgreSQL, Valkey, Mailpit, MinIO, ClamAV, and local observability services.

### 2. Prepare the project

Clone the repository, then run these commands from its root:

```bash
cp .env.example .env
dotnet tool restore
cd web
npm ci
cd ..
```

The `.env` file is only for local service ports and credentials. Do not commit it. The default values are enough for a first run.

### 3. Start everything

```bash
./scripts/start-dev.sh
```

The script starts the local containers if they are not already running, waits for the required services, applies database migrations, then starts the API, background worker, and web app. Press `Ctrl+C` to stop the application processes; the containers continue running so the next start is quicker.

Open these addresses once the script is ready:

| Service | Address | Why you would use it |
| --- | --- | --- |
| Orbit | [http://localhost:5800](http://localhost:5800) | The application |
| API health | [http://localhost:5014/health/ready](http://localhost:5014/health/ready) | Check the API is ready |
| Mailpit | [http://localhost:8025](http://localhost:8025) | Read local emails |
| MinIO console | [http://localhost:9001](http://localhost:9001) | Inspect local file storage |
| Prometheus | [http://localhost:9090](http://localhost:9090) | Inspect metrics |
| Grafana | [http://localhost:3000](http://localhost:3000) | Inspect local telemetry |

To stop only the local containers later, run:

```bash
./scripts/stop-local-services.sh
```

## First-time use

### Create an organisation from the app

1. Open [http://localhost:5800](http://localhost:5800).
2. Choose **Create account**.
3. Enter your name, email, password, organisation name, and first workspace name.
4. Orbit signs you in as that workspace's owner.
5. Create a project, invite teammates, and add work to its backlog.

This is the normal path for a new organisation. You do not need an installation bootstrap for it.

### Optional: create the site administrator

The bootstrap is for a fresh installation that needs one installation-level site administrator. It can only run once per database.

```bash
curl -X POST http://localhost:5014/api/v1/bootstrap \
  -H 'Content-Type: application/json' \
  --data '{"displayName":"First Admin","email":"admin@example.com","password":"ReplaceWithStrongPassword123","workspaceName":"My Workspace"}'
```

After that, sign in at the web app using the email and password you chose. The site administrator can create additional workspaces from the workspace selector.

## Try it with realistic sample data

The repository includes a sizeable agile sample set: initiatives, epics, stories, subtasks, bugs, teams, sprints, attachments, comments, watchers, and blocking links.

Start the stack and apply migrations first, then run:

```bash
PGPASSWORD=orbit_local psql -h localhost -U orbit -d orbit -f scripts/seed_orbit_large.sql
```

Open Orbit and sign in with any of these local-only accounts:

| Account | Email | Password |
| --- | --- | --- |
| Administrator | `admin@orbit.com` | `Password@9` |
| Developer | `dev1@orbit.com` | `Password@9` |
| QA specialist | `qa1@orbit.com` | `Password@9` |

The seed password is intentionally public and must never be used outside local development. Emails generated by the sample data appear in Mailpit.

## Everyday workflow

1. Create a project and give it a key, such as `ORB`.
2. Set up the workflow statuses and board columns that match the team’s way of working.
3. Add work items to the backlog. Use types, estimates, labels, links, custom fields, attachments, and rich descriptions where helpful.
4. Start a sprint, move the selected items into it, and arrange work on the board as the team progresses.
5. Use the sprint insights and reports to discuss delivery, scope changes, flow, and cycle time.
6. Complete the sprint and choose whether unfinished items should move into the next sprint or back to the backlog.

Workspace owners and administrators can manage people, teams, guest access, custom roles, branding, and integrations from the administration and settings areas.

## Optional integrations and configuration

### Google sign-in

Google sign-in is handled by the API so browser clients never receive a Google client secret or raw ID token. Configure the Google OAuth settings for the API before enabling the sign-in button. The callback uses a short-lived, one-time handoff code before Orbit creates its own session.

### Slack sharing

Orbit can connect one Slack incoming-webhook channel per project. Create a Slack app with the `incoming-webhook` scope and set the OAuth redirect URL to `{web origin}/slack/callback`. Then configure these API settings:

```text
Slack__ClientId=...
Slack__ClientSecret=...
Slack__SigningSecret=...
Slack__RedirectUri=https://your-web-origin/slack/callback
```

The webhook URL is encrypted at rest. In a multi-instance deployment, persist ASP.NET Core Data Protection keys in shared storage so each instance can read existing connections.

### Attachment scanning

ClamAV is included in the local Compose stack, but scanning is opt-in. Set `AttachmentScanning__Enabled=true` for the worker to scan newly uploaded files. Without it, Orbit uses the no-op scanner intended for environments where ClamAV is unavailable.

## How the project is organised

| Path | Responsibility |
| --- | --- |
| `src/Orbit.Domain` | Business rules, aggregates, and domain models |
| `src/Orbit.Application` | Use cases, commands, queries, validation, and interfaces |
| `src/Orbit.Infrastructure` | PostgreSQL, repositories, storage, email, integrations, and migrations |
| `src/Orbit.Api` | HTTP API, authentication, tenancy, and application composition |
| `src/Orbit.Worker` | Outbox email delivery and attachment-scan processing |
| `web` | React PWA and user interface |
| `deploy` | Local service, observability, and Railway configuration |

Every tenant-scoped request runs in a database transaction with the active tenant set for PostgreSQL row-level security. This is a second line of protection alongside application-level permission checks. In production, the API rejects a database connection that can bypass row-level security; migrations use a separate schema-owner connection.

## Development checks

Run the backend and frontend checks before opening a pull request:

```bash
dotnet test Orbit.slnx
cd web
npm run lint
npm test
npm run build
npm run lint:docs
```

Continuous integration runs the backend checks against PostgreSQL and Valkey, validates migration safety, runs frontend checks and an end-to-end smoke test, and builds container images on the relevant branches.

## Deployment

The repository contains OCI build files for the API, worker, and web app:

- `Dockerfile.api`
- `Dockerfile.worker`
- `Dockerfile.web`

Railway is the reference deployment target. It needs three services from this repository—API, worker, and web—plus PostgreSQL and Valkey. Follow [the Railway deployment guide](deploy/railway/README.md) for service configuration, required variables, migration access, and production database-role requirements.

## Contributing

Please keep changes focused, add or update tests where behaviour changes, and run the checks above before submitting a pull request. Do not commit `.env` files, real credentials, or production data.
