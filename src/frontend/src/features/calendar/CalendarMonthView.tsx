import type { CalendarEventResponse } from '../../lib/api'
import type { CSSProperties } from 'react'
import { CalendarEventList } from './CalendarEventList'
import {
  dateInTimeZone,
  dateKey,
  eventsByDate,
  formatEventTime,
  formatPlainDate,
  monthDays,
  type CalendarDate,
} from './calendarDates'

const maximumVisibleEvents = 3

export function CalendarMonthView({
  events,
  locale,
  month,
  onSelectDate,
  selectedDate,
  timeZone,
  weekStartsOn,
}: {
  events: CalendarEventResponse[]
  locale: string
  month: Pick<CalendarDate, 'year' | 'month'>
  onSelectDate: (date: string) => void
  selectedDate: string
  timeZone: string
  weekStartsOn: string
}) {
  const days = monthDays(month, weekStartsOn)
  const grouped = eventsByDate(events, timeZone)
  const today = dateKey(dateInTimeZone(new Date(), timeZone))
  const selectedEvents = grouped.get(selectedDate) ?? []
  const eventDays = days.filter((day) => day.isCurrentMonth && (grouped.get(day.key)?.length ?? 0) > 0)
  const weekdayStart = weekStartsOn.toLowerCase() === 'monday' ? 1 : 0
  const weekdays = Array.from({ length: 7 }, (_, index) => {
    const day = new Date(Date.UTC(2026, 7, 2 + ((weekdayStart + index) % 7), 12))
    return new Intl.DateTimeFormat(locale, { weekday: 'short', timeZone: 'UTC' }).format(day)
  })
  const monthLabel = formatPlainDate({ ...month, day: 1 }, locale, { month: 'long', year: 'numeric' })

  return (
    <>
      <div className="calendar-month" data-testid="calendar-month">
        <table>
          <caption className="sr-only">{monthLabel} family calendar</caption>
          <thead><tr>{weekdays.map((weekday) => <th key={weekday} scope="col">{weekday}</th>)}</tr></thead>
          <tbody>
            {Array.from({ length: days.length / 7 }, (_, week) => (
              <tr key={days[week * 7].key}>
                {days.slice(week * 7, week * 7 + 7).map((day) => {
                  const dayEvents = day.isCurrentMonth ? grouped.get(day.key) ?? [] : []
                  const label = formatPlainDate(day, locale, { weekday: 'long', month: 'long', day: 'numeric' })
                  return (
                    <td className={`${day.isCurrentMonth ? '' : 'calendar-month__day--outside'}${day.key === today ? ' calendar-month__day--today' : ''}${day.key === selectedDate ? ' calendar-month__day--selected' : ''}`} key={day.key}>
                      {day.isCurrentMonth ? (
                        <button
                          aria-label={`${label}, ${dayEvents.length} ${dayEvents.length === 1 ? 'event' : 'events'}`}
                          className="calendar-month__date"
                          onClick={() => onSelectDate(day.key)}
                          type="button"
                        >
                          <span>{day.day}</span>{day.key === today && <small>Today</small>}
                        </button>
                      ) : <span aria-hidden="true" className="calendar-month__outside-date">{day.day}</span>}
                      {dayEvents.length > 0 && (
                        <ul className="calendar-month__events">
                          {dayEvents.slice(0, maximumVisibleEvents).map((event) => (
                            <li key={`${day.key}:${event.sourceId}:${event.id}`}>
                              <button
                                aria-label={`${event.title}, ${formatEventTime(event, locale, timeZone)}, ${label}`}
                                onClick={() => onSelectDate(day.key)}
                                style={{ '--event-color': event.color ?? '#73b49a' } as CSSProperties}
                                type="button"
                              >
                                <span>{event.isAllDay ? '' : formatEventTime(event, locale, timeZone)}</span>
                                <strong>{event.title}</strong>
                              </button>
                            </li>
                          ))}
                          {dayEvents.length > maximumVisibleEvents && (
                            <li><button className="calendar-month__more" onClick={() => onSelectDate(day.key)} type="button">
                              +{dayEvents.length - maximumVisibleEvents} more
                            </button></li>
                          )}
                        </ul>
                      )}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <section aria-labelledby="selected-calendar-date" className="calendar-day-agenda">
        <div className="calendar-day-agenda__heading">
          <div>
            <p className="eyebrow">Selected day</p>
            <h3 id="selected-calendar-date">{formatPlainDate(selectedDate, locale, {
              weekday: 'long', month: 'long', day: 'numeric', year: 'numeric',
            })}</h3>
          </div>
          <span>{selectedEvents.length} {selectedEvents.length === 1 ? 'event' : 'events'}</span>
        </div>
        {selectedEvents.length === 0
          ? <p className="preview-note">No plans are showing for this day.</p>
          : <CalendarEventList events={selectedEvents} locale={locale} timeZone={timeZone} />}
      </section>

      <section aria-label={`${monthLabel} agenda`} className="calendar-mobile-agenda">
        {eventDays.length === 0 && <p className="preview-note">No plans are showing this month.</p>}
        {eventDays.map((day) => (
          <section aria-labelledby={`agenda-${day.key}`} className="calendar-mobile-agenda__day" key={day.key}>
            <h3 id={`agenda-${day.key}`}>{formatPlainDate(day, locale, {
              weekday: 'long', month: 'long', day: 'numeric',
            })}</h3>
            <CalendarEventList events={grouped.get(day.key) ?? []} locale={locale} timeZone={timeZone} />
          </section>
        ))}
      </section>
    </>
  )
}
