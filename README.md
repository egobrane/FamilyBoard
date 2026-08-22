# Family Dashboard

Family Dashboard is a touch-first household organization platform designed for wall displays, phones, tablets, and desktop browsers.

The first milestone provides a responsive PWA shell, an ASP.NET Core API with PostgreSQL persistence, automated tests, and portable deployment foundations. Identity and household increments now add persistent households, backend Google sign-in, revocable application sessions, authenticated onboarding, per-session household selection, household administration, email-bound adult invitation links, and shared-display parent-PIN elevation. Google Calendar Increment 1 adds separately authorized reads and a disposable request cache. Increment 2 adds feature-gated, idempotent creation to one deliberately selected writable Google calendar; editing and deletion remain deferred. Chore Management Increment 1 adds the first product-owned household workflow: reusable definitions, one-time assignments, attributed completion, and adult review.

Azure staging releases now read the reviewed immutable image and approved non-secret runtime settings from `deploy/azure/staging.bicepparam`. The protected GitHub workflow requires no manually entered digest, runs the migration job before updating the API, and verifies the resulting image, configuration, traffic, and health. The matching Netlify PWA includes guarded service-worker updates so an in-progress form is not interrupted.

## Repository

- `src/frontend` — React, TypeScript, Vite, and PWA application
- `src/backend/FamilyDashboard.Api` — ASP.NET Core API and EF Core persistence
- `tests` — backend integration and browser-level tests
- `deploy/k8s` — portable Kubernetes/K3s manifests
- `deploy/azure` — scoped Bicep for Azure staging
- `docs` — architecture, development, security, and deployment guidance

## Quick start with Docker Compose

1. Copy `.env.example` to `.env` and replace the development PostgreSQL password.
2. Run `docker compose up --build`.
3. Open `http://localhost:5173`.
4. Check API liveness at `http://localhost:8080/health/live` and readiness at `http://localhost:8080/health/ready`.

The migration service runs once before the development API starts. See [development documentation](docs/development.md) for host-based setup and common commands.

## Documentation

- [Architecture](docs/architecture.md)
- [Local development](docs/development.md)
- [Configuration](docs/configuration.md)
- [Database and migrations](docs/database.md)
- [Testing](docs/testing.md)
- [Authentication boundary](docs/authentication.md)
- [Google Calendar integration](docs/google-calendar.md)
- [Chore management](docs/chore-management.md)
- [Identity and household milestone](docs/identity-household-milestone.md)
- [Netlify deployment](docs/deployment/netlify.md)
- [Backend container publication](docs/deployment/backend-container.md)
- [K3s deployment](docs/deployment/k3s.md)
- [Local K3s staging](docs/deployment/local-k3s.md)
- [Staging deployment proof](docs/deployment/staging.md)
- [Azure staging deployment](docs/deployment/azure.md)
- [Roadmap](ROADMAP.md)

## Current scope

Health, Google-login initiation, and invitation prepare/inspection endpoints are anonymously usable; invitation preparation additionally requires JSON from the exact configured frontend origin. Account identity, household setup, selected-household context, household heading, configured calendar events, and chore data come from the authenticated API. Identity and Household Management Increments 1–6 and Google Calendar Increments 1–2 are deployed and verified in Azure staging with the matching Netlify frontend. Chore Management Increment 1 is implemented and locally verified, but remains pending CI and staging deployment. Calendar authorization remains separate from sign-in, and controlled event creation targets one deliberately selected writable Google calendar. Google remains the event source of truth; Calendar editing/deletion, Tasks, chore recurrence, points, rewards, weather, and photo storage remain deferred. OAuth codes and tokens, PINs, PIN hashes, and peppers never reach the frontend.
