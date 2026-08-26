# Google Tasks integration

Google Tasks Increment 1 is a separate, read-only authorization boundary. Google sign-in authenticates the adult, Google Calendar authorizes calendar access, and Google Tasks authorizes task access. Each uses its own OAuth web client, callback, state/correlation protection, connection record, encrypted backend-only tokens, and revocation flow.

## Ownership and storage

Google Tasks remains the source of truth. Family Dashboard stores one `GoogleTasksConnection` per adult and household-specific `HouseholdTaskListSource` selections. It does not persist task titles, notes, due dates, completion state, subtasks, or provider page tokens. Request-time results use a disposable two-minute fresh/15-minute stale in-memory cache.

An adult connection may contribute selected lists to multiple households. Routine reads include a source only while its owner remains an active adult member of that household. Connecting, selecting lists, and disconnecting require household administration; locked shared displays therefore require current parent-PIN elevation. Routine task viewing remains available on a locked shared display.

## API

- `GET /api/households/{householdId}/tasks/connection`
- `POST /api/households/{householdId}/tasks/authorization`
- `GET /api/integrations/google-tasks/callback`
- `GET /api/households/{householdId}/tasks/provider-task-lists`
- `GET /api/households/{householdId}/tasks/sources`
- `PUT /api/households/{householdId}/tasks/sources`
- `POST /api/households/{householdId}/tasks/disconnect`
- `GET /api/households/{householdId}/tasks?includeCompleted=false&cursor=...`

Unsafe endpoints require the credentialed application cookie and antiforgery header. OAuth state and pagination cursors are time-limited Data Protection payloads. The callback correlation cookie is Secure, HttpOnly, SameSite=Lax, and scoped to the exact Tasks callback.

## Provider behavior

The dedicated client requests `openid`, `email`, and `https://www.googleapis.com/auth/tasks.readonly`. Due values are returned as Google date-only values; Family Dashboard does not invent a due time. Completed tasks, subtasks, task ordering, and pagination remain provider data. Assigned-task metadata is exposed only as a boolean. Writes, reminders, notifications, polling, synchronization, and webhooks are deferred.

## Staging activation

1. Enable Google Tasks API in the approved Google Cloud project.
2. Create a separate Web application OAuth client with exact callback `https://api.egobrane.net/api/integrations/google-tasks/callback` and the three approved scopes.
3. Set `FAMILY_DASHBOARD_GOOGLE_TASKS_CLIENT_SECRET` locally, deploy `deploy/azure/google-tasks-secret.bicepparam`, then unset the variable. Never put the secret in GitHub, Netlify, a Bicep parameter, shell history, or command arguments.
4. Publish/deploy the reviewed image and run additive migration `AddGoogleTasksReadOnlyIntegration` while `enableGoogleTasks=false`.
5. Put only the public Tasks client ID in `deploy/azure/staging.bicepparam`, set `enableGoogleTasks=true`, provision the structural Bicep change so the Key Vault reference exists, and reconcile the same reviewed digest.
6. Connect, select only household-appropriate lists, verify routine reads on private and locked shared-display sessions, revoke externally, reconnect, and disconnect.

Rollback sets `GoogleTasks__Enabled=false`, preserving encrypted dormant connection metadata and additive source rows. A previous API ignores the tables. Schema rollback should use a forward fix rather than deleting retained authorization metadata.
