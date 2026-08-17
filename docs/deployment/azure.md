# Azure staging deployment

## Topology

```mermaid
flowchart LR
    Browser[Browser PWA] --> Netlify[Netlify family.egobrane.net]
    Browser -->|HTTPS| API[Azure Container App api.egobrane.net]
    GHCR[Public GHCR image by digest] --> Job[Container Apps migration job]
    GHCR --> API
    Job -->|private TLS| PostgreSQL[(PostgreSQL Flexible Server 18)]
    API -->|private TLS| PostgreSQL
    API -->|managed identity, private link| Blob[(Data Protection Blob)]
    API -->|managed identity, private link| Vault[Key Vault key, Google secret, and PIN pepper]
    GitHub[GitHub Actions OIDC] --> Job
    GitHub --> API
```

The API and job share a workload-profile Container Apps environment connected to a delegated VNet subnet. PostgreSQL uses a separate delegated subnet and private DNS zone. Public database networking is disabled.

## Provisioning region and prerequisites

On 2026-08-12, Azure CLI was logged into subscription `b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8`, and both `Microsoft.App` and `Microsoft.DBforPostgreSQL` were registered. Azure continued to restrict PostgreSQL provisioning in East US, while Central US advertised PostgreSQL 18 and `Standard_B1ms`. The owner explicitly approved moving the complete staging stack to Central US.

See [`deploy/azure/README.md`](../../deploy/azure/README.md) for commands and safety checks.

## Current staging instance

Provisioned successfully on 2026-08-12:

- API: `family-dashboard-staging-api`
- Default API origin: `https://family-dashboard-staging-api.calmplant-86bcedd8.centralus.azurecontainerapps.io`
- Migration job: `family-dashboard-staging-mig`
- PostgreSQL server: `family-dashboard-staging-pg-rwzkcdch6czlm`
- Container Apps environment: `family-dashboard-staging-env`
- Azure-managed infrastructure resource group: `ME_family-dashboard-staging-env_ryan-dev_centralus`
- GitHub OIDC client ID: `537b279f-60d3-4e5a-ac7f-bfc5120b8dc4`
- Tenant ID: `204a8dcb-68e2-4947-95a8-ed313d75b397`
- Subscription ID: `b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8`
- Runtime managed identity: `family-dashboard-staging-runtime`
- Data Protection storage account: `familydbrwzkcdch6czlm`
- Authentication Key Vault: `familydb-rwzkcdch6czlm`

The initial migration execution succeeded. Both default-hostname and `https://api.egobrane.net` health endpoints return HTTP 200. Azure managed TLS is active with insecure ingress disabled.

## Runtime configuration and secrets

The generated PostgreSQL password enters Bicep through `FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD`. Bicep marks it secure and stores the resulting connection string only as Container Apps secrets. It must also be retained in an owner-controlled password manager for emergency administration. It must never enter Git, GitHub variables, Netlify, logs, command-line arguments, or the frontend.

The API receives exact CORS origin `https://family.egobrane.net`, listens on port 8080, and uses TLS-required PostgreSQL connections. The frontend receives only public `VITE_API_BASE_URL=https://api.egobrane.net`. Azure Container Apps terminates public TLS at managed ingress, so the API container enables ASP.NET Core forwarded-header processing to preserve the original HTTPS scheme for security-sensitive absolute redirects such as the Google OAuth callback. Managed ingress is the only public route to the container target port.

The API runtime identity can write only the Data Protection Blob container, wrap/unwrap only through the Key Vault key, and read the Google and parent-access secrets. The migration job receives none of these permissions or secrets. Storage shared-key access and public networking are disabled. Key Vault uses RBAC, soft deletion, purge protection, a private endpoint, and public networking disabled. The owner created the Google web OAuth client, placed its secret directly in Key Vault, and activated staging authentication on 2026-08-14. Secrets remain absent from Git, GitHub, Netlify, container images, and browser-delivered configuration.

