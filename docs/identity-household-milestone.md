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

Major fields: `Id`, `UserAccountId`, `CreatedAt`, `LastSeenAt`, `ExpiresAt`, `AbsoluteExpiresAt`, `RevokedAt`, optional `DeviceLabel`, `IsSharedDisplay`, and optional `AdministrativeElevationExpiresAt`. No OAuth token is stored.

### `HouseholdInvitation`

Major fields: `Id`, `HouseholdId`, `CreatedByUserAccountId`, `IntendedEmailNormalized`, `TokenHash`, `Status`, `CreatedAt`, `ExpiresAt`, `AcceptedAt`, `AcceptedByUserAccountId`, `RevokedAt`, and `RevokedByUserAccountId`.

Constraints: unique 32-byte token hash; one pending invitation per household and normalized email; restrictive actor and household foreign keys; terminal timestamps and actors must match the status. Only adults are invited; expired, accepted, or revoked invitations cannot be reused.

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

- **Implemented 2026-08-11.** The authentication, household, household-member, and access DTOs now establish the JSON contract without exposing persistence entities.
- The accepted routes remain documented in ADR 0002 but are intentionally not mapped yet; inactive stub endpoints would imply functionality that does not exist.
- `HouseholdMember` and `HouseholdAdult` resource policies use the authenticated internal user-account identifier plus the route household identifier. The production access evaluator denies every request until Increment 2 supplies persistence-backed membership lookup.
- API errors use RFC 7807 `ProblemDetails` with a stable `code`, request `traceId`, and field errors when validation fails.
- A header-based authentication handler exists only in the backend test assembly. Production registers neither that scheme nor a caller-controlled identity header.
- Automated tests prove unauthenticated, malformed-identity, insufficient-role, and cross-household access is rejected; adults satisfy both household policies.

Exit criterion: complete. Contract and authorization tests pass while production access remains deny-by-default and no identity route is activated.

### Increment 2: identity and household persistence

- **Implemented on 2026-08-12 and deployed to Azure staging.** `UserAccount`, `ExternalIdentity`, and `HouseholdMembership` plus their EF configurations establish persistent identity and household access. Session, invitation, and PIN tables remain deferred until their behavior is implemented.
- The additive `AddIdentityAndHouseholdPersistence` migration adds the three tables, unique provider-subject identity, unique account-household membership, and a composite foreign key requiring the linked profile to belong to the same household.
- `GET /api/auth/me`, household list/bootstrap/read/update, and household-member list/create/update endpoints are mapped with explicit DTOs and stable problems.
- Household bootstrap atomically creates the household, configuration, initial adult profile, and account link.
- The EF-backed authorization evaluator requires an active account, household, profile, and membership; cross-household access returns not found.
- Child creation produces only a profile. Adult creation remains reserved for the invitation increment.
- Adult deactivation uses a serializable transaction and rejects removal of the last active linked adult.
- The deployed Increment 2 image uses a no-identity authentication scheme that always fails closed. The client-controlled header scheme remains compiled only into the test assembly.

Exit criterion: complete. All 30 backend tests passed against PostgreSQL 18, including migration, atomic bootstrap, multiple-household membership, child profile ownership, cross-household isolation, relational constraints, last-adult protection, and concurrent deactivation safety. GitHub Actions then ran the additive migration and deployed the same immutable image digest to Azure; public live/readiness checks pass and unauthenticated `/api/auth/me` still fails closed with the documented 401 problem response.

### Increment 3: backend Google sign-in and sessions

- **Implemented on 2026-08-13 and activated in Azure staging on 2026-08-14.** The backend Google authorization-code handler requests identity scopes only, requires verified email, uses provider `sub` as the login key, and stores no Google tokens.
- `UserSession` records provide database-backed validation, rolling idle expiration, hard absolute expiration, throttled last-seen writes, immediate revocation, shared-display state, and reserved parent-PIN elevation state.
- The host-only secure application cookie is paired with exact credentialed CORS, synchronized antiforgery validation on mutations, and local-path-only return URLs.
- Azure Data Protection uses private Blob Storage, a Key Vault wrapping key, private endpoints, and the API runtime managed identity. The migration job receives none of those permissions.
- Fifty-one backend tests pass against PostgreSQL 18, including concurrent first login, disabled/revoked/expired sessions, unavailable-login problems, CORS, CSRF, return URLs, schema migration, and all earlier household-isolation behavior.

Exit criterion: complete. The external Google web client secret resides only in Key Vault; real sign-in, persisted application sessions, exact HTTPS callback behavior, cross-revision cookie continuity, antiforgery-protected logout, revocation, and subsequent sign-in were verified in staging without exposing codes, secrets, or provider tokens to frontend JavaScript.

### Increment 4: authenticated onboarding and household selection

