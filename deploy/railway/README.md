# Railway deployment

Create `orbit-api`, `orbit-worker`, and `orbit-web` services from the same GitHub repository. In each service, set its Railway config-file path:

- API: `/deploy/railway/api.railway.json`
- Worker: `/deploy/railway/worker.railway.json`
- Web: `/deploy/railway/web.railway.json`

Provision PostgreSQL and Valkey in the same region. Configure these service variables:

| Service | Variable | Value source |
|---|---|---|
| API | `ConnectionStrings__Postgres` | Runtime connection using a `NOSUPERUSER NOBYPASSRLS` role |
| API | `ConnectionStrings__PostgresAdmin` | Schema-owner connection used only by the migration workflow |
| API | `ConnectionStrings__Redis` | Valkey private connection string |
| API | `Cors__Origins__0` | Public web origin |
| API | `Authentication__Local__SigningKey` | Base64-encoded random 32-byte key; required for local email/password login (`POST /api/v1/auth/login`) unless `Authentication__Authority` is set instead |
| API | `Authentication__Authority` | OIDC issuer URL, for federated/enterprise identity instead of local accounts |
| API | `Authentication__Audience` | API audience (only used with `Authentication__Authority`) |
| API | `Authentication__ExternalIdentityAudience` | OIDC SPA client id accepted when linking an external identity |
| API | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Set to `true` behind Railway ingress so rate limits use client IPs |
| Worker | `ConnectionStrings__Postgres` | PostgreSQL private connection string |
| Worker | `ConnectionStrings__Redis` | Valkey private connection string |
| Web build | `VITE_API_URL` | Public API URL ending in `/api/v1` |
| Web build | `VITE_OIDC_AUTHORITY` | OIDC issuer URL used for authorization code and token endpoints |
| Web build | `VITE_OIDC_CLIENT_ID` | Public SPA client id; use the same value for `Authentication__ExternalIdentityAudience` |

Set `RAILWAY_TOKEN` in protected GitHub environments. The deployment workflow runs migrations before updating the services. Use a project-scoped token, never an account-wide token.

Create a dedicated runtime role with `NOSUPERUSER NOBYPASSRLS`, grant it connect, schema usage,
and DML access to the Orbit tables and sequences, and use that role only for
`ConnectionStrings__Postgres`. Keep the database owner in `ConnectionStrings__PostgresAdmin` so
the migration workflow can apply DDL. The API refuses to start outside development when its
runtime connection can bypass row-level security.
