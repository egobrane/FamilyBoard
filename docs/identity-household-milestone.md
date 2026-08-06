# Identity and Household Management Milestone

This document turns the accepted [ADR 0002](decisions/0002-identity-and-household-access.md) into an incremental implementation plan. Dependencies, schema migrations, real credentials, and the exact parent-PIN protection parameters retain their approval gates below.

## Goal

An adult can sign in with Google through the backend, create a household, manage adult and child profiles, invite another adult, sign out, and return to an authorized household dashboard. The backend rejects unauthenticated, cross-household, and non-adult mutations.

## Explicitly out of scope

- Google Calendar and Google Tasks scopes or data;
- storage of Google access or refresh tokens;
- passwords or locally managed credentials;
- child login, PIN, or passkey flows;
- production email delivery;
- native-app bearer tokens;
- chore, point, or reward workflows;
- a separately provisioned unattended-display credential; the accepted near-term model uses a shared adult session with parent-PIN elevation.

## Domain changes

### `UserAccount`

Major fields: `Id`, `DisplayName`, `PrimaryEmail`, `IsActive`, `CreatedAt`, `UpdatedAt`.

### `ExternalIdentity`

Major fields: `Id`, `UserAccountId`, `Provider`, `ProviderSubject`, `Email`, `EmailVerified`, `LastLoginAt`, `CreatedAt`.

Constraints: unique `(Provider, ProviderSubject)`. Email is indexed only if useful for invitations; it is never used as the login key.

### `HouseholdMembership`

Major fields: `HouseholdId`, `HouseholdMemberId`, `UserAccountId`, `CreatedAt`.

Constraints: unique member profile; unique `(UserAccountId, HouseholdId)`; composite relationship requiring `HouseholdMemberId` to belong to `HouseholdId`. Authorization role remains on `HouseholdMember`.

### `UserSession`

Major fields: `Id`, `UserAccountId`, `CreatedAt`, `ExpiresAt`, `LastSeenAt`, `RevokedAt`, and an optional user-supplied device label. No OAuth token is stored.

### `HouseholdInvitation`

Major fields: `Id`, `HouseholdId`, `CreatedByMemberId`, `TokenHash`, `InvitedEmail`, `Role`, `CreatedAt`, `ExpiresAt`, `AcceptedAt`, `RevokedAt`.

Constraints: unique token hash; only adults may be invited initially; expired, accepted, or revoked invitations cannot be reused.

### `HouseholdAccessPin`

Major fields: `HouseholdId`, `PinHash`, `HashVersion`, `ChangedAt`, `ChangedByUserAccountId`.

The hash uses a slow password-hashing primitive plus a backend-only pepper. Verification is rate limited and audited. `UserSession` gains `IsSharedDisplay` and `AdministrativeElevationExpiresAt`; no plaintext PIN or reversible PIN value is stored.

## Backend organization

Keep the modular monolith and add feature folders rather than projects:

```text
src/backend/FamilyDashboard.Api/
  Features/
    Authentication/
    Households/
    HouseholdMembers/
    Invitations/
    ParentAccess/
  Domain/
    Identity/
    Households/
  Persistence/Configurations/
```

Each feature owns endpoint registration, DTOs, and focused services. EF configurations remain under persistence. Shared authorization handlers may live under `Security/`.

Proposed dependencies:

- `Microsoft.AspNetCore.Authentication.Google`, matching the .NET runtime version;
- `react-router` for stable frontend onboarding and administration URLs.

No general identity framework, object mapper, validation framework, mediator, or client state library is proposed. ASP.NET Core authentication, authorization, Data Protection, antiforgery, dependency injection, and explicit mapping are sufficient for this slice.

## Incremental delivery

### Increment 1: contracts and authorization seam

- Confirm the initial DTO names and route contracts from accepted ADR 0002.
- Define DTOs, endpoint groups, resource authorization policies, and error contracts.
- Add a test-only authentication scheme through the backend test host, never through production configuration.
- Add authorization tests proving unauthenticated and cross-household access is rejected.

Exit criterion: contract and authorization tests fail for the intended reasons before real persistence behavior is added.

### Increment 2: identity and household persistence

- Add the six proposed entities and EF configurations.
- Generate and review one additive migration.
- Implement atomic household bootstrap.
- Implement household/member read and adult-only mutation endpoints.
- Prevent deactivation of the last active adult.

Exit criterion: a PostgreSQL integration test creates two households and proves their data cannot cross authorization boundaries.

### Increment 3: backend Google sign-in and sessions

