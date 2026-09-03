# Google Calendar Integration

## Boundary and ownership

Google sign-in and Google Calendar authorization are separate server-side authorization-code flows. Signing in requests identity scopes and creates a Family Dashboard session; it does not grant calendar access. Calendar connection uses a separate Google web client, requests offline access, and asks only for:

- `openid` and `email`, to identify which Google account owns the connection;
- `https://www.googleapis.com/auth/calendar.calendarlist.readonly`, to display calendars the adult may choose;
- `https://www.googleapis.com/auth/calendar.events.readonly`, to read events from chosen calendars.

The authorization belongs to one `UserAccount`. A `HouseholdCalendarSource` separately decides which of that adult's calendars is visible to each household. The same adult may expose different calendars to different households. A different connected Google account from the sign-in account is allowed and shown explicitly.

Google Calendar remains the event source of truth. Family Dashboard stores stable source identifiers and display metadata but no event rows. This increment does not create, edit, delete, or synchronize events; it does not integrate Tasks or register webhooks.

## API

Administrative routes require an active adult membership. Private adult sessions pass the administration policy directly; shared-display sessions require current household-scoped parent-PIN elevation. Cross-household requests return `404` rather than revealing configuration.

- `GET /api/households/{householdId}/calendar/connection`
- `POST /api/households/{householdId}/calendar/authorization`
- `GET /api/integrations/google-calendar/callback`
- `GET /api/households/{householdId}/calendar/provider-calendars`
- `GET /api/households/{householdId}/calendar/sources`
- `PUT /api/households/{householdId}/calendar/sources`
- `POST /api/households/{householdId}/calendar/disconnect`

Routine `GET /api/households/{householdId}/calendar/events?from=...&to=...&cursor=...` requires only active household membership. It therefore remains available on a locked shared display. Ranges must be positive and no longer than 32 days; the extra day accommodates a 31-day household-local month containing a daylight-saving fallback hour. Event pagination uses a short-lived, Data-Protection-protected opaque cursor; raw Google page tokens never reach JavaScript.

Unsafe requests use the existing credentialed cookie, exact-origin CORS, and `X-CSRF-TOKEN` antiforgery boundary. Calendar callback state is purpose-protected, expires after ten minutes, and is bound to the initiating application account, session, household, nonce, and validated local return path. A `Secure`, `HttpOnly`, callback-path-scoped, `SameSite=Lax` correlation cookie contains only a SHA-256 state digest and is deleted on callback. The callback redirects to a clean frontend URL and never places OAuth codes, tokens, provider errors, or personal calendar data in it.

## Token and event handling

Access and refresh tokens are encrypted separately with purpose strings containing the connection ID and token kind. Ciphertext is stored in PostgreSQL; plaintext exists only transiently in backend memory. Azure uses the existing private Blob-backed Data Protection key ring, Key Vault wrapping key, and runtime managed identity. Loss of that key ring requires reconnection.

The API refreshes an access token shortly before expiry. `invalid_grant`, unreadable ciphertext, provider revocation, or a disabled owner transitions the connection to a fail-closed reauthorization state. Disconnect attempts Google revocation and always removes local token ciphertext and deactivates every source belonging to the connection, even when Google's revocation endpoint is unavailable.

Event retrieval occurs at request time through `IGoogleCalendarProviderClient`. Normalized results include only title, start/end, all-day status, source name/color, time zone, and location. Descriptions, attendees, organizer addresses, conferencing, attachments, reminders, and extended properties are not returned. Recurring instances are expanded by Google with `singleEvents=true`; all-day start/end values remain date-only with Google's exclusive end-date semantics. Timed events preserve their ISO instant/offset.

Each API replica uses a disposable in-memory cache: two minutes fresh and up to fifteen minutes for stale fallback. Cache loss is harmless. A partial provider failure returns healthy sources plus warnings; a cached source may be marked stale. There is no Redis, polling job, sync token, durable event cache, or webhook renewal cost in this increment.

## Frontend behavior

- `/calendar` is a routine month calendar available to household members. Wall displays, tablets, and desktops use a seven-column grid plus a selected-day agenda; phones use an explicit date-grouped agenda. Dates, weekday order, event placement, and times use the household locale, time zone, and configured week start.
- The dashboard `Today` card reads the selected household's configured calendars.
- `/households/{householdId}/calendars` is an adult administrative screen inside the existing parent-access gate.
- Adults connect or reconnect, see the connected Google email, choose household-visible calendars, see sources supplied by other adults, and confirm global disconnect.
- Loading, empty, disconnected, consent-denied, reauthorization, partial, stale, provider-failure, and success states use semantic status/alert feedback.

