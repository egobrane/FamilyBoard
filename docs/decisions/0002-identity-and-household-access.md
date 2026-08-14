# ADR 0002: Identity and Household Access

Status: Accepted for implementation on 2026-08-06; sibling staging domains approved and operational

## Context

Family Dashboard needs adult authentication, child-friendly household profiles, backend-enforced authorization, and a safe long-running wall-display experience. A Google account is an authentication identity; it is not the same thing as a household member. Children may have a profile without any login, and one adult may eventually participate in more than one household.

The frontend and API are independently deployed. Browser authentication must therefore work across exact, configured frontend and API origins without exposing OAuth secrets, privileged tokens, signing material, or database credentials to the frontend.

## Decision

### Identity and profile model

- `UserAccount` represents one person who can authenticate to Family Dashboard.
- `ExternalIdentity` maps a provider and immutable provider subject to a `UserAccount`. Email is profile data, not an identity key.
- `HouseholdMember` remains the product-owned family profile and retains its household role.
- `HouseholdMembership` links a `UserAccount`, `Household`, and `HouseholdMember`. This permits one adult account to participate in multiple households without requiring children to have accounts. Database constraints require the linked profile to belong to the same household.
- `HouseholdInvitation` contains a single-use, expiring, hashed invitation token. Initial invitations are for adults; child profiles are created directly by an adult.
- `UserSession` records an authenticated browser session so it can be revoked independently. It stores identifiers and lifecycle timestamps, not an OAuth access or refresh token.
- `HouseholdAccessPin` stores a slow, salted, and server-peppered hash for parent access on shared displays. The plaintext PIN never leaves the parent-entry request or enters browser storage, logs, configuration, or source control.

Authorization roles remain household-scoped. The initial roles are `Adult` and `Child`; authentication alone grants no household access.

### Google sign-in boundary

Use ASP.NET Core's supported cookie and Google authentication handlers for a backend authorization-code flow. The first slice requests only identity scopes (`openid`, `email`, and `profile`). It does not request Calendar or Tasks scopes, request offline access, or store a Google refresh token.

Calendar authorization is a later, separate, incremental-consent flow. Its encrypted credentials will be owned by a dedicated integration model rather than the login session.

The API handles the Google client secret, authorization callback, identity mapping, and application-session creation. The frontend initiates login by navigating to an API endpoint and never handles a Google authorization code or privileged token.

### Browser session

- Use a protected, HTTP-only, host-only application cookie.
- Production cookies are `Secure`, use `SameSite=Lax`, have `Path=/`, and use a `__Host-` name.
- The protected ticket contains a `UserAccountId` and `UserSessionId`; every authenticated request validates that the account and session remain active.
- Logout revokes the current `UserSession` and deletes the cookie. Adults can later revoke other household-device sessions.
- Persist the ASP.NET Core Data Protection key ring across API restarts and protect it at rest. Key-ring storage and protection are deployment secrets, not repository files.
- Credentialed CORS uses an explicit origin allowlist. Wildcard origins are forbidden.
- Unsafe browser requests require ASP.NET Core antiforgery validation using a non-secret request token sent in a custom header. Native clients will use a separate bearer-token design when they are introduced.

### Shared wall-display session and parent access

- An adult may explicitly mark their application session as a shared household display and leave it signed in.
- A shared session may perform an explicit allowlist of routine household actions without a PIN, including dashboard navigation, chore completion, task participation, reward requests, and Calendar event entry when those features exist.
- Administrative actions require recent parent-PIN verification enforced by backend authorization. Examples include household/member administration, integration connection or removal, role changes, point adjustments, reward approval, PIN changes, session management, and device settings.
- Successful PIN verification grants a short, server-recorded administrative elevation on that `UserSession`. Locking, timeout, logout, revocation, or account disablement removes the elevation.
- PIN verification is rate limited and audited. Repeated failures produce a cooldown. PIN setup or reset requires a fully authenticated adult session and may require Google reauthentication when that flow is implemented.
- The frontend may hide locked controls for usability, but the API policy is authoritative.
- Routine actions performed from the shared display record the selected household-member profile as the actor. Until child authentication exists, that attribution is a selected profile, not proof of the child's identity.

### Household bootstrap and administration

- An authenticated account with no membership may create a household.
- Household creation atomically creates the household, configuration, initial adult member profile, and account link.
- Adult authorization is required to update household settings, create or deactivate profiles, or issue and revoke invitations.
- Historical member records are deactivated, not deleted.
- The last active adult membership cannot be deactivated or unlinked.
- Accepting an invitation is transactional and verifies token hash, expiry, revocation, intended email when present, and household state.