Increment 6 adds no Azure resource. Before enabling it, generate a random 32-byte value, base64 encode it, store it in the existing vault as `parent-access-pepper-v1` through the secure Bicep parameter template, and then deploy the main template. The API readiness check fails when parent access is enabled without a valid pepper. The migration job deliberately receives neither `ParentAccess__Enabled` nor the pepper.

## DNS and managed TLS

Custom-domain activation is deliberately staged. First deploy without the binding and obtain Azure's default hostname and verification ID. Create CNAME `api` and TXT `asuid.api` records, initially without a reverse proxy. After validation resolves, attach the hostname without a certificate using `az containerapp hostname add`; Azure will otherwise reject managed-certificate creation. Then enable the custom domain in Bicep and redeploy so Azure can issue and bind its managed certificate. If Cloudflare proxying is desired later, enable it only after Azure origin TLS and health checks are proven.

## Deployment and rollback

The initial infrastructure deployment is manual under the developer identity. Routine backend deployments use the protected GitHub `staging` environment and OIDC, accept only the approved GHCR repository pinned by digest, run migrations first, then update the API and verify both health endpoints.

GitHub issues immutable OIDC subjects for this repository. Azure must trust the exact subject `repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging`; the earlier name-only subject is intentionally not accepted. The identity has Container Apps Contributor on the API and Container Apps Jobs Contributor only on the migration job. Azure's start-time image override replaces the container template and loses the migration arguments/environment, so the workflow updates the existing job image before starting it. The job-specific Contributor role is the narrowest suitable Azure built-in role for that operation.

The workflow pins Azure Container Apps CLI extension `1.3.0b4` because the required job commands are currently delivered through that preview extension. Review and deliberately update the pin when Azure publishes a suitable stable version.

For an application rollback, reactivate a healthy previous Container Apps revision or redeploy its prior digest. Database migrations are forward-only: use a forward fix when possible. If a migration irreversibly damages data, restore PostgreSQL point-in-time backup to a new server, validate it, then redirect the application through a reviewed infrastructure update. Never overwrite the original server during a restore drill.

After any session has been activated as a shared display, do not roll the API back to pre-Increment-6 code: that code does not enforce the parent-PIN policy. Revoke every shared session first, or use a forward fix. The additive schema itself remains compatible with the previous API.

## Backup and recovery

The staging server uses seven-day Azure automated backups with point-in-time restore and no geo-redundancy. Before storing irreplaceable household data:

1. record recovery point and recovery time objectives;
2. perform a point-in-time restore to a separately prefixed temporary server;
3. validate schema and representative data;
4. document the measured restore time;
5. remove the temporary server only after explicit review.

The first drill ran on 2026-08-13. Azure restored `family-dashboard-stg-pitr-20260813` privately to the 15:55 UTC point. The Azure activity log measured 7 minutes 4 seconds from restore start to server success. Temporary job execution `family-dashboard-pitr-verify-4z2ymsc` completed a read-only transaction and confirmed `family_dashboard`, both deployed EF migrations, and readable household/account tables. The original readiness endpoint remained healthy. The verifier and restored server were then deleted; no staging connection or DNS target changed.

Burstable PostgreSQL has backup and performance limitations. Increase retention, add geo-redundancy, or move to General Purpose before production requirements justify the cost.

## Cost categories

- PostgreSQL B1ms compute and 32 GiB storage are the main predictable monthly cost.
- Container Apps Consumption charges for requests/compute while active; min replicas zero reduces idle compute but introduces cold starts.
- Log Analytics charges by ingestion and retention.
- Managed certificate, VNet, private DNS, and OIDC identity are generally low/no direct cost, while data transfer and DNS-provider fees may apply.
- Standard Key Vault operations, LRS Blob capacity/transactions, and both private endpoints add small ongoing charges even while Container Apps scales to zero.

Azure pricing and sponsorship-credit treatment must be checked at deployment time. Budgets and alerts should be configured outside this app-scoped template because the shared resource group contains unrelated workloads.
