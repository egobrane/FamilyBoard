# Authentication Boundary

Real authentication is deliberately not implemented yet. Identity Increment 2 maps the identity and household endpoints and persists accounts, external identity references, household profiles, and account-to-household memberships. Production registers an unavailable authentication handler so protected endpoints fail closed with a stable `401 ProblemDetails` response until Google sign-in replaces that seam. It never trusts a client-supplied identity.

Household authorization now uses persistence-backed membership lookup and requires an active account, household, and linked member profile. Backend tests use a test-assembly-only header scheme to exercise the protected endpoints deterministically; that scheme is not available in the running application. Consequently, the deployed API exposes the contracts but no production caller can use them until Increment 3 introduces Google sign-in and revocable sessions.

The accepted identity, session, household-membership, invitation, Google sign-in, shared-display, and parent-PIN design is recorded in [ADR 0002](decisions/0002-identity-and-household-access.md).

## Future design constraints

- Authentication identities are separate from household-member profiles. A child profile may have no login.
- Google OAuth uses authorization-code flow through the backend.
- OAuth client secrets and Google refresh tokens never reach browser JavaScript.
- Refresh tokens are encrypted at rest and are only decrypted by the backend integration service.
- The backend enforces household membership and action-level authorization on every protected endpoint.
- Frontend route guards are usability features, not security controls.
- Secure, HTTP-only cookies are preferred for browser sessions.
- An adult may leave a session in shared-display mode for routine household use. Backend policies require a recent parent-PIN elevation for administrative actions; hiding controls in the frontend is not authorization.
- Production should use sibling custom domains, such as `dashboard.example.com` and `api.dashboard.example.com`, to avoid third-party-cookie limitations.

Netlify Deploy Preview origins are dynamic. Before authenticated previews are enabled, define whether they use a dedicated non-production backend, an explicit per-preview allowlist, or no authenticated API access. Broad wildcard credentialed CORS is not acceptable.

Calendar/Tasks token storage, the Data Protection key lifecycle, the exact parent-PIN protection parameters, and any future separately provisioned wall-display credential remain separate approval decisions.
