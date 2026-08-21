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
| `Invitations__Lifetime` | Public configuration | Invitation validity; default seven days |
| `Invitations__PendingCookieLifetime` | Public configuration | Maximum protected pending-cookie lifetime; default 30 minutes and capped by invitation expiry |
| `ParentAccess__Enabled` | Public configuration | Explicit parent-access feature and readiness gate |
| `ParentAccess__Pepper` | Secret | Base64-encoded random 32-byte backend-only PIN pepper |
| `ParentAccess__PepperVersion` | Public configuration | Version stored beside each PIN hash; default 1 |
| `ParentAccess__PinLength` | Public policy | Exact PIN length; default six digits |
| `ParentAccess__WorkFactor` | Public policy | PBKDF2-HMAC-SHA-256 iterations; default 600,000 |
| `ParentAccess__ElevationLifetime` | Public policy | Fixed parent elevation lifetime; default five minutes |
| `ParentAccess__RecentAuthenticationLifetime` | Public policy | Maximum session age for setup/recovery; default ten minutes |
| `ParentAccess__MaximumFailures` | Public policy | Failures before cooldown; default five |
| `ParentAccess__FailureWindow` | Public policy | Failed-attempt window; default ten minutes |
| `ParentAccess__LockoutLifetime` | Public policy | Per-session cooldown; default 15 minutes |
| `GoogleCalendar__Enabled` | Public configuration | Explicit Calendar integration feature gate; disabled by default |
| `GoogleCalendar__EventCreationEnabled` | Public configuration | Separate controlled event-creation gate; disabled by default even when Calendar reads are enabled |
| `GoogleCalendar__ClientId` | Public identifier | Separate Calendar OAuth web client ID |
| `GoogleCalendar__ClientSecret` | Secret | Separate Calendar OAuth client secret; backend only |
| `GoogleCalendar__CallbackUrl` | Public configuration | Exact backend Calendar OAuth callback URL |
| `GoogleCalendar__AuthorizationLifetime` | Public policy | Protected OAuth state lifetime; default ten minutes |
| `GoogleCalendar__FreshCacheLifetime` | Public policy | Fresh in-memory event-cache lifetime; default two minutes |
| `GoogleCalendar__StaleCacheLifetime` | Public policy | Maximum stale fallback lifetime; default fifteen minutes |
| `GoogleCalendar__MaximumCalendarsPerHousehold` | Public policy | Source-selection limit; default 25 |
| `GoogleCalendar__MaximumEventsPerRequest` | Public policy | Normalized event cap; default 1,000 |

Calendar OAuth access and refresh tokens are encrypted by the existing persisted Data Protection key ring before PostgreSQL storage. The separate Calendar client secret and all future signing keys belong only in backend runtime secret storage.

## Storage by environment

- Local Compose: ignored `.env` file copied from `.env.example`.
- Local host: shell environment or .NET user-secrets.
- Netlify: public build variables in deploy-context configuration.
- GitHub Actions: repository-scoped `GITHUB_TOKEN` for GHCR; no personal token.
- K3s: Kubernetes Secret created out of band. No Secret manifest with values is committed.
- Azure staging: secure Bicep input creates the PostgreSQL Container Apps secret; Google and parent-access secrets are Key Vault references. GitHub uses OIDC variables and no client secret. PostgreSQL, Data Protection Blob Storage, and Key Vault use private networking.

Manual full-infrastructure Azure deployment reads `FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD` only while compiling/deploying `staging.bicepparam`. Retain the generated value in an owner-controlled password manager and unset the shell variable immediately after deployment. Routine GitHub application deployment uses a clearly non-secret validation placeholder only to compile the reviewed public parameters; it neither needs nor retrieves the real password. The real value is never a frontend or GitHub Actions value.

CORS is an origin boundary, not authentication. The backend validates sessions and household permissions regardless of the requesting frontend route.

The authenticated frontend sends cookies with `credentials: "include"`. Before each unsafe request it obtains fresh antiforgery material from `/api/auth/antiforgery` and sends the returned request token in the returned header name (`X-CSRF-TOKEN` in the current backend). The token remains in memory for the request and is not persisted in browser storage. No privileged value belongs in Netlify configuration.