- Configure backend Google authorization-code login using identity scopes only.
- Map provider subject to `ExternalIdentity` and `UserAccount`.
- Create, validate, renew, revoke, and expire `UserSession` records.
- Persist and protect the Data Protection key ring.
- Add exact credentialed CORS and antiforgery validation.
- Add login, logout, disabled-account, revoked-session, return-URL, CORS, and CSRF tests.

Exit criterion: the staging API completes Google login without exposing codes, secrets, or provider tokens to frontend JavaScript.

### Increment 4: onboarding and household administration UI

- Add React Router and the proposed routes.
- Add signed-out, loading, authentication-error, no-household, and forbidden states.
- Implement household bootstrap and responsive member management.
- Replace the mock avatar identity with authenticated profile data.
- Keep the existing dashboard cards on mock feature data until their own integrations ship.

Exit criterion: Playwright covers mouse, keyboard, touch-sized controls, phone layout, adult setup, validation errors, and forbidden states.

### Increment 5: adult invitations

- Create, list, revoke, inspect, and accept expiring invitations.
- Show a copyable link; do not add an email vendor.
- Redact tokens from logs and return them only at creation time.
- Add replay, expiry, wrong-email, concurrent acceptance, and last-adult tests.

Exit criterion: a second Google account can join the staging household through a single-use invitation.

### Increment 6: shared display and parent access PIN

- Add adult-only parent-PIN setup and reset.
- Add shared-display enable, verify, lock, timeout, cooldown, and audit behavior.
- Define and test the routine-action allowlist and parent-administration policy.
- Keep parent elevation server-side and short lived; frontend visibility is not authorization.
- Add Playwright coverage for children navigating routine features while administrative routes remain locked.

Exit criterion: a shared display remains useful for routine family actions and cannot perform backend administrative mutations without recent parent-PIN verification.

### Increment 7: staging and wall-display validation

- Deploy an immutable backend image and migration job.
- Configure exact Netlify and API origins plus Google redirect URIs.
- Verify session persistence across API rollout and PWA refresh.
- Exercise sign-in, setup, invitation, revocation, and sign-out on desktop, phone, and the physical wall display.
- Record whether operational experience justifies a separately provisioned household-device credential.

Exit criterion: staging evidence is recorded, parent-PIN authorization is verified on the physical display, and the residual risk of the shared adult session is explicitly accepted or replaced.

## Test strategy

- Unit tests for invitation, last-adult, PIN cooldown, and elevation-expiry invariants.
- PostgreSQL integration tests for constraints, migration, household isolation, and transactional invitation acceptance.
- `WebApplicationFactory` tests with a test-only authentication handler for every authorization policy and endpoint outcome.
- Authentication handler tests with provider callbacks simulated at the backend boundary; no live Google calls in CI.
- Frontend component tests with typed API doubles.
- Playwright onboarding/admin tests using deterministic API fixtures.
- One manual staging test with real Google configuration and exact redirect origins.

## Configuration additions

Public frontend:

- `VITE_API_BASE_URL` only.

Sensitive backend:

- `Authentication__Google__ClientId`;
- `Authentication__Google__ClientSecret`;
- parent-PIN pepper/key material;
- Data Protection key-ring location and key-protection material;
- session lifetime and invitation lifetime;
- exact frontend origins and allowed post-login return origins.

Examples contain placeholders only. Google secrets, cookies, invitation tokens, and key material must never appear in Netlify variables, source control, container layers, logs, or frontend bundles.

## Expected implementation file changes

The exact list will be confirmed in the plan for each approved increment. Expected areas are:

- `src/backend/FamilyDashboard.Api/Program.cs`;
- new `Features/Authentication`, `Features/Households`, `Features/HouseholdMembers`, `Features/Invitations`, and `Features/ParentAccess` files;
- new `Domain/Identity` entities and household link/invitation entities;
- `Persistence/FamilyDashboardDbContext.cs`, entity configurations, migration, and snapshot;
- backend configuration classes and placeholder app settings;
- frontend routing, typed API client, authentication state, onboarding, and member-management features;
- backend, frontend, and Playwright tests;
- Compose, K3s, configuration, authentication, database, and deployment documentation.

## Approval gates

Implementation must pause for approval at these points:

1. introduction of the Google authentication package and React Router;
2. generated database migration review;
3. real Google client configuration and staging secrets;
4. the exact parent-PIN hashing, rate-limit, elevation lifetime, and administrative-action policy;
5. any future separately provisioned household-device credential.
