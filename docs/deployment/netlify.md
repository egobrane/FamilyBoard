# Frontend Deployment to Netlify

1. Link the GitHub repository to a Netlify site.
2. Netlify reads the root `netlify.toml`, builds from `src/frontend`, and publishes `dist`.
3. Set `VITE_API_BASE_URL` separately for production and Deploy Preview contexts.
4. Set `VITE_APP_NAME` if the default product name should change.
5. Confirm the production API allows the exact production frontend origin.

Pull requests receive Netlify Deploy Previews through Git integration. The preview is the same portable static build; it uses no Netlify Functions, Identity, or runtime APIs.

Do not put backend secrets in Netlify build settings. Even values hidden in its UI become public if a Vite build embeds them.

The SPA redirect and security headers are isolated in `netlify.toml`. The production service worker is served with `no-cache` so the long-running wall display can detect new releases.
