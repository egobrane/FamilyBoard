# Frontend Deployment to Netlify

1. Link the GitHub repository to a Netlify site.
2. Netlify reads the root `netlify.toml`, builds from `src/frontend`, and publishes `dist`.
3. Set `VITE_API_BASE_URL` separately for production and Deploy Preview contexts.
4. Set `VITE_APP_NAME` if the default product name should change.
5. Confirm the production API allows the exact production frontend origin.

For the approved staging topology, set the production-context value to `VITE_API_BASE_URL=https://api.egobrane.net` only after Azure managed TLS and both health endpoints are proven. Trigger a clean Netlify rebuild and verify the deployed bundle no longer contains `http://localhost:8080`.

Pull requests receive Netlify Deploy Previews through Git integration. The preview is the same portable static build; it uses no Netlify Functions, Identity, or runtime APIs.

Deploy Previews intentionally do not share the authenticated staging API until an explicit preview-origin and credential policy is approved.

Do not put backend secrets in Netlify build settings. Even values hidden in its UI become public if a Vite build embeds them.

The SPA redirect and security headers are isolated in `netlify.toml`. `/sw.js`, `/index.html`, and the manifest revalidate on every use; content-hashed `/assets/*` files are immutable for one year. Workbox keeps the current release active until the downloaded worker is deliberately activated.

The mounted PWA update provider checks at startup, every fifteen minutes, when the tab becomes visible, and when it regains focus. A persistent accessible banner offers `Update now`. Any mounted form disables activation so an in-progress household, PIN, invitation, or Calendar operation cannot be interrupted. An online client with no form may activate after five idle minutes, which prevents an unattended wall display from remaining stale indefinitely. Offline clients retain the last working shell.

After publishing a PWA lifecycle change, verify it with two real consecutive Netlify releases in Safari and on the wall display. Confirm that the older client discovers the newer worker, an open form blocks activation, leaving the form permits activation, the new bundle controls the page after reload, and manual service-worker/cache deletion is unnecessary.

The hardened lifecycle bundle was published successfully on 2026-08-21. Public inspection on 2026-08-22 confirmed that the production bundle contains the accessible update prompt and form-protection behavior, and that direct Netlify delivery serves `/sw.js` with the configured `no-cache, max-age=0, must-revalidate` policy. The Cloudflare-fronted `family.egobrane.net` response currently overrides that endpoint with a four-hour edge cache lifetime. Add a narrowly scoped Cloudflare cache rule for `/sw.js` that bypasses caching or honors the origin policy before treating immediate update discovery on the custom domain as proven. Hashed assets should remain immutable.
