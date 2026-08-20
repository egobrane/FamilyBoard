import { useEffect, useState } from 'react'
import { useLocation } from 'react-router'
import {
  ApiError,
  beginCalendarAuthorization,
  disconnectCalendar,
  getCalendarEventCreationTarget,
  getCalendarConnection,
  listCalendarSources,
  listProviderCalendars,
  updateCalendarSources,
  updateCalendarEventCreationTarget,
  type CalendarConnectionResponse,
  type CalendarSourceResponse,
  type ProviderCalendarResponse,
} from '../../lib/api'
import { CalendarStatusBanner } from './CalendarStatusBanner'

export function CalendarSettingsPage({ householdId }: { householdId: string }) {
  const location = useLocation()
  const [connection, setConnection] = useState<CalendarConnectionResponse | null>(null)
  const [calendars, setCalendars] = useState<ProviderCalendarResponse[]>([])
  const [sources, setSources] = useState<CalendarSourceResponse[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [eventCreationTargetId, setEventCreationTargetId] = useState<string>('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(
    new URLSearchParams(location.search).get('calendar') === 'connected'
      ? 'Google Calendar connected. Choose what this household can see.'
      : null,
  )
  const [error, setError] = useState<string | null>(null)
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)

  async function load() {
    try {
      const status = await getCalendarConnection(householdId)
      setConnection(status)
      if (status.status === 'connected') {
        const [available, configured, target] = await Promise.all([
          listProviderCalendars(householdId),
          listCalendarSources(householdId),
          getCalendarEventCreationTarget(householdId),
        ])
        setCalendars(available)
        setSources(configured)
        setSelected(new Set(available.filter((item) => item.isSelected).map((item) => item.id)))
        setEventCreationTargetId(target.sourceId ?? '')
      } else {
        setCalendars([])
        setSources([])
        setSelected(new Set())
        setEventCreationTargetId('')
      }
      setError(null)
    } catch (caught) {
      setError(caught instanceof ApiError && caught.problem.code === 'parent_elevation_required'
        ? 'Unlock parent access to manage calendars.'
        : 'Calendar settings could not be loaded.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setLoading(true)
      void load()
    }, 0)
    return () => window.clearTimeout(timer)
  }, [householdId]) // eslint-disable-line react-hooks/exhaustive-deps

  async function connect() {
    setBusy(true)
    setError(null)
    try {
      const result = await beginCalendarAuthorization(
        householdId,
        `/households/${householdId}/calendars`,
      )
      window.location.assign(result.authorizationUrl)
    } catch {
      setError('Google Calendar authorization could not be started.')
      setBusy(false)
    }
  }

  async function authorizeEventCreation() {
    setBusy(true)
    setError(null)
    try {
      const result = await beginCalendarAuthorization(
        householdId,
        `/households/${householdId}/calendars`,
        'eventCreation',
      )
      window.location.assign(result.authorizationUrl)
    } catch {
      setError('Google Calendar event creation authorization could not be started.')
      setBusy(false)
    }
  }

  async function saveSources() {
    if (!connection?.connectionId) return
    setBusy(true)
    setError(null)
    try {
      const updated = await updateCalendarSources(
        householdId,
        connection.connectionId,
        [...selected],
      )
      setSources(updated)
      setMessage('Household calendars saved.')
    } catch {
      setError('The selected calendars could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  async function disconnect() {
    if (!connection?.connectionId) return
    setBusy(true)
    setError(null)
    try {
      await disconnectCalendar(householdId, connection.connectionId)
      setConfirmDisconnect(false)
      setMessage('Google Calendar disconnected from every household.')
      setLoading(true)
      await load()
    } catch {
      setError('Google Calendar could not be disconnected.')
      setBusy(false)
    }
  }

  async function saveEventCreationTarget() {
    setBusy(true)
    setError(null)
    try {
      const target = await updateCalendarEventCreationTarget(
        householdId,
        eventCreationTargetId || null,
      )
      setEventCreationTargetId(target.sourceId ?? '')
      setMessage(target.sourceId
        ? 'Calendar for new family events saved.'
        : 'Event creation is turned off for this household.')
    } catch (caught) {
      setError(caught instanceof ApiError && caught.problem.code === 'calendar_event_creation_target_invalid'
        ? 'Choose a calendar where this Google account can add events.'
        : 'The event creation calendar could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <CalendarStatusBanner>Loading calendar settings…</CalendarStatusBanner>

  return (
    <section className="admin-panel calendar-settings" aria-labelledby="calendar-settings-title">
      <div className="admin-panel__heading">
        <div>
          <p className="eyebrow">Google Calendar integration</p>
          <h3 id="calendar-settings-title">Google Calendar</h3>
          <p>Calendar authorization is separate from the Google account used to sign in.</p>
        </div>
      </div>
      {message && <CalendarStatusBanner kind="success">{message}</CalendarStatusBanner>}
      {error && <CalendarStatusBanner kind="error">{error}</CalendarStatusBanner>}
      {!connection?.isAvailable && (
        <CalendarStatusBanner kind="warning">Google Calendar is not enabled for this environment.</CalendarStatusBanner>
      )}
      {connection?.isAvailable && connection.status !== 'connected' && (
        <div className="calendar-connection-card">
          <div>
            <h4>{connection.status === 'reauthorizationRequired' ? 'Reconnect Google Calendar' : 'Connect Google Calendar'}</h4>
            <p>Choose a Google account and grant read-only calendar access. Family Dashboard cannot edit events.</p>
          </div>
          <button className="primary-action" disabled={busy} onClick={() => void connect()} type="button">
            {busy ? 'Opening Google…' : connection.status === 'reauthorizationRequired' ? 'Reconnect' : 'Connect'}
          </button>
        </div>
      )}
      {connection?.status === 'connected' && (
        <>
          <div className="calendar-connection-card">
            <div>
              <p className="eyebrow">Connected account</p>
              <h4>{connection.providerEmail}</h4>
              <p>{connection.activeSourceCount} calendar{connection.activeSourceCount === 1 ? '' : 's'} from this account currently shown.</p>
            </div>
            <button className="danger-action" disabled={busy} onClick={() => setConfirmDisconnect(true)} type="button">
              Disconnect
            </button>
          </div>
          <fieldset className="calendar-source-picker">
            <legend>Calendars visible to this household</legend>
            <p>Select only calendars whose plans are appropriate for the shared family display.</p>
            {calendars.length === 0 && <p>No readable calendars were returned by Google.</p>}
            {calendars.map((calendar) => (
              <label className="calendar-source-option" key={calendar.id}>
                <input
                  checked={selected.has(calendar.id)}
                  onChange={(event) => setSelected((current) => {
                    const next = new Set(current)
                    if (event.target.checked) next.add(calendar.id)
                    else next.delete(calendar.id)
                    return next
                  })}
                  type="checkbox"
                />
                <span aria-hidden="true" style={{ backgroundColor: calendar.color ?? '#73b49a' }} />
                <strong>{calendar.name}</strong>
                {calendar.isPrimary && <small>Primary</small>}
              </label>
            ))}
          </fieldset>
          <button className="primary-action" disabled={busy} onClick={() => void saveSources()} type="button">
            {busy ? 'Saving…' : 'Save visible calendars'}
          </button>
          {connection.eventCreationAvailable && (
            <section className="calendar-creation-settings" aria-labelledby="calendar-creation-title">
              <div>
                <p className="eyebrow">Controlled event creation</p>
                <h4 id="calendar-creation-title">Add family events</h4>
                <p>Event creation needs separate Google permission. Family Dashboard still cannot edit or delete events.</p>
              </div>
              {!connection.eventCreationAuthorized ? (
                <button
                  className="secondary-action"
                  disabled={busy}
                  onClick={() => void authorizeEventCreation()}
                  type="button"
                >
                  {busy ? 'Opening Google…' : 'Authorize event creation'}
                </button>
              ) : (
                <div className="calendar-creation-target">
                  <label htmlFor="calendar-creation-target">Calendar for new events</label>
                  <select
                    id="calendar-creation-target"
                    onChange={(event) => setEventCreationTargetId(event.target.value)}
                    value={eventCreationTargetId}
                  >
                    <option value="">Do not allow event creation</option>
                    {sources
                      .filter((source) => source.isActive && source.isOwnedByCurrentAdult)
                      .filter((source) => calendars.some((calendar) =>
                        calendar.id === source.externalCalendarId && calendar.canCreateEvents))
                      .map((source) => <option key={source.id} value={source.id}>{source.name}</option>)}
                  </select>
                  <button
                    className="secondary-action"
                    disabled={busy}
                    onClick={() => void saveEventCreationTarget()}
                    type="button"
                  >
                    {busy ? 'Saving…' : 'Save event calendar'}
                  </button>
                </div>
              )}
            </section>
          )}
          {sources.some((source) => !source.isOwnedByCurrentAdult && source.isActive) && (
            <p className="admin-note">This household also includes calendars connected by another adult. Only that adult can reconnect or disconnect their Google account.</p>
          )}
        </>
      )}
      {confirmDisconnect && (
        <div className="dialog-backdrop">
          <section aria-labelledby="disconnect-calendar-title" aria-modal="true" className="confirmation-dialog" role="dialog">
            <h3 id="disconnect-calendar-title">Disconnect Google Calendar?</h3>
            <p>This removes this Google connection and its calendars from every household. Google Calendar events are not deleted.</p>
            <div className="dialog-actions">
              <button className="secondary-action" disabled={busy} onClick={() => setConfirmDisconnect(false)} type="button">Keep connected</button>
              <button className="danger-action" disabled={busy} onClick={() => void disconnect()} type="button">Disconnect everywhere</button>
            </div>
          </section>
        </div>
      )}
    </section>
  )
}
