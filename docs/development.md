# Local Development

## Container workflow

Prerequisites: Docker Engine or Docker Desktop with Compose v2.

1. Copy `.env.example` to `.env`.
2. Replace `POSTGRES_PASSWORD` with a local-only value.
3. Run `docker compose up --build`.

Compose starts PostgreSQL, applies committed migrations, starts the API with `dotnet watch`, and starts Vite with hot reload.

Useful commands:

```sh
docker compose up --build
docker compose down
docker compose logs -f api
docker compose run --rm migrate
docker compose --profile tools run --rm chore-generator
```

`docker compose down` preserves the named PostgreSQL volume. Adding `--volumes` deletes local database and dependency volumes and should only be used when that data is intentionally disposable.

## Host workflow

Prerequisites:

- Node.js 24 LTS and npm
- .NET SDK 10
- PostgreSQL 18

Set `ConnectionStrings__FamilyDashboard` in the shell or .NET user-secrets. Do not place it in tracked settings files.

Frontend:

```sh
cd src/frontend
cp .env.example .env.local
npm ci
npm run dev
```

Backend:

```sh
dotnet restore FamilyDashboard.sln
dotnet run --project src/backend/FamilyDashboard.Api
```

Apply migrations explicitly before using database-backed endpoints:

```sh
dotnet run --project src/backend/FamilyDashboard.Api -- --migrate
```

Generate all retry-safe recurring chore assignments within the configured horizon:

```sh
dotnet run --project src/backend/FamilyDashboard.Api -- --generate-chore-assignments
```

Local development does not run a background scheduler automatically. Invoke the one-shot command when testing schedules; Azure and K3s provide their own hourly schedulers around the same portable command.

## Local addresses

- Frontend: `http://localhost:5173`
- API liveness: `http://localhost:8080/health/live`
- API readiness: `http://localhost:8080/health/ready`
- Development OpenAPI document: `http://localhost:8080/openapi/v1.json`
