import type { ChoreRecurrenceRequest } from '../../lib/api'

const weekdays = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday']

interface Props {
  value: ChoreRecurrenceRequest
  onChange: (value: ChoreRecurrenceRequest) => void
}

export function ChoreRecurrenceFields({ value, onChange }: Props) {
  function toggle(day: string) {
    const daysOfWeek = value.daysOfWeek.includes(day)
      ? value.daysOfWeek.filter((item) => item !== day)
      : [...value.daysOfWeek, day]
    onChange({ ...value, daysOfWeek })
  }

  return <fieldset className="schedule-recurrence">
    <legend>Repeat</legend>
    <div className="segmented-control">
      <button aria-pressed={value.kind === 'daily'} onClick={() => onChange({ kind: 'daily', interval: 1, daysOfWeek: [] })} type="button">Daily</button>
      <button aria-pressed={value.kind === 'weekly'} onClick={() => onChange({ kind: 'weekly', interval: 1, daysOfWeek: ['monday'] })} type="button">Selected weekdays</button>
    </div>
    <label>Every
      <span className="schedule-interval"><input max={value.kind === 'daily' ? 30 : 12} min="1"
        onChange={(event) => onChange({ ...value, interval: Number(event.target.value) })}
        required type="number" value={value.interval} /> {value.kind === 'daily' ? 'day(s)' : 'week(s)'}</span>
    </label>
    {value.kind === 'weekly' && <div aria-label="Weekdays" className="weekday-picker">
      {weekdays.map((day) => <button aria-pressed={value.daysOfWeek.includes(day)} key={day}
        onClick={() => toggle(day)} type="button">{day.slice(0, 3)}</button>)}
    </div>}
  </fieldset>
}
