import { Link, useSearchParams } from 'react-router'
import { googleLoginUrl } from '../../lib/api'
import { useAuthentication } from './AuthenticationContext'

const calendarErrors: Record<string, { heading: string; message: string }> = {
  calendar_authorization_denied: {
    heading: 'Google Calendar access was not granted.',
    message: 'Nothing was connected. You can return to Calendar settings when you are ready.',
  },
  calendar_authorization_expired: {
    heading: 'The Calendar connection request expired.',
    message: 'Return to Calendar settings and start a fresh connection request.',
  },
  calendar_authorization_failed: {
    heading: 'Google Calendar could not be connected.',
    message: 'Google returned an unexpected response. No calendar connection was saved.',
  },
  calendar_offline_access_required: {
    heading: 'Google Calendar needs renewed permission.',
    message: 'Return to Calendar settings and approve offline access so events remain available later.',
  },
  calendar_scope_missing: {
    heading: 'Google Calendar permissions were incomplete.',
    message: 'No connection was saved because the required read-only Calendar permissions were unavailable.',
  },
  parent_elevation_required: {
    heading: 'Parent access is required.',
    message: 'Unlock parent access before changing Calendar connections on a shared display.',
  },
}

export function AuthenticationErrorPage() {
  const [searchParams] = useSearchParams()
  const { state } = useAuthentication()
  const code = searchParams.get('code') ?? ''
  const calendarError = calendarErrors[code]
    ?? (code.startsWith('calendar_')
      ? {
          heading: 'Google Calendar could not be connected.',
          message: 'No connection was saved. Return to Calendar settings and try again.',
        }
      : null)

  if (calendarError !== null) {
    const selectedHouseholdId = state.status === 'authenticated'
      ? state.currentUser.selectedHouseholdId
      : null
    return (
      <main className="entry-page" id="main-content">
        <div className="entry-card" role="alert">
          <p className="eyebrow">Calendar connection paused</p>
          <h1>{calendarError.heading}</h1>
          <p className="entry-card__lede">{calendarError.message}</p>
          {selectedHouseholdId === null
            ? <a className="primary-action" href={googleLoginUrl('/')}>Sign in to continue</a>
            : (
              <Link className="primary-action" to={`/households/${selectedHouseholdId}/calendars`}>
                Return to Calendar settings
              </Link>
            )}
        </div>
      </main>
    )
  }

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
