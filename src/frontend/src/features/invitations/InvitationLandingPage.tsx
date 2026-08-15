import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import {
  ApiError,
  acceptPendingInvitation,
  getPendingInvitation,
  googleLoginUrl,
  prepareInvitation,
  type PendingInvitationResponse,
} from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'

type InvitationState =
  | { status: 'loading' }
  | { status: 'ready'; invitation: PendingInvitationResponse }
  | { status: 'expired' | 'revoked' | 'used' | 'notFound' | 'failed' }

function stateForError(error: unknown): InvitationState {
  if (error instanceof ApiError) {
    const code = error.problem.code
    if (code === 'invitation_expired') return { status: 'expired' }
    if (code === 'invitation_revoked') return { status: 'revoked' }
    if (code === 'invitation_used') return { status: 'used' }
    if (code === 'invitation_not_found' || code === 'invitation_unavailable') return { status: 'notFound' }
  }
  return { status: 'failed' }
}

export function InvitationLandingPage() {
  const authentication = useAuthentication()
  const navigate = useNavigate()
  const prepared = useRef(false)
  const [state, setState] = useState<InvitationState>({ status: 'loading' })
  const [isAccepting, setIsAccepting] = useState(false)
  const [acceptError, setAcceptError] = useState<string | null>(null)

  useEffect(() => {
    if (prepared.current) return
    prepared.current = true
    const parameters = new URLSearchParams(window.location.hash.slice(1))
    const token = parameters.get('token')
    window.history.replaceState(null, '', '/invite')
    void (token ? prepareInvitation(token) : getPendingInvitation())
      .then((invitation) => setState({ status: 'ready', invitation }))
      .catch((error: unknown) => setState(stateForError(error)))
  }, [])

  const accept = async () => {
    setIsAccepting(true)
    setAcceptError(null)
    try {
      await acceptPendingInvitation()
      await authentication.refreshSilently()
      navigate('/', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.problem.code === 'invitation_email_mismatch') {
        setAcceptError('This link was created for a different Google account. Sign out and use the invited account.')
      } else if (error instanceof ApiError && error.status === 401) {
        await authentication.refreshSilently()
      } else {
        const next = stateForError(error)
        if (next.status === 'failed') setAcceptError('The invitation could not be accepted. Check your connection and try again.')
        else setState(next)
      }
    } finally {
      setIsAccepting(false)
    }
  }

  const changeAccount = async () => {
    setAcceptError(null)
    try {
      await authentication.logout()
    } catch {
      setAcceptError('Could not sign out this account. Check your connection and try again.')
    }
  }

  const unavailable = {
    expired: ['Invitation expired', 'Ask a household adult to create a new invitation link.'],
    revoked: ['Invitation revoked', 'This invitation is no longer available.'],
    used: ['Invitation already used', 'This one-time invitation has already been accepted.'],
    notFound: ['Invitation unavailable', 'The link is invalid or no longer available.'],
    failed: ['Invitation could not be checked', 'Check your connection and try opening the link again.'],
  }

  return (
    <main className="entry-page invitation-entry" id="main-content">
      <section className="entry-card" role={state.status === 'loading' ? 'status' : undefined}>
        <p className="eyebrow">Family Dashboard invitation</p>
        {state.status === 'loading' && <><h1>Checking your invitation…</h1><p className="entry-card__lede">This should only take a moment.</p></>}
        {state.status === 'ready' && (
          <>
            <h1>Join {state.invitation.householdName}</h1>
            <p className="entry-card__lede">This invitation is for {state.invitation.intendedEmailMasked}.</p>
            {authentication.state.status === 'loading' && <p role="status">Checking your sign-in…</p>}
            {authentication.state.status === 'signedOut' && <a className="primary-action" href={googleLoginUrl('/invite', true)}>Sign in with the invited Google account</a>}
            {authentication.state.status === 'authenticated' && (
              <div className="invitation-account-actions">
                <p>Signed in as <strong>{authentication.state.currentUser.user.primaryEmail}</strong></p>
                <button className="primary-action" disabled={isAccepting} onClick={() => void accept()} type="button">{isAccepting ? 'Joining…' : 'Join household'}</button>
                <button className="secondary-action" disabled={isAccepting} onClick={() => void changeAccount()} type="button">Use a different Google account</button>
              </div>
            )}
            {authentication.state.status === 'unavailable' && <p role="alert">Your sign-in could not be checked. Try again shortly.</p>}
            {authentication.state.status === 'accountUnavailable' && <p role="alert">This signed-in account is unavailable.</p>}
            {acceptError && <p className="form-error-summary" role="alert">{acceptError}</p>}
          </>
        )}
        {state.status !== 'loading' && state.status !== 'ready' && (
          <><h1>{unavailable[state.status][0]}</h1><p className="entry-card__lede">{unavailable[state.status][1]}</p><a className="secondary-action" href="/">Return to Family Dashboard</a></>
        )}
      </section>
    </main>
  )
}
