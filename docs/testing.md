# Testing

## Frontend

```sh
cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

Vitest and React Testing Library cover the dashboard shell, semantic landmarks, mock content, and keyboard skip navigation.

## Browser tests

```sh
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

Playwright runs Chromium at a 1920×1080 touch-enabled wall-display viewport and a Pixel phone viewport. It checks layout overflow, visible primary content, and serious automated accessibility findings. Emulation supplements but does not replace testing on the physical wall touchscreen, iOS Safari, and Android Chrome.

## Backend

```sh
dotnet test FamilyDashboard.sln
```

The liveness test runs without PostgreSQL. The migration test runs when `TEST_POSTGRES_CONNECTION_STRING` points to a dedicated disposable test database. It deletes and recreates that database, so it must never target shared or production data.

CI supplies an ephemeral PostgreSQL service, then restores, builds, tests, builds production containers, and renders the K3s manifests. Builds or required tests must be green before a milestone is considered complete.
