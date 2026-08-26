# Application Recovery

This runbook covers a reversible Family Dashboard API revision rollback. It does not roll back PostgreSQL and does not authorize destructive recovery against the shared `ryan-dev` resource group.

## Objectives

- Application rollback RTO: 15 minutes.
- Application rollback RPO: zero because the database remains unchanged.
- PostgreSQL disaster-recovery RTO: 30 minutes.
- PostgreSQL disaster-recovery RPO: five minutes or the closest restore point Azure reports at incident time.
- Staging backup retention: seven days.

The database targets must be reassessed before production. The measured staging point-in-time restore completed in 7 minutes 4 seconds, but restore duration and the latest available restore point can vary.

## Safety boundary

1. Confirm subscription `b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8`, resource group `ryan-dev`, and app `family-dashboard-staging-api` before changing traffic.
2. Select only an already healthy Family Dashboard revision whose digest and schema compatibility are documented.
3. Never shift a shared-display deployment to code older than Identity Increment 6 unless every shared session has first been revoked. Older code does not enforce parent-PIN elevation.
4. Do not reverse an EF Core migration during routine rollback. Additive schema remains in place and the previous application must ignore it; otherwise deploy a forward fix.
5. A data restore always targets a separate, clearly prefixed PostgreSQL server. Never overwrite the current server during a drill.

## Capture the current release

```sh
az account set --subscription b8255fca-4e0c-4f4b-933b-1cd8fcbc91b8
az containerapp show \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --query '{latestReady:properties.latestReadyRevisionName,traffic:properties.configuration.ingress.traffic,image:properties.template.containers[0].image}' \
  --output json
az containerapp revision list \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --query '[].{name:name,active:properties.active,health:properties.healthState,traffic:properties.trafficWeight,image:properties.template.containers[0].image}' \
  --output table
```

Record the current revision, previous revision, both immutable digests, feature flags, start time, and operator before continuing.

## Shift traffic to a previous compatible revision

The 2026-08-26 rehearsal uses post-parent-access revision `family-dashboard-staging-api--0000031` as the rollback target and `family-dashboard-staging-api--0000032` as the release to restore:

```sh
az containerapp ingress traffic set \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --revision-weight \
    family-dashboard-staging-api--0000031=100 \
    family-dashboard-staging-api--0000032=0
curl --fail https://api.egobrane.net/health/live
curl --fail https://api.egobrane.net/health/ready
curl --include https://api.egobrane.net/api/auth/me
```

Liveness and readiness must return HTTP 200 `Healthy`. The request without a session must return HTTP 401 ProblemDetails with code `authentication_required`. When an owner-operated authenticated browser is available, also verify session continuity, household selection, routine Calendar reads/creation, chores, points, and rewards. Calendar Increment 3 management is expected to be unavailable on revision `0000031`.

## Restore the reviewed release

Restore the original revision even if a rollback verification fails:

```sh
az containerapp ingress traffic set \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --revision-weight \
    family-dashboard-staging-api--0000031=0 \
    family-dashboard-staging-api--0000032=100
curl --fail https://api.egobrane.net/health/live
curl --fail https://api.egobrane.net/health/ready
az containerapp show \
  --resource-group ryan-dev \
  --name family-dashboard-staging-api \
  --query 'properties.configuration.ingress.traffic' \
  --output json
```

Verify the restored revision, immutable digest, runtime allowlist, health, and 100% traffic. Record elapsed rollback and restoration times. If the original release cannot be restored, disable only the affected feature gate where possible and deploy a reviewed forward fix through the normal migration-first workflow.

## PostgreSQL recovery

Application rollback leaves PostgreSQL untouched. For suspected data damage:

1. stop unsafe writes through a feature gate or application rollback;
2. identify the last known-good point within the current seven-day retention window;
3. restore to a new private, clearly prefixed server;
4. validate migrations and representative records with a temporary private verifier;
5. retain the original server unchanged;
6. redirect the application only through a reviewed infrastructure change;
7. remove temporary resources only after explicit confirmation.

Prefer a forward data/schema correction when the current database remains internally consistent.

## Staging rehearsal evidence: 2026-08-26

The owner-approved rehearsal shifted 100% traffic from current revision `family-dashboard-staging-api--0000032` to known-compatible post-parent-access revision `family-dashboard-staging-api--0000031`. Azure reported the rollback revision at 100% traffic. Public liveness and database-backed readiness returned HTTP 200 `Healthy`, and an unauthenticated request failed closed with HTTP 401 code `authentication_required`.

Traffic was restored within approximately two minutes to revision `family-dashboard-staging-api--0000032`. Independent inspection confirmed it as latest-ready with 100% traffic on reviewed digest `sha256:93ac0319cc0f870c2d12059dd343bbeae7b5b069765342258ae0430570d2dfcf`; liveness and readiness again returned HTTP 200. PostgreSQL, migrations, secrets, and the scheduled chore job were not changed. Authenticated browser session continuity and feature-level smoke checks were not observed by the terminal rehearsal and remain an owner-operated confirmation.
