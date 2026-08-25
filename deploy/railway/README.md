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
| API | `Frontend__BaseUrl` | Public web origin; used to build the link in password-reset emails |
| API | `GoogleOAuth__ClientId` | Google OAuth client id for backend-brokered sign-in |
| API | `GoogleOAuth__ClientSecret` | Google OAuth client secret |
| API | `GoogleOAuth__RedirectUri` | Google OAuth callback URL (`{api origin}/api/v1/auth/google/callback`) |
| API | `Slack__ClientId` | Slack app client id for project webhook integrations |
| API | `Slack__ClientSecret` | Slack app client secret |
| API | `Slack__SigningSecret` | Slack app signing secret |
| API | `Slack__RedirectUri` | Slack OAuth redirect URL (`{web origin}/slack/callback`) |
| API | `ObjectStorage__Endpoint` | S3 / MinIO endpoint URL (leave unset for AWS S3) |
| API | `ObjectStorage__BucketName` | Object storage bucket name for attachments and workspace logos |
| API | `ObjectStorage__AccessKey` | S3 / MinIO access key |
| API | `ObjectStorage__SecretKey` | S3 / MinIO secret key |
| API | `ObjectStorage__Region` | AWS region (e.g. `eu-west-1` or `us-east-1`) |
| API | `RateLimiting__Distributed__Enabled` | Set to `true` to use shared Valkey sliding-window rate limiting |
| API / Worker | `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint (e.g. `http://orbit-otel:4317`) |
| Worker | `ConnectionStrings__Postgres` | PostgreSQL private connection string |
| Worker | `ConnectionStrings__Redis` | Valkey private connection string |
| Worker | `Email__Smtp__Host` | SMTP relay hostname |
| Worker | `Email__Smtp__Port` | SMTP relay port |
| Worker | `Email__Smtp__Username` | SMTP relay username; leave unset for an unauthenticated relay |
| Worker | `Email__Smtp__Password` | SMTP relay password |
| Worker | `Email__Smtp__UseStartTls` | Set to `true` if the relay requires STARTTLS |
| Worker | `Email__Smtp__FromAddress` | Sender address for outbound mail |
| Worker | `Email__Smtp__FromName` | Sender display name for outbound mail |
| Worker | `AttachmentScanning__Enabled` | Set to `true` to enable ClamAV virus scanning of uploaded attachments |
| Worker | `AttachmentScanning__ClamAv__Host` | ClamAV daemon hostname |
| Worker | `AttachmentScanning__ClamAv__Port` | ClamAV daemon port (default `3310`) |
| Web build | `VITE_API_URL` | Public API URL ending in `/api/v1` |
| Web build | `VITE_OIDC_AUTHORITY` | OIDC issuer URL used for authorization code and token endpoints |
| Web build | `VITE_OIDC_CLIENT_ID` | Public SPA client id; use the same value for `Authentication__ExternalIdentityAudience` |
| Web build | `VITE_SENTRY_DSN` | Optional Sentry DSN for frontend error capture |

Set `RAILWAY_TOKEN` in protected GitHub environments. The deployment workflow runs migrations before updating the services. Use a project-scoped token, never an account-wide token.

Create a dedicated runtime role with `NOSUPERUSER NOBYPASSRLS`, grant it connect, schema usage,
and DML access to the Orbit tables and sequences, and use that role only for
`ConnectionStrings__Postgres`. Keep the database owner in `ConnectionStrings__PostgresAdmin` so
the migration workflow can apply DDL. The API refuses to start outside development when its
runtime connection can bypass row-level security.