Controls retain large touch targets, visible focus, keyboard operation, screen-reader labels, responsive phone layout, and mouse support. The shared wall display may show personal plans, so adults must deliberately choose only calendars appropriate for household visibility.

The month UI follows provider pagination up to a defensive eight-page display ceiling, groups all-day events before timed events, respects Google's exclusive all-day end date, and repeats cross-midnight or multi-day plans on each affected household-local date. Crowded cells show three plans and a `+N more` action; selecting a date exposes full times, calendar, location, and existing eligible-event management. The Add Event route accepts a validated local `date` query only to prefill its form and never sends event data through the URL.

## Configuration and activation

Calendar remains disabled by default in a new environment. Azure staging completed this activation sequence:

1. In the approved Google Cloud project, enable Google Calendar API and create a separate Web application OAuth client. Add exactly `https://api.egobrane.net/api/integrations/google-calendar/callback` as its staging redirect URI.
2. Configure the OAuth consent screen for the four scopes above. Keep the application in testing with explicit test users until verification requirements and refresh-token lifetime are understood; sensitive Calendar scopes may require Google verification for wider use.
3. Set `FAMILY_DASHBOARD_GOOGLE_CALENDAR_CLIENT_SECRET` locally, deploy `deploy/azure/google-calendar-secret.bicepparam`, and unset the variable. Never place the value in GitHub, Netlify, Bicep parameters, shell history, or command arguments.
4. Put the public Calendar client ID in `deploy/azure/staging.bicepparam`, set `enableGoogleCalendar = true`, retain the reviewed immutable backend digest, and deploy the main Bicep template.
5. Verify readiness before publishing or exercising the matching Netlify frontend.

K3s may enable the same settings through runtime ConfigMap/Secret values. The Calendar secret must be created out of band under `google-calendar-client-secret`; it must not be committed. The migration job requires only PostgreSQL and deliberately receives no OAuth secret.

## Staging verification

- Identity-only Google sign-in still works without Calendar consent.
- Connect Calendar from a private adult session; verify the chosen Google email is displayed.
- Deny consent and confirm no connection/token row is created and the browser returns to a clean error route.
- Select calendars, view timed, recurring, all-day, and daylight-saving-boundary events, and verify refresh.
- Confirm one household cannot inspect another household's sources or events.
- Confirm an adult in multiple households may choose different sources and that household switching preserves each configuration.
- Confirm a locked shared display may read configured events but cannot connect, configure, or disconnect until PIN elevation.
- Revoke Google access externally and confirm a stable reauthorization state with no event leakage.
- Disconnect and confirm all of that adult's household sources deactivate, replay fails, and Google events remain unchanged.
- Inspect browser storage, URLs, frontend source/maps, API responses, Netlify, Azure logs, and application logs for OAuth codes/tokens, client secrets, descriptions, attendees, and protected personal fields.
- Verify wall-display, phone, tablet, touch, mouse, keyboard, screen-reader, cold-start, partial-provider-failure, and stale-cache behavior.

## Azure staging status: 2026-08-19

The separate Calendar OAuth web client is active with the exact backend callback `https://api.egobrane.net/api/integrations/google-calendar/callback`. Its client secret exists only in Azure Key Vault and reaches the API through Container App secret reference `google-calendar-client-secret`; the migration job and frontend do not receive it. Identity-only Google sign-in continues to use its separate client and scopes.

The first successful token exchange exposed a strict scope-alias compatibility defect. Google returned HTTP 200 and the canonical identity scope `https://www.googleapis.com/auth/userinfo.email`; Family Dashboard incorrectly expected only the `email` alias and rejected the response before creating a connection. The correction validates identity through the signed ID token and requires the two exact Calendar data scopes, `calendar.calendarlist.readonly` and `calendar.events.readonly`. It does not weaken Calendar data access.

The corrected image `ghcr.io/egobrane/familyboard-backend@sha256:5252be746d8abbe56aa01c87c741eda42122884647654aac59f7ec52c69c4552` is active in Azure. The matching Netlify UI presents safe callback errors without exposing raw provider values. The owner verified consent denial without a saved connection, successful connection and connected-account display, household source selection and persistence, dashboard and Calendar event display, external revocation and reconnection, distinct multi-household selection, locked shared-display read/admin boundaries, timed, recurring, all-day, daylight-saving, and responsive-device behavior, and disconnect without Google event mutation. Inspection found no OAuth code, token, client secret, database credential, PIN material, pepper, or signing material in frontend JavaScript, browser storage, URLs, source control, or logs.

## Increment 2: controlled event creation

Increment 2 adds create-only behavior without weakening the read-only boundary for existing connections. The separate `GoogleCalendar__EventCreationEnabled` gate is false by default. When enabled, an adult administrator explicitly starts incremental Calendar authorization for `calendar.calendarlist.readonly` and `calendar.events`; existing read-only connections remain readable until that extra consent succeeds. Google sign-in scopes do not change.