### API shape

Use feature-grouped ASP.NET Core minimal API endpoints, explicit request/response DTOs, `ProblemDetails`, and resource-based authorization. Do not expose EF entities as API contracts and do not introduce MediatR, CQRS, a message broker, or a global frontend state library.

Route household resources explicitly:

- `GET /api/auth/me`
- `GET /api/auth/login/google?returnUrl=...`
- `GET /api/auth/callback/google` (provider callback)
- `POST /api/auth/logout`
- `GET /api/auth/antiforgery`
- `PUT /api/auth/session/household`
- `GET /api/households`
- `POST /api/households`
- `GET /api/households/{householdId}`
- `PATCH /api/households/{householdId}`
- `GET /api/households/{householdId}/members`
- `POST /api/households/{householdId}/members`
- `PATCH /api/households/{householdId}/members/{memberId}`
- `POST /api/households/{householdId}/invitations`
- `POST /api/invitations/{token}/accept`
- `DELETE /api/households/{householdId}/invitations/{invitationId}`
- `PUT /api/households/{householdId}/parent-access-pin`
- `POST /api/households/{householdId}/shared-display`
- `POST /api/households/{householdId}/parent-access/verify`
- `POST /api/households/{householdId}/parent-access/lock`

### Frontend navigation

Introduce React Router when implementation begins because onboarding, invitations, administration, and browser back/forward behavior require stable URLs. This is the only proposed significant frontend dependency. Continue using typed `fetch` wrappers and local component state until shared server-state behavior demonstrates a need for another library.

Initial routes are:

- `/` — authenticated dashboard;
- `/welcome` — signed-out entry point;
- `/auth/error` — recoverable Google sign-in failure;
- `/setup/household` — first-household bootstrap;
- `/households/select` — select a household for the current browser session;
- `/households/:householdId/members` — adult member administration;
- `/invite/:token` — invitation acceptance.

## Security implications

- Google provider subjects, not mutable email addresses, identify external accounts.
- No Google refresh token exists in this slice, reducing the impact of an early authentication defect.
- Cookie authentication requires CSRF protection and exact credentialed CORS configuration.
- Losing the Data Protection key ring invalidates sessions; disclosing it can compromise protected cookies. Backups and access controls are required.
- Invitation tokens are secrets. Store only a cryptographic hash, limit lifetime, make them single-use, and avoid logging them.
- A short PIN has limited entropy. A slow salted hash, server-held pepper, strict online rate limits, cooldowns, and audit records are required; the PIN is an administrative convenience boundary for a trusted household display, not a replacement for Google authentication.
- Every household resource query must include authorization for that household; obscurity of UUIDs is not authorization.

## Consequences and tradeoffs

- Separate identity, profile, and membership records add several small tables but prevent Google accounts from becoming the household domain model.
- Database-backed session validation adds a read to authenticated requests. It provides immediate revocation and is acceptable at household scale; optimization can follow measured need.
- Separating login consent from Calendar consent creates a second Google flow later, but it follows least privilege and keeps login usable when Calendar access is declined or revoked.
- React Router adds a well-supported dependency but avoids custom history and deep-link behavior.
- A shared adult session plus backend PIN elevation matches the near-term household workflow but retains more risk than a separately provisioned device credential. A dedicated device credential can be reconsidered if remote revocation, narrower permissions, or multiple displays demand it.

## Approved product decisions

1. An adult account may belong to more than one household.
2. Initial adult invitations are copyable links without an email-delivery service.
3. Children remain profile-only in the first identity milestone.
4. An adult may leave a shared wall-display session signed in. Routine child-accessible actions remain available; backend-enforced parent-PIN elevation gates administrative actions.
5. Google login and Calendar authorization remain separate consent flows.

The approved staging sibling domains are `family.egobrane.net` and `api.egobrane.net`. Final production OAuth origins and redirect URIs must still be reviewed when Google sign-in is configured.

## References

- [Google OAuth 2.0 for web-server applications](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Google OAuth 2.0 policies](https://developers.google.com/identity/protocols/oauth2/policies)
- [ASP.NET Core cookie authentication](https://learn.microsoft.com/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [ASP.NET Core SameSite cookies](https://learn.microsoft.com/aspnet/core/security/samesite?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection key storage](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- [ASP.NET Core `IAntiforgery`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.antiforgery.iantiforgery?view=aspnetcore-10.0)
