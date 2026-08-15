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
- Increment 5 is implemented locally: adults can create, list, copy, and revoke seven-day email-bound invitation links; recipients can inspect and atomically accept a single-use invitation after Google sign-in. CI and staging verification remain pending.
- `Staging Selection Test Household` remains intentionally stored until household deletion or an approved cleanup workflow exists.
- Validate the wall-display interaction model on physical hardware.

## Next

- Rehearse application rollback and record recovery objectives before staging stores irreplaceable household data.
- Verify a real Netlify Deploy Preview without sharing production credentials or broadening credentialed CORS.
- Publish Increment 5, run its additive migration, and verify a real two-Google-account invitation, wrong-account rejection, revocation, replay rejection, and household switching in staging.
- Add shared-display mode with a backend-enforced parent access PIN for administrative actions.
- Add a read-only Google Calendar adapter and disposable cache strategy.
- Validate routine child-accessible actions and parent-only administration on the physical wall display.

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
