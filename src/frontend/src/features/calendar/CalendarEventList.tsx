import type { CalendarEventResponse } from '../../lib/api'
import { Link } from 'react-router'

function eventTime(event: CalendarEventResponse) {
  if (event.isAllDay) return 'All day'
  const start = new Date(event.start)
  const end = new Date(event.end)
  return `${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(start)}–${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(end)}`
}

export function CalendarEventList({ events, compact = false }: {
  events: CalendarEventResponse[]
  compact?: boolean
}) {
  return (
    <ol className={`calendar-event-list${compact ? ' calendar-event-list--compact' : ''}`}>
      {events.map((event) => (
        <li className="calendar-event" key={`${event.sourceId}:${event.id}`}>
          <span
            aria-hidden="true"
            className="calendar-event__color"
            style={{ backgroundColor: event.color ?? '#73b49a' }}
          />
          <div>
            <strong>{event.title}</strong>
            <span>{eventTime(event)} · {event.calendarName}</span>
            {!compact && event.location && <small>{event.location}</small>}
          </div>
          {!compact && event.canEdit && event.managementId && (
            <Link className="secondary-action calendar-event__manage" to={`/calendar/events/${event.managementId}/edit`}>
              Manage
            </Link>
          )}
        </li>
      ))}
    </ol>
  )
}
