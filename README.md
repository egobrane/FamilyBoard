# Family Dashboard

Family Dashboard is a touch-first household organization platform designed for wall displays, phones, tablets, and desktop browsers.

The first milestone provides a responsive PWA shell, an ASP.NET Core API with PostgreSQL persistence, automated tests, and portable deployment foundations. Identity and household increments now add persistent households, backend Google sign-in, revocable application sessions, authenticated onboarding, per-session household selection, and a locally implemented household-settings and member-administration UI; calendar, task, chore, reward, adult-invitation, and parent-PIN workflows remain intentionally deferred.

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
- [Identity and household milestone](docs/identity-household-milestone.md)
- [Netlify deployment](docs/deployment/netlify.md)
- [Backend container publication](docs/deployment/backend-container.md)
- [K3s deployment](docs/deployment/k3s.md)
- [Local K3s staging](docs/deployment/local-k3s.md)
- [Staging deployment proof](docs/deployment/staging.md)
- [Azure staging deployment](docs/deployment/azure.md)
- [Roadmap](ROADMAP.md)

## Current scope

Only health and Google-login initiation endpoints are anonymously usable. Dashboard feature cards still use mock data, while account identity, household setup, selected-household context, and the household heading come from the authenticated API. Identity and Household Management Increments 1–4 are deployed and verified in staging: Netlify serves `https://family.egobrane.net`, its production bundle targets `https://api.egobrane.net`, and Azure uses private PostgreSQL 18 plus digest-pinned GitHub OIDC deployment and migration workflows. Real Google sign-in, first-household bootstrap, logout/revocation, subsequent sign-in, multi-household switching, and selection persistence have been exercised successfully. Increment 4B is implemented and validated locally with household/regional settings, member lists, child-profile administration, historical-profile deactivation, and backend self/last-adult protections; it is not staging-deployed until its CI image, Azure API, and Netlify build are published and verified. Parent-PIN elevation remains a required future backend boundary. No Google provider tokens are stored.
