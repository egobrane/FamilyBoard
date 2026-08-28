# Azure staging infrastructure

Google Tasks uses a dedicated OAuth web client with exact callback `https://api.egobrane.net/api/integrations/google-tasks/callback`. Seed `google-tasks-client-secret` through `google-tasks-secret.bicepparam`, then put only the public client ID in `staging.bicepparam`. For Increment 2, keep `enableGoogleTaskMutations=false` through `AddGoogleTaskMutations` and the first reviewed-image deployment; approve the broad Tasks scope, then enable the flag and reconcile the same digest.

```sh
read -s FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET
export FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET
az deployment group create \
  --name family-dashboard-staging-google-tasks-secret \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/google-tasks-secret.bicepparam
unset FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET
```

These Bicep templates deploy Family Dashboard staging into the **existing** `ryan-dev` resource group. They never create the group. Use incremental deployment mode only.

## Safety boundary

Managed resources use the `family-dashboard-staging` prefix or the globally unique `familydb` prefix and carry the `application=family-dashboard`, `environment=staging`, and `managed-by=bicep` tags. Provider-required private DNS zones use Azure's fixed names; the existing Blob zone is referenced rather than managed. The shared resource group contains unrelated resources; never use complete-mode deployment and never delete the resource group. Azure Container Apps creates a separate Azure-managed infrastructure resource group for the custom-VNet environment. Do not manage or delete it directly.

## Prerequisites

- Azure CLI with Bicep support.
- Owner on `ryan-dev` for resource-level role assignments.
- A subscription administrator must register `Microsoft.App` and `Microsoft.DBforPostgreSQL`.
- PostgreSQL Flexible Server 18 and `Standard_B1ms` capacity in Central US.
- `FAMILY_DASHBOARD_BACKEND_IMAGE` must contain an immutable public GHCR digest when compiling or deploying `staging.bicepparam`; no release digest is committed.

## Validate and preview

Use a strong password stored in a password manager. Avoid placing it in shell history:

```sh
read -s FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
export FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
read -r FAMILY_DASHBOARD_BACKEND_IMAGE
export FAMILY_DASHBOARD_BACKEND_IMAGE
az bicep build --file deploy/azure/main.bicep
az deployment group what-if \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --parameters deploy/azure/staging.bicepparam
unset FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
unset FAMILY_DASHBOARD_BACKEND_IMAGE
```

Review the complete output. Stop if any unrelated resource is deleted or modified.

## First deployment

```sh
read -s FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
export FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
read -r FAMILY_DASHBOARD_BACKEND_IMAGE
export FAMILY_DASHBOARD_BACKEND_IMAGE
az deployment group create \
  --name family-dashboard-staging-bootstrap \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/staging.bicepparam
unset FAMILY_DASHBOARD_POSTGRES_ADMIN_PASSWORD
unset FAMILY_DASHBOARD_BACKEND_IMAGE
```

The current staging administrator credential is stored in the project owner's macOS Keychain under service `com.egobrane.family-dashboard.azure.staging.postgres` and account `familydashboardadmin`. Do not copy it into the repository, GitHub, Netlify, or command output.

The authentication-security deployment creates private Data Protection storage, a Key Vault wrapping key, and a least-privilege runtime identity. `enableGoogleAuthentication` remains false until the Google web client exists and `google-client-secret` has been seeded through the separate secure template. Never put the Google secret in GitHub, Netlify, a committed parameter, or a shell argument recorded in history.

Because Key Vault public networking is disabled, seed the real Google secret through Azure Resource Manager rather than opening its data plane. The parameter file reads a hidden environment variable and Bicep marks the value secure, so it is not retained in source, command arguments, or deployment output:

```sh
read -s FAMILY_DASHBOARD_GOOGLE_CLIENT_SECRET
export FAMILY_DASHBOARD_GOOGLE_CLIENT_SECRET
az deployment group create \
  --name family-dashboard-staging-google-secret \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/google-secret.bicepparam
unset FAMILY_DASHBOARD_GOOGLE_CLIENT_SECRET
```

