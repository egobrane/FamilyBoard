# Database and Migrations

## Ownership

PostgreSQL stores product-owned household, integration configuration, chore, point, reward, and preference data. Google Calendar and Google Tasks data remain externally owned; Calendar event responses are cached only in process memory and are never persisted. Creation and mutation receipts retain only opaque provider/source references, hashes, attribution, versions, operation status, timestamps, and trace correlation—not event titles, notes, locations, or times.

## Model

- A household has one configuration and many members.
- A user account can belong to multiple households through `HouseholdMembership`; each membership links exactly one adult account to a profile in the same household.
- Children remain profile-only and have no user account or membership link.
- `ExternalIdentity` reserves the unique provider-subject mapping used by future Google sign-in without storing OAuth tokens.
- `UserSession` stores revocable application sessions with rolling idle and hard absolute expiration. Its optional selected household is constrained to a membership owned by the same account, so separate browser sessions may retain different valid household contexts. It stores no Google token or cookie payload.
- `HouseholdInvitation` stores an email-bound adult invitation lifecycle. It stores only a unique 32-byte SHA-256 token hash, never the raw copyable token; a partial unique index permits only one pending invitation per household and normalized email.
- `HouseholdAccessPin` stores one salted, server-peppered PBKDF2 hash per household with algorithm, work-factor, and pepper version metadata. `ParentAccessAuditEvent` records only security-event metadata; neither table stores plaintext PINs or request bodies.
- `UserSession` scopes administrative elevation to one household and stores per-session failed-attempt windows and cooldowns. Switching households, locking, logout, revocation, or PIN replacement clears applicable elevation.
- `GoogleCalendarConnection` stores one adult-owned Calendar authorization, stable Google subject/email metadata, granted scopes, status, and Data-Protection-encrypted access/refresh tokens. It is separate from `ExternalIdentity` and Google sign-in.
- `GoogleTasksConnection` stores one separately authorized Tasks connection per adult with stable Google subject/email metadata, status, granted scopes, and Data-Protection-encrypted access/refresh tokens. `HouseholdTaskListSource` records household-visible provider lists and at most one household write target without storing task content or provider page tokens. `GoogleTaskMutationReceipt` is an append-only, household-scoped idempotency/audit record containing only opaque provider identifiers, SHA-256 request fingerprints, attribution, ETags, result status, and timestamps.
- `HouseholdDashboardAppearance` stores optional greeting copy, photo focal coordinates, and the active private-photo asset ID. `HouseholdPhotoAsset` stores only household-scoped storage metadata and attribution, never original image metadata or public URLs. `HouseholdMemberPhotoAsset` applies the same pending/active/retired lifecycle to one household-member profile, while `HouseholdMember` stores its active asset reference, focal coordinates, and optimistic photo version. `HouseholdWeatherConfiguration` stores rounded household coordinates, a label, and unit preference; forecast content is never persisted.
- `HouseholdCalendarSource` maps one stable Google calendar identifier to a household through its owning connection. A composite foreign key enforces that `OwnerUserAccountId` owns the connection; inactive rows preserve configuration history without owning calendar events.
- `CalendarEventCreationReceipt` and append-only `CalendarEventMutationReceipt` rows retain only opaque provider/source references, request fingerprints, versions, attribution, status, and trace metadata. They support retry-safe provider operations without storing event titles, notes, locations, or times.
- Chore definitions belong to a household and retain an optimistic-concurrency version plus a household-scoped idempotency key.
- One-time chore assignments connect one definition snapshot to one active household member. They retain the creator, original household-local due date/time and IANA zone, derived UTC instant, status, skip metadata, and concurrency version so later definition or regional-setting edits cannot rewrite history.
- Chore completions are attributed review attempts. They retain the completing member, submitting adult account, whether submission came from a shared display, review actor/result/note, and a household-scoped idempotency key. At most one pending review exists per assignment; rejected attempts remain historical while the assignment returns to pending.
- Chore schedules belong to one household, definition, active assigned member, and creating adult profile. They retain recurrence, local due-time intent, lifecycle state, next occurrence, and optimistic concurrency. Generated assignments link back to the schedule and use a unique household/schedule/local-date occurrence key while preserving their own snapshots.
- Point transactions form an append-only signed ledger for each member. Assignments and completion attempts snapshot point values; a completion can link to at most one normal award, while corrections link exact compensating entries to their originals.
- Rewards belong to a household and retain lifecycle actors plus optimistic versions. Redemptions snapshot title, description, and point cost; record request, review, fulfillment, and cancellation attribution; and reserve points through a unique negative ledger transaction. Rejection or cancellation releases the reservation through an exact append-only reversal.
- Preferences can be household- or member-scoped and store validated JSON values.

