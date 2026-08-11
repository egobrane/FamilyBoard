# Family Dashboard Roadmap

## Now

- Foundation and delivery baseline verified: React/Vite PWA, ASP.NET Core API, PostgreSQL/EF Core migration, automated tests, Docker Compose, Netlify, current-commit CI/GHCR publication, and local K3s.
- Complete full-stack staging with a public HTTPS API, exact Netlify API configuration, durable secrets/Data Protection keys, and a tested PostgreSQL backup/restore path.
- Identity and Household Management Increment 1 is implemented: explicit API contracts, stable problem responses, and a deny-by-default backend household authorization seam.
- Review and implement Increment 2: identity and household persistence, atomic household bootstrap, member management, and PostgreSQL household-isolation tests.
- Validate the wall-display interaction model on physical hardware.

## Next

- Add identity and household persistence, atomic household bootstrap, member management, and household-isolation tests.
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
