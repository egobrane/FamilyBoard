# Reward management

Reward Management Increment 1 is implemented in the current working tree and awaits deployment verification.

## Lifecycle

- Household adults create, edit, activate, and deactivate reward definitions. Historical definitions are retained.
- An active household member requests one active reward. The request snapshots the reward title, description, and point cost.
- Request creation atomically records a negative `RewardRedemption` point transaction, immediately reserving the cost. The request fails without writes if the member lacks the required balance.
- Every request requires adult review. Approval retains the reservation; rejection records an exact positive reversal. An approved request may be marked fulfilled or cancelled. Cancellation also releases the reservation through an exact reversal.
- `Fulfilled`, `Rejected`, and `Cancelled` are terminal. Historical redemption and ledger rows are never edited or deleted to erase history.

## Access and attribution

Catalog browsing, point-backed requests, and redemption history use household-member authorization. A private adult session defaults an omitted member to the signed-in adult; a shared display requires explicit attribution to an active household member. Definition administration, review, fulfillment, and cancellation require adult administration authorization and therefore parent-PIN elevation on a locked shared display.

Every lookup is household-scoped. Cross-household identifiers return not found. Unsafe requests use the existing credentialed cookie, exact-origin CORS, and antiforgery boundary.

## Deferred scope

Inventory and quantity limits, images and uploads, notifications, expiration, provider fulfillment, editing fulfilled history, and arbitrary client-supplied debits are not part of Increment 1.
