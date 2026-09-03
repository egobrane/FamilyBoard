import type { CalendarEventResponse } from '../../lib/api'

export interface CalendarDate {
  year: number
  month: number
  day: number
}

export interface CalendarDay extends CalendarDate {
  key: string
  isCurrentMonth: boolean
}

const dateKeyPattern = /^(\d{4})-(\d{2})-(\d{2})$/
const monthKeyPattern = /^(\d{4})-(\d{2})$/

function pad(value: number) {
  return String(value).padStart(2, '0')
}

export function dateKey(date: CalendarDate) {
  return `${date.year}-${pad(date.month)}-${pad(date.day)}`
}

export function parseDateKey(value: string): CalendarDate | null {
  const match = dateKeyPattern.exec(value)
  if (!match) return null
  const candidate = { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) }
  const verified = new Date(Date.UTC(candidate.year, candidate.month - 1, candidate.day, 12))
  return verified.getUTCFullYear() === candidate.year
    && verified.getUTCMonth() + 1 === candidate.month
    && verified.getUTCDate() === candidate.day ? candidate : null
}

export function parseMonthKey(value: string | null): Pick<CalendarDate, 'year' | 'month'> | null {
  const match = value ? monthKeyPattern.exec(value) : null
  if (!match) return null
  const year = Number(match[1])
  const month = Number(match[2])
  return year >= 1900 && year <= 2200 && month >= 1 && month <= 12 ? { year, month } : null
}

export function monthKey(value: Pick<CalendarDate, 'year' | 'month'>) {
  return `${value.year}-${pad(value.month)}`
}

export function addMonths(value: Pick<CalendarDate, 'year' | 'month'>, amount: number) {
  const result = new Date(Date.UTC(value.year, value.month - 1 + amount, 1, 12))
  return { year: result.getUTCFullYear(), month: result.getUTCMonth() + 1 }
}

export function addDays(value: CalendarDate, amount: number): CalendarDate {
  const result = new Date(Date.UTC(value.year, value.month - 1, value.day + amount, 12))
  return { year: result.getUTCFullYear(), month: result.getUTCMonth() + 1, day: result.getUTCDate() }
}

function zonedParts(date: Date, timeZone: string): CalendarDate & { hour: number; minute: number; second: number } {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
  }).formatToParts(date)
  const part = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((item) => item.type === type)?.value)
  return { year: part('year'), month: part('month'), day: part('day'), hour: part('hour'), minute: part('minute'), second: part('second') }
}

export function dateInTimeZone(date: Date, timeZone: string): CalendarDate {
  const { year, month, day } = zonedParts(date, timeZone)
  return { year, month, day }
}

// Month boundaries are ordinary midnight wall times. Iterating the offset calculation
// keeps the request aligned to the household when its browser is in another time zone.
function zonedMidnight(value: CalendarDate, timeZone: string) {
  const wanted = Date.UTC(value.year, value.month - 1, value.day)
  let instant = new Date(wanted)
  for (let attempt = 0; attempt < 3; attempt += 1) {
    const actual = zonedParts(instant, timeZone)
    const represented = Date.UTC(actual.year, actual.month - 1, actual.day, actual.hour, actual.minute, actual.second)
    instant = new Date(instant.getTime() + wanted - represented)
  }
  return instant
}

export function monthRange(value: Pick<CalendarDate, 'year' | 'month'>, timeZone: string) {
  const next = addMonths(value, 1)
  return {
    from: zonedMidnight({ ...value, day: 1 }, timeZone).toISOString(),
    to: zonedMidnight({ ...next, day: 1 }, timeZone).toISOString(),
  }
}

export function monthDays(value: Pick<CalendarDate, 'year' | 'month'>, weekStartsOn: string): CalendarDay[] {
  const first = new Date(Date.UTC(value.year, value.month - 1, 1, 12))
  const startIndex = weekStartsOn.toLowerCase() === 'monday' ? 1 : 0
  const leading = (first.getUTCDay() - startIndex + 7) % 7
  const last = new Date(Date.UTC(value.year, value.month, 0, 12)).getUTCDate()
  const cellCount = Math.ceil((leading + last) / 7) * 7
  return Array.from({ length: cellCount }, (_, index) => {
    const item = addDays({ ...value, day: 1 }, index - leading)
    return { ...item, key: dateKey(item), isCurrentMonth: item.month === value.month }
  })
}

export function eventDateKeys(event: CalendarEventResponse, timeZone: string) {
  let start: CalendarDate | null
  let end: CalendarDate | null
  if (event.isAllDay) {
    start = parseDateKey(event.start.slice(0, 10))
    const exclusiveEnd = parseDateKey(event.end.slice(0, 10))
    end = exclusiveEnd ? addDays(exclusiveEnd, -1) : null
  } else {
    const startInstant = new Date(event.start)
    const endInstant = new Date(new Date(event.end).getTime() - 1)
    start = Number.isNaN(startInstant.getTime()) ? null : dateInTimeZone(startInstant, timeZone)
    end = Number.isNaN(endInstant.getTime()) ? null : dateInTimeZone(endInstant, timeZone)
  }
  if (!start || !end || dateKey(end) < dateKey(start)) return []
  const keys: string[] = []
  for (let current = start; dateKey(current) <= dateKey(end) && keys.length < 62; current = addDays(current, 1)) {
    keys.push(dateKey(current))
  }
  return keys
}

export function eventsByDate(events: CalendarEventResponse[], timeZone: string) {
  const result = new Map<string, CalendarEventResponse[]>()
  for (const event of events) {
    for (const key of eventDateKeys(event, timeZone)) {
      const items = result.get(key) ?? []
      items.push(event)
      result.set(key, items)
    }
  }
  for (const items of result.values()) {
    items.sort((left, right) => Number(right.isAllDay) - Number(left.isAllDay)
      || left.start.localeCompare(right.start) || left.title.localeCompare(right.title))
  }
  return result
}

export function formatPlainDate(value: CalendarDate | string, locale: string, options: Intl.DateTimeFormatOptions) {
  const parsed = typeof value === 'string' ? parseDateKey(value) : value
  if (!parsed) return ''
  return new Intl.DateTimeFormat(locale, { ...options, timeZone: 'UTC' })
    .format(new Date(Date.UTC(parsed.year, parsed.month - 1, parsed.day, 12)))
}

export function formatEventTime(event: CalendarEventResponse, locale?: string, timeZone?: string) {
  if (event.isAllDay) return 'All day'
  const formatter = new Intl.DateTimeFormat(locale, {
    hour: 'numeric', minute: '2-digit', ...(timeZone ? { timeZone } : {}),
  })
  return `${formatter.format(new Date(event.start))}–${formatter.format(new Date(event.end))}`
}
