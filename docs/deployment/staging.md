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
- real Google sign-in and durable session continuity across a deployed API revision;
- removal or explicit acceptance of the non-fatal missing `libgssapi_krb5.so.2` warning emitted by the migration image when PostgreSQL GSS authentication is not in use;

## Current blockers

1. Create and configure the external Google web OAuth client without exposing its secret.
2. Publish and deploy the Increment 3 image, run its migration, and prove real sign-in plus session continuity across a revision.
3. Verify a real Netlify pull-request Deploy Preview.

Update this record when each item is independently verified. Do not relabel a frontend-only deployment as full-stack staging.

## Azure preparation evidence: 2026-08-11

- Azure CLI `2.89.0` is authenticated to the approved `PAYG-Sponsorship` subscription and the signed-in user is Owner on the existing `ryan-dev` resource group.
- On 2026-08-12, both required resource providers were registered. The owner explicitly approved Central US after Azure continued to restrict PostgreSQL 18 in East US and Central US advertised PostgreSQL 18 with `Standard_B1ms`.
- Bicep CLI `0.46.1` is installed; `main.bicep` and `staging.bicepparam` compile without diagnostics.
- Azure server-side deployment validation succeeded.
- Incremental Azure `what-if` succeeded with 13 creations, 114 existing resources ignored, and no modifications or deletions. Every planned resource is prefixed `family-dashboard-staging` except child resources beneath those prefixed parents.
- No Azure application resources had been provisioned at the time of this preparation check; provisioning evidence follows below.
- Frontend lint, four component tests, production PWA build, six wall-display/phone Playwright tests, backend Release build, all 17 backend tests including PostgreSQL 18 migration, Docker Compose validation, and both K3s renders passed.
- The existing Compose PostgreSQL service is healthy; the existing local K3s PostgreSQL/API pods are ready, its migration job is complete, its PVC is bound, and local live/readiness endpoints return `Healthy`.

## Azure provisioning evidence: 2026-08-12

- The owner explicitly approved Central US after Azure continued to restrict PostgreSQL 18 in East US.
- Final Central US Bicep compilation, server-side validation, and incremental `what-if` succeeded. The preview contained only the 13 planned creations and no shared-resource modification or deletion.
- Deployment `family-dashboard-staging-bootstrap` succeeded in the existing `ryan-dev` resource group.
- PostgreSQL Flexible Server is ready on version 18 with `Standard_B1ms`, 32 GiB storage, seven-day backup retention, no HA/geo backup, and public network access disabled.
- The migration execution `family-dashboard-staging-mig-iqo0fom` succeeded.
- The scale-to-zero API runs immutable image digest `sha256:ddcae5448e13448b70ff9d81fd1f3dcf5e2ca832fa5de69c3b71a3c304558b56`; its default Azure HTTPS liveness and readiness endpoints return HTTP 200 `Healthy`.
- A request from origin `https://family.egobrane.net` receives that exact `Access-Control-Allow-Origin` value; insecure ingress is disabled.
- The GitHub OIDC identity has Container Apps Contributor only on `family-dashboard-staging-api` and Container Apps Jobs Contributor only on `family-dashboard-staging-mig`.
- Azure created managed infrastructure group `ME_family-dashboard-staging-env_ryan-dev_centralus`; it must not be managed or deleted directly.
- The database administrator credential is stored in the project owner's macOS Keychain and was not written to source control or command output.
- Cloudflare CNAME/TXT validation records resolve publicly. Azure managed TLS is active for `api.egobrane.net`; its liveness/readiness checks return HTTP 200, the certificate SAN matches the hostname, and insecure ingress is disabled.
- Azure requires the validated hostname to be attached to the Container App before managed-certificate creation. The documented bootstrap sequence now includes this platform requirement.
- After the production variable was entered, Netlify initially skipped a content-unchanged build. The owner then forced a cleared-cache production rebuild, and the deployed bundle was independently verified to contain `https://api.egobrane.net` with no localhost API origin.
- The first manual Azure deploy run proved the image-digest guard but failed OIDC login because GitHub emitted its immutable owner/repository-ID subject while Azure trusted the legacy name-only form. The Bicep trust definition now uses the exact immutable subject reported by GitHub.
- Azure's live federated credential was updated to that immutable subject on 2026-08-12.
- The second attempt authenticated with OIDC but exposed that Azure's Container Apps Contributor role contains no `Microsoft.App/jobs` permissions.
- A validation execution then proved that Azure's start-time `--image` override replaces the job's container template and drops the `--migrate` arguments/environment. That execution launched the API instead of migrating and was stopped without a schema or API deployment change.
- The corrected design uses job-scoped Container Apps Jobs Contributor, the narrowest suitable built-in role for updating the existing migration job image while preserving its complete template, then starts the job normally.
- Azure's live assignments now match Bicep: Container Apps Contributor only on the API and Container Apps Jobs Contributor only on the migration job. A local validation updated the migration job to digest `sha256:111d3f3d7a80b2a4ab39b0e3a30967ef3b471869ef86d4468dc8b08193ffbb6b`, preserved `--migrate` and its secret-backed environment, and completed execution `family-dashboard-staging-mig-qwe3ot3` successfully.
- The third GitHub workflow attempt authenticated through OIDC, completed migration execution `family-dashboard-staging-mig-atvyjwk`, deployed digest `sha256:111d3f3d7a80b2a4ab39b0e3a30967ef3b471869ef86d4468dc8b08193ffbb6b` to the API, and passed both public health checks. Live Azure inspection independently confirmed the digest, successful migration, ready revision, and HTTP 200 liveness/readiness responses.

