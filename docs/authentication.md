# Authentication Boundary

Google Calendar and Google Tasks authorization are independent from identity sign-in and from one another. Each integration has its own OAuth client, exact callback, protected state and correlation cookie, encrypted backend-only refresh token, revocation lifecycle, and household source-selection boundary. A Google Tasks grant cannot authenticate an application session or authorize Calendar access.

Identity Increment 3 implements the backend Google authorization-code flow and revocable application-cookie sessions. Google authentication is active in Azure staging with its client secret held in Key Vault. Protected endpoints continue to fail closed with stable `401 ProblemDetails` when no valid application session is present; disabling Google configuration makes the login endpoint return `503 authentication_unavailable`.

Household authorization uses persistence-backed membership lookup and requires an active account, household, and linked member profile. Backend tests retain a test-assembly-only header scheme for household authorization and use real protected cookies plus database sessions for authentication tests. Neither test bypass is available in production.

The accepted identity, session, household-membership, invitation, Google sign-in, shared-display, and parent-PIN design is recorded in [ADR 0002](decisions/0002-identity-and-household-access.md).

## Implemented browser flow

- `GET /api/auth/login/google?returnUrl=/local-path` starts the Google challenge.
- Invitation sign-in adds `chooseAccount=true`, which asks Google to show account selection without adding scopes or changing the identity-only authorization flow.
- Google returns only to `https://api.egobrane.net/api/auth/callback/google` in staging.
- The backend accepts only `openid profile email`, identifies accounts by Google `sub`, requires verified email, and does not request offline access.
- Provider access tokens, refresh tokens, and authorization codes are never stored in the application database, application cookie, frontend bundle, or browser storage.
- `UserSession` provides rolling idle expiration, a hard absolute expiration, last-seen throttling, revocation, shared-display state, household-scoped administrative elevation, and per-session parent-PIN cooldown state.
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
- Google login and Calendar authorization remain separate clients and flows. Calendar authorization requests offline access; reads use the exact read-only Calendar scopes, while controlled event creation requires a separate incremental `calendar.events` grant. Its refresh token is purpose-protected with the persisted Azure Data Protection key ring before PostgreSQL storage.
- The backend enforces household membership and action-level authorization on every protected endpoint.
- Frontend route guards are usability features, not security controls.
- Secure, HTTP-only cookies are preferred for browser sessions.
- An adult may leave a session in shared-display mode for routine household use. Backend policies require a recent parent-PIN elevation for administrative actions; hiding controls in the frontend is not authorization.
- Production should use sibling custom domains, such as `dashboard.example.com` and `api.dashboard.example.com`, to avoid third-party-cookie limitations.

Netlify Deploy Preview origins are dynamic. Before authenticated previews are enabled, define whether they use a dedicated non-production backend, an explicit per-preview allowlist, or no authenticated API access. Broad wildcard credentialed CORS is not acceptable.

Google Tasks authorization and controlled mutations are active in staging through the dedicated client and exact callback. Its client secret reaches only the API through the `google-tasks-client-secret` Key Vault reference and runtime managed identity; access and refresh tokens are purpose-protected with the persisted Azure Data Protection key ring before PostgreSQL storage. Increment 2 requests the broad Tasks scope only through explicit administrative reauthorization and never treats that grant as an application session or Calendar authorization. The owner completed incremental consent and provider-visible creation/completion on 2026-08-29. Locked shared displays may perform enabled routine task mutations only with explicit active-member attribution; connection, scope, list, and writable-target configuration remain parent-elevated administration.

## Shared-display parent access

- Ordinary private adult sessions satisfy the household-administration policy through Google-backed application authentication. Shared-display sessions must additionally hold an unexpired five-minute elevation for the target household.
- PIN setup and recovery require a private application session created by Google sign-in within the preceding ten minutes. Replacing an existing PIN requires current elevation. A forgotten PIN is recovered by signing out, signing in again on a private device, and choosing a new PIN.
- A PIN contains exactly six digits. The backend HMACs it with a Key Vault-backed 32-byte pepper, then applies PBKDF2-HMAC-SHA-256 with a unique 16-byte salt and a 600,000-iteration default. PostgreSQL stores only the derived hash, salt, version, work factor, and actor/timestamps.
- Five failed attempts in ten minutes place only that session into a 15-minute cooldown. The ASP.NET request limiter also bounds expensive verification work per session and household. Responses remain generic and cooldown responses include `Retry-After`.
- Selecting another household, explicit lock, timeout, logout, session revocation, account disablement, PIN replacement, or recovery removes applicable elevation. Replacing or recovering a PIN clears other sessions' elevation but does not silently sign shared displays out.
- Parent-access audit events include identifiers, event type, outcome, time, trace identifier, and optional cooldown expiry. They never contain PINs, hashes, salts, peppers, cookie values, antiforgery tokens, or request bodies.
- Household settings, member administration, invitation administration, chore configuration/review, and point correction/reversal enforce the policy at the API. Bootstrap and invitation acceptance require a private session. Routine household access includes dashboard, Calendar and chore reads, attributed chore completion, point balance/history, household selection, antiforgery retrieval, and logout. Calendar configuration, chore administration/review, and point corrections require current household-scoped elevation on shared displays.

