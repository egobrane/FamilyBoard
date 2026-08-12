# Backend Container and GHCR

The backend Dockerfile has development, build, and non-root production stages. Build locally with:

```sh
docker build \
  --file src/backend/FamilyDashboard.Api/Dockerfile \
  --target production \
  --tag family-dashboard-api:local .
```

`.github/workflows/publish-backend.yml` publishes AMD64 and ARM64 images on `main`, semantic-version tags, or manual dispatch. It authenticates to GHCR with the repository-scoped `GITHUB_TOKEN` and emits SHA/version tags, SBOM data, and provenance.

Production deployments should select an immutable digest or SHA tag. Do not deploy `latest` when reproducibility matters.

Azure staging accepts only the public `ghcr.io/egobrane/familyboard-backend` image pinned by SHA-256 digest. The protected manual workflow runs the migration job before updating the API and uses GitHub OIDC rather than an Azure client secret.

If the GHCR package is private, K3s needs a narrowly scoped image-pull secret. That credential belongs in the cluster, not in manifests or frontend configuration.
