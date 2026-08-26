# Testing

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

Playwright runs Chromium at a 1920×1080 touch-enabled wall-display viewport and a Pixel phone viewport. It checks layout overflow, visible authenticated content, mouse/keyboard navigation, read-only Calendar navigation and source selection, controlled event creation, explicit chore-completion attribution, daily 8 AM schedule creation, PWA form-update protection, secure logout, invitation creation/acceptance, raw-fragment removal, shared-display locked administration and PIN unlock, and serious automated accessibility findings using deterministic API fixtures. Emulation supplements but does not replace physical wall-touchscreen, iOS Safari, and Android Chrome testing.

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

PWA update component tests cover reliable prompt delivery and form blocking. Playwright repeats the form-blocking boundary at wall-display and phone sizes. The production build is additionally inspected for a waiting worker that activates only after the application sends `SKIP_WAITING`; physical Safari/installed-PWA update timing cannot be fully simulated by these tests.

Deploy Preview builds deliberately compile against `https://api-preview.invalid`. Preview validation covers the static application shell, routes, responsive assets, manifest, service worker, and safe API-unavailable state without production cookies, credentials, CORS expansion, or household data. Authenticated staging behavior remains covered by the exact production origin and owner-operated staging checks.
