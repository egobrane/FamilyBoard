# Backend Container and GHCR

The backend Dockerfile has development, build, and non-root production stages. Build locally with:

```sh
docker build \
  --file src/backend/FamilyDashboard.Api/Dockerfile \
  --target production \
  --tag family-dashboard-api:local .
```

`.github/workflows/publish-backend.yml` publishes AMD64 and ARM64 images on relevant `main` changes, semantic-version tags, or manual dispatch. It authenticates to GHCR with the repository-scoped `GITHUB_TOKEN` and emits SHA/version tags, SBOM data, and provenance.

Main-branch publication is intentionally limited to backend application files, backend tests, the solution and shared .NET dependency/build inputs, the backend Docker context controls, and the publication workflow itself. Frontend-only, documentation-only, Netlify-only, and deployment-parameter-only changes continue through normal CI but do not create a redundant backend image. Release tags and `workflow_dispatch` remain explicit publication paths regardless of ordinary change classification. When adding a new shared build input, update this allowlist in the same change; incorrectly omitting a backend-relevant input could otherwise leave GHCR stale even though CI is green.

Production deployments should select an immutable digest or SHA tag. Do not deploy `latest` when reproducibility matters.

Azure staging accepts only the public `ghcr.io/egobrane/familyboard-backend` image pinned by SHA-256 digest. On a backend-relevant `main` push, buildx exposes the exact published digest as a job output and passes it directly to the reusable protected Azure workflow. `staging.bicepparam` reads the transient `FAMILY_DASHBOARD_BACKEND_IMAGE` environment value only for Bicep compilation; no per-release digest is committed. The workflow requires successful CI for the current `main` SHA, runs the migration job, reconciles the API and provisioned chore-generator job, verifies the ready revision and traffic, and uses GitHub OIDC rather than an Azure client secret. It never performs a resource-group Bicep deployment or receives the real PostgreSQL administrator password.

If the GHCR package is private, K3s needs a narrowly scoped image-pull secret. That credential belongs in the cluster, not in manifests or frontend configuration.
