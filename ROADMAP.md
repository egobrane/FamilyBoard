# Family Dashboard Roadmap

## Now

- Foundation milestone complete: React/Vite PWA, ASP.NET Core API, PostgreSQL model, EF Core migration, local containers, automated tests, and portable deployment configuration.
- Prove one full staging release across GitHub Actions, Netlify, GHCR, K3s, PostgreSQL migration, and public API health checks.
- Begin the accepted identity and household-access vertical slice incrementally.
- Validate the wall-display interaction model on physical hardware.

## Next

- Implement authentication identities separately from household-member profiles.
- Add secure server-side Google sign-in, revocable application sessions, and CSRF protection.
- Add household bootstrap, member management, adult invitations, and backend authorization.
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
