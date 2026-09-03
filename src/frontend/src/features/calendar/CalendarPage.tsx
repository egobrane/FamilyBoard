import { useEffect, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import {
  ApiError,
  getCalendarEventCreationTarget,
  getHousehold,
  listCalendarEvents,
  type CalendarEventsResponse,
  type HouseholdResponse,
} from '../../lib/api'
import { CalendarMonthView } from './CalendarMonthView'
import { CalendarStatusBanner } from './CalendarStatusBanner'
import {
  addMonths,
  dateInTimeZone,
  dateKey,
  formatPlainDate,
  monthKey,
  monthRange,
  parseMonthKey,
} from './calendarDates'

async function loadCalendarMonth(householdId: string, from: string, to: string) {
  const events: CalendarEventsResponse['events'] = []
  const warnings: CalendarEventsResponse['warnings'] = []
  const cursors = new Set<string>()
  let cursor: string | undefined
  let isStale = false
  let pageCount = 0
  do {
    const response = await listCalendarEvents(householdId, from, to, cursor)
    pageCount += 1
    events.push(...response.events)
    warnings.push(...response.warnings)
    isStale ||= response.isStale
    cursor = response.nextCursor ?? undefined
    if (cursor && cursors.has(cursor)) throw new Error('Calendar pagination repeated a cursor.')
    if (cursor) cursors.add(cursor)
  } while (cursor && pageCount < 8)
  if (cursor) warnings.push({
    sourceId: '', code: 'calendar_display_limit_reached',
    message: 'This month contains more events than the calendar can display at once.',
  })
  return { events, warnings, isStale, nextCursor: null } satisfies CalendarEventsResponse
}

export function CalendarPage() {
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const { state } = useAuthentication()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId)
    : undefined
  const [configuration, setConfiguration] = useState<{
    key: string; value: HouseholdResponse | null; failed: boolean
  }>({ key: '', value: null, failed: false })
  const [response, setResponse] = useState<{
    key: string
    result: CalendarEventsResponse | null
    failure: string | null
  }>({ key: '', result: null, failure: null })
  const [creation, setCreation] = useState<{ key: string; isReady: boolean }>({ key: '', isReady: false })
  const [selection, setSelection] = useState<{ month: string; date: string }>({ month: '', date: '' })

  useEffect(() => {
    if (!household) return
    let active = true
    void getHousehold(household.id)
      .then((settings) => {
        if (!active) return
        setConfiguration({ key: household.id, value: settings, failed: false })
      })
      .catch(() => {
        if (!active) return
        setConfiguration({ key: household.id, value: null, failed: true })
      })
    void getCalendarEventCreationTarget(household.id)
      .then((target) => {
        if (active) setCreation({
          key: household.id,
          isReady: target.isAvailable && target.isAuthorized && target.sourceId !== null,
        })
      })
      .catch(() => { if (active) setCreation({ key: household.id, isReady: false }) })
    return () => { active = false }
  }, [household])

  const settings = configuration.key === household?.id ? configuration.value : null
  const today = settings ? dateInTimeZone(new Date(), settings.timeZone) : null
  const requestedMonth = parseMonthKey(searchParams.get('month'))
  const displayedMonth = requestedMonth ?? (today ? { year: today.year, month: today.month } : null)
  const range = displayedMonth && settings ? monthRange(displayedMonth, settings.timeZone) : null
  const rangeFrom = range?.from ?? ''
  const rangeTo = range?.to ?? ''
  const requestKey = `${household?.id ?? ''}:${rangeFrom}:${rangeTo}`

  useEffect(() => {
    if (!household || !rangeFrom || !rangeTo) return
    let active = true
    void loadCalendarMonth(household.id, rangeFrom, rangeTo)
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
  }, [household, rangeFrom, rangeTo, requestKey])

  if (!household) return null
  const loading = !settings || response.key !== requestKey
  const result = loading ? null : response.result
  const failure = loading ? null : response.failure
  const monthLabel = displayedMonth && settings
    ? formatPlainDate({ ...displayedMonth, day: 1 }, settings.locale, { month: 'long', year: 'numeric' })
    : ''
  const displayedMonthKey = displayedMonth ? monthKey(displayedMonth) : ''
  const isCurrentMonth = Boolean(displayedMonth && today
    && today.year === displayedMonth.year && today.month === displayedMonth.month)
  const selectedDate = selection.month === displayedMonthKey
    ? selection.date
    : displayedMonth ? dateKey(isCurrentMonth && today ? today : { ...displayedMonth, day: 1 }) : ''

  function showMonth(offset: number) {
    if (!displayedMonth) return
    const updated = new URLSearchParams(searchParams)
    updated.set('month', monthKey(addMonths(displayedMonth, offset)))
    setSearchParams(updated)
  }

  function showToday() {
    if (!today) return
    const updated = new URLSearchParams(searchParams)
    updated.set('month', monthKey(today))
    setSearchParams(updated)
    setSelection({ month: monthKey(today), date: dateKey(today) })
  }

  return (
    <main className="calendar-page" id="main-content" tabIndex={-1}>
      <header className="calendar-page__header">
        <div>
          <p className="eyebrow">Household month</p>
          <h2>Family calendar</h2>
          <p>Plans from the Google calendars your household has chosen.</p>
        </div>
        <div className="calendar-page__actions">
          {creation.key === household.id && creation.isReady && (
            <Link className="primary-action" to={`/calendar/new${selectedDate ? `?date=${selectedDate}` : ''}`}>Add event</Link>
          )}
          {household.role === 'adult' && (
            <Link className="secondary-action" to={`/households/${household.id}/calendars`}>Calendar settings</Link>
          )}
        </div>
      </header>
      {location.state?.calendarEventCreated === true && <CalendarStatusBanner kind="success">Event added to Google Calendar.</CalendarStatusBanner>}
      {location.state?.calendarEventUpdated === true && <CalendarStatusBanner kind="success">Event updated in Google Calendar.</CalendarStatusBanner>}
      {location.state?.calendarEventDeleted === true && <CalendarStatusBanner kind="success">Event deleted from Google Calendar.</CalendarStatusBanner>}
      {configuration.key === household.id && configuration.failed && <CalendarStatusBanner kind="error">Household calendar settings could not be loaded.</CalendarStatusBanner>}
      {!(configuration.key === household.id && configuration.failed) && (
        <nav aria-label="Calendar month" className="calendar-month-toolbar">
          <div className="calendar-month-toolbar__buttons">
            <button aria-label="Previous month" className="secondary-action" disabled={!displayedMonth} onClick={() => showMonth(-1)} type="button">←</button>
            <button className="secondary-action" disabled={!today} onClick={showToday} type="button">Today</button>
            <button aria-label="Next month" className="secondary-action" disabled={!displayedMonth} onClick={() => showMonth(1)} type="button">→</button>
          </div>
          <strong aria-live="polite">{monthLabel || 'Loading month…'}</strong>
        </nav>
      )}
      {loading && !(configuration.key === household.id && configuration.failed) && <CalendarStatusBanner>Loading family plans…</CalendarStatusBanner>}
      {failure && <CalendarStatusBanner kind="error">{failure}</CalendarStatusBanner>}
      {result?.isStale && <CalendarStatusBanner kind="warning">Showing recently cached events while Google Calendar reconnects.</CalendarStatusBanner>}
      {result && result.warnings.length > 0 && !result.isStale && <CalendarStatusBanner kind="warning">Some calendars could not be refreshed. Other events are still shown.</CalendarStatusBanner>}
      {result && settings && displayedMonth && selectedDate && (
        <CalendarMonthView
          events={result.events}
          locale={settings.locale}
          month={displayedMonth}
          onSelectDate={(date) => setSelection({ month: displayedMonthKey, date })}
          selectedDate={selectedDate}
          timeZone={settings.timeZone}
          weekStartsOn={settings.weekStartsOn}
        />
      )}
    </main>
  )
}
