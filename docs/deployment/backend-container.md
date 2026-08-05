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

If the GHCR package is private, K3s needs a narrowly scoped image-pull secret. That credential belongs in the cluster, not in manifests or frontend configuration.
