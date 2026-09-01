# Testing

Google Tasks Increment 1 adds backend protection tests for connection- and token-kind-specific ciphertext, protected OAuth state, and protected pagination cursors. Increment 2 adds exact incremental-scope, PostgreSQL write-target/receipt, attribution, idempotency, task-content-absence, credentialed antiforgery, creation-form, and status-control coverage. Provider doubles exercise creation and ETag status changes without personal Google credentials. Live incremental consent, ambiguous provider outcomes, external conflicts, shared-display attribution, multi-household target isolation, and leakage inspection remain owner staging checks.

Google Tasks Increment 2 local validation passed all 116 backend tests against the dedicated disposable PostgreSQL 18 database with no skips, 35 frontend component tests, and 32 wall-display/phone Playwright tests. Frontend lint, the production PWA build, production backend/frontend container builds, dependency security checks, Bicep compilation, Docker Compose validation, and both Kubernetes overlays also passed.

Dashboard Personalization and Weather Increment 1 passes 122 backend tests against PostgreSQL 18 with no skips, 39 frontend component tests, and all 38 wall-display/phone Playwright scenarios. Coverage includes typed persistence, validation, authorized settings persistence, household isolation, NWS normalization and bounded retry, personalized dashboard copy, weather dialog accessibility, private Azure/Compose/K3s storage rendering, and a real-cookie multipart-upload regression test. The owner subsequently verified the Azure/Netlify path, including upload, authenticated delivery, replacement, removal, focal positioning, fallback, weather configuration and forecast display. The owner also completed the responsive and sensitive-data checklist; no storage key, SAS token, private blob URL, OAuth token, PIN material, or other privileged value appeared in the inspected browser, source, URL, or log surfaces.

The owner completed the core Increment 2 staging path on 2026-08-29: incremental broad-scope consent, writable-list selection, task creation, task completion, and provider reflection on another device succeeded. Reopening, date-only due dates, shared-display attribution, duplicate and ambiguous submission recovery, provider conflicts, revocation, multi-household isolation, responsive devices, and live leakage inspection remain automated or unconfirmed checks rather than owner-observed staging evidence.

## Frontend

```sh
cd src/frontend
npm ci
npm run lint
npm test
npm run build
```

Vitest and React Testing Library cover authentication states, first-household setup, multi-household selection, invitation creation and fragment removal, shared-display PIN entry, credentialed/antiforgery API behavior, Calendar events/source selection, controlled event creation and managed-event navigation, chore-list completion states, recurring-schedule summaries and weekday controls, real point-history semantics, dashboard semantics, and keyboard and pointer navigation.

## Browser tests

