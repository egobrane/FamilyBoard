# Frontend Deployment to Netlify

1. Link the GitHub repository to a Netlify site.
2. Netlify reads the root `netlify.toml`, builds from `src/frontend`, and publishes `dist`.
3. Set `VITE_API_BASE_URL` separately for production and Deploy Preview contexts.
4. Set `VITE_APP_NAME` if the default product name should change.
5. Confirm the production API allows the exact production frontend origin.

For the approved staging topology, set the production-context value to `VITE_API_BASE_URL=https://api.egobrane.net` only after Azure managed TLS and both health endpoints are proven. Trigger a clean Netlify rebuild and verify the deployed bundle no longer contains `http://localhost:8080`.

Pull requests receive Netlify Deploy Previews through Git integration. The preview is the same portable static build; it uses no Netlify Functions, Identity, or runtime APIs. `netlify.toml` forces the Deploy Preview build to use the reserved non-resolving origin `https://api-preview.invalid`, even if a broader Netlify UI variable was accidentally scoped to every deploy context.

Deploy Previews intentionally do not share the authenticated staging API. Production keeps host-only `SameSite=Lax` session and antiforgery cookies plus exact credentialed CORS for `https://family.egobrane.net`; dynamic `*.netlify.app` preview origins must not be added to that boundary. A preview therefore proves the build, static routes, assets, responsive shell, manifest, service worker, and safe unavailable state without receiving production credentials or household data. A future authenticated preview requires a separately approved same-site preview API and data environment.

Do not put backend secrets in Netlify build settings. Even values hidden in its UI become public if a Vite build embeds them.

The SPA redirect and security headers are isolated in `netlify.toml`. `/sw.js`, `/index.html`, and the manifest revalidate on every use; content-hashed `/assets/*` files are immutable for one year. Workbox keeps the current release active until the downloaded worker is deliberately activated.

The mounted PWA update provider checks at startup, every fifteen minutes, when the tab becomes visible, and when it regains focus. A persistent accessible banner offers `Update now`. Any mounted form disables activation so an in-progress household, PIN, invitation, or Calendar operation cannot be interrupted. An online client with no form may activate after five idle minutes, which prevents an unattended wall display from remaining stale indefinitely. Offline clients retain the last working shell.

After publishing a PWA lifecycle change, verify it with two real consecutive Netlify releases in Safari and on the wall display. Confirm that the older client discovers the newer worker, an open form blocks activation, leaving the form permits activation, the new bundle controls the page after reload, and manual service-worker/cache deletion is unnecessary.

The hardened lifecycle bundle was published successfully on 2026-08-21. Public inspection on 2026-08-26 confirmed that the production bundle contains the accessible update prompt and form-protection behavior and that direct Netlify delivery serves `/sw.js` with the configured `no-cache, max-age=0, must-revalidate` policy. Cloudflare currently returns the same worker body but overrides it with a four-hour edge cache lifetime. Add a narrowly scoped bypass rule matching only host `family.egobrane.net` and path `/sw.js`, purge that single URL, and complete the two-version physical-device proof before treating immediate custom-domain update discovery as proven. Hashed assets should remain immutable.