- **Implemented on 2026-08-14 and deployed and verified in staging on 2026-08-15.** React Router provides stable `/`, `/welcome`, `/auth/error`, `/setup/household`, and `/households/select` URLs with browser back/forward behavior.
- The frontend has explicit loading, signed-out, unavailable, disabled-account, no-household, household-selection, and ready-dashboard states. Production authorization remains entirely backend-enforced.
- A typed `fetch` client always includes browser credentials and obtains fresh antiforgery material before household creation, household selection, and logout. It stores no cookie, token, or privileged configuration in browser storage.
- First-household bootstrap now atomically creates the household, configuration, adult profile, account membership, and current session selection in one EF Core save.
- Each `UserSession` may persist a different selected household. The database enforces that the selection belongs to that session's account, and cross-household selection returns the same not-found boundary used by other household resources.
- The dashboard avatar and household heading use authenticated API data. The bundled public family photo remains an explicitly accepted fallback until household photo storage, resizing, deletion, access control, and privacy behavior are designed.
- Existing schedule, chore, and reward cards remain mock feature data. Household settings and member administration move to the next focused slice; parent-only administration will remain behind the future backend-enforced parent PIN.
- Ten frontend component/API tests, eight wall-display/phone Playwright cases, and 55 backend tests cover the behavior, including pointer and keyboard navigation, responsive overflow, secure logout, CSRF, atomic selection, per-session independence, inactive membership filtering, cross-household isolation, and the new PostgreSQL constraint.

Exit criterion: complete. CI and multi-architecture image publication passed; the additive migration and digest-pinned API deployment succeeded; Netlify published the matching frontend; and the owner verified real first-household bootstrap, authenticated household context, logout/revocation, subsequent Google sign-in, two-household switching in both directions, and refresh persistence. `Staging Selection Test Household` remains intentionally stored because deletion is not implemented.

### Increment 4B: household settings and member administration

- **Implemented, deployed, and verified in staging on 2026-08-15.** Adult accounts can open stable household-scoped settings and members routes from the account menu.
- Settings support household name, time zone, locale, and first-day-of-week updates. A successful name update refreshes authenticated context so the shared dashboard heading changes without a new sign-in.
- Member administration lists active and inactive profiles; creates and edits child-only profiles; and deactivates or reactivates profiles without deleting historical records. Arbitrary linked-adult creation remains unavailable pending copyable invitation links.
- Existing backend household isolation remains fail-closed. The API rejects cross-household access as not found, protects the last active adult under a serializable transaction, and now rejects self-deactivation with `409 self_deactivation_requires_leave_flow` until a dedicated leave-household workflow exists.
- All unsafe requests use the existing credentialed cookie and fresh antiforgery-token client. Frontend visibility is convenience only; the API continues to enforce adult authorization.
- The responsive screens provide large touch targets, pointer and keyboard operation, labeled status/error states, focus-managed modal dialogs, and wall-display and phone coverage. The public demo photo remains the fallback because photo storage and privacy behavior are still deferred.
- Thirteen frontend component/API tests, ten wall-display/phone Playwright cases, and 58 backend tests pass locally. The backend suite includes real PostgreSQL 18 coverage for authorization, antiforgery, last-adult concurrency, and the self-deactivation contract.
- No database migration or new runtime dependency is required because Increment 2 already introduced all household, configuration, profile, and membership fields used by this slice.

Exit criterion: complete. CI passed, the transient first GHCR upload failure succeeded on its failed-job rerun, and the resulting multi-architecture digest was used by both the successful migration job and healthy Azure API revision. Netlify published the exact commit with the production API origin. The owner confirmed authenticated administration, child-profile creation, deactivation, and reactivation. The deployed self-deactivation contract remains covered by automated API/PostgreSQL tests; a separate direct authenticated staging request is still pending because no controllable signed-in browser session was available during evidence refresh.

### Increment 5: adult invitations

- **Implemented locally on 2026-08-15; deployment verification pending.** Adults can create, list, revoke, inspect, and accept seven-day invitations without an email-delivery vendor.
- Each invitation is bound to a required normalized Google email. Creation returns a cryptographically random 256-bit base64url token exactly once; PostgreSQL stores only its SHA-256 hash.
- Copyable links use `/invite#token=...`. The recipient page captures the fragment, immediately replaces browser history with `/invite`, and exchanges the token for the protected host-only `__Host-FamilyDashboard.PendingInvitation` cookie. The page declares `no-referrer`, stores nothing in browser storage, and never passes the secret through Google return URLs.
- Anonymous preparation requires `application/json` and the exact configured frontend `Origin`. Acceptance requires a valid application session and antiforgery token, compares the verified account email, and clears the pending cookie on success or terminal failure.
- Acceptance uses a serializable transaction to consume the invitation, create or reactivate the adult profile and membership, and select the household on the current session. Existing memberships are reused; memberships in other households remain unchanged; same-account concurrent retries are idempotent.
- Household-scoped create/list/revoke endpoints require active adult membership. Cross-household requests retain the not-found isolation boundary. Invitation tokens and hashes are excluded from list and inspection responses.
- No new runtime package or Azure resource is required. Existing ASP.NET Data Protection protects the pending cookie; Google login may request account selection while retaining identity scopes only.
- The shared adult wall-display session can still perform these administrative actions until Increment 6 introduces backend parent-PIN elevation. This residual risk must remain explicit.
- Local validation passes with 66 backend tests against PostgreSQL 18, 15 frontend component/API tests, and 14 Playwright cases across wall-display and phone projects.

Exit criterion: local automated validation is complete; final completion requires CI, the additive migration and digest-pinned Azure deployment, Netlify publication, and proof that a second real Google account can join staging once while wrong-account, revoked, expired, and replay attempts fail safely.

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
- session lifetime;
- `Invitations__Lifetime` (default seven days) and `Invitations__PendingCookieLifetime` (default 30 minutes), which are public backend policy values rather than secrets;
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

1. introduction of React Router during Increment 4 (approved and implemented);
2. generated database migration review;
3. real Google client configuration and staging secrets;
4. the exact parent-PIN hashing, rate-limit, elevation lifetime, and administrative-action policy;
5. any future separately provisioned household-device credential.