```sh
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

Playwright runs Chromium at a 1920×1080 touch-enabled wall-display viewport and a Pixel phone viewport. It checks layout overflow, a single-row five-item dock, usable dashboard card widths, pointer-click and mouse-drag workspace navigation, visible authenticated content, mouse/keyboard navigation, read-only Calendar navigation and source selection, controlled event creation, explicit chore-completion attribution, daily 8 AM schedule creation, PWA form-update protection, secure logout, invitation creation/acceptance, raw-fragment removal, shared-display locked administration and PIN unlock, and serious automated accessibility findings using deterministic API fixtures. Emulation supplements but does not replace physical wall-touchscreen, iOS Safari, and Android Chrome testing.

The UI Cohesion and Workspace Navigation increment passed 39 frontend component tests and all 34 wall-display/phone Playwright scenarios locally, and current-commit CI repeated the frontend, responsive/accessibility, backend, container, Bicep, Compose, and K3s checks successfully. The browser suite verifies the repaired Tasks-card width, five-tab single-row dock, adjacent-route mouse dragging, pointer tab selection, browser routes, vertical-gesture protection, reduced motion, scoped route focus, preserved control focus indicators, and the horizontal-overflow boundary. The owner confirmed the cohesive layout, matching bordered feature surfaces, and removal of the page-sized outline in production; physical touch-drag behavior and subjective transition feel remain owner-operated checks.

## Backend

```sh
dotnet test FamilyDashboard.sln
```

The liveness, return-URL, Google option, Calendar token/state protection, invitation-token, parent-PIN hashing, chore due-time/DST and recurrence calculations, CORS, and production fail-closed authentication tests can run without PostgreSQL. Migration, chore lifecycle/idempotency/history, schedule generation and duplicate prevention, endpoint, Calendar constraints/household isolation/event-creation idempotency and concurrency, atomic-bootstrap-and-selection, per-session household selection and elevation, concurrent last-adult, invitation isolation/revocation/email binding/concurrent acceptance, PIN cooldown/audit/administration, Google identity mapping, session renewal/revocation/expiration, disabled-account, and antiforgery tests run when `TEST_POSTGRES_CONNECTION_STRING` points to a dedicated disposable test database. These tests delete and recreate that database, so it must never target shared or production data.

CI supplies an ephemeral PostgreSQL service, then restores, builds, tests, builds production containers, and renders the K3s manifests. Builds or required tests must be green before a milestone is considered complete.

Chore Management Increment 1 also passed its owner-operated Azure/Netlify staging checklist on 2026-08-22. The verified paths covered definition lifecycle, one-time assignment, dashboard and full-list rendering, private and shared-display attribution, approval, rejection/retry/approval, skipping and retained history, shared-display parent-PIN enforcement, household isolation, and touch, mouse, keyboard, screen-reader, phone, tablet, and wall-display behavior. No point award was expected because point transactions remain outside Increment 1.

Chore Management Increment 2 is deployed with the matching Azure API and Netlify PWA. The additive migration, automatic immutable-digest handoff, corrected scheduled-job provisioning, and manual generator execution succeeded. Independent Azure inspection also shows successful hourly executions at minute 7. Daily/weekday generation, pause/resume, inactive dependencies, time-zone and daylight-saving behavior, duplicate prevention, and responsive interaction remain automated coverage unless separately recorded as owner-observed staging checks.

Chore Management Increment 3 local validation passed 102 backend tests against disposable PostgreSQL 18 with no skips, 30 frontend component tests, and 26 wall-display/phone Playwright tests. Coverage includes migration preservation, point snapshots, atomic and concurrent approval, single-award idempotency, zero-point approval, rejection/retry, recurring snapshots, derived balances, inactive-member history, adjustments, exact reversals, household isolation, shared-display correction gates, pointer/keyboard navigation, responsive layout, and serious automated accessibility checks.

Reward Management Increment 1 has PostgreSQL endpoint coverage for authorized catalog reads and balances, atomic point reservation, exact append-only release, insufficient-balance rejection, concurrent overspend prevention, and household isolation. Frontend component coverage verifies explicit member attribution and keyboard-operable redemption actions. The owner completed the staging checklist across definition lifecycle, catalog/balances, private and shared-display attribution, review and terminal states, point-cost snapshots, parent-PIN enforcement, isolation, and responsive input modes. After the catalog query correction, the complete backend suite passed 107 tests against disposable PostgreSQL 18 with no skips.

The owner completed the Increment 3 staging deployment path on 2026-08-25 and confirmed recurring chores, definition point edits, point-bearing completion/approval, balance and history display, and administrative negative correction. Zero-point approval, concurrent duplicate prevention, inactive-member history, household isolation, shared-display elevation, and responsive behavior retain automated coverage but are not recorded as separate owner checks.

Calendar Increment 1 was also exercised against real staging OAuth and Google Calendar data on 2026-08-19. Owner-confirmed checks covered consent denial, connection, connected-account display, source persistence, dashboard and full Calendar reads, revocation and reconnection, multi-household isolation, locked shared-display access, timed/recurring/all-day/daylight-saving events, responsive input modes, and disconnect without provider-event deletion. Automated provider doubles remain necessary in CI so tests do not require personal Google credentials or mutate external calendars.

Calendar Increment 2 was exercised in staging on 2026-08-21. Owner-confirmed checks cover incremental write consent, selection of a writable household calendar, timed-event creation, and the event appearing through Google Calendar on another device. Automated tests cover exact incremental scope selection, fail-closed cross-household and locked-administration behavior, required shared-display member attribution, private-adult default attribution, single-target persistence, provider-owned/idempotent creation, concurrent duplicate convergence, credentialed antiforgery requests, keyboard operation, responsive layout, and serious accessibility findings. Live all-day creation, invalid input, replay recovery, read-only-target rejection, external revocation, multi-household write isolation, shared-display creation, phone/tablet/wall-display creation, and write-path leakage inspection remain owner staging checks rather than confirmed manual results.

Calendar Increment 3 was exercised in staging on 2026-08-26. The owner confirmed eligible Family Dashboard-created simple-event management, provider-visible updates, explicit provider deletion, read-only external and recurring boundaries, safe stale-version conflict handling, idempotent retry recovery, shared-display parent-PIN enforcement, household isolation, responsive input modes, screen-reader behavior, and sensitive-data leakage inspection. Automated provider-double, API/PostgreSQL, component, and Playwright coverage remains deterministic and avoids personal Google credentials while exercising version preconditions, mutation receipt uniqueness, antiforgery, authorization, destructive confirmation, dirty-form protection, keyboard operation, responsive layout, and serious accessibility findings.

PWA update component tests cover reliable prompt delivery and form blocking. Playwright repeats the form-blocking boundary at wall-display and phone sizes. The production build is additionally inspected for a waiting worker that activates only after the application sends `SKIP_WAITING`. The owner completed the physical Safari and wall-display Version 1-to-Version 2 proof on 2026-08-28: form protection, safe idle activation, session continuity, and update without unregistering the worker or clearing caches succeeded.

Deploy Preview builds deliberately compile against `https://api-preview.invalid`. Pull request #24 proved the deployed artifact contained that reserved origin and no production API origin; the owner confirmed the expected static shell and fail-closed unavailable state. Preview validation covers routes, responsive assets, manifest, service worker, and safe API-unavailable behavior without production cookies, credentials, CORS expansion, or household data. Authenticated staging behavior remains covered by the exact production origin and owner-operated staging checks.

Dashboard Personalization Increment 2 adds PostgreSQL/API coverage for private member-photo upload, decoder-based file validation, sanitized authenticated delivery, private revalidation caching, ETag `304`, focal positioning, optimistic concurrency, removal, retired metadata, and cross-household cloaking. Frontend coverage verifies authenticated variant selection, focal rendering, failed-image initials fallback, explicit removal confirmation, and antiforgery-protected API requests. The complete local backend suite passes 123 tests against disposable PostgreSQL 18, all 41 frontend component tests pass, the production PWA builds, and all 40 wall-display/phone Playwright scenarios pass including the upload/removal dialog and serious accessibility scan. The staging owner checklist remains required before recording deployment completion.
