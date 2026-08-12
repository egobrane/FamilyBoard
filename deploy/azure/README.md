# Azure staging infrastructure

These Bicep templates deploy Family Dashboard staging into the **existing** `ryan-dev` resource group. They never create the group. Use incremental deployment mode only.

## Safety boundary

Every managed resource starts with `family-dashboard-staging` and carries the `application=family-dashboard`, `environment=staging`, and `managed-by=bicep` tags. The shared resource group contains unrelated resources; never use complete-mode deployment and never delete the resource group. Azure Container Apps creates a separate Azure-managed infrastructure resource group for the custom-VNet environment. Do not manage or delete it directly.

## Prerequisites

- Azure CLI with Bicep support.
- Owner on `ryan-dev` for resource-level role assignments.
- A subscription administrator must register `Microsoft.App` and `Microsoft.DBforPostgreSQL`.
- PostgreSQL Flexible Server 18 and `Standard_B1ms` capacity in Central US.
- The public backend image in `staging.bicepparam` must be an immutable GHCR digest.

## Validate and preview

Use a strong password stored in a password manager. Avoid placing it in shell history:

```sh
read -s FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
export FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
az bicep build --file deploy/azure/main.bicep
az deployment group what-if \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --parameters deploy/azure/staging.bicepparam
unset FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
```

Review the complete output. Stop if any unrelated resource is deleted or modified.

## First deployment

```sh
read -s FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
export FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
az deployment group create \
  --name family-dashboard-staging-bootstrap \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/staging.bicepparam
unset FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
```

The current staging administrator credential is stored in the project owner's macOS Keychain under service `com.egobrane.family-dashboard.azure.staging.postgres` and account `familydashboardadmin`. Do not copy it into the repository, GitHub, Netlify, or command output.

The initial deployment leaves the custom API domain disabled. Record the `apiDefaultHostname`, `customDomainVerificationId`, `githubClientId`, `tenantId`, and `subscriptionId` outputs.

Next create these DNS records with the DNS provider:

- CNAME `api.egobrane.net` to `apiDefaultHostname`.
- TXT `asuid.api.egobrane.net` to `customDomainVerificationId`.

Use DNS-only proxying until Azure issues the managed certificate. After both records resolve, Azure requires the hostname to be attached before it will create a managed certificate:

```sh
az containerapp hostname add \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --hostname api.egobrane.net
```

Then set `enableCustomDomain = true`, repeat `what-if`, and redeploy. The first certificate deployment may take several minutes. Verify both public health endpoints before configuring Netlify.

## Database migration

Run the one-shot job before exposing an API revision that depends on a new schema:

```sh
az containerapp job start \
  --resource-group ryan-dev \
  --name family-dashboard-staging-mig
```

The normal GitHub deployment workflow updates the job to the chosen immutable image, waits for a successful execution, updates the API, and verifies health.

## GitHub environment configuration

Create a protected GitHub environment named `staging`, require an approver if available, and add these non-secret environment variables from the Bicep outputs:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

OIDC uses GitHub's immutable subject `repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging`; no client secret is created. The owner and repository IDs prevent a renamed or recycled repository name from inheriting this trust. The identity receives Container Apps Contributor only on the staging API and Container Apps Jobs Contributor only on the migration job. Azure's CLI cannot safely override only the image at job start—it replaces the container template—so the workflow must update the existing job image before starting it. Job Contributor is the narrowest suitable Azure built-in role for that operation and remains scoped to this one migration job.
