# ADR 0001: Monorepo and Deployment Boundaries

Status: Accepted for the foundation milestone

## Context

The product needs one coordinated repository while keeping a static frontend and containerized backend independently deployable. The codebase has no demonstrated need for distributed services or complex monorepo orchestration.

## Decision

- Store frontend, backend, tests, documentation, and deployment definitions in one repository.
- Keep one React package and one ASP.NET Core API project initially.
- Deploy the static frontend through Netlify or any equivalent static host.
- Publish the backend image to GHCR and deploy it to a portable Kubernetes/K3s environment.
- Use PostgreSQL through EF Core migrations and provider interfaces for future external integrations.

## Consequences

Changes can be reviewed together while each deployable retains its own build boundary. A single backend process simplifies transactions and operations. Feature folders provide organization without committing to microservices or multi-project layering prematurely.

Netlify-specific behavior remains in `netlify.toml`. Kubernetes-specific behavior remains under `deploy`. Application business logic does not depend on either provider.