An elevated shared display or ordinary private adult session may choose one active, adult-owned household source for creation, but only after Google reports `writer` or `owner` access. Configuration routes remain administrative and require parent elevation on shared displays. Routine `POST /api/households/{householdId}/calendar/events` requires active household membership, the selected session household, antiforgery, exact credentialed CORS, and a per-session/household rate limit. A locked shared display may create an event but must attribute it to an active household member; a private adult session defaults attribution to that adult's linked profile.

The create contract accepts a client-generated UUID idempotency key, target source ID, title, optional location/notes, all-day or offset-aware timed boundaries, IANA time zone for timed events, and optional member attribution. Titles, locations, notes, ranges, time-zone offsets, and a yesterday-to-two-years window are validated server-side. The backend derives a deterministic Google event ID from the idempotency key. Concurrent retries converge on that provider ID; reuse of the key with different details returns `409 calendar_idempotency_conflict`.

Google remains the source of truth. PostgreSQL stores only a SHA-256 request fingerprint, provider event ID, source/account/member attribution, shared-display flag, timestamps, status, and trace correlation. It stores no title, location, notes, start/end values, attendees, or local event copy. A successful creation rotates the disposable source-cache version so the next dashboard or Calendar read goes back to Google. This increment does not edit, delete, recur, invite attendees, add conferencing, synchronize in the background, or use webhooks.

Frontend routes and states:

- `/calendar/new` provides the responsive create form and explains that later edits/deletion happen in Google Calendar.
- `/households/{householdId}/calendars` exposes incremental authorization and writable-target selection inside the existing administration gate.
- disconnected, unavailable, read-only, missing-target, invalid member, validation, cooldown/rate-limit, revoked-token, provider failure, submission, recovered retry, and success behavior use stable ProblemDetails and semantic feedback.

No new npm or NuGet package and no new Azure resource or secret is required. The existing Calendar OAuth client secret, Data Protection key ring, managed identity, Key Vault, Container App, and PostgreSQL server are reused. Google Cloud consent configuration must add the sensitive `https://www.googleapis.com/auth/calendar.events` scope before activation; production use may require additional Google verification.

Activation order is deliberately fail-closed:

1. Publish the reviewed backend image and record its immutable digest.
2. Run the additive `AddGoogleCalendarEventCreation` migration and deploy that image with `enableGoogleCalendarEventCreation=false`.
3. Publish the matching Netlify frontend; its UI remains unavailable while the backend gate is false.
4. Add/approve the exact Calendar events scope in the existing separate Calendar OAuth client and consent configuration.
5. Set `enableGoogleCalendarEventCreation=true` and retain the same reviewed digest in the environment deployment parameters, commit that parameter-only change, and dispatch the protected staging workflow. The workflow now reconciles the approved runtime flag as well as the image.
6. Reauthorize Calendar incrementally, select one writable target, and complete the private/shared-display, idempotency, revocation, time-zone, accessibility, responsive, and leakage checks before recording staging success.

Rollback first disables only `GoogleCalendar__EventCreationEnabled`; read-only Calendar operation continues and receipt rows remain inert. A prior Increment 1 API can then be reactivated because the migration is additive. Google events already created are not deleted or changed during rollback.

## Increment 2 staging status: 2026-08-21

Migration execution `family-dashboard-staging-mig-027lwjn` applied `20260819210734_AddGoogleCalendarEventCreation` successfully. The reviewed multi-architecture image `ghcr.io/egobrane/familyboard-backend@sha256:028a47778229f74aae12725df0665f0a9042476169c6b69b7ec60ac35e40d318` is active on healthy revision `family-dashboard-staging-api--0000019`, which receives 100% of traffic. Both `GoogleCalendar__Enabled` and `GoogleCalendar__EventCreationEnabled` are `true`.

Event creation was initially unavailable because the protected GitHub deployment workflow migrated and replaced the image but did not apply the changed Bicep runtime parameter. A deliberate Container App configuration update supplied `GoogleCalendar__EventCreationEnabled=true` and created revision `0000019`. The follow-up workflow implementation now reconciles an approved non-secret setting allowlist from the reviewed parameters. The Netlify production deploy already contained the Increment 2 interface, but Safari continued serving a Workbox-pre-cached older shell. Unregistering the stale worker and clearing its Cache Storage exposed the current interface; the follow-up PWA lifecycle implementation makes update discovery reliable, blocks activation while a form is mounted, and allows safe idle wall-display activation. Both follow-ups still require CI and staging proof after publication.

The owner then completed incremental Google write authorization, selected a writable household calendar, and created a timed event. It appeared in Google Calendar on another device, confirming that Google remains the source of truth; Family Dashboard intentionally persisted only the non-sensitive idempotency receipt and no local event copy.