Identifiers are UUIDs. Instants are `timestamptz` and interpreted as UTC. Household display uses its IANA time-zone identifier. Historical records use restrictive foreign keys; members, chores, and rewards should be deactivated instead of deleting history.

Migration `AddIdentityAndHouseholdPersistence` is additive. Its composite foreign key prevents linking an account to a profile from a different household, and its provider/subject uniqueness constraint prevents two accounts from claiming the same external identity.

Migration `AddUserSessions` is additive. It restricts account deletion, indexes active-session lookup and cleanup expiration, and enforces that idle expiration follows creation and never exceeds absolute expiration.

Migration `AddSelectedHouseholdToUserSession` is additive. It adds a nullable selection plus a composite foreign key to `(UserAccountId, HouseholdId)` on `HouseholdMembership`, preventing a session from selecting another account's household. Existing sessions remain valid with no selection and are routed through household selection on their next frontend load.

Migration `AddHouseholdInvitations` is additive. It creates the invitation table, actor and household relationships, terminal-state checks, exact hash-length and normalized-email checks, metadata lookup indexes, unique token hashes, and the partial pending-invitation uniqueness rule. It does not modify existing household, membership, or session rows. The migration ran successfully in Azure staging on 2026-08-16 before API revision `family-dashboard-staging-api--0000012` received traffic.

Migration `AddHouseholdParentAccess` is additive. It creates the household PIN and audit tables, adds nullable elevation-household and cooldown fields to sessions, and constrains elevated households through the account-membership relationship. Existing sessions remain private, locked, and otherwise valid. Azure execution `family-dashboard-staging-mig-j16fbps` applied migration `20260817143618_AddHouseholdParentAccess` successfully on 2026-08-17 before revision `family-dashboard-staging-api--0000014` received traffic. The older API can read the expanded schema, but rolling back to pre-Increment-6 code after a shared display has been activated would remove PIN enforcement; revoke shared sessions or forward-fix instead.

Migration `AddGoogleCalendarReadOnlyIntegration` is additive. It creates adult-owned connection and household-source tables, enforces one connection per adult, requires protected refresh-token material for active connections, prevents duplicate source mappings, and constrains each source to the recorded connection owner. It stores no calendar events and changes no existing identity, household, or session row. Azure execution `family-dashboard-staging-mig-kae3bex` applied migration `20260818152451_AddGoogleCalendarReadOnlyIntegration` on 2026-08-19. The later correction deployment execution `family-dashboard-staging-mig-96s1m67` succeeded and confirmed that the database was already current before API revision `family-dashboard-staging-api--0000017` received traffic. The prior API ignores the additive tables, so application rollback remains possible.

Migration `AddGoogleCalendarEventCreation` is also additive. It marks at most one active household source as the writable target and creates `CalendarEventCreationReceipts` for idempotency, attribution, provider event identifiers, and non-sensitive operational trace correlation. It deliberately stores no event title, location, notes, start/end values, attendees, or event copy. A composite household key and deterministic Google event ID make concurrent retry convergence safe. Azure execution `family-dashboard-staging-mig-027lwjn` applied migration `20260819210734_AddGoogleCalendarEventCreation` successfully on 2026-08-20. The latest execution, `family-dashboard-staging-mig-asciech`, succeeded on 2026-08-21 before the current configuration revision was verified.

Migration `AddGoogleCalendarEventManagement` is additive. It creates append-only mutation receipts with household-safe relationships, operation and status constraints, unique idempotency keys, request fingerprints, provider version tokens, actor/shared-display attribution, and trace metadata. It stores no Calendar event content and changes no provider-owned event during migration. Azure execution `family-dashboard-staging-mig-qg1bzhk` completed successfully on 2026-08-26 before API revision `family-dashboard-staging-api--0000032` received traffic. The previous API ignores the additive receipt table; routine rollback disables event management and uses a forward schema fix rather than deleting retained receipts.

Migration `AddGoogleTasksReadOnlyIntegration` is additive. It creates one adult-owned connection and household list-source mappings with unique ownership, active-token, source-identity, and household-isolation constraints. It stores no Google task content and changes no identity, Calendar, household, session, chore, point, or reward row. Azure migration execution `family-dashboard-staging-mig-qynh433` completed successfully on 2026-08-28 before Tasks-enabled revision `family-dashboard-staging-api--0000037` received 100% traffic. A previous API ignores these tables.

