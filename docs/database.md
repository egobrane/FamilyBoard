# Database and Migrations

## Ownership

PostgreSQL stores product-owned household, chore, point, reward, and preference data. Google Calendar and Google Tasks data will remain externally owned; any future cache is disposable and isolated from this schema.

## Model

- A household has one configuration and many members.
- A user account can belong to multiple households through `HouseholdMembership`; each membership links exactly one adult account to a profile in the same household.
- Children remain profile-only and have no user account or membership link.
- `ExternalIdentity` reserves the unique provider-subject mapping used by future Google sign-in without storing OAuth tokens.
- `UserSession` stores revocable application sessions with rolling idle and hard absolute expiration. It stores no Google token or cookie payload.
- Chore definitions belong to a household; assignments connect one definition to one member.
- A completion is a unique reviewable record for one assignment.
- Point transactions form an append-only signed ledger for each member.
- Rewards belong to a household; redemptions capture their point cost at request time.
- Preferences can be household- or member-scoped and store validated JSON values.

Identifiers are UUIDs. Instants are `timestamptz` and interpreted as UTC. Household display uses its IANA time-zone identifier. Historical records use restrictive foreign keys; members, chores, and rewards should be deactivated instead of deleting history.

Migration `AddIdentityAndHouseholdPersistence` is additive. Its composite foreign key prevents linking an account to a profile from a different household, and its provider/subject uniqueness constraint prevents two accounts from claiming the same external identity.

Migration `AddUserSessions` is additive. It restricts account deletion, indexes active-session lookup and cleanup expiration, and enforces that idle expiration follows creation and never exceeds absolute expiration.

## Creating a migration

Restore the repository-pinned EF tool:

```sh
dotnet tool restore
```

Then run:

```sh
dotnet tool run dotnet-ef migrations add MigrationName \
  --project src/backend/FamilyDashboard.Api \
  --startup-project src/backend/FamilyDashboard.Api \
  --output-dir Persistence/Migrations
```

Review every generated migration and model snapshot. Commit both. Do not use `EnsureCreated`.

## Applying migrations

Local Compose runs the `migrate` service before the API. The backend image also accepts `--migrate`, which allows a one-shot K3s Job to use the exact release image.

Application replicas do not migrate automatically. Before a production migration:

1. verify a current restorable backup;
2. review locks and destructive changes;
3. run one migration job;
4. verify completion before deploying code that requires the schema;
5. retain an application rollback plan, noting that database rollback may require a forward-fix.

Azure staging uses PostgreSQL Flexible Server 18 with private VNet access, TLS-required application connections, seven-day automated backup retention, and a Container Apps migration job. The job must succeed before the API is updated. The first isolated point-in-time restore drill completed successfully on 2026-08-13; repeat it after material recovery-policy or topology changes.
