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
| `Authentication__FrontendOrigin` | Public configuration | Exact post-login frontend origin |
| `Authentication__Google__Enabled` | Public configuration | Explicit Google login feature gate |
| `Authentication__Google__ClientId` | Public identifier | Google web OAuth client ID |
| `Authentication__Google__ClientSecret` | Secret | Google web OAuth client secret; backend only |
| `Authentication__SessionIdleLifetime` | Public configuration | Ordinary rolling idle lifetime; default 14 days |
| `Authentication__SessionAbsoluteLifetime` | Public configuration | Ordinary hard lifetime; default 30 days |
| `DataProtection__UseAzure` | Public configuration | Enables Azure-backed key persistence |
| `DataProtection__BlobUri` | Public configuration | Private key-ring blob URI |
| `DataProtection__KeyIdentifier` | Public configuration | Versionless Key Vault wrapping-key URI |
| `DataProtection__ManagedIdentityClientId` | Public identifier | Runtime managed identity used for Blob and Key Vault |

Future OAuth client secrets, token-encryption keys, and signing keys belong only in backend runtime secret storage.

## Storage by environment

- Local Compose: ignored `.env` file copied from `.env.example`.
- Local host: shell environment or .NET user-secrets.
- Netlify: public build variables in deploy-context configuration.
- GitHub Actions: repository-scoped `GITHUB_TOKEN` for GHCR; no personal token.
- K3s: Kubernetes Secret created out of band. No Secret manifest with values is committed.
- Azure staging: secure Bicep input creates the PostgreSQL Container Apps secret; the Google client secret is a versionless Key Vault reference. GitHub uses OIDC variables and no client secret. PostgreSQL, Data Protection Blob Storage, and Key Vault use private networking.

Azure deployment reads `FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD` only while compiling/deploying `staging.bicepparam`. Retain the generated value in an owner-controlled password manager and unset the shell variable immediately after deployment. It is never a frontend or GitHub Actions value.

CORS is an origin boundary, not authentication. The backend validates sessions and household permissions regardless of the requesting frontend route.

The authenticated frontend sends cookies with `credentials: "include"`. Before each unsafe request it obtains fresh antiforgery material from `/api/auth/antiforgery` and sends the returned request token in the returned header name (`X-CSRF-TOKEN` in the current backend). The token remains in memory for the request and is not persisted in browser storage. No privileged value belongs in Netlify configuration.
