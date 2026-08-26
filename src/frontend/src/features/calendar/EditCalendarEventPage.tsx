import { type FormEvent, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import {
  ApiError,
  deleteManagedCalendarEvent,
  getManagedCalendarEvent,
  updateManagedCalendarEvent,
  type ManagedCalendarEventResponse,
} from '../../lib/api'
import { CalendarStatusBanner } from './CalendarStatusBanner'
import { DeleteCalendarEventDialog } from './DeleteCalendarEventDialog'

function localInput(value: string) {
  const date = new Date(value)
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

function withLocalOffset(value: string) {
  const date = new Date(value)
  const offset = -date.getTimezoneOffset()
  const sign = offset >= 0 ? '+' : '-'
  return `${value}:00${sign}${String(Math.floor(Math.abs(offset) / 60)).padStart(2, '0')}:${String(Math.abs(offset) % 60).padStart(2, '0')}`
}

function dayAfter(value: string) {
  const date = new Date(`${value}T12:00:00`)
  date.setDate(date.getDate() + 1)
  return date.toISOString().slice(0, 10)
}

export function EditCalendarEventPage() {
  const { managementId } = useParams()
  const { state } = useAuthentication()
  const navigate = useNavigate()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId)
    : undefined
  const [event, setEvent] = useState<ManagedCalendarEventResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showDelete, setShowDelete] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [title, setTitle] = useState('')
  const [location, setLocation] = useState('')
  const [notes, setNotes] = useState('')
  const [isAllDay, setIsAllDay] = useState(false)
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [updateKey] = useState(() => crypto.randomUUID())
  const [deleteKey] = useState(() => crypto.randomUUID())

  useEffect(() => {
    if (!household || !managementId) return
    let active = true
    void getManagedCalendarEvent(household.id, managementId).then((loaded) => {
      if (!active) return
      setEvent(loaded)
      setTitle(loaded.title)
      setLocation(loaded.location ?? '')
      setNotes(loaded.notes ?? '')
      setIsAllDay(loaded.isAllDay)
      setStart(loaded.isAllDay ? loaded.start : localInput(loaded.start))
      const exclusiveEnd = loaded.isAllDay
        ? new Date(`${loaded.end}T12:00:00`) : null
      if (exclusiveEnd) exclusiveEnd.setDate(exclusiveEnd.getDate() - 1)
      setEnd(loaded.isAllDay ? exclusiveEnd!.toISOString().slice(0, 10) : localInput(loaded.end))
    }).catch((caught: unknown) => {
      setError(caught instanceof ApiError && caught.problem.code === 'parent_elevation_required'
        ? 'Unlock parent access before managing this event.'
        : 'This event could not be loaded.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [household, managementId])

  useEffect(() => {
    const protect = (event: BeforeUnloadEvent) => { if (dirty && !busy) event.preventDefault() }
    window.addEventListener('beforeunload', protect)
    return () => window.removeEventListener('beforeunload', protect)
  }, [dirty, busy])

  if (!household || !managementId) return null
  if (loading) return <main className="calendar-create-page" id="main-content"><CalendarStatusBanner>Loading event…</CalendarStatusBanner></main>

  function problemMessage(caught: unknown) {
    if (!(caught instanceof ApiError)) return 'Google Calendar could not be updated.'
    if (caught.problem.code === 'calendar_event_version_conflict') return 'This event changed in Google Calendar. Return to the calendar and open it again.'
    if (caught.problem.code === 'calendar_event_not_found') return 'This event no longer exists in Google Calendar.'
    if (caught.problem.code === 'calendar_reauthorization_required') return 'Reconnect Google Calendar before trying again.'
    if (caught.problem.code === 'parent_elevation_required') return 'Unlock parent access before managing this event.'
    return caught.problem.detail ?? caught.problem.title
  }

  async function submit(formEvent: FormEvent) {
    formEvent.preventDefault()
    if (!event) return
    setBusy(true); setError(null)
    try {
      await updateManagedCalendarEvent(household!.id, managementId!, {
        idempotencyKey: updateKey,
        expectedProviderVersion: event.providerVersion,
        title,
        location: location.trim() || null,
        notes: notes.trim() || null,
        isAllDay,
        start: isAllDay ? start : withLocalOffset(start),
        end: isAllDay ? dayAfter(end) : withLocalOffset(end),
        timeZone: isAllDay ? null : Intl.DateTimeFormat().resolvedOptions().timeZone,
      })
      setDirty(false)
      navigate('/calendar', { replace: true, state: { calendarEventUpdated: true } })
    } catch (caught) { setError(problemMessage(caught)); setBusy(false) }
  }

  async function remove() {
    if (!event) return
    setBusy(true); setError(null)
    try {
      await deleteManagedCalendarEvent(household!.id, managementId!, {
        idempotencyKey: deleteKey,
        expectedProviderVersion: event.providerVersion,
        confirmDelete: true,
      })
      setDirty(false)
      navigate('/calendar', { replace: true, state: { calendarEventDeleted: true } })
    } catch (caught) { setShowDelete(false); setError(problemMessage(caught)); setBusy(false) }
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
      <header><p className="eyebrow">{event?.calendarName}</p><h2>Manage event</h2><p>Changes are saved directly to Google Calendar.</p></header>
      {error && <CalendarStatusBanner kind="error">{error}</CalendarStatusBanner>}
      {event && !event.canEdit && <CalendarStatusBanner kind="warning">{event.managementUnavailableReason ?? 'This event is read-only.'}</CalendarStatusBanner>}
      {event?.canEdit && (
        <form className="calendar-event-form" onChange={() => setDirty(true)} onSubmit={(formEvent) => void submit(formEvent)}>
          <label>Event title<input autoFocus maxLength={200} required value={title} onChange={(e) => setTitle(e.target.value)} /></label>
          <label className="calendar-event-form__all-day"><input checked={isAllDay} onChange={(e) => toggleAllDay(e.target.checked)} type="checkbox" />All-day event</label>
          <div className="calendar-event-form__times">
            <label>Starts<input required type={isAllDay ? 'date' : 'datetime-local'} value={start} onChange={(e) => setStart(e.target.value)} /></label>
            <label>{isAllDay ? 'Last day' : 'Ends'}<input required type={isAllDay ? 'date' : 'datetime-local'} value={end} onChange={(e) => setEnd(e.target.value)} /></label>
          </div>
          <label>Location <span>(optional)</span><input maxLength={500} value={location} onChange={(e) => setLocation(e.target.value)} /></label>
          <label>Notes <span>(optional)</span><textarea maxLength={2000} rows={4} value={notes} onChange={(e) => setNotes(e.target.value)} /></label>
          <div className="calendar-event-form__actions">
            <button className="danger-action" disabled={busy} onClick={() => setShowDelete(true)} type="button">Delete event</button>
            <Link className="secondary-action" to="/calendar">Cancel</Link>
            <button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save to Google Calendar'}</button>
          </div>
        </form>
      )}
      {showDelete && event && <DeleteCalendarEventDialog busy={busy} title={event.title} onCancel={() => setShowDelete(false)} onConfirm={() => void remove()} />}
    </main>
  )
}
