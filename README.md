# Family Dashboard

Family Dashboard is a touch-first household organization platform designed for wall displays, phones, tablets, and desktop browsers.

The first milestone provides a responsive PWA shell, an ASP.NET Core API with PostgreSQL persistence, automated tests, and portable deployment foundations. Identity and household increments now add persistent households, backend Google sign-in, revocable application sessions, authenticated onboarding, per-session household selection, household administration, email-bound adult invitation links, and shared-display parent-PIN elevation. Google Calendar Increment 1 adds separately authorized reads and a disposable request cache. Increment 2 adds feature-gated, idempotent creation to one deliberately selected writable Google calendar. Increment 3 adds feature-gated editing and explicit deletion only for simple, non-recurring events originally created by Family Dashboard. Chore Management Increments 1–3 add reusable definitions, one-time and recurring assignments, attributed completion, adult review, retry-safe automatic generation, and an append-only household point ledger. Reward Management Increment 1 is deployed and staging verified with household reward definitions, attributed requests, balance-safe point reservations, adult review, fulfillment, cancellation, and retained history. Google Tasks Increments 1–2 are deployed and staging verified with separately authorized provider access, household task-list selection, one deliberately selected writable list, top-level creation, completion/reopening, explicit shared-display attribution, protected provider versions, and append-only mutation receipts without copying Google-owned task content.

Backend-relevant `main` changes publish one immutable GHCR image and pass its exact digest directly to the reusable, protected Azure staging workflow. Bicep parameters continue to own approved non-secret runtime settings without storing a per-release digest. The workflow requires matching CI, migrates first, and verifies image, configuration, traffic, and health. The matching Netlify PWA includes guarded service-worker updates so an in-progress form is not interrupted.

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
- [Google Tasks integration](docs/google-tasks.md)
- [Workspace navigation and gesture behavior](docs/workspace-navigation.md)
- [Dashboard personalization](docs/dashboard-personalization.md)
- [Household weather](docs/weather.md)
- [Chore management](docs/chore-management.md)
- [Reward management](docs/reward-management.md)
- [Identity and household milestone](docs/identity-household-milestone.md)
- [Netlify deployment](docs/deployment/netlify.md)
- [Backend container publication](docs/deployment/backend-container.md)
- [K3s deployment](docs/deployment/k3s.md)
- [Local K3s staging](docs/deployment/local-k3s.md)
- [Staging deployment proof](docs/deployment/staging.md)
- [Azure staging deployment](docs/deployment/azure.md)
- [Application recovery](docs/deployment/recovery.md)
- [Roadmap](ROADMAP.md)

## Current scope

Health, Google-login initiation, and invitation prepare/inspection endpoints are anonymously usable; invitation preparation additionally requires JSON from the exact configured frontend origin. Account identity, household setup, selected-household context, household heading, configured calendar events, chores, points, rewards, and selected Google task lists come from the authenticated API. Identity and Household Management Increments 1–6, Google Calendar Increments 1–3, Chore Management Increments 1–3, Reward Management Increment 1, and Google Tasks Increments 1–2 are deployed and staging verified with the matching Netlify frontend. General Tasks editing, deletion, movement, ordering, synchronization, reminders, and recurrence remain deferred. OAuth codes and tokens, PINs, PIN hashes, and peppers never reach the frontend.

The primary Home, Calendar, Tasks, Chores, and Rewards experience now runs as one cohesive routed workspace with a persistent household shell, pointer and touch gestures, browser-history-safe URLs, reduced-motion support, matching feature surfaces, and keyboard-visible control focus. Dashboard Personalization and Weather Increment 1 adds household-specific greetings, authenticated private photo variants and focal positioning, approximate weather location, NWS current conditions, and an accessible forecast dialog. Staging activation remains to be owner-verified after this release is reviewed.
