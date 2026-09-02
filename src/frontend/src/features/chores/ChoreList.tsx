import type { ChoreAssignmentResponse } from '../../lib/api'
import { MemberAvatar } from '../../components/MemberAvatar'

export function ChoreList({ assignments, onComplete, onClaim }: {
  assignments: ChoreAssignmentResponse[]
  onComplete?: (assignment: ChoreAssignmentResponse) => void
  onClaim?: (assignment: ChoreAssignmentResponse) => void
}) {
  return (
    <ul className="chore-board-list">
      {assignments.map((assignment) => (
        <li className={`chore-board-item ${assignment.isOverdue ? 'chore-board-item--overdue' : ''}`} key={assignment.id}>
          {assignment.assignedMember
            ? <MemberAvatar className="chore__member-avatar" member={assignment.assignedMember} />
            : <span aria-hidden="true" className="chore__open-mark">✦</span>}
          <div className="chore-board-item__details">
            <p className="eyebrow">{assignment.isOverdue ? 'Overdue' : assignment.dueLocalDate ?? 'No due date'}</p>
            <h3>{assignment.title}</h3>
            <p>{assignment.assignedMember?.displayName ?? 'Up for grabs'}{assignment.dueLocalTime ? ` · ${assignment.dueLocalTime.slice(0, 5)}` : ''}</p>
          </div>
          {assignment.status === 'awaitingReview' ? (
            <span className="status-pill status-pill--mint">Waiting for review</span>
          ) : assignment.assignedMember === null && onClaim ? (
            <button className="primary-action" onClick={() => onClaim(assignment)} type="button">I’ll do it</button>
          ) : onComplete ? (
            <button className="primary-action" onClick={() => onComplete(assignment)} type="button">Mark done</button>
          ) : null}
        </li>
      ))}
    </ul>
  )
}
