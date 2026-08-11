# Authentication Boundary

Real authentication is deliberately not implemented yet. Identity Increment 1 adds API contracts and a resource-authorization seam, but it does not register an authentication scheme, create sessions, map identity endpoints, or trust a client-supplied identity.

Production household authorization currently uses a deny-all access evaluator. Increment 2 must replace it with persistence-backed household membership lookup before protected household endpoints are activated. Backend tests use a test-assembly-only header scheme to exercise authorization deterministically; that scheme is not available in the running application.

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
