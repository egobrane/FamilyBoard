import { googleLoginUrl } from '../../lib/api'

export function WelcomePage() {
  return (
    <main className="entry-page" id="main-content">
      <div className="entry-card">
        <p className="eyebrow">Welcome home</p>
        <h1>Bring the whole family into one clear day.</h1>
        <p className="entry-card__lede">
          Sign in to open your household dashboard, schedules, and shared routines.
        </p>
        <a className="primary-action" href={googleLoginUrl('/')}>
          Continue with Google
        </a>
        <p className="entry-card__note">Children use household profiles and do not need Google accounts.</p>
      </div>
    </main>
  )
}
