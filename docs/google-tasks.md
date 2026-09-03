# Google Tasks integration

Google Tasks is a separate authorization boundary. Increment 1 provides read-only access. Increment 2 adds feature-gated creation, completion, and reopening through explicit incremental write consent; general editing, deletion, moving, ordering, reminders, recurrence, synchronization, and webhooks remain deferred. Google sign-in, Calendar, and Tasks continue to use separate OAuth clients, callbacks, state/correlation protection, encrypted backend-only tokens, and revocation flows.

## Ownership and storage

Google Tasks remains the source of truth. Family Dashboard stores one `GoogleTasksConnection` per adult, household-specific `HouseholdTaskListSource` selections, one optional writable source per household, and append-only `GoogleTaskMutationReceipt` metadata. Receipts contain opaque provider identifiers, request fingerprints, attribution, status, ETags, and timestamps—not titles, notes, due dates, or task state. Request-time results use a disposable two-minute fresh/15-minute stale in-memory cache.

An adult connection may contribute selected lists to multiple households. Routine reads include a source only while its owner remains an active adult member of that household. Connecting, selecting lists, and disconnecting require household administration; locked shared displays therefore require current parent-PIN elevation. Routine task viewing remains available on a locked shared display.

Each household may select at most one active, adult-owned writable list. The same provider list cannot be writable for multiple households. Creation, completion, and reopening are household-shared actions: a locked display records the authenticated account, session, and shared-display origin without asking for or claiming a specific household member. Private adult actions retain their linked adult attribution. This is audit context, not independent child authentication.

## API

- `GET /api/households/{householdId}/tasks/connection`
- `POST /api/households/{householdId}/tasks/authorization`
- `GET /api/integrations/google-tasks/callback`
- `GET /api/households/{householdId}/tasks/provider-task-lists`
- `GET /api/households/{householdId}/tasks/sources`
- `PUT /api/households/{householdId}/tasks/sources`
- `PUT /api/households/{householdId}/tasks/write-target`
- `POST /api/households/{householdId}/tasks/disconnect`
- `GET /api/households/{householdId}/tasks?includeCompleted=false&cursor=...`
- `POST /api/households/{householdId}/tasks`
- `PUT /api/households/{householdId}/tasks/status`

Unsafe endpoints require the credentialed application cookie and antiforgery header. OAuth state and pagination cursors are time-limited Data Protection payloads. The callback correlation cookie is Secure, HttpOnly, SameSite=Lax, and scoped to the exact Tasks callback.

The creation request contains an idempotency key, title, optional notes, and optional date-only due date. It does not accept a client-selected member identity. The backend derives private-adult attribution from the selected household membership and records shared-display creation with nullable member attribution.

## Provider behavior

The initial connection requests `openid`, `email`, and `https://www.googleapis.com/auth/tasks.readonly`. Enabling task actions explicitly requests the broader `https://www.googleapis.com/auth/tasks` scope because Google provides no narrower create/complete scope. Existing connections remain read-only until reauthorized. Due values are date-only; Family Dashboard does not invent a time. Increment 2 creates top-level tasks only and does not accept parent or ordering input. Assigned tasks are read-only.

Status updates use a short-lived Data-Protection version token binding household, source, task ID, and provider ETag. Stale mutations return a conflict. Mutation idempotency is household-scoped. Because Google Tasks has no provider create-idempotency key, an ambiguous create timeout is retained as `OutcomeUnknown` and is never replayed automatically; the user is told to inspect Google before retrying.

## Staging activation

1. Enable Google Tasks API in the approved Google Cloud project.
2. Create a separate Web application OAuth client with exact callback `https://api.egobrane.net/api/integrations/google-tasks/callback` and the three approved scopes.
3. Set `FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET` locally, deploy `deploy/azure/google-tasks-secret.bicepparam`, then unset the variable. Never put the secret in GitHub, Netlify, a Bicep parameter, shell history, or command arguments.
4. Publish/deploy the reviewed image and run additive migration `AddGoogleTasksReadOnlyIntegration` while `enableGoogleTasks=false`.
5. Put only the public Tasks client ID in `deploy/azure/staging.bicepparam`, set `enableGoogleTasks=true`, provision the structural Bicep change so the Key Vault reference exists, and reconcile the same reviewed digest.
6. Connect, select only household-appropriate lists, verify routine reads on private and locked shared-display sessions, revoke externally, reconnect, and disconnect.

