# ADR 0003: Azure staging hosting

Status: accepted, implementation blocked on Azure provider and regional capacity prerequisites

## Context

The Netlify frontend needs a public HTTPS backend. The owner has Azure credits and approved Azure Container Apps Consumption, PostgreSQL Flexible Server 18, `api.egobrane.net`, and the existing shared `ryan-dev` resource group. East US was initially preferred.

## Decision

- Keep the frontend on portable static hosting at Netlify.
- Deploy the complete staging stack to Central US. The owner approved this change after Azure reported PostgreSQL 18 provisioning restricted in East US and confirmed Central US support for PostgreSQL 18 on `Standard_B1ms`.
- Run the backend as a scale-to-zero Azure Container App using the existing public GHCR image.
- Run EF Core migrations as a manually triggered Container Apps Job using the same immutable image.
- Use a private-networked PostgreSQL Flexible Server with public access disabled.
- Define the environment in modular, resource-group-scoped Bicep and deploy incrementally.
- Use a GitHub OIDC user-assigned identity scoped to the API and migration job.
- Use Azure-managed TLS after CNAME/TXT validation.
- Keep Container Apps, PostgreSQL, and Netlify behavior in deployment configuration rather than application business logic.

## Consequences

The shared resource group remains untouched outside clearly prefixed resources. A custom-VNet Container Apps environment causes Azure to create a separate managed infrastructure resource group. PostgreSQL is the principal fixed staging cost; scale-to-zero reduces API compute cost but adds cold starts. Private database access improves security but prevents direct developer access without a controlled network path. Password authentication remains an interim secret-management compromise; managed identity database access would require an approved application change.

The first live Azure check found unregistered resource providers and PostgreSQL provisioning restricted in East US. The providers were subsequently registered, and the owner explicitly approved Central US on 2026-08-12 rather than reducing the approved PostgreSQL version.
