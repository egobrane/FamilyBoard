import type { CalendarEventResponse } from '../../lib/api'
import { Link } from 'react-router'
import { formatEventTime } from './calendarDates'

export function CalendarEventList({ events, compact = false, locale, timeZone }: {
  events: CalendarEventResponse[]
  compact?: boolean
  locale?: string
  timeZone?: string
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
            <span>{formatEventTime(event, locale ?? undefined, timeZone ?? event.timeZone ?? undefined)} · {event.calendarName}</span>
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
