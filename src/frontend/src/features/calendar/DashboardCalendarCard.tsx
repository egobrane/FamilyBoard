import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { listCalendarEvents, type CalendarEventsResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { CalendarEventList } from './CalendarEventList'

function todayRange() {
  const from = new Date()
  from.setHours(0, 0, 0, 0)
  const to = new Date(from)
  to.setDate(to.getDate() + 1)
  return { from: from.toISOString(), to: to.toISOString() }
}

export function DashboardCalendarCard() {
  const { state } = useAuthentication()
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  const range = useMemo(() => todayRange(), [])
  const [result, setResult] = useState<CalendarEventsResponse | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    if (!householdId) return
    let active = true
    void listCalendarEvents(householdId, range.from, range.to)
      .then((response) => { if (active) setResult(response) })
      .catch(() => { if (active) setFailed(true) })
    return () => { active = false }
  }, [householdId, range.from, range.to])

  return (
    <DashboardCard
      className="schedule-card"
      eyebrow={new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'long', day: 'numeric' }).format(new Date())}
      id="calendar-preview"
      title="Today"
      action={<Link className="status-pill status-pill--link" to="/calendar">Open calendar</Link>}
    >
      {!result && !failed && <p className="preview-note" role="status">Loading today’s plans…</p>}
      {failed && <p className="preview-note" role="alert">Calendar information is temporarily unavailable.</p>}
      {result && result.events.length === 0 && <p className="preview-note">No calendar plans are showing for today.</p>}
      {result && result.events.length > 0 && <CalendarEventList compact events={result.events.slice(0, 4)} />}
      {result?.isStale && <p className="preview-note">Showing recently cached events.</p>}
    </DashboardCard>
  )
}
