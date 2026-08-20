import { type FormEvent, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import {
  ApiError,
  createCalendarEvent,
  getCalendarEventCreationTarget,
  listHouseholdMembers,
  type CalendarEventCreationTargetResponse,
  type HouseholdMemberResponse,
} from '../../lib/api'
import { CalendarStatusBanner } from './CalendarStatusBanner'

function localDateTime(date: Date) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

function withLocalOffset(value: string) {
  const date = new Date(value)
  const offsetMinutes = -date.getTimezoneOffset()
  const sign = offsetMinutes >= 0 ? '+' : '-'
  const absolute = Math.abs(offsetMinutes)
  const hours = String(Math.floor(absolute / 60)).padStart(2, '0')
  const minutes = String(absolute % 60).padStart(2, '0')
  return `${value}:00${sign}${hours}:${minutes}`
}

function dayAfter(value: string) {
  const date = new Date(`${value}T12:00:00`)
  date.setDate(date.getDate() + 1)
  return localDateTime(date).slice(0, 10)
}

export function CreateCalendarEventPage() {
  const { state } = useAuthentication()
  const navigate = useNavigate()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId)
    : undefined
  const isSharedDisplay = state.status === 'authenticated'
    && state.currentUser.session?.isSharedDisplay === true
  const defaults = useMemo(() => {
    const start = new Date()
    start.setMinutes(0, 0, 0)
    start.setHours(start.getHours() + 1)
    const end = new Date(start)
    end.setHours(end.getHours() + 1)
    return { start: localDateTime(start), end: localDateTime(end) }
  }, [])
  const [target, setTarget] = useState<CalendarEventCreationTargetResponse | null>(null)
  const [members, setMembers] = useState<HouseholdMemberResponse[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [title, setTitle] = useState('')
  const [location, setLocation] = useState('')
  const [notes, setNotes] = useState('')
  const [isAllDay, setIsAllDay] = useState(false)
  const [start, setStart] = useState(defaults.start)
  const [end, setEnd] = useState(defaults.end)
  const [attributedMemberId, setAttributedMemberId] = useState('')
  const [idempotencyKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    if (!household) return
    let active = true
    void Promise.all([
      getCalendarEventCreationTarget(household.id),
      isSharedDisplay ? listHouseholdMembers(household.id) : Promise.resolve([]),
    ]).then(([creationTarget, householdMembers]) => {
      if (!active) return
      setTarget(creationTarget)
      setMembers(householdMembers.filter((member) => member.isActive))
      setError(null)
    }).catch(() => {
      if (active) setError('Event creation settings could not be loaded.')
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [household, isSharedDisplay])

  if (!household) return null
  if (loading) {
    return <main className="calendar-create-page" id="main-content"><CalendarStatusBanner>Loading event form…</CalendarStatusBanner></main>
  }
  if (!target?.isAvailable || !target.isAuthorized || !target.sourceId) {
    return (
      <main className="calendar-create-page" id="main-content">
        <Link className="back-link" to="/calendar">← Calendar</Link>
        <section className="calendar-empty">
          <h2>Event creation is not ready.</h2>
          <p>An adult can authorize event creation and choose a writable calendar in household settings.</p>
          {household.role === 'adult' && (
            <Link className="primary-action" to={`/households/${household.id}/calendars`}>Calendar settings</Link>
          )}
        </section>
      </main>
    )
  }
  const householdId = household.id

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!target?.sourceId) return
    setBusy(true)
    setError(null)
    try {
      await createCalendarEvent(householdId, {
        sourceId: target.sourceId,
        idempotencyKey,
        attributedMemberId: isSharedDisplay ? attributedMemberId || null : null,
        title,
        location: location.trim() || null,
        notes: notes.trim() || null,
        isAllDay,
        start: isAllDay ? start.slice(0, 10) : withLocalOffset(start),
        end: isAllDay ? dayAfter(end.slice(0, 10)) : withLocalOffset(end),
        timeZone: isAllDay ? null : Intl.DateTimeFormat().resolvedOptions().timeZone,
      })
      navigate('/calendar', { replace: true, state: { calendarEventCreated: true } })
    } catch (caught) {
      if (caught instanceof ApiError) {
        if (caught.problem.code === 'calendar_reauthorization_required'
          || caught.problem.code === 'calendar_write_authorization_required') {
          setError('Google Calendar authorization needs attention. Ask an adult to reconnect it in settings.')
        } else if (caught.problem.code === 'calendar_event_creation_rate_limited') {
          setError('Several events were added recently. Wait a minute and try again.')
        } else if (caught.problem.code === 'validation_failed') {
          setError(caught.problem.detail ?? 'Check the event details and try again.')
        } else {
          setError(caught.problem.detail ?? 'The event could not be added.')
        }
      } else {
        setError('The event could not be added.')
      }
      setBusy(false)
    }
  }

  function toggleAllDay(checked: boolean) {
    setIsAllDay(checked)
    if (checked) {
      setStart((value) => value.slice(0, 10))
      setEnd((value) => value.slice(0, 10))
    } else {
      setStart((value) => `${value.slice(0, 10)}T09:00`)
      setEnd((value) => `${value.slice(0, 10)}T10:00`)
    }
  }

  return (
    <main className="calendar-create-page" id="main-content">
      <Link className="back-link" to="/calendar">← Calendar</Link>
      <header>
        <p className="eyebrow">{target.name}</p>
        <h2>Add a family event</h2>
        <p>The event is saved directly to Google Calendar. It can be changed or deleted there.</p>
      </header>
      {error && <CalendarStatusBanner kind="error">{error}</CalendarStatusBanner>}
      <form className="calendar-event-form" onSubmit={(event) => void submit(event)}>
        {isSharedDisplay && (
          <label>
            Who is adding this event?
            <select
              required
              value={attributedMemberId}
              onChange={(event) => setAttributedMemberId(event.target.value)}
            >
              <option value="">Choose a family member</option>
              {members.map((member) => <option key={member.id} value={member.id}>{member.displayName}</option>)}
            </select>
          </label>
        )}
        <label>
          Event title
          <input autoFocus maxLength={200} required value={title} onChange={(event) => setTitle(event.target.value)} />
        </label>
        <label className="calendar-event-form__all-day">
          <input checked={isAllDay} onChange={(event) => toggleAllDay(event.target.checked)} type="checkbox" />
          All-day event
        </label>
        <div className="calendar-event-form__times">
          <label>
            Starts
            <input required type={isAllDay ? 'date' : 'datetime-local'} value={isAllDay ? start.slice(0, 10) : start} onChange={(event) => setStart(event.target.value)} />
          </label>
          <label>
            {isAllDay ? 'Last day' : 'Ends'}
            <input required type={isAllDay ? 'date' : 'datetime-local'} value={isAllDay ? end.slice(0, 10) : end} onChange={(event) => setEnd(event.target.value)} />
          </label>
        </div>
        <label>
          Location <span>(optional)</span>
          <input maxLength={500} value={location} onChange={(event) => setLocation(event.target.value)} />
        </label>
        <label>
          Notes <span>(optional)</span>
          <textarea maxLength={2000} rows={4} value={notes} onChange={(event) => setNotes(event.target.value)} />
        </label>
        <div className="calendar-event-form__actions">
          <Link className="secondary-action" to="/calendar">Cancel</Link>
          <button className="primary-action" disabled={busy} type="submit">{busy ? 'Adding…' : 'Add to calendar'}</button>
        </div>
      </form>
    </main>
  )
}
