# Google Calendar Increment 1

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

Routine `GET /api/households/{householdId}/calendar/events?from=...&to=...&cursor=...` requires only active household membership. It therefore remains available on a locked shared display. Ranges must be positive and no longer than 31 days. Event pagination uses a short-lived, Data-Protection-protected opaque cursor; raw Google page tokens never reach JavaScript.

Unsafe requests use the existing credentialed cookie, exact-origin CORS, and `X-CSRF-TOKEN` antiforgery boundary. Calendar callback state is purpose-protected, expires after ten minutes, and is bound to the initiating application account, session, household, nonce, and validated local return path. A `Secure`, `HttpOnly`, callback-path-scoped, `SameSite=Lax` correlation cookie contains only a SHA-256 state digest and is deleted on callback. The callback redirects to a clean frontend URL and never places OAuth codes, tokens, provider errors, or personal calendar data in it.

## Token and event handling

Access and refresh tokens are encrypted separately with purpose strings containing the connection ID and token kind. Ciphertext is stored in PostgreSQL; plaintext exists only transiently in backend memory. Azure uses the existing private Blob-backed Data Protection key ring, Key Vault wrapping key, and runtime managed identity. Loss of that key ring requires reconnection.

The API refreshes an access token shortly before expiry. `invalid_grant`, unreadable ciphertext, provider revocation, or a disabled owner transitions the connection to a fail-closed reauthorization state. Disconnect attempts Google revocation and always removes local token ciphertext and deactivates every source belonging to the connection, even when Google's revocation endpoint is unavailable.

Event retrieval occurs at request time through `IGoogleCalendarProviderClient`. Normalized results include only title, start/end, all-day status, source name/color, time zone, and location. Descriptions, attendees, organizer addresses, conferencing, attachments, reminders, and extended properties are not returned. Recurring instances are expanded by Google with `singleEvents=true`; all-day start/end values remain date-only with Google's exclusive end-date semantics. Timed events preserve their ISO instant/offset.

Each API replica uses a disposable in-memory cache: two minutes fresh and up to fifteen minutes for stale fallback. Cache loss is harmless. A partial provider failure returns healthy sources plus warnings; a cached source may be marked stale. There is no Redis, polling job, sync token, durable event cache, or webhook renewal cost in this increment.

## Frontend behavior

- `/calendar` is a routine, touch-first seven-day view available to household members.
- The dashboard `Today` card reads the selected household's configured calendars.
- `/households/{householdId}/calendars` is an adult administrative screen inside the existing parent-access gate.
- Adults connect or reconnect, see the connected Google email, choose household-visible calendars, see sources supplied by other adults, and confirm global disconnect.
- Loading, empty, disconnected, consent-denied, reauthorization, partial, stale, provider-failure, and success states use semantic status/alert feedback.

Controls retain large touch targets, visible focus, keyboard operation, screen-reader labels, responsive phone layout, and mouse support. The shared wall display may show personal plans, so adults must deliberately choose only calendars appropriate for household visibility.

## Configuration and activation

Calendar is disabled by default. Deploy the additive migration and API image while disabled, then:

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

## Rollback

The migration is additive and the feature is gated. First set `GoogleCalendar__Enabled=false` and redeploy configuration; this stops authorization/configuration while leaving ciphertext dormant. A previous API revision can then be reactivated because it ignores the new tables. Do not delete the Data Protection key ring. Remove the Key Vault secret only after all connections have been locally disconnected or intentionally abandoned. Schema removal is not part of routine rollback; use a forward fix.
