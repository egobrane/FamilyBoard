# Frontend Deployment to Netlify

1. Link the GitHub repository to a Netlify site.
2. Netlify reads the root `netlify.toml`, builds from `src/frontend`, and publishes `dist`.
3. Set `VITE_API_BASE_URL` for production; the checked-in Deploy Preview context deliberately overrides it with `https://api-preview.invalid`.
4. Set `VITE_APP_NAME` if the default product name should change.
5. Confirm the production API allows the exact production frontend origin.

For the approved staging topology, set the production-context value to `VITE_API_BASE_URL=https://api.egobrane.net` only after Azure managed TLS and both health endpoints are proven. Trigger a clean Netlify rebuild and verify the deployed bundle no longer contains `http://localhost:8080`.

Pull requests receive Netlify Deploy Previews through Git integration. The preview is the same portable static build; it uses no Netlify Functions, Identity, or runtime APIs. `netlify.toml` forces the Deploy Preview build to use the reserved non-resolving origin `https://api-preview.invalid`, even if a broader Netlify UI variable was accidentally scoped to every deploy context.

Deploy Previews intentionally do not share the authenticated staging API. Production keeps host-only `SameSite=Lax` session and antiforgery cookies plus exact credentialed CORS for `https://family.egobrane.net`; dynamic `*.netlify.app` preview origins must not be added to that boundary. A preview therefore proves the build, static routes, assets, responsive shell, manifest, service worker, and safe unavailable state without receiving production credentials or household data. A future authenticated preview requires a separately approved same-site preview API and data environment.

Do not put backend secrets in Netlify build settings. Even values hidden in its UI become public if a Vite build embeds them.

The SPA redirect and security headers are isolated in `netlify.toml`. `/sw.js`, `/index.html`, and the manifest revalidate on every use; content-hashed `/assets/*` files are immutable for one year. Workbox keeps the current release active until the downloaded worker is deliberately activated.

The mounted PWA update provider checks at startup, every fifteen minutes, when the tab becomes visible, and when it regains focus. A persistent accessible banner offers `Update now`. Any mounted form disables activation so an in-progress household, PIN, invitation, or Calendar operation cannot be interrupted. An online client with no form may activate after five idle minutes, which prevents an unattended wall display from remaining stale indefinitely. Offline clients retain the last working shell.

After publishing a PWA lifecycle change, verify it with two real consecutive Netlify releases in Safari and on the wall display. Confirm that the older client discovers the newer worker, an open form blocks activation, leaving the form permits activation, the new bundle controls the page after reload, and manual service-worker/cache deletion is unnecessary.

The hardened lifecycle bundle was published successfully on 2026-08-21. On 2026-08-26, the owner added a narrowly scoped Cloudflare bypass rule matching only host `family.egobrane.net` and path `/sw.js` and purged that URL. Subsequent public verification returned the origin `no-cache, max-age=0, must-revalidate` policy with `cf-cache-status: DYNAMIC`; hashed assets remain immutable.

Pull request #24 proved the safe preview boundary. Netlify deploy `6a8f3c2119e3360008e7f2c1` served `https://deploy-preview-24--effortless-bubblegum-ad0643.netlify.app` from commit `c323e4e387503c4bba7afaf5f5b4df825ca5d6c6`; its compiled bundle contained `https://api-preview.invalid` exactly once and no `https://api.egobrane.net`. The owner confirmed the expected static application and fail-closed API-unavailable experience.

The owner recorded `/assets/index-D2ZJAxw2.js` as Version 1, retained its worker, and used genuine Tasks implementation commit `52debdf32f2651aac19dcff40253749ed9e87dbc` as Version 2. Production deploy `6a8f566b4b7ff400082e1689` is ready and serves `/assets/index-Ccml6tL4.js` with the approved `https://api.egobrane.net` origin and Tasks interface. Safari and the physical wall display discovered and activated Version 2 without unregistering the worker or clearing Cache Storage; mounted forms blocked activation, safe idle activation worked, and the authenticated session remained continuous. Public `/sw.js` still returns `no-cache, max-age=0, must-revalidate` and `cf-cache-status: DYNAMIC`.
