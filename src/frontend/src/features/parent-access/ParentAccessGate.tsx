import { useEffect, useState, type ReactNode } from 'react'
import { ApiError, getParentAccessState, verifyParentPin, type ParentAccessState } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ParentPinDialog } from './ParentPinDialog'

export function ParentAccessGate({ householdId, children }: { householdId: string; children: ReactNode }) {
  const { state, refreshSession } = useAuthentication()
  const [access, setAccess] = useState<ParentAccessState | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isBusy, setIsBusy] = useState(false)
  const [now, setNow] = useState(() => new Date())

  const session = state.status === 'authenticated' ? state.currentUser.session : null
  const isShared = session?.isSharedDisplay === true
  const sessionElevationIsCurrent = session?.administrativeElevationHouseholdId === householdId
    && session.administrativeElevationExpiresAt !== null
    && new Date(session.administrativeElevationExpiresAt).getTime() > now.getTime()
  const stateElevationIsCurrent = access?.isElevated === true
    && access.elevationExpiresAt !== null
    && new Date(access.elevationExpiresAt).getTime() > now.getTime()
  const cooldownUntil = access?.lockedUntil
  const cooldownIsCurrent = cooldownUntil !== null
    && cooldownUntil !== undefined
    && new Date(cooldownUntil).getTime() > now.getTime()

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 10_000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (!isShared) return
    let active = true
    void getParentAccessState(householdId)
      .then((value) => { if (active) setAccess(value) })
      .catch(() => { if (active) setError('Parent controls could not be loaded. Try again.') })
    return () => { active = false }
  }, [householdId, isShared])

  if (!isShared || sessionElevationIsCurrent || stateElevationIsCurrent) return children

  if (access === null && error === null) {
    return <section className="admin-status" role="status"><h2>Checking parent controls…</h2></section>
  }

  return (
    <ParentPinDialog
      description={cooldownIsCurrent
        ? `Try again after ${new Date(cooldownUntil).toLocaleTimeString()}.`
        : 'Enter the household PIN to open administration for five minutes.'}
      error={error}
      heading={cooldownIsCurrent ? 'Parent controls are cooling down.' : 'Unlock parent controls'}
      isBusy={isBusy || cooldownIsCurrent}
      onSubmit={async (pin) => {
        setIsBusy(true)
        setError(null)
        try {
          const next = await verifyParentPin(householdId, pin)
          setAccess(next)
          await refreshSession()
        } catch (caught) {
          if (caught instanceof ApiError && caught.problem.code === 'parent_pin_locked') {
            setError('Too many attempts. Parent controls are temporarily locked.')
            try {
              setAccess(await getParentAccessState(householdId))
            } catch {
              // Keep the specific verification failure visible when state refresh also fails.
            }
          } else {
            setError('That PIN did not work. Try again.')
          }
        } finally {
          setIsBusy(false)
        }
      }}
      pinLength={access?.pinLength ?? 6}
      submitLabel="Unlock"
    />
  )
}
