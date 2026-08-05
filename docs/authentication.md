# Authentication Boundary

Authentication is deliberately not implemented in the first milestone.

## Future design constraints

- Authentication identities are separate from household-member profiles. A child profile may have no login.
- Google OAuth uses authorization-code flow through the backend.
- OAuth client secrets and Google refresh tokens never reach browser JavaScript.
- Refresh tokens are encrypted at rest and are only decrypted by the backend integration service.
- The backend enforces household membership and action-level authorization on every protected endpoint.
- Frontend route guards are usability features, not security controls.
- Secure, HTTP-only cookies are preferred for browser sessions.
- Production should use sibling custom domains, such as `dashboard.example.com` and `api.dashboard.example.com`, to avoid third-party-cookie limitations.

Netlify Deploy Preview origins are dynamic. Before authenticated previews are enabled, define whether they use a dedicated non-production backend, an explicit per-preview allowlist, or no authenticated API access. Broad wildcard credentialed CORS is not acceptable.

The token storage model, encryption-key lifecycle, session revocation, child access, adult administration, and account-to-household invitation model require a separate approved design.
