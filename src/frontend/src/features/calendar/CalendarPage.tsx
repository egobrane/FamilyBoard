import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import {
  ApiError,
  getCalendarEventCreationTarget,
  listCalendarEvents,
  type CalendarEventsResponse,
} from '../../lib/api'
import { CalendarEventList } from './CalendarEventList'
import { CalendarStatusBanner } from './CalendarStatusBanner'

function rangeForNextWeek() {
  const from = new Date()
  from.setHours(0, 0, 0, 0)
  const to = new Date(from)
  to.setDate(to.getDate() + 7)
  return { from: from.toISOString(), to: to.toISOString() }
}

export function CalendarPage() {
  const location = useLocation()
  const { state } = useAuthentication()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId)
    : undefined
  const range = useMemo(() => rangeForNextWeek(), [])
  const requestKey = `${household?.id ?? ''}:${range.from}:${range.to}`
  const [response, setResponse] = useState<{
    key: string
    result: CalendarEventsResponse | null
    failure: string | null
  }>({ key: '', result: null, failure: null })
  const [creation, setCreation] = useState<{ key: string; isReady: boolean }>({
    key: '',
    isReady: false,
  })

  useEffect(() => {
    if (!household) return
    let active = true
    void listCalendarEvents(household.id, range.from, range.to)
      .then((result) => { if (active) setResponse({ key: requestKey, result, failure: null }) })
      .catch((error: unknown) => {
        if (!active) return
        setResponse({
          key: requestKey,
          result: null,
          failure: error instanceof ApiError && error.problem.code === 'calendar_reauthorization_required'
            ? 'A connected Google Calendar needs to be reauthorized.'
            : 'Calendar information is temporarily unavailable.',
        })
      })
    return () => { active = false }
  }, [household, range.from, range.to, requestKey])

  useEffect(() => {
    if (!household) return
    let active = true
    void getCalendarEventCreationTarget(household.id)
      .then((target) => {
        if (active) setCreation({
          key: household.id,
          isReady: target.isAvailable && target.isAuthorized && target.sourceId !== null,
        })
      })
      .catch(() => {
        if (active) setCreation({ key: household.id, isReady: false })
      })
    return () => { active = false }
  }, [household])

  if (!household) return null
  const loading = response.key !== requestKey
  const result = loading ? null : response.result
  const failure = loading ? null : response.failure
  return (
    <main className="calendar-page" id="main-content">
      <header className="calendar-page__header">
        <div>
          <p className="eyebrow">The next seven days</p>
          <h2>Family calendar</h2>
          <p>Read-only plans from the Google calendars your household has chosen.</p>
        </div>
        <div className="calendar-page__actions">
          {creation.key === household.id && creation.isReady && (
            <Link className="primary-action" to="/calendar/new">Add event</Link>
          )}
          {household.role === 'adult' && (
            <Link className="secondary-action" to={`/households/${household.id}/calendars`}>
              Calendar settings
            </Link>
          )}
        </div>
      </header>
      {location.state?.calendarEventCreated === true && (
        <CalendarStatusBanner kind="success">Event added to Google Calendar.</CalendarStatusBanner>
      )}
      {loading && <CalendarStatusBanner>Loading family plans…</CalendarStatusBanner>}
      {failure && <CalendarStatusBanner kind="error">{failure}</CalendarStatusBanner>}
      {result?.isStale && (
        <CalendarStatusBanner kind="warning">Showing recently cached events while Google Calendar reconnects.</CalendarStatusBanner>
      )}
      {result && result.warnings.length > 0 && !result.isStale && (
        <CalendarStatusBanner kind="warning">Some calendars could not be refreshed. Other events are still shown.</CalendarStatusBanner>
      )}
      {result && result.events.length === 0 && (
        <section className="calendar-empty">
          <span aria-hidden="true">□</span>
          <h3>No plans are showing yet.</h3>
          <p>{household.role === 'adult'
            ? 'Connect Google Calendar or choose calendars in household settings.'
            : 'An adult can choose which calendars appear for this household.'}</p>
        </section>
      )}
      {result && result.events.length > 0 && <CalendarEventList events={result.events} />}
    </main>
  )
}
