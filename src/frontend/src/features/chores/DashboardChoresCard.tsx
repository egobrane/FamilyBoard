import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { getChoreDashboard, type ChoreAssignmentResponse } from '../../lib/api'

export function DashboardChoresCard() {
  const { state } = useAuthentication()
  const [items, setItems] = useState<ChoreAssignmentResponse[]>([])
  const [waiting, setWaiting] = useState(0)
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  const load = useCallback(async () => {
    if (!householdId) return
    try {
      const response = await getChoreDashboard(householdId)
      setItems([...response.overdue, ...response.dueToday, ...response.upcoming].slice(0, 3))
      setWaiting(response.awaitingReviewCount)
    } catch { setItems([]) }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])

  return (
    <DashboardCard className="chores-card" eyebrow="A little progress" id="chores-preview" title="Chores"
      action={<Link className="card-link" to="/chores">View all</Link>}>
      {items.length === 0 ? <p className="preview-note">No chores are due right now.</p> : (
        <ul className="chore-list">
          {items.map((chore) => <li className="chore" key={chore.id}>
            <span className="chore__check" aria-hidden="true">{chore.status === 'awaitingReview' ? '…' : ''}</span>
            <span className="chore__details"><strong>{chore.title}</strong><span>{chore.assignedMember.displayName}</span></span>
            {chore.isOverdue && <span className="status-pill status-pill--coral">Overdue</span>}
          </li>)}
        </ul>
      )}
      {waiting > 0 && <p className="preview-note">{waiting} waiting for adult review.</p>}
    </DashboardCard>
  )
}
