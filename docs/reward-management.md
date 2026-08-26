# Reward management

Reward Management Increment 1 is deployed and staging verified. The matching frontend supports definition administration, catalog browsing, attributed requests, review, fulfillment, cancellation, and retained history.

## Lifecycle

- Household adults create, edit, activate, and deactivate reward definitions. Historical definitions are retained.
- An active household member requests one active reward. The request snapshots the reward title, description, and point cost.
- Request creation atomically records a negative `RewardRedemption` point transaction, immediately reserving the cost. The request fails without writes if the member lacks the required balance.
- Every request requires adult review. Approval retains the reservation; rejection records an exact positive reversal. An approved request may be marked fulfilled or cancelled. Cancellation also releases the reservation through an exact reversal.
- `Fulfilled`, `Rejected`, and `Cancelled` are terminal. Historical redemption and ledger rows are never edited or deleted to erase history.

## Access and attribution

Catalog browsing, point-backed requests, and redemption history use household-member authorization. A private adult session defaults an omitted member to the signed-in adult; a shared display requires explicit attribution to an active household member. Definition administration, review, fulfillment, and cancellation require adult administration authorization and therefore parent-PIN elevation on a locked shared display.

Every lookup is household-scoped. Cross-household identifiers return not found. Unsafe requests use the existing credentialed cookie, exact-origin CORS, and antiforgery boundary.

## Deployment verification

The additive `AddRewardRedemptionWorkflow` migration ran before the first reward API revision received traffic. The owner verified definition creation/editing/activation/deactivation, catalog and balance display, private and shared-display attribution, insufficient-balance rejection, reservation, approval, rejection, cancellation, fulfillment, append-only release, point-cost snapshots, history, parent-PIN enforcement, household isolation, and responsive input modes.

The first authorized production catalog read exposed an EF Core translation defect caused by filtering after a balance DTO projection. Azure logs identified the query without exposing credentials or request secrets. The service now filters active household-member entities, aggregates ledger balances separately, and maps the response in memory. An authorized PostgreSQL catalog regression test covers the corrected path; the current backend suite passes 107 tests with PostgreSQL enabled and no skips.

## Deferred scope

Inventory and quantity limits, images and uploads, notifications, expiration, provider fulfillment, editing fulfilled history, and arbitrary client-supplied debits are not part of Increment 1.
