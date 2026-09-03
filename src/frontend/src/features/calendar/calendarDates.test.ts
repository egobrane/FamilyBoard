import { describe, expect, it } from 'vitest'
import type { CalendarEventResponse } from '../../lib/api'
import {
  eventDateKeys,
  eventsByDate,
  monthDays,
  monthRange,
  parseDateKey,
} from './calendarDates'

function event(values: Partial<CalendarEventResponse> = {}): CalendarEventResponse {
  return {
    id: 'event-1', sourceId: 'source-1', calendarName: 'Family', title: 'Family plan',
    isAllDay: false, start: '2026-09-03T23:30:00Z', end: '2026-09-04T01:30:00Z',
    timeZone: 'America/New_York', location: null, color: '#73b49a', ...values,
  }
}

describe('calendar date handling', () => {
  it('builds complete Sunday- and Monday-first month grids', () => {
    const sunday = monthDays({ year: 2026, month: 9 }, 'Sunday')
    const monday = monthDays({ year: 2026, month: 9 }, 'Monday')
    expect(sunday).toHaveLength(35)
    expect(sunday[0].key).toBe('2026-08-30')
    expect(monday[0].key).toBe('2026-08-31')
    expect(sunday.find((day) => day.key === '2026-09-03')?.isCurrentMonth).toBe(true)
  })

  it('creates household-local month boundaries across daylight-saving changes', () => {
    expect(monthRange({ year: 2026, month: 3 }, 'America/New_York')).toEqual({
      from: '2026-03-01T05:00:00.000Z',
      to: '2026-04-01T04:00:00.000Z',
    })
    const europeanFallback = monthRange({ year: 2026, month: 10 }, 'Europe/Berlin')
    expect(europeanFallback).toEqual({
      from: '2026-09-30T22:00:00.000Z',
      to: '2026-10-31T23:00:00.000Z',
    })
  })

  it('places timed, overnight, and exclusive-end all-day events on the correct dates', () => {
    expect(eventDateKeys(event(), 'America/New_York')).toEqual(['2026-09-03'])
    expect(eventDateKeys(event({ end: '2026-09-04T05:30:00Z' }), 'America/New_York'))
      .toEqual(['2026-09-03', '2026-09-04'])
    expect(eventDateKeys(event({
      isAllDay: true, start: '2026-09-03', end: '2026-09-06',
    }), 'America/New_York')).toEqual(['2026-09-03', '2026-09-04', '2026-09-05'])
  })

  it('sorts all-day plans before timed plans without copying their data', () => {
    const timed = event({ id: 'timed', title: 'Dinner' })
    const allDay = event({ id: 'all-day', title: 'No school', isAllDay: true,
      start: '2026-09-03', end: '2026-09-04' })
    expect(eventsByDate([timed, allDay], 'America/New_York').get('2026-09-03')?.map((item) => item.id))
      .toEqual(['all-day', 'timed'])
    expect(parseDateKey('2026-02-30')).toBeNull()
  })
})
