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
- The site loads at its intended access level, its manifest and service worker load, SPA fallback works, and security/cache headers match `netlify.toml`.
- A pull request produces an isolated Deploy Preview. Until authenticated previews have an approved backend/origin strategy, they must not receive production credentials or broad credentialed CORS.

### Backend image and K3s

- `publish-backend.yml` publishes `linux/amd64` and `linux/arm64` images to GHCR with an immutable `sha-*` tag, SBOM, and provenance.
- The K3s overlay references that immutable tag or digest.
- A configured K3s context, staging DNS, TLS, exact CORS origin, image-pull access, PostgreSQL storage, runtime secrets, and Data Protection key storage exist.
- PostgreSQL becomes ready; the one-shot migration job succeeds; then the API rollout succeeds.
- Public HTTPS `/health/live` and `/health/ready` respond successfully.
- Backup and restore are tested before staging holds irreplaceable household data.

## Evidence record: 2026-08-11

Target commit: `65eed5c0c459d6deca1f31a1a87bc71c471b56c5` (`Updated headers with family information and picture additions`).

Verified:

- at the start of the 2026-08-11 review, the local checkout was clean and `main` matched `origin/main` for `egobrane/FamilyBoard` at the target commit;
- the repository is public and the Netlify production URL is `https://effortless-bubblegum-ad0643.netlify.app`;
- the public Netlify deployment returns HTTPS 200 with HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and the configured referrer policy;
- an unknown nested path returns the deployed `index.html`, proving the SPA fallback;
- the manifest, icons, service worker, and family-photo asset load; `manifest.webmanifest` has the standard manifest content type and `/sw.js` has the configured `no-cache` policy;
- the deployed bundle contains the current household heading, member names, chore text, and configured demo-photo reference;
- [Continuous Integration run 31128065580](https://github.com/egobrane/FamilyBoard/actions/runs/31128065580) completed successfully for the target commit: frontend, backend, and containers/manifests jobs all passed;
- [Publish Backend Image run 31128065586](https://github.com/egobrane/FamilyBoard/actions/runs/31128065586) completed successfully for the target commit;
- `ghcr.io/egobrane/familyboard-backend:sha-65eed5c` is publicly readable as an OCI image index with `linux/amd64`, `linux/arm64`, SBOM, and provenance attestations at digest `sha256:0e8e0098e12cbd6021a88b202d76357be92eca9680ffb667fb45e05f8c3d594b`;
- GitHub reports Actions and Packages operational on 2026-08-11; the 2026-08-06 Actions incident is resolved;
- Docker Compose started successfully, its migration container exited zero, PostgreSQL is healthy, the frontend returns HTTP 200, and both API health endpoints returned `Healthy`;
- the Docker-backed `family-dashboard-staging` K3s cluster restarted successfully with Traefik ingress and a bound 10 GiB `local-path` PostgreSQL volume;
- a fresh one-shot Entity Framework migration job completed and reported the database up to date; the API deployment is available and both ingress health endpoints returned `Healthy`.
- the validated Identity Increment 1 working-tree production image was tagged `family-dashboard-api:local-staging`, imported into k3d, and rolled out successfully; the replacement pod reports image ID `sha256:124885b53b78c7c6f3d3e210d3b3ab18caef242c89a1885c6ded82e8795ca159`, zero restarts, HTTP 200 live/readiness checks, and the intentionally unmapped `/api/auth/me` route returns HTTP 404;
- the project owner explicitly accepted retaining the demo family photo in the public repository and deployed frontend on 2026-08-11.

Not yet proven:

- a real Netlify pull-request Deploy Preview;
- independent confirmation from the Netlify dashboard that its deploy metadata names the target commit SHA; deployed content matches the target features;
- a public staging API hostname and remotely reachable Kubernetes environment;
- replacement of `dashboard.example.com`, `api.family-dashboard.example`, and `replace-with-immutable-tag` placeholders;
- trusted TLS, public health checks, production-grade secret delivery, durable Data Protection keys, or database backup and restore;
- a public HTTPS staging API; the deployed frontend still embeds the fallback `http://localhost:8080` API origin and must be rebuilt with an HTTPS API URL before real API calls are introduced;
- removal or explicit acceptance of the non-fatal missing `libgssapi_krb5.so.2` warning emitted by the migration image when PostgreSQL GSS authentication is not in use;

## Current blockers

1. Choose a public HTTPS staging API hostname or an explicitly approved secure tunnel before Netlify-to-API or Google OAuth testing.
2. Choose in-cluster PostgreSQL with off-cluster backups or an external PostgreSQL service before data becomes irreplaceable.
3. Configure Netlify's staging `VITE_API_BASE_URL` after the HTTPS API origin exists.
4. Verify a real pull-request Deploy Preview.
5. Supply deployment secrets only through Netlify, GitHub, and Kubernetes secret boundaries; never through repository files or chat.

Update this record when each item is independently verified. Do not relabel a frontend-only deployment as full-stack staging.
