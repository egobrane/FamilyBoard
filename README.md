# Family Dashboard

Family Dashboard is a touch-first household organization platform designed for wall displays, phones, tablets, and desktop browsers.

The first milestone provides a responsive PWA shell, an ASP.NET Core API with PostgreSQL persistence, automated tests, and portable deployment foundations. Identity and household increments now add persistent households, backend Google sign-in, revocable application sessions, authenticated onboarding, per-session household selection, household administration, and email-bound adult invitation links; calendar, task, chore, reward, and parent-PIN workflows remain intentionally deferred.

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

Health, Google-login initiation, and invitation prepare/inspection endpoints are anonymously usable; invitation preparation additionally requires JSON from the exact configured frontend origin. Dashboard feature cards still use mock data, while account identity, household setup, selected-household context, and the household heading come from the authenticated API. Identity and Household Management Increments 1–4B are deployed and verified in staging. Increment 5 is implemented locally and awaiting CI, migration, deployment, and a two-account staging acceptance test. Netlify serves `https://family.egobrane.net`, its production bundle targets `https://api.egobrane.net`, and Azure uses private PostgreSQL 18 plus digest-pinned GitHub OIDC deployment and migration workflows. Parent-PIN elevation remains the next required backend boundary. No Google provider tokens are stored.
