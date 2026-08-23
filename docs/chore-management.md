# Chore Management

Chore Management Increment 1 implements product-owned chore definitions, one-time assignments, attributed completion, and adult review. Increment 2 adds household-local recurring schedules and automatic assignment generation. Points, rewards, notifications, and Calendar coupling remain intentionally deferred.

## Behavior

- Adults create, edit, activate, and deactivate reusable household chore definitions.
- Adults assign an active definition once to any active household member with a required household-local due date and optional time.
- Definition and due-date details are snapshotted onto the assignment so historical meaning does not change when settings or definitions change.
- Routine completion is available to authenticated household members. A private adult session defaults attribution to that adult; a shared display requires an explicit active-member choice.
- Completion moves the assignment to `awaitingReview`. An adult may approve it, making the assignment complete, or reject it, preserving the attempt and returning the assignment to pending.
- Adults may skip a pending assignment. Definitions, assignments, completions, and skip/review records are retained rather than deleted.
- No point transaction is created in this increment.

## API

Routine household-member routes:

- `GET /api/households/{householdId}/chores/dashboard`
- `GET /api/households/{householdId}/chores/participants`
- `GET /api/households/{householdId}/chore-assignments?view=active|history`
- `POST /api/households/{householdId}/chore-assignments/{assignmentId}/completions`

Adult-administration routes:

- `GET|POST /api/households/{householdId}/chore-definitions`
- `PATCH /api/households/{householdId}/chore-definitions/{definitionId}`
- `POST /api/households/{householdId}/chore-definitions/{definitionId}/activate|deactivate`
- `POST /api/households/{householdId}/chore-assignments`
- `POST /api/households/{householdId}/chore-assignments/{assignmentId}/skip`
- `GET /api/households/{householdId}/chore-completions?status=pendingReview`
- `POST /api/households/{householdId}/chore-completions/{completionId}/review`

All unsafe requests require the credentialed application session and antiforgery header. Administration and review require the adult policy; on a shared display that policy additionally requires current parent-PIN elevation for the same household. Cross-household resources are not disclosed.

## Concurrency and retries

Definitions, assignments, and completions use explicit versions. Stale writes return a conflict rather than overwriting newer state. Client-generated UUID request IDs make definition creation, assignment creation, and completion retryable; reuse with a different payload is rejected. A database constraint permits only one pending completion per assignment.

## Time zones

Assignment input is interpreted in the household's IANA time zone. Date-only work is due at the end of that household-local day. Nonexistent or ambiguous daylight-saving local times fail validation instead of silently shifting. Overdue state is derived from the saved UTC instant while the original local values and zone remain visible.

## Staging checklist

After the migration and matching frontend deploy:

1. Create and edit a definition, then deactivate and reactivate it.
2. Assign it to an active child and confirm it appears on the dashboard and full chore list.
3. Complete it from a private adult session and confirm adult attribution.
4. Complete another from a locked shared display after explicitly choosing a member.
5. Confirm shared-display administration and review remain PIN-gated.
6. Reject a completion, retry it, then approve it; confirm both attempts remain in history and no points are awarded.
7. Skip a pending assignment and confirm it moves to history.
8. Verify another household cannot read or mutate the records.
9. Exercise touch, mouse, keyboard, screen reader, phone, tablet, and wall-display layouts.

## Increment 1 staging result

The owner completed the checklist successfully on 2026-08-22 against the matching Azure backend and Netlify frontend. Migration `AddChoreManagementWorkflow` succeeded before revision `family-dashboard-staging-api--0000021` received traffic. Definition creation, editing, activation and deactivation; one-time assignment; dashboard and full-list display; private-session and shared-display attribution; approval; rejection, retry, and later approval; skipping; historical retention; parent-PIN enforcement; household isolation; and the documented device and input modes all behaved as expected.

Approved completions do not award points in Increment 1. This is intentional: the point ledger exists as a foundation, but connecting reviewed completions to point transactions is deferred to a later chore increment.

## Recurring schedules

An adult can configure daily, every-N-day, selected-weekday, weekly, or every-N-week schedules under `/households/{householdId}/chores`. The configured time is the household-local due time. Assignments are generated up to 36 hours in advance, so “every day at 8 AM” means due at 8 AM and visible beforehand.

Generated assignments snapshot their definition, member, occurrence date, local due time, IANA time zone, derived UTC instant, and daylight-saving resolution. Editing a schedule changes only ungenerated work. Paused and blocked periods do not create assignments; inactive definitions or members block their schedules until an adult reviews and resumes them. Schedules and generated assignments are retained rather than deleted.

Administrative routes are:

- `GET|POST /api/households/{householdId}/chore-schedules`
- `GET|PATCH /api/households/{householdId}/chore-schedules/{scheduleId}`
- `POST /api/households/{householdId}/chore-schedules/{scheduleId}/pause|resume`
- `POST /api/households/{householdId}/chore-schedules/preview`

The portable generator command is `FamilyDashboard.Api --generate-chore-assignments`. Docker Compose exposes it through the opt-in `tools` profile, K3s uses an hourly CronJob with `concurrencyPolicy: Forbid`, and Azure uses an hourly Consumption scheduled job. A PostgreSQL advisory lock and a unique schedule-occurrence index make overlapping and retried runs safe without Hangfire, Quartz, or another scheduler database.

Missed active occurrences are generated late in batches of at most 100 rather than silently discarded. Spring-forward gaps shift to the first valid local time; fall-back ambiguity chooses the earlier instant. Existing generated assignments remain unchanged after a household time-zone edit.
