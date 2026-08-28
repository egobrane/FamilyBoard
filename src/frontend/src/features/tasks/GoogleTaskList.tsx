import type { GoogleTaskResponse } from '../../lib/api'

function dueLabel(value: string | null) {
  if (!value) return null
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' })
    .format(new Date(`${value}T00:00:00Z`))
}

export function GoogleTaskList({ tasks, compact = false, busyTaskId, onStatusChange }: {
  tasks: GoogleTaskResponse[]
  compact?: boolean
  busyTaskId?: string | null
  onStatusChange?: (task: GoogleTaskResponse) => void
}) {
  return (
    <ul className={`google-task-list ${compact ? 'google-task-list--compact' : ''}`}>
      {tasks.map((task) => (
        <li className={task.status === 'completed' ? 'google-task google-task--completed' : 'google-task'} key={`${task.sourceId}:${task.id}`}>
          <span aria-hidden="true" className="google-task__check">{task.status === 'completed' ? '✓' : '○'}</span>
          <span className="google-task__content">
            <strong>{task.title}</strong>
            <small>{task.taskListName}{dueLabel(task.dueDate) ? ` · Due ${dueLabel(task.dueDate)}` : ''}</small>
            {!compact && task.notes && <span>{task.notes}</span>}
          </span>
          {!compact && task.canChangeStatus && onStatusChange && <button
            className="secondary-action google-task__action" disabled={busyTaskId === task.id}
            onClick={() => onStatusChange(task)} type="button">
            {busyTaskId === task.id ? 'Saving…' : task.status === 'completed' ? 'Reopen' : 'Complete'}
          </button>}
        </li>
      ))}
    </ul>
  )
}