## Staging activation handoff

1. In Google Cloud, create a Web application OAuth client and add only `https://api.egobrane.net/api/auth/callback/google` as its authorized redirect URI. Do not add Calendar scopes, request offline access, or place the secret in Netlify or GitHub.
2. Seed the client secret as Key Vault secret `google-client-secret` with `deploy/azure/google-secret.bicepparam`. It reads a hidden environment variable and passes a secure ARM parameter, so the private vault can remain closed and the value does not enter source, command arguments, or deployment output.
3. Set `enableGoogleAuthentication = true` and the public client ID in `deploy/azure/staging.bicepparam`, then deploy the infrastructure template with the existing secure PostgreSQL input.
4. Push the reviewed backend commit. The backend publication workflow records the immutable GHCR digest and passes it directly to the protected staging deployment without changing `deploy/azure/staging.bicepparam`. The migration job must succeed before the reconciled API update.
5. Verify login, `/api/auth/me`, antiforgery-protected logout, revocation, expiration behavior, exact CORS, and cookie continuity across a scale-to-zero wake and a new API revision.

The Google client and secret are external prerequisites and cannot be generated from this repository. Keep Google authentication disabled in a new environment until both values are real and the secret reference resolves.

## Staging activation status

Google sign-in was activated and verified on 2026-08-14. Azure Container Apps terminates public TLS before forwarding requests to Kestrel, so the API container sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; this preserves the original HTTPS scheme when ASP.NET Core constructs the Google callback URL. The Container App is the only public path to the target port, which is the trust boundary for forwarded headers.

The live OAuth challenge uses the exact callback `https://api.egobrane.net/api/auth/callback/google`, requests only `openid`, `email`, and `profile`, uses `response_type=code`, and does not request offline access. A real first-time sign-in created the application account and database-backed session, redirected to `https://family.egobrane.net/`, and returned the authenticated account through `/api/auth/me`. The same session remained valid after traffic moved to a functionally identical API revision, proving that persisted Azure Data Protection keys support cross-revision cookie continuity. A live antiforgery-protected logout returned `204`, the revoked session then received `401` from `/api/auth/me`, and a subsequent Google sign-in succeeded.

Shared-display parent access was activated and verified in staging on 2026-08-18. The API reads `parent-access-pepper-v1` through its existing Key Vault reference; neither the migration job nor frontend receives it. Setup, replacement, recovery, failed-attempt cooldown, five-minute expiry, explicit lock, household-switch clearing, private-only bootstrap and invitation acceptance, and elevated exit from shared mode were exercised successfully. Routine dashboard navigation remained available while administrative APIs continued to fail closed until elevation. Physical wall-display, phone, touch, mouse, keyboard, and browser-storage inspection also passed.

Separate Google Calendar authorization was activated and verified on 2026-08-19. Its OAuth web client, callback, scopes, refresh token, Key Vault secret, and connection lifecycle remain independent of Google sign-in. Calendar connection, source configuration, and disconnect require household administration; a shared display therefore needs current household-scoped parent elevation. Routine Calendar reads remain available while locked. The corrected callback accepts Google's canonical email identity-scope alias while continuing to require both exact read-only Calendar data scopes.

Controlled Calendar event creation was activated and verified on 2026-08-21 without changing Google sign-in scopes or exposing provider tokens to the browser. Incremental write consent and writable-target selection are administrative and retain the parent-elevation boundary on shared displays. Creating an event is routine household participation: a locked shared display must supply an active household-member attribution, while a private adult session defaults attribution to its linked adult profile. The server still enforces selected-household isolation, antiforgery, exact credentialed CORS, and the per-session/household rate limit.

Controlled Calendar editing and deletion reuse that separate Calendar grant but are administrative rather than routine. The backend accepts only successful Family Dashboard creation receipts, checks the current Google ETag, and requires household administration; a shared display therefore needs current parent-PIN elevation. Frontend visibility and opaque management routes do not replace these server checks.