Run that deployment only after creating the Google web OAuth client. Secret rotation uses the same exact deployment and creates a new Key Vault secret version. The main template references the versionless secret URI, so no application configuration change is required for rotation; roll out a new API revision to force a fresh secret resolution.

Google Calendar uses a second OAuth web client and a separately named secret. After creating that client with exact callback `https://api.egobrane.net/api/integrations/google-calendar/callback`, seed its secret without printing it:

```sh
read -s FAMILY_DASHBOARD_GOOGLE_CALENDAR_CLIENT_SECRET
export FAMILY_DASHBOARD_GOOGLE_CALENDAR_CLIENT_SECRET
az deployment group create \
  --name family-dashboard-staging-google-calendar-secret \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/google-calendar-secret.bicepparam
unset FAMILY_DASHBOARD_GOOGLE_CALENDAR_CLIENT_SECRET
```

Set the public `googleCalendarClientId` and `enableGoogleCalendar=true` only after that deployment succeeds. Keep the sign-in and Calendar client IDs/secrets distinct. First deploy the Calendar migration and API image with the feature disabled; enabling later changes only Container App configuration and creates a new revision of the same reviewed image.

Controlled Calendar event creation has its own `enableGoogleCalendarEventCreation` parameter. Keep it `false` while applying the additive migration. After the existing Calendar OAuth client's consent configuration includes the exact `calendar.events` scope, set it to `true` and redeploy the same reviewed backend digest. No additional Azure secret is required.

Controlled editing and deletion of Family Dashboard-created events uses `enableGoogleCalendarEventManagement`. Deploy the additive migration before enabling it. It reuses the existing `calendar.events` authorization and requires no new OAuth client, secret, or Azure resource.

Before enabling Increment 6, generate and save a random 32-byte parent-access pepper without printing it or placing it in shell history. Seed it as the separately named `parent-access-pepper-v1` secret through Azure Resource Manager, then unset the variable:

```sh
export FAMILY_DASHBOARD_PARENT_ACCESS_PEPPER="$(openssl rand -base64 32)"
az deployment group create \
  --name family-dashboard-staging-parent-access-secret \
  --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8 \
  --resource-group ryan-dev \
  --mode Incremental \
  --parameters deploy/azure/parent-access-secret.bicepparam
unset FAMILY_DASHBOARD_PARENT_ACCESS_PEPPER
```

The generated value should also be retained in the owner's password manager for disaster recovery. Do not rotate or overwrite `parent-access-pepper-v1`: existing hashes cannot be verified after a pepper is lost. A future rotation uses a new versioned secret name and a reviewed reset or dual-pepper transition.

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

The normal GitHub release path requires no image input or digest commit. A backend-relevant `main` push publishes the image, captures buildx's exact digest, and calls the reusable protected workflow. That workflow confirms matching CI and current `main`, supplies the digest transiently while compiling reviewed public parameters, migrates, reconciles the API and provisioned chore-generator image, verifies configuration/traffic/health, and restores the prior application release if verification fails. Manual dispatch retains an immutable digest input only for deliberate rollback or redeployment. The workflow does not deploy Bicep infrastructure, retrieve secrets, or modify unrelated `ryan-dev` resources.

## GitHub environment configuration

Create a protected GitHub environment named `staging`, require an approver if available, and add these non-secret environment variables from the Bicep outputs:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

OIDC uses GitHub's immutable subject `repo:egobrane@23132912/FamilyBoard@1324023581:environment:staging`; no client secret is created. The owner and repository IDs prevent a renamed or recycled repository name from inheriting this trust. The identity receives Container Apps Contributor only on the staging API and Container Apps Jobs Contributor separately on the migration and chore-generator jobs. Job Contributor remains scoped to those prefixed jobs.
