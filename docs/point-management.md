# Point Management

Family Dashboard derives every household-member balance from an append-only signed point ledger. Chore approval creates positive awards, reward requests create negative reservations, releases and corrections create compensating entries, and no endpoint directly overwrites a balance.

## Quick deductions

Adults may define reusable household deductions such as **Hitting someone — 500 points**. A definition stores a positive magnitude so its administrative meaning is unambiguous; applying it snapshots the current title and records the negative amount in `PointTransactions`. Editing a definition changes only future applications. Deactivation removes it from the quick-action area while retaining its definition and every historical transaction.

Applying a deduction requires an active household member and an explicit confirmation. Negative balances are permitted so the configured consequence is recorded exactly instead of being silently capped at zero. The existing reversal endpoint corrects a mistake by appending the exact opposite amount; it never edits or deletes the original deduction.

The database retains an optional definition relationship on each quick-deduction transaction. The signed amount and description remain self-contained snapshots, so history stays meaningful after a definition is edited or deactivated. Applications use a household-scoped idempotency key, and replaying the same request returns the original transaction rather than deducting twice.

## API

Read and administer definitions:

- `GET /api/households/{householdId}/point-deduction-definitions`
- `POST /api/households/{householdId}/point-deduction-definitions`
- `PATCH /api/households/{householdId}/point-deduction-definitions/{definitionId}`
- `POST /api/households/{householdId}/point-deduction-definitions/{definitionId}/activate|deactivate`

Apply and correct ledger activity:

- `POST /api/households/{householdId}/point-deduction-definitions/{definitionId}/applications`
- `POST /api/households/{householdId}/point-transactions/{transactionId}/reverse`

Every unsafe route requires the application cookie and antiforgery token. Definition reads and changes, deduction application, manual correction, and reversal require an active adult. A shared-display session additionally requires current parent-PIN elevation for that household. Cross-household identifiers return the established not-found boundary.

Titles contain 1–120 trimmed characters, and deduction magnitudes range from 1 through 10,000 points. Definition edits and lifecycle changes require the current optimistic version. Applications require a client-generated UUID and the definition version displayed for confirmation. Stable `ProblemDetails` distinguish missing or inactive definitions, inactive members, idempotency conflicts, and stale versions.

## Interface and operational behavior

The household Points administration screen presents a photo-backed member picker, large quick-deduction cards, and a destructive confirmation naming the member, reason, and amount. Definition lifecycle controls remain separate from application. Manual signed corrections are retained as an advanced escape hatch.

The feature introduces no provider, secret, scheduled process, or new Azure resource. Deployment applies `AddPointDeductionDefinitions` before the matching API. The migration is additive: it creates the definition table and adds a nullable relationship to existing point transactions. An older API ignores both, while rollback after new deductions should preserve the schema and use an application forward fix so audit history is never discarded.

## Staging checklist

1. Create **Hitting someone** for 500 points, edit it, then deactivate and reactivate it.
2. Apply it to an active member and confirm the balance falls by exactly the displayed amount, including below zero if applicable.
3. Edit the definition and apply it again; confirm each history row retains its original title and amount.
4. Double-tap or retry one application and confirm only one ledger transaction exists.
5. Reverse one application and confirm both the original and compensating entry remain visible.
6. Confirm inactive members and inactive definitions cannot receive new quick deductions.
7. Confirm another household cannot read or apply the definition.
8. On the locked wall display, confirm definition access and application require parent-PIN elevation.
9. Exercise touch, mouse, keyboard, screen reader, phone, tablet, dark mode, and wall-display layouts.

