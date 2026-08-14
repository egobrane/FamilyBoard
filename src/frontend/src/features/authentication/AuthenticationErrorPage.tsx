import { googleLoginUrl } from '../../lib/api'

export function AuthenticationErrorPage() {
  return (
    <main className="entry-page" id="main-content">
      <div className="entry-card" role="alert">
        <p className="eyebrow">Sign-in paused</p>
        <h1>We could not complete sign-in.</h1>
        <p className="entry-card__lede">
          No household information was changed. Try again when you are ready.
        </p>
        <a className="primary-action" href={googleLoginUrl('/')}>
          Try Google sign-in again
        </a>
      </div>
    </main>
  )
}
