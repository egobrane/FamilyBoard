# Staging Deployment Proof

Staging is proven only when one identifiable commit passes CI, produces immutable deployable artifacts, and is verified through the deployed frontend and API. A successful Netlify frontend build is an important part of this proof, but it does not prove the backend, database, migration, or cross-origin configuration.

## Required evidence

### GitHub and CI

- The GitHub commit SHA matches the intended release.
- Frontend lint, component tests, PWA build, Playwright tests, backend build, backend tests, PostgreSQL migration test, production container builds, and K3s rendering pass.
- Branch protection requires the relevant CI checks before merge.

### Frontend on Netlify

- Netlify reports the same commit SHA.
- The build uses the root `netlify.toml` and publishes `src/frontend/dist`.
- `VITE_API_BASE_URL` is the HTTPS staging API origin, not localhost.
- The site loads through its privacy gate, its manifest and service worker load, SPA fallback works, and security/cache headers match `netlify.toml`.
- A pull request produces an isolated Deploy Preview. Until authenticated previews have an approved backend/origin strategy, they must not receive production credentials or broad credentialed CORS.

### Backend image and K3s

- `publish-backend.yml` publishes `linux/amd64` and `linux/arm64` images to GHCR with an immutable `sha-*` tag, SBOM, and provenance.
- The K3s overlay references that immutable tag or digest.
- A configured K3s context, staging DNS, TLS, exact CORS origin, image-pull access, PostgreSQL storage, runtime secrets, and Data Protection key storage exist.
- PostgreSQL becomes ready; the one-shot migration job succeeds; then the API rollout succeeds.
- Public HTTPS `/health/live` and `/health/ready` respond successfully.
- Backup and restore are tested before staging holds irreplaceable household data.

## Evidence record: 2026-08-06

Target commit: `edbc32466b907975a9272e0951d32bfa993c8f3c` (`Added functionality for point and click support`).

Verified:

- local checkout and GitHub repository identify `egobrane/FamilyBoard` on `main`;
- the repository is public and the Netlify production URL is `https://effortless-bubblegum-ad0643.netlify.app`;
- the public Netlify deployment returns HTTPS 200 with HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and the configured referrer policy;
- an unknown nested path returns the deployed `index.html`, proving the SPA fallback;
- the manifest, icons, and service worker load; `/sw.js` has the configured `no-cache` policy;
- the deployed asset hashes match the locally validated point-and-click build, and the bundle contains the Calendar, Chores, and Rewards navigation anchors;
- local Compose, PostgreSQL migration, live/readiness endpoints, frontend build/tests, production images, and K3s render passed during the foundation milestone;
- a Docker-backed K3s v1.35.5 cluster named `family-dashboard-staging` is running locally through k3d on macOS, with Traefik ingress and the default `local-path` storage class;
- the production API image was built as `family-dashboard-api:local-staging`, imported into k3d, and deployed through the local Kustomize overlay;
- the PostgreSQL persistent volume bound successfully, the one-shot Entity Framework migration job completed, and the API deployment rolled out successfully;
- both `/health/live` and the PostgreSQL-backed `/health/ready` returned `Healthy` through `http://api.family-dashboard.localhost:8081`;
- GitHub Actions created [Continuous Integration run 19](https://github.com/egobrane/FamilyBoard/actions/runs/31125345009) and [Publish Backend Image run 3](https://github.com/egobrane/FamilyBoard/actions/runs/31125344993) for the target commit;
- GitHub's official status API reports an active critical Actions incident. GitHub states that hosted-runner jobs may remain queued for an extended period or time out; the queued result is therefore an external service interruption, not a repository test conclusion. Track the [GitHub status incident](https://stspg.io/rcz3fcm83sff).

Not yet proven:

- a real Netlify pull-request Deploy Preview;
- completion of the target commit's queued GitHub workflows after the active GitHub Actions incident is resolved;
- a readable GHCR image version for the target commit;
- a public staging API hostname and remotely reachable Kubernetes environment;
- replacement of `dashboard.example.com`, `api.family-dashboard.example`, and `replace-with-immutable-tag` placeholders;
- trusted TLS, public health checks, production-grade secret delivery, durable Data Protection keys, or database backup and restore;
- a public HTTPS staging API; the deployed frontend currently embeds the fallback `http://localhost:8080` API origin and must be rebuilt with an HTTPS API URL before real API calls are introduced.
- redeployment of the working-tree Netlify configuration that gives `manifest.webmanifest` its standard manifest content type; the current deployment serves it as `application/octet-stream`.

## Current blockers

1. Choose a public HTTPS staging API hostname or an explicitly approved secure tunnel before Netlify-to-API or Google OAuth testing.
2. Choose in-cluster PostgreSQL with off-cluster backups or an external PostgreSQL service before data becomes irreplaceable.
3. Configure Netlify's staging `VITE_API_BASE_URL` after the HTTPS API origin exists.
4. Verify a real pull-request Deploy Preview.
5. Supply deployment secrets only through Netlify, GitHub, and Kubernetes secret boundaries; never through repository files or chat.

Update this record when each item is independently verified. Do not relabel a frontend-only deployment as full-stack staging.
