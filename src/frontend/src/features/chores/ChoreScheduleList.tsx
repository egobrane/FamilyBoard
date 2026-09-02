import type { ChoreScheduleResponse } from '../../lib/api'

interface Props {
  schedules: ChoreScheduleResponse[]
  onEdit: (schedule: ChoreScheduleResponse) => void
  onStateChange: (schedule: ChoreScheduleResponse, active: boolean) => void
}

function summary(schedule: ChoreScheduleResponse) {
  const repeat = schedule.recurrence.kind === 'daily'
    ? schedule.recurrence.interval === 1 ? 'Every day' : `Every ${schedule.recurrence.interval} days`
    : `${schedule.recurrence.daysOfWeek.map((day) => day.slice(0, 3)).join(', ')} every ${schedule.recurrence.interval === 1 ? 'week' : `${schedule.recurrence.interval} weeks`}`
  return `${repeat} · ${schedule.dueLocalTime ? `Due ${schedule.dueLocalTime.slice(0, 5)}` : 'Due by end of day'}`
}

export function ChoreScheduleList({ schedules, onEdit, onStateChange }: Props) {
  return <section className="admin-section schedule-list-section"><h3>Recurring schedules</h3>
    {schedules.length === 0 ? <p>No recurring chores yet.</p> : <ul className="definition-list">
      {schedules.map((schedule) => <li key={schedule.id}>
        <div><strong>{schedule.definition.title} · {schedule.assignedMember?.displayName ?? 'Up for grabs'}</strong>
          <span>{summary(schedule)}</span><span className={`schedule-status schedule-status--${schedule.status}`}>{schedule.status}</span>
          {schedule.blockedReason && <span>Needs attention: {schedule.blockedReason}</span>}
          {schedule.nextOccurrenceLocalDate && <span>Next: {schedule.nextOccurrenceLocalDate}</span>}</div>
        <div className="form-actions"><button onClick={() => onEdit(schedule)} type="button">Edit</button>
          {schedule.status === 'active'
            ? <button onClick={() => onStateChange(schedule, false)} type="button">Pause</button>
            : schedule.status !== 'completed' && <button onClick={() => onStateChange(schedule, true)} type="button">Resume</button>}</div>
      </li>)}
    </ul>}
  </section>
}
