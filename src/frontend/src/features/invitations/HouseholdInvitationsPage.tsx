import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { useParams } from 'react-router'
import {
  ApiError,
  createHouseholdInvitation,
  listHouseholdInvitations,
  revokeHouseholdInvitation,
  type HouseholdInvitationResponse,
} from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'

type LoadState =
  | { status: 'loading' }
  | { status: 'ready'; invitations: HouseholdInvitationResponse[] }
  | { status: 'notFound' | 'forbidden' | 'failed' }

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
    .format(new Date(value))
}

export function HouseholdInvitationsPage() {
  const { householdId = '' } = useParams()
  const { refreshSilently } = useAuthentication()
  const [loadState, setLoadState] = useState<LoadState>({ status: 'loading' })
  const [email, setEmail] = useState('')
  const [createdLink, setCreatedLink] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const linkRef = useRef<HTMLInputElement>(null)

  const load = useCallback(async () => {
    setLoadState({ status: 'loading' })
    try {
      setLoadState({ status: 'ready', invitations: await listHouseholdInvitations(householdId) })
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.status === 401) {
        await refreshSilently()
      } else if (requestError instanceof ApiError && requestError.status === 404) {
        setLoadState({ status: 'notFound' })
      } else if (requestError instanceof ApiError && requestError.status === 403) {
        setLoadState({ status: 'forbidden' })
      } else {
        setLoadState({ status: 'failed' })
      }
    }
  }, [householdId, refreshSilently])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setIsSaving(true)
    setError(null)
    setMessage(null)
    try {
      const created = await createHouseholdInvitation(householdId, email)
      setCreatedLink(`${window.location.origin}/invite#token=${created.token}`)
      setEmail('')
      setLoadState((current) => current.status === 'ready'
        ? { status: 'ready', invitations: [created.invitation, ...current.invitations] }
        : current)
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.status === 401) {
        await refreshSilently()
      } else if (requestError instanceof ApiError && requestError.problem.code === 'active_invitation_exists') {
        setError('A pending invitation already exists for that email address.')
      } else if (requestError instanceof ApiError && requestError.problem.errors?.intendedEmail) {
        setError(requestError.problem.errors.intendedEmail[0])
      } else {
        setError('The invitation could not be created. Check your connection and try again.')
      }
    } finally {
      setIsSaving(false)
    }
  }

  const copyLink = async () => {
    if (!createdLink) return
    try {
      await navigator.clipboard.writeText(createdLink)
      setMessage('Invitation link copied.')
    } catch {
      linkRef.current?.focus()
      linkRef.current?.select()
      setMessage('Select and copy the invitation link shown below.')
    }
  }

  const revoke = async (invitation: HouseholdInvitationResponse) => {
    setError(null)
    setMessage(null)
    try {
      const updated = await revokeHouseholdInvitation(householdId, invitation.id)
      setLoadState((current) => current.status === 'ready'
        ? {
            status: 'ready',
            invitations: current.invitations.map((candidate) =>
              candidate.id === updated.id ? updated : candidate),
          }
        : current)
      setMessage(`Invitation for ${updated.intendedEmail} was revoked.`)
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.status === 401) {
        await refreshSilently()
      } else if (requestError instanceof ApiError && requestError.problem.code === 'invitation_used') {
        setError('That invitation was accepted before it could be revoked. Refresh the list.')
      } else {
        setError('The invitation could not be revoked. Refresh and try again.')
      }
    }
  }

  if (loadState.status !== 'ready') {
    const content = {
      loading: ['Loading invitations…', 'Retrieving invitation history.'],
      notFound: ['Household not found', 'This household is unavailable to the current account.'],
      forbidden: ['Adult access required', 'An adult household account must manage invitations.'],
      failed: ['Invitations could not be loaded', 'Check your connection and try again.'],
    }[loadState.status]
    return (
      <section className="admin-panel admin-status" role={loadState.status === 'loading' ? 'status' : 'alert'}>
        <h3>{content[0]}</h3><p>{content[1]}</p>
        {loadState.status === 'failed' && <button className="secondary-action" onClick={() => void load()} type="button">Try again</button>}
      </section>
    )
  }

  return (
    <section className="admin-panel" aria-labelledby="household-invitations-title">
      <div className="admin-panel__heading">
        <div><p className="eyebrow">Adult access</p><h3 id="household-invitations-title">Invitation links</h3></div>
      </div>
      <p className="admin-panel__lede">Create a single-use link for a specific adult Google account. Links expire after seven days.</p>
      <form className="invitation-form" noValidate onSubmit={(event) => void create(event)}>
        <label className="form-field">
          <span>Adult email address</span>
          <input autoComplete="email" inputMode="email" maxLength={320} onChange={(event) => setEmail(event.target.value)} required type="email" value={email} />
        </label>
        <button className="primary-action" disabled={isSaving} type="submit">{isSaving ? 'Creating…' : 'Create invitation'}</button>
      </form>
      {error && <p className="form-error-summary" role="alert">{error}</p>}
      {message && <p className="save-success" role="status">{message}</p>}
      {createdLink && (
        <section aria-labelledby="created-invitation-title" className="invitation-created">
          <h4 id="created-invitation-title">Copy this link now</h4>
          <p>For security, Family Dashboard will not show this link again.</p>
          <div className="copy-link-row">
            <input aria-label="Invitation link" readOnly ref={linkRef} value={createdLink} />
            <button className="primary-action" onClick={() => void copyLink()} type="button">Copy link</button>
          </div>
          <button className="text-action" onClick={() => setCreatedLink(null)} type="button">I have saved the link</button>
        </section>
      )}
      <section className="invitation-history" aria-labelledby="invitation-history-title">
        <h4 id="invitation-history-title">Invitation history</h4>
        {loadState.invitations.length === 0 && <p className="empty-state">No invitations have been created.</p>}
        <div className="invitation-list">
          {loadState.invitations.map((invitation) => (
            <article className="invitation-card" key={invitation.id}>
              <div><strong>{invitation.intendedEmail}</strong><p>Expires {formatDate(invitation.expiresAt)}</p></div>
              <span className={`invitation-status invitation-status--${invitation.status}`}>{invitation.status}</span>
              {invitation.status === 'pending' && <button className="danger-link" onClick={() => {
                if (window.confirm(`Revoke the invitation for ${invitation.intendedEmail}?`)) {
                  void revoke(invitation)
                }
              }} type="button">Revoke</button>}
            </article>
          ))}
        </div>
      </section>
    </section>
  )
}
