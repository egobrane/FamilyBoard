# Authentication Boundary

Identity Increment 3 implements the backend Google authorization-code flow and revocable application-cookie sessions. Google authentication is active in Azure staging with its client secret held in Key Vault. Protected endpoints continue to fail closed with stable `401 ProblemDetails` when no valid application session is present; disabling Google configuration makes the login endpoint return `503 authentication_unavailable`.

Household authorization uses persistence-backed membership lookup and requires an active account, household, and linked member profile. Backend tests retain a test-assembly-only header scheme for household authorization and use real protected cookies plus database sessions for authentication tests. Neither test bypass is available in production.

The accepted identity, session, household-membership, invitation, Google sign-in, shared-display, and parent-PIN design is recorded in [ADR 0002](decisions/0002-identity-and-household-access.md).

## Implemented browser flow

- `GET /api/auth/login/google?returnUrl=/local-path` starts the Google challenge.
- Invitation sign-in adds `chooseAccount=true`, which asks Google to show account selection without adding scopes or changing the identity-only authorization flow.
- Google returns only to `https://api.egobrane.net/api/auth/callback/google` in staging.
- The backend accepts only `openid profile email`, identifies accounts by Google `sub`, requires verified email, and does not request offline access.
- Provider access tokens, refresh tokens, and authorization codes are never stored in the application database, application cookie, frontend bundle, or browser storage.
- `UserSession` provides rolling idle expiration, a hard absolute expiration, last-seen throttling, revocation, shared-display state, and the reserved administrative-elevation expiry.
- `PUT /api/auth/session/household` stores the selected active membership on the current `UserSession`; a composite database constraint prevents selecting a household owned only by another account.
- `__Host-FamilyDashboard.Session` is host-only, `Secure`, `HttpOnly`, `Path=/`, and `SameSite=Lax`.
- Unsafe cookie-authenticated requests require the synchronized `X-CSRF-TOKEN` antiforgery header.
- Credentialed CORS allows only the configured exact frontend origin. Staging allows `https://family.egobrane.net`.
- Return URLs must be local paths; external or ambiguous targets are rejected before the Google challenge.
- The React client models loading, signed-out, unavailable, disabled-account, first-household, household-selection, and ready states. It uses credentialed requests, keeps antiforgery material in memory only, and never attempts to read the HTTP-only session cookie.
- Invitation links place the raw token in a URL fragment, which is removed immediately and exchanged from the exact frontend origin for a 30-minute protected, host-only, secure, HTTP-only pending-invitation cookie. The raw token is not stored in PostgreSQL, browser storage, Google state, analytics, or subsequent browser history.

## Continuing constraints

- Authentication identities are separate from household-member profiles. A child profile may have no login.
- Google OAuth uses authorization-code flow through the backend.
- OAuth client secrets and Google refresh tokens never reach browser JavaScript.
- Google login and future Calendar authorization remain separate. Future Calendar refresh tokens require a separately reviewed encrypted store.
- The backend enforces household membership and action-level authorization on every protected endpoint.
- Frontend route guards are usability features, not security controls.
- Secure, HTTP-only cookies are preferred for browser sessions.
- An adult may leave a session in shared-display mode for routine household use. Backend policies require a recent parent-PIN elevation for administrative actions; hiding controls in the frontend is not authorization.
- Production should use sibling custom domains, such as `dashboard.example.com` and `api.dashboard.example.com`, to avoid third-party-cookie limitations.

Netlify Deploy Preview origins are dynamic. Before authenticated previews are enabled, define whether they use a dedicated non-production backend, an explicit per-preview allowlist, or no authenticated API access. Broad wildcard credentialed CORS is not acceptable.

Calendar/Tasks token storage, the exact parent-PIN protection parameters, and any future separately provisioned wall-display credential remain separate approval decisions. Azure Data Protection keys are persisted in private Blob Storage and wrapped by a versionless Key Vault key using the API runtime managed identity.

## Staging activation handoff

1. In Google Cloud, create a Web application OAuth client and add only `https://api.egobrane.net/api/auth/callback/google` as its authorized redirect URI. Do not add Calendar scopes, request offline access, or place the secret in Netlify or GitHub.
2. Seed the client secret as Key Vault secret `google-client-secret` with `deploy/azure/google-secret.bicepparam`. It reads a hidden environment variable and passes a secure ARM parameter, so the private vault can remain closed and the value does not enter source, command arguments, or deployment output.
3. Set `enableGoogleAuthentication = true` and the public client ID in `deploy/azure/staging.bicepparam`, then deploy the infrastructure template with the existing secure PostgreSQL input.
4. Publish the reviewed backend commit, select its immutable GHCR digest, and run the protected `Deploy backend to Azure staging` workflow. The migration job must succeed before the API update.
5. Verify login, `/api/auth/me`, antiforgery-protected logout, revocation, expiration behavior, exact CORS, and cookie continuity across a scale-to-zero wake and a new API revision.

The Google client and secret are external prerequisites and cannot be generated from this repository. Keep Google authentication disabled in a new environment until both values are real and the secret reference resolves.

## Staging activation status

Google sign-in was activated and verified on 2026-08-14. Azure Container Apps terminates public TLS before forwarding requests to Kestrel, so the API container sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; this preserves the original HTTPS scheme when ASP.NET Core constructs the Google callback URL. The Container App is the only public path to the target port, which is the trust boundary for forwarded headers.

The live OAuth challenge uses the exact callback `https://api.egobrane.net/api/auth/callback/google`, requests only `openid`, `email`, and `profile`, uses `response_type=code`, and does not request offline access. A real first-time sign-in created the application account and database-backed session, redirected to `https://family.egobrane.net/`, and returned the authenticated account through `/api/auth/me`. The same session remained valid after traffic moved to a functionally identical API revision, proving that persisted Azure Data Protection keys support cross-revision cookie continuity. A live antiforgery-protected logout returned `204`, the revoked session then received `401` from `/api/auth/me`, and a subsequent Google sign-in succeeded.
