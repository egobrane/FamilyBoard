# Family Dashboard Roadmap

## Now

- Foundation and delivery baseline verified: React/Vite PWA, ASP.NET Core API, PostgreSQL/EF Core migration, automated tests, Docker Compose, Netlify, current-commit CI/GHCR publication, and local K3s.
- Full-stack staging is operational: the Netlify production bundle uses the public HTTPS Azure API, the API uses private PostgreSQL 18, and digest-pinned GitHub OIDC migration/deployment is proven.
- Azure Central US staging is provisioned: private PostgreSQL 18, scale-to-zero Container Apps API, successful migration job, healthy default HTTPS endpoint, monitoring, and narrowly scoped GitHub OIDC identity.
- `api.egobrane.net` is bound with Cloudflare DNS and Azure managed TLS; `family.egobrane.net` is rebuilt and verified with that HTTPS API origin.
- Identity and Household Management Increment 1 is implemented: explicit API contracts, stable problem responses, and a deny-by-default backend household authorization seam.
- Refresh staging operations with a PostgreSQL restore drill, a real Netlify Deploy Preview, and durable Data Protection key storage before browser sessions are introduced.
- Review and deploy the locally completed Increment 2: identity and household persistence, atomic household bootstrap, member management, and PostgreSQL household-isolation tests.
- Validate the wall-display interaction model on physical hardware.

## Next

- Rehearse application rollback and PostgreSQL point-in-time restore before staging stores irreplaceable household data.
- Decide when authentication requires durable ASP.NET Data Protection storage and a stronger database secret lifecycle.
- Verify Increment 2 migration and protected endpoint behavior through the staging deployment pipeline.
- Add secure server-side Google sign-in, revocable application sessions, exact credentialed CORS, and CSRF protection.
- Add authenticated onboarding, household selection/settings, member administration, and copyable adult invitations.
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