A fresh static scan of the deployed bundle found no OAuth secret/token, PostgreSQL host, parent-pepper, or backend secret-environment markers. Manual staging confirmation remains outstanding for locked shared-display creation with member attribution, private-session attribution, all-day creation, invalid range/time-zone handling, duplicate recovery, read-only target rejection, external revocation, multi-household write isolation, Calendar configuration PIN gates, responsive creation on every target device, and write-path inspection of browser storage, URLs, API/application logs, and event-detail handling. Deployed automated tests cover the core fail-closed household, administration, attribution, idempotency/concurrency, antiforgery, keyboard, responsive, and accessibility boundaries; those results are not represented as manual staging observations.

## Rollback

The migration is additive and the feature is gated. First set `GoogleCalendar__Enabled=false` and redeploy configuration; this stops authorization/configuration while leaving ciphertext dormant. A previous API revision can then be reactivated because it ignores the new tables. Do not delete the Data Protection key ring. Remove the Key Vault secret only after all connections have been locally disconnected or intentionally abandoned. Schema removal is not part of routine rollback; use a forward fix.

## Increment 3: controlled event editing and deletion

Increment 3 manages only events with a successful Family Dashboard creation receipt. Externally created, recurring, moved, structurally complex, or versionless events remain read-only. Google Calendar remains the sole event source of truth; PostgreSQL adds an append-only `CalendarEventMutationReceipt` containing an idempotency fingerprint, opaque relationships, actor/shared-display attribution, expected/result provider versions, operation status, timestamps, and trace correlation. Event content is never copied into the receipt.

The existing `calendar.events` grant supports update and delete, so no reauthorization, package, client secret, or Azure resource is added. `GoogleCalendar__EventManagementEnabled` defaults to false and requires both Calendar integration and event creation to be enabled. Private adults may manage eligible household events. Shared displays require current household parent-PIN elevation; mutations are attributed to the authenticated adult session rather than a child profile.

Routes are deliberately opaque and use the creation-receipt UUID rather than a Google event ID:

- `GET /api/households/{householdId}/calendar/managed-events/{managementId}`
- `PUT /api/households/{householdId}/calendar/managed-events/{managementId}`
- `POST /api/households/{householdId}/calendar/managed-events/{managementId}/delete`

Unsafe requests require credentialed CORS and antiforgery. Update and delete accept a client UUID and the last Google ETag. Google receives an `If-Match` precondition; stale versions return `409 calendar_event_version_conflict`. Same-key/same-body retries recover from the append-only receipt, while different bodies fail with `calendar_event_mutation_idempotency_conflict`. Delete requires an explicit confirmation value and treats a provider `404`/`410` during a pending retry as successful recovery. A successful mutation invalidates only the disposable source cache.

The frontend adds a large Manage action only to eligible full-list events and `/calendar/events/{managementId}/edit` for the responsive form and explicit “Delete from Google Calendar” confirmation dialog. It warns before unloading dirty forms. Deletion never offers a one-click list action. Recurrence instance/series mutation, attendee/conference/attachment changes, and management of externally sourced events remain deferred.

Deployment is migration-first through the immutable-image staging workflow. The workflow reads, applies, asserts, and on failure restores `GoogleCalendar__EventManagementEnabled` with the rest of its approved non-secret runtime allowlist. Rollback disables this gate; Calendar reads and creation continue, receipts remain inert, and no Google event is automatically reverted.

### Increment 3 staging status: 2026-08-26

Migration execution `family-dashboard-staging-mig-qg1bzhk` succeeded before healthy API revision `family-dashboard-staging-api--0000032` received 100% traffic. Public tag `sha-3343013` and the API and scheduled chore job all resolve to reviewed multi-architecture digest `sha256:93ac0319cc0f870c2d12059dd343bbeae7b5b069765342258ae0430570d2dfcf`. The approved runtime configuration has Calendar reads, creation, and event management enabled. Netlify production deploy `6a8eff2ae60931000898d742` serves the matching frontend commit and event-management interface.

The owner confirmed management is offered only for eligible simple Family Dashboard-created events, updates appear in Google Calendar, explicit deletion removes the provider event, and Google remains the only event-content source of truth. Externally created and recurring or otherwise ineligible events remain read-only. Stale provider versions fail safely, identical retries recover idempotently, shared-display mutations require parent-PIN elevation, household isolation holds, and the workflow is usable with touch, mouse, keyboard, screen reader, phone, tablet, and wall display. Inspection found no OAuth code or token, client secret, event content, database credential, PIN material, pepper, signing material, or privileged configuration in browser storage, URLs, frontend assets, source control, or logs.