Migration `AddGoogleTaskMutations` is additive. It adds write-target metadata and partial unique indexes enforcing one writable source per household and preventing the same provider list from being writable in multiple households. It creates `GoogleTaskMutationReceipts` with composite household foreign keys and no task-content columns. Azure execution `family-dashboard-staging-mig-krltv0a` applied the migration successfully on reviewed digest `sha256:858c26934aa0af30f254fd71ef8c5d26856f10a64f118f1b77d3aebd78cbf502`; a later same-digest activation execution also succeeded. PostgreSQL retains mutation metadata only, while Google Tasks remains the source of truth for task content.

Migration `AllowSharedGoogleTaskStatusActions` makes only `GoogleTaskMutationReceipt.AttributedHouseholdMemberId` nullable. Shared-display completion and reopening retain the requesting account, shared-display flag, household, opaque provider references, idempotency fingerprint, and trace metadata without falsely attributing the action to one member. Existing receipts are unchanged. A schema rollback maps nullable shared actions back to the requesting account's retained household membership before restoring the non-null constraint; normal rollback should keep the compatible schema and use a forward application fix.

Migration `AddChoreManagementWorkflow` is additive and preservation-oriented. It expands the foundational chore tables with household-scoped foreign keys, definition and due-date snapshots, actors, skip metadata, idempotency keys, review metadata, and concurrency versions. Existing assignments and completions are backfilled from their definitions, members, and household configuration before new non-null constraints are applied. It retains existing historical rows and creates no point transaction. The migration passed old-schema preservation and clean-database migration tests. Azure execution `family-dashboard-staging-mig-dmxpe0z` applied migration `20260822174708_AddChoreManagementWorkflow` successfully with EF Core `10.0.10` on 2026-08-22.

Migration `AddRecurringChoreSchedules` is additive. It creates `ChoreSchedules`, adds nullable schedule provenance to assignments, backfills existing assignments with exact due-time resolution, and adds the filtered unique occurrence index. It passed the complete empty-database migration suite and recurring generation test against disposable PostgreSQL 18. Azure migration execution `family-dashboard-staging-mig-4du4s5z` completed successfully during the initial recurring-schedule rollout; current reviewed-release execution `family-dashboard-staging-mig-t6o6bik` also completed successfully on 2026-08-24.

Migration `AddChorePointLedger` is additive and preservation-oriented. It adds point snapshots to assignments and completions, backfills them from definitions and assignments, adds point actors and compensating-reversal relationships, scopes idempotency to a household, and adds household-safe foreign keys and ledger indexes. It creates no retroactive award for an existing approved completion. Empty-database and populated-history migration tests pass against PostgreSQL 18. Azure execution `family-dashboard-staging-mig-n77m6k1` applied `20260825122424_AddChorePointLedger` with EF Core `10.0.10` on 2026-08-25 before API revision `family-dashboard-staging-api--0000029` received traffic. Current balances are calculated as 64-bit sums of immutable signed transactions rather than stored in a mutable cache.

Migration `AddRewardRedemptionWorkflow` is additive and preservation-oriented. It backfills existing rewards and redemptions with household scope, stable request IDs, snapshots, and version values before adding uniqueness and household-safe foreign keys. It deliberately creates no point reservation for a legacy redemption; such a row can be rejected or cancelled but cannot be approved or fulfilled without an explicit future resolution workflow. Azure execution `family-dashboard-staging-mig-ljztzuk` completed successfully before reward API revision `family-dashboard-staging-api--0000030` received traffic; correction execution `family-dashboard-staging-mig-9oznksx` subsequently confirmed the reviewed release could migrate before revision `family-dashboard-staging-api--0000031`.

Migration `AddDashboardPersonalizationAndWeather` is additive. It creates one versioned appearance row and one weather-configuration row per household plus retained metadata for private photo assets; it stores neither original image bytes nor forecast content in PostgreSQL. Azure execution `family-dashboard-staging-mig-mlfwh0o` applied migration `20260831212841_AddDashboardPersonalizationAndWeather`; corrected-release execution `family-dashboard-staging-mig-faqb5ag` subsequently confirmed the database current before healthy revision `family-dashboard-staging-api--0000043` received traffic. The prior API ignores these additive tables, while stored photos remain private during an application rollback.

Migration `AddHouseholdMemberProfilePhotos` is additive. It adds focal coordinates, a concurrency version, and a nullable active-photo reference to every existing household member with safe defaults, then creates household-scoped member-photo lifecycle metadata. Composite foreign keys prevent cross-household or wrong-member asset references, and a filtered unique index permits at most one active asset per member. No image bytes, original filenames, public URLs, SAS tokens, or storage credentials are stored in PostgreSQL. The prior API ignores the additive table and columns, so application rollback keeps uploaded blobs private; schema rollback should use a forward fix rather than deleting member-photo history.

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
