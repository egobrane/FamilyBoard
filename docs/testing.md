# Testing

## Frontend

```sh
cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

Vitest and React Testing Library cover authentication states, first-household setup, multi-household selection, invitation creation and fragment removal, shared-display PIN entry, credentialed/antiforgery API behavior, Calendar events/source selection, dashboard semantics, mock feature content, and keyboard and pointer navigation.

## Browser tests

```sh
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

Playwright runs Chromium at a 1920×1080 touch-enabled wall-display viewport and a Pixel phone viewport. It checks layout overflow, visible authenticated content, mouse/keyboard navigation, read-only Calendar navigation and source selection, secure logout, invitation creation/acceptance, raw-fragment removal, shared-display locked administration and PIN unlock, and serious automated accessibility findings using deterministic API fixtures. Emulation supplements but does not replace testing on the physical wall touchscreen, iOS Safari, and Android Chrome.

## Backend

```sh
dotnet test FamilyDashboard.sln
```

The liveness, return-URL, Google option, Calendar token/state protection, invitation-token, parent-PIN hashing, CORS, and production fail-closed authentication tests can run without PostgreSQL. Migration, endpoint, Calendar constraints/household isolation, atomic-bootstrap-and-selection, per-session household selection and elevation, concurrent last-adult, invitation isolation/revocation/email binding/concurrent acceptance, PIN cooldown/audit/administration, Google identity mapping, session renewal/revocation/expiration, disabled-account, and antiforgery tests run when `TEST_POSTGRES_CONNECTION_STRING` points to a dedicated disposable test database. These tests delete and recreate that database, so it must never target shared or production data.

CI supplies an ephemeral PostgreSQL service, then restores, builds, tests, builds production containers, and renders the K3s manifests. Builds or required tests must be green before a milestone is considered complete.