Rollback sets `GoogleTasks__Enabled=false`, preserving encrypted dormant connection metadata and additive source rows. A previous API ignores the tables. Schema rollback should use a forward fix rather than deleting retained authorization metadata.

Increment 2 deployment keeps `enableGoogleTaskMutations=false` while `AddGoogleTaskMutations` runs and the API is deployed. Add the broad Tasks scope to the approved Google OAuth consent configuration, then set the flag true and reconcile the same reviewed digest. Disabling the flag immediately removes application mutations but does not reduce a scope already granted at Google; a security rollback must disconnect/revoke and reconnect read-only.

## Staging activation status: 2026-08-28

Implementation commit `52debdf32f2651aac19dcff40253749ed9e87dbc` supplied the genuine Version 2 PWA and Tasks backend. Workflow correction commit `e6a25776891dab7a13ff147ace18c8479d59bf87` passed CI, published public multi-architecture digest `sha256:99c704484334a863addfce2f155ee7522e6d8591b7e3cae4e38c24edab168580`, and handed it automatically to Azure. The first deployment created healthy revision `0000033` but failed closed at 0% traffic because traffic was still pinned to the prior revision. The corrected release logic now waits for readiness, explicitly promotes the exact healthy revision, and explicitly restores rollback traffic.

The owner enabled the Google Tasks API, configured a dedicated OAuth web client with exact callback `https://api.egobrane.net/api/integrations/google-tasks/callback`, approved the identity and `tasks.readonly` scopes, and seeded `google-tasks-client-secret` into the private staging Key Vault without placing its value in source or frontend configuration. Activation commit `0fa443ba4ad144447d4ba59b9d637541562e32f2` passed CI without redundantly publishing an image; protected manual deployment run `33185643005` then completed migration execution `family-dashboard-staging-mig-qynh433` and reconciled revision `family-dashboard-staging-api--0000037` to the reviewed digest with 100% traffic and `GoogleTasks__Enabled=true`.

Azure exposes only the Container App secret reference `google-tasks-client-secret`, backed by the versionless Key Vault URI and runtime managed identity. PostgreSQL 18 is Ready and contains the `family_dashboard` database. Public liveness/readiness are Healthy and unauthenticated `/api/auth/me` still fails closed with `401 authentication_required`. The owner also confirmed the Safari and wall-display Version 1-to-Version 2 update proof, mounted-form protection, safe idle activation, session continuity, and no manual service-worker/cache removal. Live Tasks connection, list selection/read behavior, revocation, multi-household isolation, locked shared-display access, parent elevation, provider-change caching, disconnect, responsive-device behavior, and leakage inspection are not yet recorded as owner-confirmed.

## Increment 2 staging verification: 2026-08-29

Implementation commit `b8a9372e67713ce3e121615b575f3d21508efe00` passed CI and published public multi-architecture digest `sha256:858c26934aa0af30f254fd71ef8c5d26856f10a64f118f1b77d3aebd78cbf502`. Migration execution `family-dashboard-staging-mig-krltv0a` applied `AddGoogleTaskMutations` with the gate disabled. Activation commit `f35e25ca1a1464c61b7d2a72d844b8afd2d2c834` passed CI without republishing the image; protected deployment `33259719447` reused the reviewed digest and promoted healthy revision `family-dashboard-staging-api--0000039` to 100% traffic with `GoogleTasks__Enabled=true` and `GoogleTasks__MutationsEnabled=true`.

The public Netlify PWA serves bundle `/assets/index-fvohcgDK.js`, which compiles the approved API origin and Increment 2 controls. The owner confirmed incremental write consent, selection of one writable household list, task creation, task completion, and the resulting provider changes on another device. Reopening, date-only due dates, shared-display attribution, conflicts, ambiguous outcomes, revocation, multi-household isolation, responsive-device behavior, and live sensitive-data inspection are not recorded as owner-confirmed unless separately exercised.
