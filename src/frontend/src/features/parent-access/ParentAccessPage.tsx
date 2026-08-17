import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import {
  ApiError,
  getParentAccessState,
  lockParentAccess,
  recoverParentPin,
  setParentPin,
  updateSharedDisplay,
  verifyParentPin,
  type ParentAccessState,
} from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ParentPinDialog } from './ParentPinDialog'

export function ParentAccessPage() {
  const { householdId = '' } = useParams()
  const { refreshSession } = useAuthentication()
  const [access, setAccess] = useState<ParentAccessState | null>(null)
  const [mode, setMode] = useState<'view' | 'setup' | 'change' | 'recover' | 'verify'>('view')
  const [deviceLabel, setDeviceLabel] = useState('Family wall display')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isBusy, setIsBusy] = useState(false)

  const refreshAccess = async () => {
    const result = await getParentAccessState(householdId)
    setAccess(result)
    return result
  }

  useEffect(() => {
    let active = true
    void getParentAccessState(householdId)
      .then((result) => { if (active) setAccess(result) })
      .catch(() => { if (active) setError('Parent access settings could not be loaded.') })
    return () => { active = false }
  }, [householdId])

  const submitPin = async (pin: string) => {
    setIsBusy(true)
    setError(null)
    try {
      const result = mode === 'verify'
        ? await verifyParentPin(householdId, pin)
        : mode === 'recover'
          ? await recoverParentPin(householdId, pin)
          : await setParentPin(householdId, pin)
      setAccess(result)
      setMode('view')
      setNotice(mode === 'verify' ? 'Parent controls are unlocked.' : 'The parent PIN was saved.')
      await refreshSession()
    } catch (caught) {
      const code = caught instanceof ApiError ? caught.problem.code : undefined
      setError(code === 'recent_authentication_required'
        ? 'Sign out and sign in with Google again before recovering the PIN.'
        : code === 'parent_elevation_required'
          ? 'Verify the current PIN before replacing it.'
          : 'The PIN could not be saved or verified. Try again.')
    } finally {
      setIsBusy(false)
    }
  }

  if (access === null) {
    return <section className="admin-status" role={error ? 'alert' : 'status'}><h3>{error ?? 'Loading parent access…'}</h3></section>
  }

  if (mode !== 'view') {
    return (
      <div>
        <button className="back-link" onClick={() => { setMode('view'); setError(null) }} type="button">← Cancel</button>
        <ParentPinDialog
          description={mode === 'recover'
            ? 'This requires a private Google session created within the last ten minutes.'
            : mode === 'verify'
              ? 'Unlock parent controls for five minutes.'
              : 'Use six digits that adults can remember and children cannot easily guess.'}
          error={error}
          heading={mode === 'setup' ? 'Set parent PIN' : mode === 'change' ? 'Choose a new PIN' : mode === 'recover' ? 'Recover parent PIN' : 'Verify parent PIN'}
          isBusy={isBusy}
          onSubmit={submitPin}
          pinLength={access.pinLength}
          submitLabel={mode === 'verify' ? 'Unlock' : 'Save PIN'}
        />
      </div>
    )
  }

  return (
    <section className="admin-panel parent-access-settings" aria-labelledby="parent-access-title">
      <p className="eyebrow">Shared wall display</p>
      <h3 id="parent-access-title">Parent access</h3>
      <p>Routine family actions stay available. Household administration is locked behind this PIN on a shared display.</p>
      {notice && <p className="form-success" role="status">{notice}</p>}
      {error && <p className="form-error" role="alert">{error}</p>}

      {!access.isPinConfigured ? (
        <button className="primary-action" onClick={() => setMode('setup')} type="button">Set parent PIN</button>
      ) : (
        <div className="admin-actions">
          {!access.isElevated && <button className="primary-action" onClick={() => setMode('verify')} type="button">Verify PIN</button>}
          <button className="secondary-action" onClick={() => setMode('change')} type="button">Replace PIN</button>
          {!access.isSharedDisplay && <button className="secondary-action" onClick={() => setMode('recover')} type="button">Forgot PIN</button>}
        </div>
      )}

      {access.isPinConfigured && !access.isSharedDisplay && (
        <form className="settings-form" onSubmit={(event) => {
          event.preventDefault()
          setIsBusy(true)
          setError(null)
          void updateSharedDisplay(householdId, true, deviceLabel)
            .then(async () => {
              await refreshSession()
              await refreshAccess()
              setNotice('Shared-display mode is on. Parent controls are locked.')
            })
            .catch(() => setError('Verify the PIN before enabling shared-display mode.'))
            .finally(() => setIsBusy(false))
        }}>
          <label htmlFor="device-label">Display name</label>
          <input id="device-label" maxLength={80} onChange={(event) => setDeviceLabel(event.target.value)} value={deviceLabel} />
          <button className="primary-action" disabled={isBusy || !access.isElevated} type="submit">Enable shared display</button>
        </form>
      )}

      {access.isSharedDisplay && (
        <div className="admin-actions">
          <button disabled={!access.isElevated || isBusy} onClick={() => {
            setIsBusy(true)
            void updateSharedDisplay(householdId, false)
              .then(async () => {
                await refreshSession()
                await refreshAccess()
                setNotice('Shared-display mode is off.')
              })
              .catch(() => setError('Unlock parent controls before leaving shared-display mode.'))
              .finally(() => setIsBusy(false))
          }} type="button">Leave shared-display mode</button>
          <button onClick={() => {
            void lockParentAccess(householdId).then(async () => { await refreshSession(); setAccess({ ...access, isElevated: false, elevationExpiresAt: null }) })
          }} type="button">Lock now</button>
        </div>
      )}
      <p><Link to="/">Return to dashboard</Link></p>
    </section>
  )
}
