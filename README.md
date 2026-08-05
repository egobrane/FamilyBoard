# Family Dashboard

Family Dashboard is a touch-first household organization platform designed for wall displays, phones, tablets, and desktop browsers.

The first milestone provides a responsive PWA shell, an ASP.NET Core API with PostgreSQL persistence, automated tests, and portable deployment foundations. Calendar, task, chore, reward, and authentication workflows are intentionally not implemented yet.

## Repository

- `src/frontend` — React, TypeScript, Vite, and PWA application
- `src/backend/FamilyDashboard.Api` — ASP.NET Core API and EF Core persistence
- `tests` — backend integration and browser-level tests
- `deploy/k8s` — portable Kubernetes/K3s manifests
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
- [Netlify deployment](docs/deployment/netlify.md)
- [Backend container publication](docs/deployment/backend-container.md)
- [K3s deployment](docs/deployment/k3s.md)
- [Roadmap](ROADMAP.md)

## Current scope

Only health endpoints are public. Dashboard content is mock data. External services remain their own sources of truth and no Google credentials or data are stored.
