# Configuration and Secrets

ASP.NET Core reads hierarchical settings from environment variables using double underscores. Vite variables are embedded into browser JavaScript at build time and are always public.

## Frontend public values

| Variable | Purpose |
|---|---|
| `VITE_API_BASE_URL` | Public HTTPS origin of the backend API |
| `VITE_APP_NAME` | Public display name |

Never place secrets, refresh tokens, connection strings, signing keys, or administrative credentials in a `VITE_` variable.

## Backend values

| Variable | Sensitivity | Purpose |
|---|---|---|
| `ConnectionStrings__FamilyDashboard` | Secret | PostgreSQL connection string |
| `Cors__AllowedOrigins__0` and subsequent indexes | Public configuration | Exact trusted frontend origins |
| `ASPNETCORE_ENVIRONMENT` | Public configuration | Runtime environment |
| `ASPNETCORE_HTTP_PORTS` | Public configuration | Container listening port |

Future OAuth client secrets, token-encryption keys, and signing keys belong only in backend runtime secret storage.

## Storage by environment

- Local Compose: ignored `.env` file copied from `.env.example`.
- Local host: shell environment or .NET user-secrets.
- Netlify: public build variables in deploy-context configuration.
- GitHub Actions: repository-scoped `GITHUB_TOKEN` for GHCR; no personal token.
- K3s: Kubernetes Secret created out of band. No Secret manifest with values is committed.

CORS is an origin boundary, not authentication. When authenticated APIs arrive, the backend must validate sessions and permissions regardless of the requesting frontend route.
