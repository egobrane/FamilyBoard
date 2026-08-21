# Family Dashboard Roadmap

## Now

- Foundation and delivery baseline verified: React/Vite PWA, ASP.NET Core API, PostgreSQL/EF Core migration, automated tests, Docker Compose, Netlify, current-commit CI/GHCR publication, and local K3s.
- Full-stack staging is operational: the Netlify production bundle uses the public HTTPS Azure API, the API uses private PostgreSQL 18, and digest-pinned GitHub OIDC migration/deployment is proven.
- Azure Central US staging is provisioned: private PostgreSQL 18, scale-to-zero Container Apps API, successful migration job, healthy default HTTPS endpoint, monitoring, and narrowly scoped GitHub OIDC identity.
- `api.egobrane.net` is bound with Cloudflare DNS and Azure managed TLS; `family.egobrane.net` is rebuilt and verified with that HTTPS API origin.
- Identity and Household Management Increment 1 is implemented: explicit API contracts, stable problem responses, and a deny-by-default backend household authorization seam.
- Identity and Household Management Increment 2 is deployed: identity and household persistence, atomic household bootstrap, member management, last-active-adult protection, and PostgreSQL household-isolation tests.
- Identity Increment 3 is deployed and staging verified: backend Google sign-in, revocable database sessions, exact credentialed CORS, antiforgery protection, Azure-backed Data Protection, and session continuity across API revisions.
- Azure authentication security is provisioned with private Blob Storage, Key Vault, private endpoints, and a least-privilege runtime managed identity.
- The first PostgreSQL point-in-time restore drill succeeded against a separate temporary target without changing staging; the verifier and restored server were removed afterward.
- Identity Increment 4 is deployed and staging verified: authenticated frontend state, first-household bootstrap, per-session multi-household selection, dynamic account/household context, secure logout, and subsequent Google sign-in all work end to end.
- Increment 4B is deployed and staging verified: adults can edit household and regional settings, list members, create/edit child profiles, and deactivate/reactivate historical profiles. The deployed backend prevents self-deactivation and preserves the last active adult.
- Increment 5 is deployed and staging verified: adults can create, list, copy, and revoke seven-day email-bound invitation links; recipients can safely prepare the fragment token and atomically accept once after Google sign-in. Wrong-account, replay, and revocation behavior were exercised in staging; expiration is covered automatically but has not been manually waited out in staging.
- Increment 6 is deployed and staging verified: shared-display mode, six-digit parent PINs, household-scoped five-minute elevation, per-session cooldown, audit events, protected administration, recent-private-session recovery, and the responsive keypad work across the physical wall display, phone, touch, mouse, and keyboard.
- Google Calendar Increment 1 is deployed and staging verified: calendar authorization remains separate from sign-in, OAuth tokens are encrypted backend-only, adults select household-visible calendars, routine dashboard/calendar reads use request-time Google retrieval with a short disposable cache, and disconnect leaves Google events unchanged.
- Backend image publication is hardened so backend-relevant main changes still publish while frontend-only, documentation-only, and deployment-parameter-only changes avoid redundant GHCR images; version tags and manual dispatch remain supported.
- Google Calendar Increment 2 is deployed and staging verified: the additive migration, incremental write authorization, one adult-configured writable household calendar, idempotent create-only API behavior, shared-display member attribution boundary, and responsive event form are active. A timed event created in Family Dashboard appeared in Google Calendar on another device, confirming provider ownership.
- The staging API runs reviewed digest `sha256:028a47778229f74aae12725df0665f0a9042476169c6b69b7ec60ac35e40d318` on healthy revision `family-dashboard-staging-api--0000019` with both Calendar feature flags enabled. The Netlify production bundle contains the matching Increment 2 UI.
- Azure release reconciliation is implemented pending CI/staging proof: the protected workflow reads the immutable image and an explicit non-secret runtime allowlist from reviewed Bicep parameters, migrates first, verifies image/configuration/traffic/health, and restores the prior API release after a failed deployment without accessing the PostgreSQL administrator password.
- PWA freshness hardening is implemented pending Netlify/Safari proof: update discovery occurs after React mounts, checks repeat on focus/visibility and long-running displays, forms block activation, safe idle displays may update, and Netlify separates revalidated shell assets from immutable hashed assets.
- `Staging Selection Test Household` remains intentionally stored until household deletion or an approved cleanup workflow exists.

## Next

- Rehearse application rollback and record recovery objectives before staging stores irreplaceable household data; revoke shared sessions before any rollback to pre-parent-access code.
- Verify a real Netlify Deploy Preview without sharing production credentials or broadening credentialed CORS.
- Prove the reconciled GitHub deployment workflow against Azure staging and confirm its job summary reports the reviewed digest, ready revision, configuration, traffic, and health without exposing secrets.
- Prove a two-version PWA update on Netlify/Safari and the wall display, including prompt visibility, form protection, safe idle activation, offline retention, and the new cache headers.
- Complete the remaining owner staging checks for locked shared-display attribution, private-session attribution, all-day creation, invalid ranges/time zones, idempotent recovery, read-only target rejection, external revocation, multi-household isolation, parent-PIN configuration gates, responsive devices, and write-path leakage inspection. Automated coverage exists for the core authorization, attribution, idempotency, and accessibility boundaries but does not replace these live checks.
- Plan the first chore-management vertical slice before extending Calendar creation into editing and deletion.
- Record Google OAuth consent verification status before broad public use and retain manual revocation, recurrence, daylight-saving, shared-display, multi-household, and responsive checks in the release checklist.

## Later

- Add Google Tasks through a provider interface.
- Define chore recurrence and generate individual assignments.
- Implement chore completion review and the append-only point ledger.
- Implement reward configuration and redemption approval.
- Add notifications and deliberate offline-data behavior.

## Someday

- Grocery lists and meal planning.
- Family messaging and photo displays.
- Package, weather, and school information.
- Home Assistant and household-maintenance modules.
- Family budgeting and screen-time rewards.
- Native Android and iOS applications using the same backend API.