## Full-stack staging verification: 2026-08-12

Target commit: `7a4798c88b62f8aa838102c05a80bb1684292c3e` (`Testing complete GitHub to Azure deployment path for backend updates`).

- Before these evidence-only documentation updates, local `main` was clean and matched `origin/main` at the target commit.
- [Continuous Integration run 31627987919](https://github.com/egobrane/FamilyBoard/actions/runs/31627987919) and [Publish Backend Image run 31627987813](https://github.com/egobrane/FamilyBoard/actions/runs/31627987813) both completed successfully for the target commit.
- [Azure staging deployment run 31615282299](https://github.com/egobrane/FamilyBoard/actions/runs/31615282299) completed successfully on its third attempt. It used the protected `staging` environment and deployed the immutable backend digest after a successful migration.
- `https://family.egobrane.net` returns HTTPS 200 with the configured security headers and serves the rebuilt production bundle. The compiled bundle contains `https://api.egobrane.net` and no `http://localhost:8080` API origin.
- The in-app browser control surface was unavailable during this verification, so the frontend check covered public HTTP responses and compiled assets rather than a new visual interaction pass.
- Azure reports `family-dashboard-staging-api` provisioned and running with insecure ingress disabled, `minReplicas` zero, the custom domain attached, and digest `sha256:111d3f3d7a80b2a4ab39b0e3a30967ef3b471869ef86d4468dc8b08193ffbb6b` active on the latest ready revision.
- The latest migration execution, `family-dashboard-staging-mig-atvyjwk`, succeeded. The earlier deliberately stopped validation execution remains visible in history and is not an active failure.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 with public access disabled, seven-day backup retention, and the UTF-8 `family_dashboard` database present.
- Public API liveness and database-backed readiness both return HTTP 200 `Healthy` at `https://api.egobrane.net`.

This proves the first end-to-end staging delivery path. Netlify deploy metadata for the exact commit, a Deploy Preview, and a PostgreSQL restore remain separate operational checks.

## Identity Increment 2 staging verification: 2026-08-13

Target commit: `54e2398649c68202e015a042b76a366d7201b7dd` (`Added functionality for household management, including members.`).

- The local `main` branch was clean and matched `origin/main` at the target commit.
- [Continuous Integration run 31636538321](https://github.com/egobrane/FamilyBoard/actions/runs/31636538321), [Publish Backend Image run 31636538357](https://github.com/egobrane/FamilyBoard/actions/runs/31636538357), and [Azure staging deployment run 31636925148](https://github.com/egobrane/FamilyBoard/actions/runs/31636925148) completed successfully for the target commit.
- Public GHCR tag `sha-54e2398` resolves to the multi-architecture OCI index digest `sha256:6e01123fba9925b55b1f86ba7309e297fed9b14ca318d54c2bef5b08ea08df24` with `linux/amd64`, `linux/arm64`, and attestation manifests.
- Migration execution `family-dashboard-staging-mig-socv2hn` succeeded using that exact digest. The migration job remains provisioned with the same digest.
- Azure Container App revision `family-dashboard-staging-api--0000002` is the latest ready revision and runs that exact digest. Older healthy scale-to-zero revisions remain in revision history but receive no current replica.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 in Central US with `Standard_B1ms`, 32 GiB storage, seven-day backup retention, public access disabled, and the UTF-8 `family_dashboard` database present.
- `https://api.egobrane.net/health/live` and `/health/ready` return HTTP 200 `Healthy`. An unauthenticated request to `/api/auth/me` returns HTTP 401 `application/problem+json` with stable code `authentication_required`, proving production still fails closed before Google sign-in exists.
- `https://family.egobrane.net` returns HTTPS 200 and its current compiled bundle contains `https://api.egobrane.net`. This increment changed no frontend content, so this evidence does not claim Netlify deploy metadata for the target commit. The interactive browser surface was unavailable; the frontend check used public HTTP and compiled-asset inspection.

This proves Increment 2 schema migration and backend deployment without enabling production authentication. The restore drill and durable Data Protection infrastructure were completed on 2026-08-13; real Google configuration, Increment 3 deployment proof, and a real Netlify Deploy Preview remain.

## Recovery and Identity Increment 3 infrastructure evidence: 2026-08-13

- PostgreSQL was restored privately to the separate temporary server `family-dashboard-stg-pitr-20260813` at the 15:55 UTC restore point. Azure recorded 7 minutes 4 seconds from restore start to success. Read-only verifier execution `family-dashboard-pitr-verify-4z2ymsc` confirmed the application database, both deployed EF migrations, and readable household/account tables without modifying the restored or original database.
- The original PostgreSQL server and public readiness endpoint remained healthy throughout. The verifier job and temporary restored server were deleted after verification; no application connection, DNS target, or original database changed.
- Deployment `family-dashboard-staging-auth-infra` succeeded at 16:45:59 UTC. It provisioned runtime identity `family-dashboard-staging-runtime`, private Data Protection Blob Storage `familydbrwzkcdch6czlm`, Key Vault `familydb-rwzkcdch6czlm`, its RSA wrapping key, and approved private endpoints.
- Storage public networking and shared-key access are disabled. Key Vault public networking is disabled with RBAC, soft deletion, and purge protection enabled. The runtime identity has only Storage Blob Data Contributor on that storage account plus Key Vault Crypto User and Secrets User on that vault.
- Azure revision `family-dashboard-staging-api--0000003` remained healthy on the previously published Increment 2 digest. Both public health checks pass and `/api/auth/me` continues to return the expected `401 application/problem+json` until Increment 3 is published and Google is enabled.
- The Increment 3 working tree passes 51 backend tests with PostgreSQL 18, four frontend component tests, six Playwright wall-display/phone tests, frontend lint/build, both production container builds, Compose validation, both K3s renders, Bicep compilation, and dependency vulnerability checks.

This evidence proves recovery and the durable key-storage foundation, not real Google authentication. The Google client does not exist in repository configuration, no placeholder secret was provisioned, and the working-tree application image has not been deployed to staging.

## Identity Increment 3 activation evidence: 2026-08-14

Target commit: `0b8bbdc336f09e9bf7d5cbfd286c747a3b6ac0f1` (`Implemented successful Google OAuth authentication`).

- The public Google client ID is configuration, while the client secret was placed directly in private Key Vault secret `google-client-secret`; the secret value was never read into command output or added to the repository.
- [Continuous Integration run 31837753121](https://github.com/egobrane/FamilyBoard/actions/runs/31837753121), [Publish Backend Image run 31837753176](https://github.com/egobrane/FamilyBoard/actions/runs/31837753176), and [Azure staging deployment run 31838329773](https://github.com/egobrane/FamilyBoard/actions/runs/31838329773) all completed successfully for the target commit.
- Public GHCR tag `sha-0b8bbdc` resolves to multi-architecture OCI digest `sha256:0d07536b2185bbfc4eb695188e64ca7ef54c5cbd1e82ec965f7305037bc76e18` with `linux/amd64`, `linux/arm64`, and attestation manifests.
- Initial activation exposed that managed Container Apps ingress terminates TLS and Kestrel saw the forwarded request as HTTP. Google was immediately disabled without changing the image, database, client ID, or secret while the fix was reviewed.
- The API container now sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, following the Azure Container Apps reverse-proxy boundary. With Google disabled, the intermediary revision remained healthy and the login endpoint returned the expected `503 authentication_unavailable` ProblemDetails.
- Google was re-enabled only after that intermediary check. The resulting OAuth challenge returned HTTP 302 to `accounts.google.com`, used the exact HTTPS callback `https://api.egobrane.net/api/auth/callback/google`, matched the configured client ID, requested only `email`, `openid`, and `profile`, used authorization code response type, requested online access, and included transient state.
- Public liveness and database-backed readiness return `Healthy`. Unauthenticated `/api/auth/me` returns the expected `401 authentication_required` ProblemDetails. Credentialed CORS permits only `https://family.egobrane.net`, including the approved `Content-Type` and `X-CSRF-TOKEN` headers and GET/POST/PUT/PATCH methods.
- The owner completed a real first-time Google sign-in, returned to the Netlify dashboard, and confirmed `/api/auth/me` returned the new persisted account, no household memberships yet, and a non-shared database-backed session.
- The session issued by API revision `0000008` remained authenticated after traffic moved to healthy functionally identical revision `authproof1`, proving cookie continuity through Blob-persisted, Key-Vault-wrapped Data Protection keys.
- From the configured frontend origin, the owner obtained an antiforgery token and completed credentialed `POST /api/auth/logout`; it returned HTTP 204. The same browser then received HTTP 401 from `/api/auth/me`, proving application-session revocation, and a subsequent Google sign-in succeeded.
- Migration execution `family-dashboard-staging-mig-pu871ue` succeeded using the target digest. Azure revision `family-dashboard-staging-api--0000009` is healthy, receives 100% of traffic, and runs that same digest with Google authentication, forwarded-header processing, and persistent Azure Data Protection configured.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 with public access disabled, seven-day retention, `Standard_B1ms`, 32 GiB storage, and the UTF-8 `family_dashboard` database present.
- Public liveness and database-backed readiness return `Healthy`; an independent request without the owner's private session cookie receives the expected credentialed-CORS `401 authentication_required` response from `/api/auth/me`.
- Netlify production deploy `6a7e002702ab2100083aac26` remains ready at `https://family.egobrane.net` from frontend commit `ad5e100a159dbaa098b5f673745732d618596fb4`. Later commits changed no frontend source, so retaining that deploy is expected. Its compiled bundle contains `https://api.egobrane.net`, the PWA manifest is available, and the response retains the configured security headers.

Identity Increment 3 staging activation and final digest deployment are proven end to end. On 2026-08-14 the owner confirmed that the private browser session remained authenticated after revision `0000009`; the cookie itself was not inspected or disclosed. A real Netlify Deploy Preview remains unproven.

## Identity Increment 4 staging verification: 2026-08-15

Target commit: `eaccfd8fb122651e8ae282344a929b0c8f1432e4` (`Deplying onboarding functions and procedures.`).

- The local `main` branch is clean and matches `origin/main` at the target commit.
- [Continuous Integration run 31850616363](https://github.com/egobrane/FamilyBoard/actions/runs/31850616363) completed successfully. Backend, frontend, and containers/manifests jobs passed, including PostgreSQL tests, frontend lint/component/build checks, responsive Playwright coverage, production container builds, Compose validation, Bicep compilation, and K3s rendering.
- [Publish Backend Image run 31850616332](https://github.com/egobrane/FamilyBoard/actions/runs/31850616332) completed successfully. Public tag `sha-eaccfd8` resolves to multi-architecture OCI digest `sha256:591f816f10a155591cfbbbd8a5ed974eaae9bbb5cdf0eabfc4476cfed081e8b0` with `linux/amd64`, `linux/arm64`, and attestation manifests.
- [Azure staging deployment run 31885267333](https://github.com/egobrane/FamilyBoard/actions/runs/31885267333) completed successfully through GitHub OIDC. Migration execution `family-dashboard-staging-mig-bwd5p3f` succeeded, and the migration job retains `--migrate` while using the target digest.
- Azure revision `family-dashboard-staging-api--0000010` is healthy, provisioned, receives 100% of traffic, and uses the same immutable digest. Insecure ingress remains disabled and `minReplicas` remains zero.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 in Central US with `Standard_B1ms`, 32 GiB storage, seven-day backup retention, no HA or geo-redundant backup, and public access disabled. Database `family_dashboard` is present with UTF-8 encoding.
- `https://api.egobrane.net/health/live` and `/health/ready` return HTTP 200 `Healthy`. An unauthenticated request from the configured frontend origin receives the expected credentialed-CORS `401 authentication_required` response from `/api/auth/me`.
- Netlify production deploy `6a7fa5201b7d69000801485d` is `ready`, targets the exact commit, and serves `https://family.egobrane.net`. The published bundle contains `https://api.egobrane.net`, `/api/auth/session/household`, and the Increment 4 household-selection UI; it contains no localhost API origin. The PWA manifest remains available.
- The owner completed first-household bootstrap with the intended household configuration and confirmed the authenticated household heading. Logout revoked the application session, and a subsequent Google sign-in succeeded.
- The owner created `Staging Selection Test Household` through the authenticated, antiforgery-protected API. The account could switch between both households in both directions, and the selected household persisted after refresh.
- `Staging Selection Test Household` remains intentionally stored in staging because household deletion is not implemented. Removing it requires a separately reviewed cleanup or deletion workflow.

Identity Increment 4 is proven end to end in staging. At this checkpoint, a real Netlify Deploy Preview and physical wall-display validation remained separate; the physical validation was later completed with Increment 6.

## Identity Increment 4B staging verification: 2026-08-15

Target commit: `3b5c3dbe7269868f8b33275103a6235f9172c7fa` (`Added household and member management features and focused dashboards for different family units.`).

- The local `main` branch is clean and matches `origin/main` at the target commit.
- [Continuous Integration run 31897612128](https://github.com/egobrane/FamilyBoard/actions/runs/31897612128) completed successfully. Backend, frontend, and containers/manifests jobs passed, including 58 PostgreSQL-backed backend tests, frontend lint and 13 component/API tests, the production PWA build, ten responsive Playwright cases, production container builds, Compose validation, Bicep compilation, and K3s rendering.
- [Publish Backend Image run 31897612110](https://github.com/egobrane/FamilyBoard/actions/runs/31897612110) initially failed only while GHCR returned `blob upload unknown to registry`. Its failed-job rerun completed successfully, including provenance attestation. Public tag `sha-3b5c3db` resolves to multi-architecture OCI digest `sha256:b43300a03c1d02036ff1c25c0c5d091257942764866a5ea804fafba4a1d4e12f` with `linux/amd64`, `linux/arm64`, and attestation manifests.
- [Azure staging deployment run 31900932186](https://github.com/egobrane/FamilyBoard/actions/runs/31900932186) completed successfully through GitHub OIDC. Migration execution `family-dashboard-staging-mig-y0bnuki` succeeded with `--migrate` and the target digest. Increment 4B had no model change, so no new migration was expected.
- Azure revision `family-dashboard-staging-api--0000011` is healthy, provisioned, receives 100% of traffic, and runs the same immutable digest. Insecure ingress remains disabled and `minReplicas` remains zero.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 in Central US with `Standard_B1ms`, 32 GiB storage, seven-day backup retention, no HA or geo-redundant backup, and public access disabled. Database `family_dashboard` remains present with UTF-8 encoding.
- `https://api.egobrane.net/health/live` and `/health/ready` return HTTP 200 `Healthy`. An unauthenticated request from the configured frontend origin receives credentialed CORS and the expected `401 authentication_required` ProblemDetails from `/api/auth/me`.
- Netlify production deploy `6a809dcde4c43a0008668c1e` is `ready`, targets the exact commit, and serves `https://family.egobrane.net`. Its compiled bundle contains `https://api.egobrane.net`, the household-settings and child-administration UI, and the self-deactivation error contract; it contains no localhost API origin.
- The owner confirmed that authenticated household settings and member administration loaded successfully, then created child profiles and successfully deactivated and reactivated them. Historical records were retained as designed.
- The self-deactivation path is hidden for the current adult in the UI and has deployed automated API/PostgreSQL coverage for `409 self_deactivation_requires_leave_flow`. A fresh direct authenticated staging request was not attempted during this evidence refresh because no controllable signed-in browser session was available; staging data was not modified to manufacture the check.

Identity Increment 4B is proven end to end in staging. At this checkpoint, a real Netlify Deploy Preview, direct staging proof of the self-deactivation ProblemDetails contract, and physical wall-display validation remained separate; the physical validation was later completed with Increment 6.

## Identity Increment 5 staging verification: 2026-08-17

Target commit: `9051cc741c2a389e3316cb6f1b211c9dc6fa6dea` (`Set up invitation functionality`).

- The local `main` branch was clean and matched `origin/main` at the target commit when this evidence was refreshed.
- [Continuous Integration run 31907608854](https://github.com/egobrane/FamilyBoard/actions/runs/31907608854) and [Publish Backend Image run 31907608835](https://github.com/egobrane/FamilyBoard/actions/runs/31907608835) completed successfully for the target commit. Public tag `sha-9051cc7` resolves to multi-architecture OCI digest `sha256:d9192d8eb64163b0afeb612d009c7dad4b105957f8f8c3c80091961813fc320c`, with `linux/amd64`, `linux/arm64`, and attestation manifests.
- [Azure staging deployment run 31917695236](https://github.com/egobrane/FamilyBoard/actions/runs/31917695236) completed successfully through GitHub OIDC. Migration execution `family-dashboard-staging-mig-7y7psn9` succeeded using the target digest. Log Analytics confirms it applied additive migration `20260815201908_AddHouseholdInvitations` and recorded EF Core product version `10.0.10`.
- The completed migration emitted native `libgssapi_krb5.so.2` load warnings after successfully applying the schema. They did not change the successful job result or API health, but should be monitored and removed from the container runtime if they recur or obscure actionable logs.
- Azure revision `family-dashboard-staging-api--0000012` is healthy, provisioned, receives 100% of traffic, and runs the same immutable digest. Insecure ingress remains disabled and `minReplicas` remains zero.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 in Central US with `Standard_B1ms`, 32 GiB storage, seven-day backup retention, no geo-redundant backup, and public access disabled. UTF-8 database `family_dashboard` remains present.
- `https://api.egobrane.net/health/live` and `/health/ready` return HTTP 200 `Healthy`. An independent request without a private session cookie, from the configured frontend origin, receives credentialed CORS and the expected `401 authentication_required` ProblemDetails from `/api/auth/me`.
- Netlify production deploy `6a80d02c20eb730008321a1a` is `ready`, targets the exact commit, and serves `https://family.egobrane.net`. Its compiled bundle contains `https://api.egobrane.net` and the invitation UI, with no localhost API origin.
- The owner created an invitation for a specific Google email, copied and opened the link, and confirmed the frontend removed the raw fragment token from browser history before Google sign-in. The intended account accepted the invitation and joined the correct household; household switching worked in both directions and the selection persisted after refresh.
- The owner also completed the prescribed staging checks for wrong-account rejection, replay rejection after successful consumption, and revocation. Those terminal paths failed safely without adding an unintended membership. Seven-day expiration is covered by deployed automated API/PostgreSQL tests but was not manually waited out in staging, so no real-time expiration claim is made here.
- This refresh could not independently replay the authenticated browser journey because no connected browser session was available. The manual invitation observations above are owner-confirmed; repository, public deployment, API, and Azure evidence were independently refreshed.

Identity Increment 5 is proven end to end in staging. A manually elapsed invitation-expiration check, real Netlify Deploy Preview, and direct staging proof of the self-deactivation ProblemDetails contract remain outstanding. Physical wall-display validation was later completed with Increment 6.

## Identity Increment 6 staging verification: 2026-08-18

Target commit: `2847e2b03944307734fba7585f3980dc7c6fe022` (`Adding support for parent access pin to allow administrative actions to be locked behind normal access.`).

- Local `main` is clean and matches `origin/main` at the target commit.
- [Continuous Integration run 32062641094](https://github.com/egobrane/FamilyBoard/actions/runs/32062641094), [Publish Backend Image run 32062641213](https://github.com/egobrane/FamilyBoard/actions/runs/32062641213), and [Azure staging deployment run 32063307881](https://github.com/egobrane/FamilyBoard/actions/runs/32063307881) all completed successfully for the exact commit.
- Public tag `sha-2847e2b` resolves to multi-architecture OCI digest `sha256:ab848c7964f60eda378def3faff127533d54ccfc498195cd18446f9e5dd8c5ce` with `linux/amd64`, `linux/arm64`, and attestation manifests.
- Migration execution `family-dashboard-staging-mig-j16fbps` succeeded using that digest. Retained Log Analytics output records `Applying migration '20260817143618_AddHouseholdParentAccess'` and insertion of that migration with EF Core version `10.0.10`.
- The migration again emitted `libgssapi_krb5.so.2` load warnings before successfully applying the schema. They remain non-blocking but should be removed or suppressed if they begin obscuring actionable migration failures.
- Azure revision `family-dashboard-staging-api--0000014` is healthy, provisioned, receives 100% of traffic, and runs the exact published digest with `minReplicas` zero. Parent access is enabled and reads secret reference `parent-access-pepper` from versionless Key Vault URI `parent-access-pepper-v1`; no secret value was retrieved during verification.
- The secure parent-access deployment reports `Succeeded`. This proves the ARM deployment and application reference without opening the private Key Vault data plane or exposing the pepper.
- PostgreSQL server `family-dashboard-staging-pg-rwzkcdch6czlm` is `Ready` on version 18 in Central US with `Standard_B1ms`, 32 GiB storage, seven-day retention, geo-redundant backup disabled, public access disabled, and UTF-8 database `family_dashboard` present.
- Public liveness and database-backed readiness return HTTP 200 `Healthy`. An unauthenticated `/api/auth/me` request returns HTTP 401 ProblemDetails with code `authentication_required`.
- Netlify production deploy `6a83663b72454f0008b05d36` is `ready`, targets the exact commit, and serves `https://family.egobrane.net`. Its compiled bundle contains `https://api.egobrane.net` and the parent-access routes, with no localhost API origin.
- The owner verified PIN setup and replacement, shared-mode refresh persistence, routine locked-dashboard access, administration gating, generic incorrect-PIN behavior, correct verification, explicit lock, household-switch clearing, five-minute expiry, private-only bootstrap and invitation acceptance, elevated shared-mode exit, recent-private-session recovery, failed-attempt cooldown, logout, and subsequent Google sign-in.
- The owner also verified the physical wall display, phone, responsive layout, touch, mouse, and keyboard flows. Browser inspection found no PIN, hash, salt, pepper, OAuth secret, database credential, or signing material in URLs, browser storage, frontend source, or logs.

Identity Increment 6 and the accepted shared-display boundary are proven end to end in staging. The remaining operational work is a deliberate rollback rehearsal before irreplaceable household data is stored and a real Netlify Deploy Preview with an explicitly safe authentication strategy.
